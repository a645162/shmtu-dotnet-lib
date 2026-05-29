# ============================================================
# Build stage (shared by CPU and GPU targets)
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG USE_GPU=false
WORKDIR /src

# Copy project files first for better layer caching
COPY shmtu-ocr-onnx-lib/shmtu-ocr-onnx-lib.csproj shmtu-ocr-onnx-lib/
COPY shmtu-ocr-onnx-server/shmtu-ocr-onnx-server.csproj shmtu-ocr-onnx-server/

# Restore dependencies (GPU or CPU, with RuntimeIdentifier for self-contained)
RUN if [ "$USE_GPU" = "true" ]; then \
      dotnet restore shmtu-ocr-onnx-server/shmtu-ocr-onnx-server.csproj -p:UseGpu=true -r linux-x64; \
    else \
      dotnet restore shmtu-ocr-onnx-server/shmtu-ocr-onnx-server.csproj -r linux-x64; \
    fi

# Copy source code
COPY shmtu-ocr-onnx-lib/ shmtu-ocr-onnx-lib/
COPY shmtu-ocr-onnx-server/ shmtu-ocr-onnx-server/

# Build and publish (self-contained, no .NET runtime needed in target image)
RUN if [ "$USE_GPU" = "true" ]; then \
      dotnet publish shmtu-ocr-onnx-server/shmtu-ocr-onnx-server.csproj \
        -c Release -o /app/publish --no-restore \
        -p:UseGpu=true --self-contained true -r linux-x64; \
    else \
      dotnet publish shmtu-ocr-onnx-server/shmtu-ocr-onnx-server.csproj \
        -c Release -o /app/publish --no-restore \
        --self-contained true -r linux-x64; \
    fi

# ============================================================
# CPU Runtime stage
# ============================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime-cpu
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends \
    curl libfontconfig1 libfreetype6 libice6 libsm6 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:21600 \
    OcrServer__ModelDirectory=/app/models \
    OcrServer__TcpPort=21601 \
    OcrServer__TcpListenAddress=0.0.0.0 \
    OcrServer__PoolSize=0 \
    OcrServer__ExecutionProvider=CPU \
    OcrServer__GpuDeviceId=0

RUN mkdir -p /app/models
EXPOSE 21600 21601

HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:21600/api/health || exit 1

ENTRYPOINT ["./shmtu-ocr-onnx-server"]

# ============================================================
# GPU Runtime stage
# ============================================================
FROM nvidia/cuda:12.4.0-runtime-ubuntu22.04 AS runtime-gpu
WORKDIR /app

# Only need SkiaSharp deps here; .NET runtime is self-contained in publish output
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl libfontconfig1 libfreetype6 libice6 libsm6 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:21600 \
    OcrServer__ModelDirectory=/app/models \
    OcrServer__TcpPort=21601 \
    OcrServer__TcpListenAddress=0.0.0.0 \
    OcrServer__PoolSize=0 \
    OcrServer__ExecutionProvider=CUDA \
    OcrServer__GpuDeviceId=0 \
    LD_LIBRARY_PATH=/app:/usr/local/cuda/lib64:$LD_LIBRARY_PATH

RUN mkdir -p /app/models
EXPOSE 21600 21601

HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:21600/api/health || exit 1

ENTRYPOINT ["./shmtu-ocr-onnx-server"]

# ============================================================
# Bundled variants (with model weights included)
# ============================================================
FROM runtime-cpu AS runtime-cpu-bundled

COPY models/ /app/models/

FROM runtime-gpu AS runtime-gpu-bundled

COPY models/ /app/models/
