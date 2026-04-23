FROM python:3.12-slim

WORKDIR /app

# Create non-root user for security
RUN groupadd --gid 1001 appgroup && \
    useradd --uid 1001 --gid appgroup --no-create-home appuser

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Generate gRPC stubs from proto (proto is mounted or copied at build time)
COPY . .

# Set ownership before dropping privileges
RUN chown -R appuser:appgroup /app
USER appuser

# Default port
ENV HYPERON_GRAMMAR_PORT=50051
EXPOSE 50051

HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD python -c "import grpc; from grammar_service_pb2_grpc import GrammarServiceStub; from grammar_service_pb2 import HealthCheckRequest; channel = grpc.insecure_channel('localhost:50051'); stub = GrammarServiceStub(channel); stub.HealthCheck(HealthCheckRequest()); print('healthy')" || exit 1

CMD ["python", "grammar_service.py"]
