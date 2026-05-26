# OCR ONNX 服务 Docker 部署指南

## 前置要求

- Docker 20.10+
- Docker Compose V2
- ONNX 模型文件（约 166.8 MB）

## 快速启动

### 1. 准备模型文件

将 ONNX 模型文件放入 `models/` 目录：

```bash
mkdir -p models
# 将以下文件放入 models/ 目录：
# - resnet18_equal_symbol_latest.onnx (42.6 MB)
# - resnet18_operator_latest.onnx (42.6 MB)
# - resnet34_digit_latest.onnx (81.2 MB)
```

> 如果 models 目录为空，服务启动时会自动从 gitee.com 下载模型文件。

### 2. 使用 Docker Compose 启动

```bash
docker compose up -d
```

### 3. 查看日志

```bash
docker compose logs -f
```

### 4. 验证服务

```bash
# 健康检查
curl http://localhost:21600/api/health

# OCR 识别（Base64 方式）
curl -X POST http://localhost:21600/api/ocr \
  -H "Content-Type: application/json" \
  -d '{"imageBase64": "<base64编码的图片>"}'

# OCR 识别（文件上传方式）
curl -X POST http://localhost:21600/api/ocr/upload \
  -F "file=@captcha.png"
```

## 手动 Docker 构建

```bash
# 构建镜像
docker build -t shmtu-ocr-server .

# 运行容器
docker run -d \
  --name shmtu-ocr-server \
  -p 21600:21600 \
  -p 21601:21601 \
  -v $(pwd)/models:/app/models:ro \
  -e OcrServer__ModelDirectory=/app/models \
  -e OcrServer__TcpPort=21601 \
  -e OcrServer__TcpListenAddress=0.0.0.0 \
  shmtu-ocr-server
```

## 端口说明

| 端口 | 协议 | 用途 |
|------|------|------|
| 21600 | HTTP | ASP.NET Core REST API |
| 21601 | TCP | TCP OCR 服务 |

## 环境变量

| 变量名 | 默认值 | 说明 |
|--------|--------|------|
| `ASPNETCORE_URLS` | `http://+:21600` | HTTP 监听地址 |
| `OcrServer__ModelDirectory` | `/app/models` | ONNX 模型文件目录 |
| `OcrServer__TcpPort` | `21601` | TCP 服务端口 |
| `OcrServer__TcpListenAddress` | `0.0.0.0` | TCP 监听地址 |
| `OcrServer__PoolSize` | `0` | 连接池大小（0=自动，取 CPU 核数与 4 的最大值） |

## 停止服务

```bash
docker compose down
```

## 重新构建

```bash
docker compose build --no-cache
docker compose up -d
```
