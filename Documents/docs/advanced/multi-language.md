# 多语言绑定

`shmtu-dotnet-lib` 主要面向 .NET 生态，但也通过 HTTP/TCP 服务暴露能力给其他语言。本章说明如何从 Rust、Python、Go、Java 等调用本库。

## 调用方式总览

| 方式 | 适用 | 复杂度 |
|---|---|---|
| HTTP 同步调用 | 任何语言 | ⭐ |
| TCP 同步调用 | 高性能 | ⭐⭐ |
| NuGet 直接引用 | .NET 项目 | ⭐ |
| 进程内 P/Invoke | C/C++/Rust | ⭐⭐⭐ |
| 子进程 + IPC | 任意 | ⭐⭐ |

## HTTP 协议（推荐）

任何能发 HTTP 请求的语言都能用。

### Python

```python
import base64
import requests

def recognize(captcha_bytes: bytes) -> str:
    resp = requests.post(
        "http://127.0.0.1:5000/captcha/recognize",
        json={"image": base64.b64encode(captcha_bytes).decode()},
        timeout=10
    )
    resp.raise_for_status()
    return resp.json()["text"]

# 使用
with open("captcha.png", "rb") as f:
    print(recognize(f.read()))
```

### Go

```go
package main

import (
    "bytes"
    "encoding/base64"
    "encoding/json"
    "fmt"
    "io"
    "net/http"
)

func recognize(captcha []byte) (string, error) {
    body, _ := json.Marshal(map[string]string{
        "image": base64.StdEncoding.EncodeToString(captcha),
    })
    resp, err := http.Post("http://127.0.0.1:5000/captcha/recognize",
        "application/json", bytes.NewReader(body))
    if err != nil {
        return "", err
    }
    defer resp.Body.Close()

    var result struct {
        Text string `json:"text"`
    }
    json.NewDecoder(resp.Body).Decode(&result)
    return result.Text, nil
}

func main() {
    text, _ := recognize([]byte{...})
    fmt.Println(text)
}
```

### Rust

```rust
use base64::Engine;
use serde::{Deserialize, Serialize};

#[derive(Serialize)]
struct Request<'a> {
    image: &'a str,
}

#[derive(Deserialize)]
struct Response {
    text: String,
}

pub async fn recognize(captcha: &[u8]) -> Result<String, reqwest::Error> {
    let image = base64::engine::general_purpose::STANDARD.encode(captcha);
    let client = reqwest::Client::new();
    let resp: Response = client
        .post("http://127.0.0.1:5000/captcha/recognize")
        .json(&Request { image: &image })
        .send()
        .await?
        .json()
        .await?;
    Ok(resp.text)
}
```

### Java

```java
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.util.Base64;
import com.fasterxml.jackson.databind.ObjectMapper;

public class OcrClient {
    private static final String URL = "http://127.0.0.1:5000/captcha/recognize";
    private final HttpClient client = HttpClient.newHttpClient();
    private final ObjectMapper mapper = new ObjectMapper();

    public String recognize(byte[] captcha) throws Exception {
        String image = Base64.getEncoder().encodeToString(captcha);
        var body = mapper.writeValueAsString(java.util.Map.of("image", image));

        var request = HttpRequest.newBuilder(URI.create(URL))
            .header("Content-Type", "application/json")
            .POST(HttpRequest.BodyPublishers.ofString(body))
            .build();

        var response = client.send(request, HttpResponse.BodyHandlers.ofString());
        var result = mapper.readTree(response.body());
        return result.get("text").asText();
    }
}
```

### JavaScript / TypeScript (Node.js)

```typescript
import axios from "axios";

export async function recognize(captcha: Buffer): Promise<string> {
  const { data } = await axios.post("http://127.0.0.1:5000/captcha/recognize", {
    image: captcha.toString("base64"),
  });
  return data.text;
}
```

## TCP 协议

适合需要高并发低延迟的场景。

### Python

```python
import socket
import struct

def recognize_tcp(host="127.0.0.1", port=6000, image_bytes: bytes) -> str:
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        s.connect((host, port))
        # 发送长度（4 字节 big-endian）
        s.sendall(struct.pack(">I", len(image_bytes)))
        # 发送图片
        s.sendall(image_bytes)
        # 接收响应
        length_buf = recv_exactly(s, 4)
        length = struct.unpack(">I", length_buf)[0]
        text_buf = recv_exactly(s, length)
        return text_buf.decode("utf-8").rstrip("\n")

def recv_exactly(sock, n):
    buf = b""
    while len(buf) < n:
        chunk = sock.recv(n - len(buf))
        if not chunk:
            raise ConnectionError("closed")
        buf += chunk
    return buf
```

### Go (TCP)

```go
func RecognizeTCP(host string, port int, image []byte) (string, error) {
    conn, err := net.Dial("tcp", fmt.Sprintf("%s:%d", host, port))
    if err != nil {
        return "", err
    }
    defer conn.Close()

    // 发送长度
    binary.Write(conn, binary.BigEndian, uint32(len(image)))
    // 发送图片
    conn.Write(image)

    // 接收响应
    var length uint32
    binary.Read(conn, binary.BigEndian, &length)
    text := make([]byte, length)
    io.ReadFull(conn, text)
    return strings.TrimRight(string(text), "\n"), nil
}
```

## 进程内调用（C/C++ → .NET）

通过 .NET 7+ 的 `NativeAOT` 或 COM 互操作：

```cpp
// C++ 客户端（COM）
#import "ShmtuDotnetLib.tlb"

IShmtuOcrService* service;
CoCreateInstance(CLSID_ShmtuOcr, NULL, CLSCTX_INPROC_SERVER,
    IID_IShmtuOcrService, (void**)&service);

BSTR result;
service->Recognize(imageBytes, length, &result);
printf("识别: %S\n", result);
```

## 异步 / 流式调用

HTTP 服务支持 SSE (Server-Sent Events) 进行流式输出（开发中）：

```
GET /captcha/recognize-stream
  ↓
data: {"partial": "12"}
data: {"partial": "123"}
data: {"final": "1234"}
```

## 错误处理

跨语言调用时，错误统一通过 HTTP 状态码表达：

| 状态码 | 含义 | 处理 |
|---|---|---|
| 200 | 成功 | 解析 `text` 字段 |
| 400 | 请求格式错误 | 检查图片 base64 |
| 429 | 限流 | 退避后重试 |
| 500 | 服务器错误 | 报告 issue |
| 503 | 服务不可用 | 检查 OCR 服务状态 |

## 性能与成本

| 方式 | 单次延迟 | 吞吐量 | 部署复杂度 |
|---|---|---|---|
| HTTP | 3-10ms | 3000 QPS | ⭐ |
| TCP | 1-3ms | 8000 QPS | ⭐⭐ |
| NuGet (.NET) | < 1ms | 进程内 | ⭐ |
| 进程内 P/Invoke | < 1ms | 进程内 | ⭐⭐⭐ |

## 部署建议

```
┌──────────────┐
│  你的服务     │ (Go / Python / Java / ...)
└──────┬───────┘
       │ HTTP (推荐) 或 TCP
       ▼
┌──────────────┐
│ shmtu-ocr-onnx-server  ←─ 模型文件
│  (Docker)    │
└──────────────┘
```

## 下一步

- [NuGet 发布与 CI](/advanced/nuget-ci) — 库本身如何发布
- [ONNX 模型格式](/advanced/onnx-models) — 模型训练与导出
