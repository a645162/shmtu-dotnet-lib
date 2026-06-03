# OCR TCP 服务

`shmtu-ocr-cli` 提供基于 TCP 协议的 OCR 服务，相比 HTTP 在**高并发低延迟**场景下有 2-3x 性能优势。本章说明协议格式、客户端实现和性能对比。

## 启动

```bash
cd shmtu-dotnet-lib/ocr/shmtu-ocr-cli
dotnet run -- --port 6000
```

监听 `tcp://0.0.0.0:6000`。

## 协议格式

简单二进制协议（Length-Prefixed）：

```
┌──────────────┬──────────────┐
│  Length (4B) │  Payload     │
│  Big-endian  │  PNG bytes   │
└──────────────┴──────────────┘
```

### 请求

```csharp
var stream = tcpClient.GetStream();

// 1. 发送长度（4 字节 big-endian）
var lenBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(captcha.Length));
await stream.WriteAsync(lenBytes);

// 2. 发送 PNG/JPG 数据
await stream.WriteAsync(captcha);
await stream.FlushAsync();
```

### 响应

```
┌──────────────┬──────────────┬────────────┐
│  Length (4B) │  Text (UTF8) │  \n        │
└──────────────┴──────────────┴────────────┘
```

```csharp
// 1. 读长度
var lenBuf = new byte[4];
await stream.ReadExactlyAsync(lenBuf);
var len = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(lenBuf));

// 2. 读文字 + 换行
var textBuf = new byte[len];
await stream.ReadExactlyAsync(textBuf);
var text = Encoding.UTF8.GetString(textBuf).TrimEnd('\n');
```

## 完整客户端示例

```csharp
using System.Net;
using System.Net.Sockets;
using System.Text;

public class TcpOcrClient
{
    private readonly string _host;
    private readonly int _port;

    public TcpOcrClient(string host = "127.0.0.1", int port = 6000)
    {
        _host = host;
        _port = port;
    }

    public async Task<string> RecognizeAsync(byte[] imageBytes, CancellationToken ct = default)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(_host, _port, ct);
        var stream = client.GetStream();

        // 发送长度
        var lenBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(imageBytes.Length));
        await stream.WriteAsync(lenBytes, ct);
        // 发送图片
        await stream.WriteAsync(imageBytes, ct);
        await stream.FlushAsync(ct);

        // 接收响应
        var lenBuf = new byte[4];
        await stream.ReadExactlyAsync(lenBuf, ct);
        var len = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(lenBuf));

        var textBuf = new byte[len];
        await stream.ReadExactlyAsync(textBuf, ct);
        return Encoding.UTF8.GetString(textBuf).TrimEnd('\n');
    }
}
```

## 并发性能

TCP 没有 HTTP 请求/响应头开销，单次 RTT 约：

| 协议 | 单次 RTT | 100 并发 QPS |
|---|---|---|
| HTTP (LAN) | ~3ms | ~3000 |
| TCP (LAN) | ~1ms | ~8000 |

## 适用场景

| 场景 | 推荐 |
|---|---|
| 单客户端偶尔调用 | HTTP（简单） |
| 多客户端中等并发 | HTTP |
| 大量客户端高频 | **TCP** |
| 跨网络/WAN | HTTP（更易调试） |
| 服务内部组件 | **TCP**（最快） |

## 错误处理

| 异常 | 含义 |
|---|---|
| `IOException` | 连接断开 / 超时 |
| `ProtocolException` | 长度字段错误 |
| `ServerException` | 服务器内部错误（罕见） |

服务器侧错误响应：

```
┌──────────────┬──────────────┐
│  -1 (4B)     │  Error msg   │
└──────────────┴──────────────┘
```

## 安全

TCP 协议**无内置鉴权**，建议：

1. **仅监听 localhost** 或内网
2. 用防火墙限制 IP 段
3. 需要鉴权时叠加 TLS（见 `shmtu-ocr-cli --tls`）

## 下一步

- [OCR HTTP 服务](/guide/ocr-server) — 更通用的选择
- [Docker 部署](/guide/docker-deployment) — 容器化
