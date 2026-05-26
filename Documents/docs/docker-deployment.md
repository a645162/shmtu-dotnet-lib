# SHMTU OCR Server Docker 部署指南

## 镜像版本说明

| 版本 | 标签 | 说明 | 适用场景 |
|------|------|------|----------|
| **CPU** | `latest` / `1.0.0` | 基于 `linux/amd64` + `linux/arm64`，无 GPU 依赖 | 通用场景，x86 和 ARM 服务器 |
| **GPU** | `latest-gpu` / `1.0.0-gpu` | 基于 NVIDIA CUDA 12.4，需要 GPU 支持 | 有 NVIDIA GPU 的服务器，推断加速 |

## 镜像地址

### 1. Docker Hub（推荐国内用户）

```bash
# CPU 版本
docker pull a645162/shmtu-ocr-server:latest
docker pull a645162/shmtu-ocr-server:1.0.0

# GPU 版本
docker pull a645162/shmtu-ocr-server:latest-gpu
docker pull a645162/shmtu-ocr-server:1.0.0-gpu
```

### 2. GitHub Container Registry（适合海外/开发者）

```bash
# 登录（需要 GitHub Token）
echo $GITHUB_TOKEN | docker login ghcr.io -u <用户名> --password-stdin

# CPU 版本
docker pull ghcr.io/a645162/shmtu-terminal/shmtu-ocr-server:latest
docker pull ghcr.io/a645162/shmtu-terminal/shmtu-ocr-server:1.0.0

# GPU 版本
docker pull ghcr.io/a645162/shmtu-terminal/shmtu-ocr-server:latest-gpu
docker pull ghcr.io/a645162/shmtu-terminal/shmtu-ocr-server:1.0.0-gpu
```

### 3. 阿里云容器镜像服务（适合国内企业）

```bash
# 登录
docker login registry.cn-shanghai.aliyuncs.com -u <用户名>

# CPU 版本
docker pull registry.cn-shanghai.aliyuncs.com/a645162/shmtu-ocr-server:latest
docker pull registry.cn-shanghai.aliyuncs.com/a645162/shmtu-ocr-server:1.0.0

# GPU 版本
docker pull registry.cn-shanghai.aliyuncs.com/a645162/shmtu-ocr-server:latest-gpu
docker pull registry.cn-shanghai.aliyuncs.com/a645162/shmtu-ocr-server:1.0.0-gpu
```

## 快速启动

### CPU 版本

```bash
docker run -d \
  --name shmtu-ocr-server \
  -p 21600:21600 \
  -p 21601:21601 \
  -v /path/to/models:/app/models:ro \
  a645162/shmtu-ocr-server:latest
```

### GPU 版本

```bash
docker run -d \
  --name shmtu-ocr-server-gpu \
  -p 21600:21600 \
  -p 21601:21601 \
  -v /path/to/models:/app/models:ro \
  --gpus all \
  a645162/shmtu-ocr-server:latest-gpu
```

### 使用 Docker Compose

```bash
# CPU 版本
docker compose up -d

# GPU 版本
docker compose -f docker-compose.yml -f docker-compose.gpu.yml up -d
```

## 配置说明

### 环境变量

| 变量 | 默认值 | 说明 |
|------|--------|------|
| `OcrServer__ModelDirectory` | `/app/models` | ONNX 模型文件目录 |
| `OcrServer__ExecutionProvider` | `CPU` | 推理提供者：`CPU` 或 `CUDA` |
| `OcrServer__GpuDeviceId` | `0` | GPU 设备 ID |
| `OcrServer__PoolSize` | `0` (自动) | CasOcr 对象池大小，建议设为 CPU 核心数 |
| `OcrServer__TcpPort` | `21601` | TCP 服务端口 |
| `ASPNETCORE_URLS` | `http://+:21600` | HTTP 服务地址 |

### 端口说明

| 端口 | 协议 | 说明 |
|------|------|------|
| `21600` | HTTP (RESTful) | RESTful API |
| `21601` | TCP | TCP 二进制协议 |

## API 使用

### RESTful API

```bash
# 健康检查
curl http://localhost:21600/api/health

# OCR 识别
curl -X POST http://localhost:21600/api/ocr \
  -H "Content-Type: application/json" \
  -d '{"imageBase64": "'$(base64 -w0 captcha.png)'"}'

# 文件上传
curl -X POST http://localhost:21600/api/ocr/upload \
  -F "file=@captcha.png"
```

响应格式：

```json
{
  "success": true,
  "expression": "3 + 5 = 8",
  "result": 8,
  "equalSymbol": 1,
  "operator": 0,
  "digit1": 3,
  "digit2": 5
}
```

### TCP 协议

```python
import socket

sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
sock.connect(('127.0.0.1', 21601))
with open('captcha.png', 'rb') as f:
    sock.sendall(f.read() + b'<END>')
response = b''
while chunk := sock.recv(4096):
    response += chunk
print(response.decode().strip())  # e.g. "3 + 5 = 8"
sock.close()
```

## 模型文件

将 ONNX 模型文件放入挂载目录：

```
/path/to/models/
├── resnet18_equal_symbol_latest.onnx
├── resnet18_operator_latest.onnx
└── resnet34_digit_latest.onnx
```

首次启动时，如果模型目录为空，服务会自动从 Gitee 下载模型。

## 硬件要求

### CPU 版本

- 架构：x86_64 或 ARM64
- 内存：建议 2GB+
- 磁盘：约 600MB

### GPU 版本

- NVIDIA GPU（支持 CUDA 12.x）
- 驱动：建议 525+
- 内存：建议 4GB+ VRAM
- 磁盘：约 2GB

## 常见问题

### Q: 如何选择 CPU 还是 GPU 版本？

- 无 GPU 或追求通用性 → CPU 版本
- 有 NVIDIA GPU 且对推断速度有要求 → GPU 版本

### Q: 如何验证 GPU 是否正常工作？

```bash
# 检查容器日志
docker logs <container-name>

# 应该看到类似输出
[OCR] Using CUDA GPU device 0
```

### Q: 如何调整推理池大小？

```bash
docker run -e OcrServer__PoolSize=4 ...
```

建议设置为 CPU 核心数的 1-2 倍。

### Q: 多卡环境下如何使用特定 GPU？

```bash
docker run --gpus '"device=1"' ...
```
