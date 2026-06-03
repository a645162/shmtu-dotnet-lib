# NuGet 发布与 CI

本章说明 `shmtu-dotnet-lib` 和 `shmtu-ocr-onnx-lib` 的 NuGet 发布流程、版本号策略与 CI 配置。

## 包发布目标

| 包 | NuGet | 频率 |
|---|---|---|
| `shmtu-dotnet-lib` | ✅ [nuget.org/packages/shmtu-dotnet-lib](https://www.nuget.org/packages/shmtu-dotnet-lib) | 每次 tag |
| `shmtu-ocr-onnx-lib` | ✅ 已发布 | 每次 tag |
| `shmtu-ocr-onnx-server` | ❌ 容器镜像 (GHCR) | 每次 tag |
| `shmtu-ocr-cli` | ❌ 容器镜像 | 每次 tag |

## 版本号策略

遵循 [SemVer 2.0](https://semver.org/)：

```
MAJOR.MINOR.PATCH[-PRERELEASE][+BUILD]
2.0.0
2.0.0-preview.1
2.0.0+build.20250115
```

| 变化类型 | 例子 | 影响 |
|---|---|---|
| **MAJOR** | 1.x → 2.0 | API 破坏性变更 |
| **MINOR** | 2.0 → 2.1 | 新增功能，向后兼容 |
| **PATCH** | 2.0.0 → 2.0.1 | Bug 修复 |

`.csproj` 模板：

```xml
<PropertyGroup>
    <Version>2.0.0.1</Version>
    <AssemblyVersion>2.0.0.1</AssemblyVersion>
    <FileVersion>2.0.0.1</FileVersion>
    <PackageVersion>2.0.0</PackageVersion>
</PropertyGroup>
```

## 包元数据

`Nuget/Package.nuspec` 或 `.csproj` 中的 `<PropertyGroup>`：

```xml
<PropertyGroup>
    <PackageId>shmtu-dotnet-lib</PackageId>
    <Version>2.0.0</Version>
    <Authors>Haomin Kong</Authors>
    <Description>上海海事大学校园消费账单 .NET 基础库</Description>
    <PackageLicenseExpression>GPL-3.0-only</PackageLicenseExpression>
    <PackageProjectUrl>https://github.com/a645162/shmtu-dotnet-lib</PackageProjectUrl>
    <RepositoryUrl>https://github.com/a645162/shmtu-dotnet-lib</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <PackageTags>shmtu cas ocr onnx captcha</PackageTags>
    <PackageReadmeFile>Package-ReadMe.md</PackageReadmeFile>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
</PropertyGroup>
```

## 手动打包

```bash
# 1. 还原
dotnet restore shmtu-dotnet.sln

# 2. 打包（输出到 bin/Release/*.nupkg）
dotnet pack shmtu-dotnet-lib/shmtu-dotnet-lib.csproj \
    -c Release \
    -o ./artifacts

# 3. 推送到 NuGet
dotnet nuget push ./artifacts/shmtu-dotnet-lib.2.0.0.nupkg \
    --api-key $NUGET_API_KEY \
    --source https://api.nuget.org/v3/index.json
```

环境变量：

```bash
export NUGET_API_KEY=oy2xxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
```

## CI 自动发布

`.github/workflows/dotnet.yml`：

```yaml
name: Build and publish to NuGet

on:
  push:
    tags: ["v*", "*"]   # tag 触发发布
  pull_request:
    branches: ["main"]
  release:
    types: [published]

permissions:
  contents: read
  packages: write

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v6

      - name: Setup .NET
        uses: actions/setup-dotnet@v5
        with:
          dotnet-version: |
            8.0.x
            10.0.x

      - name: Restore
        run: dotnet restore shmtu-dotnet.sln

      - name: Build
        run: dotnet build shmtu-dotnet.sln -c Release --no-restore

      - name: Test
        run: dotnet test shmtu-dotnet.sln -c Release --no-build --verbosity normal

      - name: Pack
        run: |
          dotnet pack Core/shmtu-dotnet-lib/shmtu-dotnet-lib.csproj -c Release -o ./artifacts
          dotnet pack ocr/shmtu-ocr-onnx-lib/shmtu-ocr-onnx-lib.csproj -c Release -o ./artifacts

      - name: Push to NuGet
        if: startsWith(github.ref, 'refs/tags/')
        run: |
          dotnet nuget push ./artifacts/*.nupkg \
              --api-key ${{ secrets.NUGET_API_KEY }} \
              --source https://api.nuget.org/v3/index.json \
              --skip-duplicate
        env:
          NUGET_API_KEY: ${{ secrets.NUGET_API_KEY }}
```

## Docker 镜像发布

`docker-publish.yml`：

```yaml
name: Build OCR Server Docker Image

on:
  push:
    tags: ["v*"]
  workflow_dispatch:

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v6

      - name: Log in to GHCR
        uses: docker/login-action@v3
        with:
          registry: ghcr.io
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}

      - name: Build and push
        uses: docker/build-push-action@v6
        with:
          context: ocr/shmtu-ocr-onnx-server
          file: ocr/shmtu-ocr-onnx-server/Dockerfile
          push: true
          tags: |
            ghcr.io/a645162/shmtu-ocr-server:latest
            ghcr.io/a645162/shmtu-ocr-server:${{ github.ref_name }}
            ghcr.io/a645162/shmtu-ocr-server:${{ github.sha }}
          platforms: linux/amd64,linux/arm64
          cache-from: type=gha
          cache-to: type=gha,mode=max
```

## 多架构支持

- `linux/amd64`：x86 服务器
- `linux/arm64`：Apple Silicon / ARM 服务器
- `linux/arm/v7`：Raspberry Pi（仅 CPU 镜像）

`buildx` 自动用 QEMU 模拟。

## 预发布版本

发布 `2.1.0-preview.1`：

```bash
git tag v2.1.0-preview.1
git push origin v2.1.0-preview.1
```

CI 会自动：

1. 构建并测试
2. 打包为 `2.1.0-preview.1`
3. 推送到 NuGet（标记为预发布）

引用：

```xml
<PackageReference Include="shmtu-dotnet-lib" Version="2.1.0-preview.1" />
```

## 弃用策略

1. 标记为 `[Obsolete]`：

```csharp
[Obsolete("请使用 NewMethod")]
public void OldMethod() { }
```

2. 在 CHANGELOG 中说明

3. 经过 1 个 MINOR 版本后（>= 6 个月）才删除

4. 删除时增加 MAJOR 版本号

## 监控发布后

- NuGet 下载统计：[nuget.org/packages/shmtu-dotnet-lib/Stats](https://www.nuget.org/packages/shmtu-dotnet-lib)
- GitHub Insights → Traffic
- GitHub Container Insights

## 应急回滚

发现问题版本：

```bash
# 1. 在 NuGet.org Unlist 该版本（24 小时内生效）
# 2. 推送修复版本
git tag v2.0.2
git push origin v2.0.2

# 3. 通知用户升级
```

> NuGet **不允许删除已发布版本**，只能 Unlist。新版本仍然可以正常引用旧版本（已下载的）。

## 下一步

回到 [高级文档总览](/advanced/overview)。
