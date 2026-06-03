# OCR HTTP 服务

`shmtu-ocr-onnx-server` 是一个独立的 HTTP OCR 服务，把本地 ONNX 推理封装为 REST API。客户端无需安装 ONNX 运行时，只需 HTTP 请求即可。

## 启动

### Docker（推荐）

```bash
cd shmtu-dotnet-lib/ocr
docker compose -f docker-compose.yml up -d
```

监听 `http://0.0.0.0:5000`。

### 手动启动

```bash
cd shmtu-dotnet-lib/ocr/shmtu-ocr-onnx-server
dotnet run
```

## 端点

| 方法 | 路径 | 说明 |
|---|---|---|
| GET | `/health` | 健康检查 |
| GET | `/version` | 版本信息 |
| POST | `/captcha/recognize` | base64 图片 |
| POST | `/captcha/recognize-file` | multipart 文件上传 |
| GET | `/swagger` | OpenAPI 文档（仅开发模式） |

## 健康检查

```bash
curl http://127.0.0.1:5000/health
# {"status":"ok","uptime":"00:05:23"}
```

## 识别 base64 图片

```bash
curl -X POST http://127.0.0.1:5000/captcha/recognize \
  -H "Content-Type: application/json" \
  -d '{"image":"<base64>"}'
# {"text":"1234","confidence":0.97,"elapsedMs":48}
```

C# 客户端：

```csharp
var client = new FlurlClient("http://127.0.0.1:5000");
var response = await client.Request("/captcha/recognize")
    .PostJsonAsync(new { image = Convert.ToBase64String(captchaBytes) });
var result = await response.GetJsonAsync<OcrResult>();
Console.WriteLine(result.Text);
```

## 上传文件

```bash
curl -X POST http://127.0.0.1:5000/captcha/recognize-file \
  -F "file=@captcha.png"
# {"text":"1234","confidence":0.97,"elapsedMs":48}
```

## 响应格式

```json
{
  "text": "1234",
  "confidence": 0.97,
  "elapsedMs": 48,
  "modelVersions": {
    "equalSymbol": 5,
    "operator": 3,
    "digit": 7
  }
}
```

| 字段 | 类型 | 含义 |
|---|---|---|
| `text` | string | 识别结果（已拼接） |
| `confidence` | float 0-1 | 平均置信度 |
| `elapsedMs` | int | 推理耗时（毫秒） |
| `modelVersions` | object | 各模型 epoch |

## 配置

`appsettings.json`：

```json
{
  "Server": {
    "Host": "0.0.0.0",
    "Port": 5000
  },
  "Ocr": {
    "ModelDir": "ocr/models",
    "UseGpu": false,
    "IntraOpNumThreads": 2,
    "MaxImageBytes": 1048576
  },
  "Auth": {
    "RequireApiKey": false,
    "ApiKey": ""
  }
}
```

可通过环境变量覆盖：

```bash
export OCR__UseGpu=true
export OCR__IntraOpNumThreads=4
```

## 鉴权（可选）

```json
{
  "Auth": {
    "RequireApiKey": true,
    "ApiKey": "your-secret-key"
  }
}
```

客户端带 `X-API-Key` header：

```bash
curl -H "X-API-Key: your-secret-key" \
  -X POST http://127.0.0.1:5000/captcha/recognize \
  -d '{"image":"..."}'
```

## 限流

内置基于 IP 的滑动窗口限流（60 req/min）：

```json
{
  "RateLimit": {
    "RequestsPerMinute": 60,
    "Burst": 10
  }
}
```

超限返回 429 Too Many Requests。

## 性能调优

| 场景 | 配置 |
|---|---|
| 单客户端高频 | `IntraOpNumThreads = 1` |
| 多客户端并发 | `IntraOpNumThreads = 4` + 多 worker |
| 极致性能 | `UseGpu = true` + 多 GPU |

Kestrel 调优（`appsettings.json`）：

```json
{
  "Kestrel": {
    "Limits": {
      "MaxConcurrentConnections": 100,
      "MaxConcurrentUpgradedConnections": 100
    },
    "Endpoints": {
      "Http": { "Url": "http://0.0.0.0:5000" }
    }
  }
}
```

## 部署模式

### 模式 1：单实例

适合个人/小团队。Docker compose 即可。

### 模式 2：反向代理

```nginx
location /ocr/ {
    proxy_pass http://ocr-cluster/;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
}
```

### 模式 3：集群

```yaml
# docker-compose.yml
services:
  ocr-1:
    image: shmtu-ocr-server:latest
    ports: ["5001:5000"]
  ocr-2:
    image: shmtu-ocr-server:latest
    ports: ["5002:5000"]
```

客户端用轮询/最少连接选择实例。

## 监控

内置 Prometheus 指标（默认 `/metrics`）：

```
ocr_requests_total{status="ok"} 1234
ocr_requests_total{status="failed"} 5
ocr_request_duration_seconds_bucket{le="0.05"} 1100
ocr_active_requests 2
```

Grafana 仪表盘 JSON 见 `monitoring/grafana-dashboard.json`。

## 下一步

- [OCR TCP 服务](/guide/ocr-tcp-server) — 高吞吐 TCP 版
- [Docker 部署](/guide/docker-deployment) — CPU/GPU 镜像
