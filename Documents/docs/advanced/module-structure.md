# 模块结构

`shmtu-dotnet-lib` 是一个包含多个子项目的 monorepo，使用 `shmtu-dotnet.sln` 统一管理。本章说明每个子项目的职责、依赖关系与发布目标。

## 解决方案结构

```bash
shmtu-dotnet.sln
├── Core/
│   ├── shmtu-dotnet-lib/        主类库（发布到 NuGet）
│   └── shmtu-dotnet-demo/       控制台 demo
│
├── ocr/
│   ├── shmtu-ocr-onnx-lib/      ONNX 推理（发布到 NuGet）
│   ├── shmtu-ocr-onnx-server/   ASP.NET Core HTTP 服务
│   ├── shmtu-ocr-cli/           TCP 服务 + CLI 工具
│   ├── shmtu-ocr-onnx-demo/     控制台 demo
│   ├── shmtu-ocr-onnx-demo-gui/ Avalonia GUI demo
│   └── shmtu-ocr-onnx-tests/    xUnit 单元测试
│
├── Nuget/                        NuGet 打包配置
├── Scripts/                      Python 辅助脚本
├── docker-compose.yml            生产部署
└── docker-compose.gpu.yml        GPU 部署
```

## 子项目详解

### shmtu-dotnet-lib（核心）

发布到 [nuget.org/packages/shmtu-dotnet-lib](https://www.nuget.org/packages/shmtu-dotnet-lib)

```xml
<TargetFramework>net8.0</TargetFramework>
<AssemblyName>shmtu-dotnet-lib</AssemblyName>
<PackageReadmeFile>Package-ReadMe.md</PackageReadmeFile>
<PackageLicenseExpression>GPL-3.0-only</PackageLicenseExpression>
<Version>2.0.0.1</Version>

<PackageReference Include="Flurl.Http" Version="4.0.2" />
<PackageReference Include="HtmlAgilityPack" Version="1.12.4" />
```

#### 命名空间

```csharp
namespace shmtu.cas.auth;            // CAS 认证
namespace shmtu.cas.captcha;         // 验证码解析
namespace shmtu.sync;                // 账单同步
namespace shmtu.parser.bill;         // HTML 解析
namespace shmtu.datatype.bill;       // 账单数据类型
namespace shmtu.datatype.auth;       // 认证数据类型
namespace shmtu.export.bill;         // 数据导出
namespace shmtu.classifier;          // 账单分类
namespace shmtu.utils;               // 工具类
```

### shmtu-ocr-onnx-lib（ONNX 推理）

发布到 NuGet（待发布）

```xml
<TargetFramework>net8.0</TargetFramework>
<PackageReference Include="Microsoft.ML.OnnxRuntime" Version="1.18.0" />
<PackageReference Include="SixLabors.ImageSharp" Version="3.1.5" />
```

#### 命名空间

```csharp
namespace shmtu.ocr.onnx;             // 主入口 CasOcr
namespace shmtu.ocr.onnx.Backend;    // ONNX 推理后端
namespace shmtu.ocr.onnx.ImageProcess; // 图像预处理
namespace shmtu.ocr.onnx.Utils;      // 工具
```

### shmtu-ocr-onnx-server（HTTP 服务）

```xml
<TargetFramework>net8.0</TargetFramework>
<OutputType>Exe</OutputType>
<PackageReference Include="Microsoft.AspNetCore.App" />
```

监听端口默认 5000，提供：

- `/health` — 健康检查
- `/captcha/recognize` — base64 图片识别
- `/captcha/recognize-file` — 文件上传识别
- `/metrics` — Prometheus 指标
- `/swagger` — OpenAPI 文档（开发模式）

### shmtu-ocr-cli（TCP 服务 + CLI）

```xml
<TargetFramework>net8.0</TargetFramework>
<OutputType>Exe</OutputType>
```

启动参数：

```bash
dotnet run -- --port 6000           # TCP 服务模式
dotnet run -- --cli image.png       # CLI 模式（单次识别）
dotnet run -- --tls                 # 启用 TLS
```

### shmtu-ocr-onnx-tests（测试）

```xml
<TargetFramework>net8.0</TargetFramework>
<IsPackable>false</IsPackable>
<PackageReference Include="xunit" Version="2.6.0" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.5.0" />
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
```

运行：

```bash
dotnet test
```

## 项目依赖

```
shmtu-terminal-desktop ──→ shmtu-dotnet-lib ──→ Flurl.Http
                         └─→ shmtu-ocr-onnx-lib ──→ OnnxRuntime

shmtu-ocr-onnx-server ────→ shmtu-ocr-onnx-lib
shmtu-ocr-cli ────────────→ shmtu-ocr-onnx-lib
shmtu-ocr-onnx-demo ──────→ shmtu-ocr-onnx-lib
shmtu-ocr-onnx-demo-gui ──→ shmtu-ocr-onnx-lib + Avalonia
shmtu-ocr-onnx-tests ─────→ shmtu-ocr-onnx-lib

shmtu-dotnet-demo ────────→ shmtu-dotnet-lib
```

无循环依赖。

## 共享配置

`Directory.Build.props` 定义所有项目的通用属性：

```xml
<Project>
  <PropertyGroup>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

## 全局配置

`global.json` 固定 .NET SDK 版本：

```json
{
  "sdk": {
    "version": "8.0.0",
    "rollForward": "latestFeature"
  }
}
```

## NuGet 源

`NuGet.Config`：

```xml
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

## 构建顺序

```bash
# 1. 还原
dotnet restore shmtu-dotnet.sln

# 2. 构建（自动按依赖顺序）
dotnet build shmtu-dotnet.sln -c Release

# 3. 测试
dotnet test shmtu-dotnet.sln

# 4. 打包
dotnet pack shmtu-dotnet-lib/shmtu-dotnet-lib.csproj -c Release
```

## 下一步

- [CAS 登录链路](/advanced/cas-flow) — 详细时序图
- [NuGet 发布与 CI](/advanced/nuget-ci) — 发布流水线
