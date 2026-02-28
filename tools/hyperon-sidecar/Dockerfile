FROM python:3.12-slim

WORKDIR /app

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Generate gRPC stubs from proto (proto is mounted or copied at build time)
COPY . .

# Default port
ENV HYPERON_GRAMMAR_PORT=50051
EXPOSE 50051

CMD ["python", "grammar_service.py"]
