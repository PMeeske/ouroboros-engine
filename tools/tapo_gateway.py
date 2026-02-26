"""
Tapo Gateway - Self-contained REST proxy with auto-discovery.

Replaces the external tapo-rest server with a Python FastAPI gateway
that auto-discovers Tapo devices and provides API-compatible endpoints.

Usage:
    python tapo_gateway.py --tapo-username EMAIL --tapo-password PASS --server-password SRVPASS [--port 8123]
"""

import argparse
import asyncio
import ipaddress
import logging
import secrets
import socket
from contextlib import asynccontextmanager
from dataclasses import dataclass, field
from typing import Any

import uvicorn
from fastapi import Depends, FastAPI, HTTPException, Query, Request
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import PlainTextResponse
from pydantic import BaseModel
from tapo import ApiClient

logger = logging.getLogger("tapo_gateway")

# ---------------------------------------------------------------------------
# Models
# ---------------------------------------------------------------------------

class LoginRequest(BaseModel):
    password: str

class DeviceInfo(BaseModel):
    name: str
    device_type: str
    ip_addr: str

# ---------------------------------------------------------------------------
# State
# ---------------------------------------------------------------------------

# Device type aliases that share the same handler type
DEVICE_TYPE_ALIASES: dict[str, list[str]] = {
    "L510": ["L520", "L610"],
    "L530": ["L535", "L630"],
    "L900": [],
    "L920": ["L930"],
    "P100": ["P105"],
    "P110": ["P110M", "P115"],
    "P300": [],
    "P304": ["P304M", "P316"],
    "C200": ["C100", "C210", "C220", "C310", "C320", "C420", "C500", "C520"],
}

# Flatten to a lookup: model -> canonical handler type
MODEL_TO_HANDLER: dict[str, str] = {}
for canonical, aliases in DEVICE_TYPE_ALIASES.items():
    MODEL_TO_HANDLER[canonical] = canonical
    for alias in aliases:
        MODEL_TO_HANDLER[alias] = canonical

# Handler method name on ApiClient for each canonical type (lowercase)
HANDLER_METHODS: dict[str, str] = {
    "L510": "l510", "L520": "l520", "L530": "l530", "L535": "l535",
    "L610": "l610", "L630": "l630", "L900": "l900", "L920": "l920", "L930": "l930",
    "P100": "p100", "P105": "p105", "P110": "p110", "P115": "p115",
    "P300": "p300", "P304": "p304", "P316": "p316",
}

@dataclass
class DeviceEntry:
    name: str
    device_type: str
    ip_addr: str
    handler: Any = None  # Cached tapo handler

@dataclass
class GatewayState:
    tapo_username: str
    tapo_password: str
    server_password: str
    broadcast_addr: str = "192.168.1.255"
    client: ApiClient | None = None
    devices: dict[str, DeviceEntry] = field(default_factory=dict)
    sessions: dict[str, bool] = field(default_factory=dict)
    action_routes: list[str] = field(default_factory=list)

    def init_client(self):
        self.client = ApiClient(self.tapo_username, self.tapo_password)

state = GatewayState("", "", "")

# ---------------------------------------------------------------------------
# Discovery
# ---------------------------------------------------------------------------

def _detect_local_subnets() -> list[str]:
    """Auto-detect local network subnets from system interfaces."""
    subnets = set()
    try:
        for info in socket.getaddrinfo(socket.gethostname(), None, socket.AF_INET):
            ip = info[4][0]
            if ip.startswith("127.") or ip.startswith("169.254."):
                continue
            # Assume /24 for typical home networks
            parts = ip.rsplit(".", 1)
            subnets.add(parts[0])
    except Exception:
        pass

    # Also include the configured broadcast address subnet
    if state.broadcast_addr:
        parts = state.broadcast_addr.rsplit(".", 1)
        subnets.add(parts[0])

    return sorted(subnets) if subnets else ["192.168.1"]


async def discover_devices_on_network() -> list[DeviceEntry]:
    """Discover Tapo devices using broadcast + general-purpose TCP scan across all local subnets."""
    if state.client is None:
        state.init_client()

    discovered: list[DeviceEntry] = []

    # Strategy 1: Use tapo library's built-in broadcast discovery
    try:
        logger.info("Starting broadcast discovery on %s...", state.broadcast_addr)
        discovery = await state.client.discover_devices(state.broadcast_addr, timeout_s=5)

        # DeviceDiscovery is an async iterator
        async for maybe_result in discovery:
            try:
                result = maybe_result.get()
                if result is None:
                    continue
                await _process_discovery_result(result, discovered)
            except Exception as e:
                logger.debug("Skipping discovery result: %s", e)

        if discovered:
            logger.info("Broadcast discovery found %d device(s)", len(discovered))
            return discovered
    except Exception as e:
        logger.warning("Broadcast discovery failed: %s, falling back to TCP scan", e)

    # Strategy 2: General-purpose TCP scan across all detected subnets
    subnets = _detect_local_subnets()
    logger.info("Scanning %d subnet(s): %s", len(subnets), ", ".join(f"{s}.0/24" for s in subnets))

    seen_ips: set[str] = set()
    for subnet in subnets:
        scan_targets = [f"{subnet}.{i}" for i in range(2, 255) if f"{subnet}.{i}" not in seen_ips]
        seen_ips.update(scan_targets)

        # Scan all IPs in parallel (0.3s TCP timeout keeps it fast)
        tasks = [_probe_device(ip) for ip in scan_targets]
        results = await asyncio.gather(*tasks, return_exceptions=True)
        for result in results:
            if isinstance(result, DeviceEntry):
                discovered.append(result)

    logger.info("TCP scan found %d device(s) across %d subnet(s)", len(discovered), len(subnets))
    return discovered


async def _process_discovery_result(result: Any, discovered: list[DeviceEntry]):
    """Extract device info from a DiscoveryResult variant."""
    # DiscoveryResult is an enum with variants like Light, ColorLight, Plug, etc.
    # Each variant contains device info. Try to get common fields.
    try:
        # The result object has the device info directly
        info = result
        ip = getattr(info, "ip", None) or getattr(info, "ip_addr", None)
        model = getattr(info, "model", None) or "Unknown"
        nickname = getattr(info, "nickname", None) or model

        if ip and model:
            device_type = _normalize_model(model)
            if device_type:
                name = nickname if nickname != model else f"{device_type}-{ip.split('.')[-1]}"
                discovered.append(DeviceEntry(
                    name=name,
                    device_type=device_type,
                    ip_addr=str(ip),
                ))
    except Exception as e:
        logger.debug("Could not extract discovery result: %s", e)


async def _probe_device(ip: str) -> DeviceEntry | None:
    """Probe a single IP to check if it's a Tapo device."""
    # Quick async TCP check on port 80 (Tapo devices use HTTP)
    try:
        _, writer = await asyncio.wait_for(
            asyncio.open_connection(ip, 80), timeout=0.3)
        writer.close()
        await writer.wait_closed()
    except (OSError, asyncio.TimeoutError):
        return None

    # Port is open - try to connect as a Tapo device
    try:
        device = await asyncio.wait_for(
            state.client.generic_device(ip), timeout=5)
        info = await asyncio.wait_for(
            device.get_device_info(), timeout=5)
        model = info.model or "Unknown"
        nickname = info.nickname or f"{model}-{ip.split('.')[-1]}"
        device_type = _normalize_model(model)
        if device_type:
            return DeviceEntry(
                name=nickname,
                device_type=device_type,
                ip_addr=ip,
            )
    except Exception:
        pass  # Not a Tapo device or auth failed

    return None


def _normalize_model(model: str) -> str | None:
    """Normalize a model string (e.g., 'Tapo L530' -> 'L530')."""
    model = model.strip()
    # Strip 'Tapo ' prefix if present
    if model.lower().startswith("tapo "):
        model = model[5:]
    # Strip any trailing version info (e.g., 'L530(EU)' -> 'L530')
    for ch in ("(", " ", "/"):
        if ch in model:
            model = model[:model.index(ch)]
    model = model.upper()
    return model if model in MODEL_TO_HANDLER else None


def _build_action_routes() -> list[str]:
    """Build the list of available action routes based on registered device types."""
    routes = []
    device_types_seen = set()

    for dev in state.devices.values():
        dt = dev.device_type
        if dt in device_types_seen:
            continue
        device_types_seen.add(dt)
        prefix = f"/{dt.lower()}"
        canonical = MODEL_TO_HANDLER.get(dt, dt)

        # Common actions
        routes.extend([f"{prefix}/on", f"{prefix}/off", f"{prefix}/get-device-info"])

        if canonical in ("L510", "L530", "L900", "L920"):
            routes.append(f"{prefix}/set-brightness")
            routes.append(f"{prefix}/get-device-usage")

        if canonical in ("L530", "L900", "L920"):
            routes.extend([
                f"{prefix}/set-color",
                f"{prefix}/set-hue-saturation",
                f"{prefix}/set-color-temperature",
            ])

        if canonical == "L920":
            routes.append(f"{prefix}/set-lighting-effect")

        if canonical in ("P100", "P110"):
            routes.append(f"{prefix}/get-device-usage")

        if canonical == "P110":
            routes.extend([
                f"{prefix}/get-energy-usage",
                f"{prefix}/get-current-power",
                f"{prefix}/get-hourly-energy-data",
                f"{prefix}/get-daily-energy-data",
                f"{prefix}/get-monthly-energy-data",
            ])

        if canonical in ("P300", "P304"):
            routes.append(f"{prefix}/get-child-device-list")

    return sorted(routes)


# ---------------------------------------------------------------------------
# Auth helpers
# ---------------------------------------------------------------------------

def _generate_session_id() -> str:
    return secrets.token_urlsafe(24)


def _verify_bearer(request: Request) -> str:
    auth = request.headers.get("authorization", "")
    if not auth.startswith("Bearer "):
        raise HTTPException(status_code=403, detail="Missing or invalid bearer token")
    token = auth[7:]
    if token not in state.sessions:
        raise HTTPException(status_code=403, detail="Invalid bearer token")
    return token


# ---------------------------------------------------------------------------
# Device handler helpers
# ---------------------------------------------------------------------------

async def _get_or_connect_handler(device: DeviceEntry) -> Any:
    """Get cached handler or create a new connection."""
    if device.handler is not None:
        return device.handler

    if state.client is None:
        state.init_client()

    dt = device.device_type
    method_name = HANDLER_METHODS.get(dt)
    if method_name is None:
        raise HTTPException(status_code=400, detail=f"Unsupported device type: {dt}")

    method = getattr(state.client, method_name, None)
    if method is None:
        raise HTTPException(status_code=400, detail=f"No handler for device type: {dt}")

    try:
        handler = await method(device.ip_addr)
        device.handler = handler
        return handler
    except Exception as e:
        raise HTTPException(status_code=502, detail=f"Failed to connect to {device.name}: {e}")


def _get_device(device_name: str) -> DeviceEntry:
    dev = state.devices.get(device_name)
    if dev is None:
        raise HTTPException(status_code=404, detail=f"Device not found: {device_name}")
    return dev


def _dict_result(obj: Any) -> dict:
    """Convert a tapo response object to a dict."""
    if hasattr(obj, "to_dict"):
        return obj.to_dict()
    if hasattr(obj, "__dict__"):
        return {k: v for k, v in obj.__dict__.items() if not k.startswith("_")}
    return {}


# ---------------------------------------------------------------------------
# App lifecycle
# ---------------------------------------------------------------------------

async def _run_startup_discovery():
    """Run device discovery in the background after server starts."""
    await asyncio.sleep(0.5)  # Let the server bind first
    logger.info("Running auto-discovery...")
    try:
        discovered = await discover_devices_on_network()
        for dev in discovered:
            state.devices[dev.name] = dev
        state.action_routes = _build_action_routes()
        logger.info("Discovery complete: %d device(s) found", len(state.devices))
    except Exception as e:
        logger.error("Discovery failed: %s", e)

_discovery_task: asyncio.Task | None = None

@asynccontextmanager
async def lifespan(app: FastAPI):
    global _discovery_task
    state.init_client()
    logger.info("Tapo Gateway starting...")
    # Launch discovery in background so /health responds immediately
    _discovery_task = asyncio.create_task(_run_startup_discovery())
    yield
    # Cleanup
    if _discovery_task and not _discovery_task.done():
        _discovery_task.cancel()
    state.devices.clear()
    state.sessions.clear()


app = FastAPI(title="Tapo Gateway", lifespan=lifespan)
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["*"],
    allow_headers=["*"],
)

# ---------------------------------------------------------------------------
# Core endpoints (tapo-rest compatible)
# ---------------------------------------------------------------------------

@app.get("/health")
async def health():
    return PlainTextResponse("ok")


@app.post("/login")
async def login(body: LoginRequest):
    if body.password != state.server_password:
        raise HTTPException(status_code=403, detail="Invalid credentials provided")
    session_id = _generate_session_id()
    state.sessions[session_id] = True
    return PlainTextResponse(session_id)


@app.get("/devices")
async def list_devices(_token: str = Depends(_verify_bearer)):
    return [
        {"name": d.name, "device_type": d.device_type, "ip_addr": d.ip_addr}
        for d in state.devices.values()
    ]


@app.get("/actions")
async def list_actions():
    # Unauthenticated (matches tapo-rest: /actions without auth returns routes)
    return state.action_routes


@app.get("/refresh-session")
async def refresh_session(
    device: str = Query(...),
    _token: str = Depends(_verify_bearer),
):
    dev = _get_device(device)
    handler = await _get_or_connect_handler(dev)
    try:
        await handler.refresh_session()
    except Exception as e:
        # Force reconnect on next access
        dev.handler = None
        raise HTTPException(status_code=502, detail=str(e))
    return PlainTextResponse("OK")


@app.post("/reload-config")
async def reload_config(_token: str = Depends(_verify_bearer)):
    """Re-run device discovery."""
    old_count = len(state.devices)
    state.devices.clear()
    discovered = await discover_devices_on_network()
    for dev in discovered:
        state.devices[dev.name] = dev
    state.action_routes = _build_action_routes()
    return PlainTextResponse(f"Reloaded: {len(state.devices)} devices (was {old_count})")


@app.post("/discover")
async def discover_endpoint(_token: str = Depends(_verify_bearer)):
    """Trigger network discovery and merge new devices."""
    discovered = await discover_devices_on_network()
    new_devices = []
    for dev in discovered:
        if dev.name not in state.devices:
            state.devices[dev.name] = dev
            new_devices.append(dev)
    state.action_routes = _build_action_routes()
    return [
        {"name": d.name, "device_type": d.device_type, "ip_addr": d.ip_addr}
        for d in new_devices
    ]


# ---------------------------------------------------------------------------
# Device action endpoints (tapo-rest compatible URL patterns)
# ---------------------------------------------------------------------------

# Light actions (L510, L520, L610, L530, L535, L630, L900, L920, L930)

@app.get("/actions/{device_type}/on")
async def device_on(
    device_type: str,
    device: str = Query(...),
    _token: str = Depends(_verify_bearer),
):
    dev = _get_device(device)
    handler = await _get_or_connect_handler(dev)
    await handler.on()
    return PlainTextResponse("OK")


@app.get("/actions/{device_type}/off")
async def device_off(
    device_type: str,
    device: str = Query(...),
    _token: str = Depends(_verify_bearer),
):
    dev = _get_device(device)
    handler = await _get_or_connect_handler(dev)
    await handler.off()
    return PlainTextResponse("OK")


@app.get("/actions/{device_type}/set-brightness")
async def set_brightness(
    device_type: str,
    device: str = Query(...),
    level: int = Query(..., ge=1, le=100),
    _token: str = Depends(_verify_bearer),
):
    dev = _get_device(device)
    handler = await _get_or_connect_handler(dev)
    await handler.set_brightness(level)
    return PlainTextResponse("OK")


@app.get("/actions/{device_type}/set-color")
async def set_color(
    device_type: str,
    device: str = Query(...),
    color: str = Query(...),
    _token: str = Depends(_verify_bearer),
):
    from tapo.requests import Color
    dev = _get_device(device)
    handler = await _get_or_connect_handler(dev)
    color_enum = getattr(Color, color, None)
    if color_enum is None:
        raise HTTPException(status_code=400, detail=f"Unknown color: {color}")
    await handler.set_color(color_enum)
    return PlainTextResponse("OK")


@app.get("/actions/{device_type}/set-hue-saturation")
async def set_hue_saturation(
    device_type: str,
    device: str = Query(...),
    hue: int = Query(..., ge=0, le=360),
    saturation: int = Query(..., ge=0, le=100),
    _token: str = Depends(_verify_bearer),
):
    dev = _get_device(device)
    handler = await _get_or_connect_handler(dev)
    await handler.set_hue_saturation(hue, saturation)
    return PlainTextResponse("OK")


@app.get("/actions/{device_type}/set-color-temperature")
async def set_color_temperature(
    device_type: str,
    device: str = Query(...),
    color_temperature: int = Query(..., ge=2500, le=6500),
    _token: str = Depends(_verify_bearer),
):
    dev = _get_device(device)
    handler = await _get_or_connect_handler(dev)
    await handler.set_color_temperature(color_temperature)
    return PlainTextResponse("OK")


@app.get("/actions/{device_type}/set-lighting-effect")
async def set_lighting_effect(
    device_type: str,
    device: str = Query(...),
    lighting_effect: str = Query(...),
    _token: str = Depends(_verify_bearer),
):
    from tapo.requests import LightingEffectPreset
    dev = _get_device(device)
    handler = await _get_or_connect_handler(dev)
    effect = getattr(LightingEffectPreset, lighting_effect, None)
    if effect is None:
        raise HTTPException(status_code=400, detail=f"Unknown effect: {lighting_effect}")
    await handler.set_lighting_effect(effect)
    return PlainTextResponse("OK")


@app.get("/actions/{device_type}/get-device-info")
async def get_device_info(
    device_type: str,
    device: str = Query(...),
    _token: str = Depends(_verify_bearer),
):
    dev = _get_device(device)
    handler = await _get_or_connect_handler(dev)
    info = await handler.get_device_info()
    return _dict_result(info)


@app.get("/actions/{device_type}/get-device-usage")
async def get_device_usage(
    device_type: str,
    device: str = Query(...),
    _token: str = Depends(_verify_bearer),
):
    dev = _get_device(device)
    handler = await _get_or_connect_handler(dev)
    usage = await handler.get_device_usage()
    return _dict_result(usage)


@app.get("/actions/{device_type}/get-energy-usage")
async def get_energy_usage(
    device_type: str,
    device: str = Query(...),
    _token: str = Depends(_verify_bearer),
):
    dev = _get_device(device)
    handler = await _get_or_connect_handler(dev)
    usage = await handler.get_energy_usage()
    return _dict_result(usage)


@app.get("/actions/{device_type}/get-current-power")
async def get_current_power(
    device_type: str,
    device: str = Query(...),
    _token: str = Depends(_verify_bearer),
):
    dev = _get_device(device)
    handler = await _get_or_connect_handler(dev)
    power = await handler.get_current_power()
    return _dict_result(power)


@app.get("/actions/{device_type}/get-child-device-list")
async def get_child_device_list(
    device_type: str,
    device: str = Query(...),
    _token: str = Depends(_verify_bearer),
):
    dev = _get_device(device)
    handler = await _get_or_connect_handler(dev)
    children = await handler.get_child_device_list()
    return [_dict_result(c) for c in children]


# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------

def main():
    parser = argparse.ArgumentParser(description="Tapo Gateway")
    parser.add_argument("--port", type=int, default=8123)
    parser.add_argument("--tapo-username", required=True)
    parser.add_argument("--tapo-password", required=True)
    parser.add_argument("--server-password", required=True)
    parser.add_argument("--broadcast-addr", default="192.168.1.255")
    parser.add_argument("--log-level", default="INFO")
    args = parser.parse_args()

    logging.basicConfig(
        level=getattr(logging, args.log_level.upper(), logging.INFO),
        format="%(asctime)s [%(name)s] %(levelname)s: %(message)s",
    )

    state.tapo_username = args.tapo_username
    state.tapo_password = args.tapo_password
    state.server_password = args.server_password
    state.broadcast_addr = args.broadcast_addr

    uvicorn.run(app, host="127.0.0.1", port=args.port, log_level="warning")


if __name__ == "__main__":
    main()
