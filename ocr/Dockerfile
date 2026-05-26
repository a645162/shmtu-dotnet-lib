# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files first for better layer caching
COPY shmtu-ocr-onnx-lib/shmtu-ocr-onnx-lib.csproj shmtu-ocr-onnx-lib/
COPY shmtu-ocr-onnx-server/shmtu-ocr-onnx-server.csproj shmtu-ocr-onnx-server/

# Restore dependencies
RUN dotnet restore shmtu-ocr-onnx-server/shmtu-ocr-onnx-server.csproj

# Copy source code
COPY shmtu-ocr-onnx-lib/ shmtu-ocr-onnx-lib/
COPY shmtu-ocr-onnx-server/ shmtu-ocr-onnx-server/

# Build and publish
RUN dotnet publish shmtu-ocr-onnx-server/shmtu-ocr-onnx-server.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install curl for health check + SkiaSharp native dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    libfontconfig1 \
    libfreetype6 \
    libice6 \
    libsm6 \
    && rm -rf /var/lib/apt/lists/*

# Copy published app
COPY --from=build /app/publish .

# Set environment variables for Docker-appropriate defaults
ENV ASPNETCORE_URLS=http://+:5000 \
    OcrServer__ModelDirectory=/app/models \
    OcrServer__TcpPort=21601 \
    OcrServer__TcpListenAddress=0.0.0.0 \
    OcrServer__PoolSize=0

# Create models directory (will be overridden by volume mount)
RUN mkdir -p /app/models

EXPOSE 5000 21601

HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:5000/api/health || exit 1

ENTRYPOINT ["dotnet", "shmtu-ocr-onnx-server.dll"]
