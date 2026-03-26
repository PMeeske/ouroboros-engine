FROM rocm/pytorch:rocm6.2_ubuntu22.04_py3.11_pytorch2.4.0

ENV ROCM_PATH=/opt/rocm
ENV PYTHONUNBUFFERED=1

WORKDIR /app

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY tensor_service.py .

RUN groupadd --gid 1001 appgroup && \
    useradd --uid 1001 --gid appgroup --no-create-home appuser && \
    chown -R appuser:appgroup /app

USER appuser

EXPOSE 8768

HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD python -c "import urllib.request; urllib.request.urlopen('http://localhost:8768/health')"

CMD ["uvicorn", "tensor_service:app", "--host", "0.0.0.0", "--port", "8768"]
