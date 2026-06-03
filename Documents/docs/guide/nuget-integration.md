# NuGet 集成

`shmtu-dotnet-lib` 的两个核心包都发布在 [NuGet.org](https://www.nuget.org/)，本文说明如何在不同项目类型中引用。

## 可用包

| 包名 | 用途 | 大小 |
|---|---|---|
| `shmtu-dotnet-lib` | CAS 认证、BillSync 同步、HTML 解析、分类、导出 | ~80KB |
| `shmtu-ocr-onnx-lib` | ONNX 推理引擎（含 resnet18/34） | ~2MB + 模型 |

## 控制台 / 类库

```bash
dotnet add package shmtu-dotnet-lib
```

`.csproj`:

```xml
<PackageReference Include="shmtu-dotnet-lib" Version="2.0.0" />
```

## Avalonia / WPF / WinForms 桌面

直接 `dotnet add package` 即可，库不依赖任何 UI 框架。

```bash
dotnet add package shmtu-dotnet-lib
dotnet add package shmtu-ocr-onnx-lib
```

## ASP.NET Core 服务

在 `Program.cs` 注册服务：

```csharp
builder.Services.AddSingleton<ICaptchaResolver, RemoteOcrHttpCaptchaResolver>();
builder.Services.AddHttpContextAccessor();
// 其他自定义服务...
```

> `EpayAuth` 和 `BillSync` 是无状态类，直接 `new` 即可，不需要 DI 容器。

## 单元测试

库本身**不**依赖具体 UI 或存储后端，可直接在 xUnit / NUnit / MSTest 中测试：

```xml
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
<PackageReference Include="xunit" Version="2.6.0" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.5.0" />
<PackageReference Include="shmtu-dotnet-lib" Version="2.0.0" />
```

测试示例：

```csharp
[Fact]
public async Task BillSync_StopsEarly_OnExisting()
{
    var store = new ListBillStore();
    store.Merge(new List<BillItemInfo> { /* 种子数据 */ });

    var options = new SyncOptions { EarlyStopThreshold = 3 };
    var result = await BillSync.RunAsync(mockAuth, mockAccount, store, options);

    Assert.True(result.PagesFetched <= options.MaxPages);
}
```

## 自定义源（私有 NuGet）

公司内部可搭建私有 NuGet 服务器：

```xml
<PackageSource>
    <Add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <Add key="internal" value="https://nuget.internal/api/v3/index.json" />
</PackageSource>
```

## 离线环境

如果目标机器无法访问 NuGet：

```bash
dotnet nuget locals all --clear
dotnet restore --source /path/to/local-nuget-packages
```

把 `.nupkg` 文件放在 `local-nuget-packages/` 即可。

## 版本选择

| 版本 | 状态 | 推荐 |
|---|---|---|
| `2.0.x` | 稳定 | ✅ 日常使用 |
| `1.x` | 旧版 | 不推荐 |
| `*-preview*` | 预发布 | 试用新功能 |

固定到具体版本号：

```xml
<PackageReference Include="shmtu-dotnet-lib" Version="2.0.0" />
```

> ⚠️ 主版本号变化（如 1.x → 2.x）通常包含 API 破坏性变更，请阅读 [CHANGELOG](https://github.com/a645162/shmtu-dotnet-lib/releases)。

## 下一步

- [CAS 认证](/guide/cas-auth) — 登录流程
- [ONNX 推理](/guide/onnx-inference) — 模型加载
