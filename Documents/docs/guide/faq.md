# FAQ

常见问题与解决方案。

## NuGet 相关

### 安装失败：`NU1605: Detected package downgrade`

```bash
dotnet nuget locals all --clear
dotnet restore
```

### 离线环境怎么用

下载 `.nupkg` 文件并放在本地 feed：

```bash
dotnet nuget add source /path/to/local --name local
dotnet add package shmtu-dotnet-lib --source local
```

## CAS 认证

### `Captcha failed`

- 切换为远程 OCR：检查 OCR 服务是否启动
- 增加重试：在循环中重试 3-5 次
- 切回手动：UI 弹窗让用户输入

### `登录失败，但密码正确`

- 校园账号被锁定 → 登录 web 端解锁
- CAS 服务器维护 → 等几分钟
- 客户端时间不准确 → 同步系统时间

### Cookie 失效

CAS TGC 默认 2 小时。`RefreshAsync()` 续期：

```csharp
if (!await auth.RefreshAsync())
{
    // 重新登录
}
```

## BillSync

### 同步很慢

- 调小 `MaxPages`
- 调大 `EarlyStopThreshold`
- 用 `IProgress<>` 看是不是卡在某页

### 重复账单

- 检查 `IBillStore.Contains` 是否正确去重
- 多个账号用同一个 `IBillStore` 时按 `account_id` 隔离

### 某些商户分类错

自定义规则：

```csharp
classifier.AddRule("新开的店", BillCategory.Other);
```

## ONNX

### 模型文件找不到

```csharp
var ocr = new CasOcr(modelDir: Path.Combine(AppContext.BaseDirectory, "ocr/models"));
```

确保模型目录在输出目录中：

```xml
<ItemGroup>
  <None Update="ocr/models/*.onnx" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

### 推理报错：`Microsoft.ML.OnnxRuntime.dll not found`

安装正确的变体：

```bash
dotnet add package Microsoft.ML.OnnxRuntime   # CPU
dotnet add package Microsoft.ML.OnnxRuntime.Gpu  # CUDA
dotnet add package Microsoft.ML.OnnxRuntime.DirectML  # Windows DirectML
```

### 首次推理很慢

ONNX Runtime 首次调用会编译模型图。预热：

```csharp
ocr.Recognize(dummyBytes);  // 预热
```

## Docker 部署

### 容器无法访问 CAS

CAS 是校园内网，容器需要：

1. 与宿主机在同一校园网
2. 或通过 VPN/代理访问

### 模型下载失败

GitHub Release 可能被墙。挂代理：

```yaml
# docker-compose.yml
services:
  ocr:
    environment:
      - HTTP_PROXY=http://proxy:8080
      - HTTPS_PROXY=http://proxy:8080
```

### 端口冲突

修改 `docker-compose.yml` 端口映射：

```yaml
ports:
  - "5001:5000"  # 主机:容器
```

## 性能

### 1 万笔账单同步需要多久

| 阶段 | 耗时 |
|---|---|
| 网络请求（30 页） | 5-15s |
| HTML 解析 | < 0.5s |
| 分类 | < 0.1s |
| 写入数据库 | 0.5-1s |
| **总计** | **~10s** |

### 内存占用

| 组件 | 内存 |
|---|---|
| CasOcr (3 模型) | ~600MB |
| BillSync 一次 | < 50MB |
| 全量解析 1 万条 | ~30MB |

## 反馈

- 提 Issue: [github.com/a645162/shmtu-dotnet-lib/issues](https://github.com/a645162/shmtu-dotnet-lib/issues)
- 邮件: 见仓库 README

## 下一步

回到 [快速开始](/guide/quick-start) 或 [高级文档总览](/advanced/overview)。
