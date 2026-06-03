# 高级文档总览

本节面向 `shmtu-dotnet-lib` 的**贡献者、维护者与高级使用者**，解释模块划分、关键流程的内部实现与扩展点。

## 阅读建议

按以下顺序阅读，建立完整心智模型：

1. [模块结构](/advanced/module-structure) — 仓库的项目分解与依赖
2. [CAS 登录链路](/advanced/cas-flow) — 详细时序图与重试策略
3. [验证码解析器](/advanced/captcha-resolver) — 抽象与多实现
4. [同步抽象与存储](/advanced/sync-store) — IBillStore 与适配
5. [ONNX 模型格式](/advanced/onnx-models) — 输入输出与训练
6. [多语言绑定](/advanced/multi-language) — 与 Rust/Python/Go 的互操作
7. [NuGet 发布与 CI](/advanced/nuget-ci) — 版本号与发布流水线

## 与其他仓库的关系

```
┌──────────────────────────────────────────────────────────────┐
│                shmtu-dotnet-lib (本仓库)                      │
│                  纯类库，无 UI                                  │
│                  .NET 8 / NuGet                               │
└──────┬───────────────────────────────────────────┬────────────┘
       │ 引用                                       │ 引用
       ▼                                            ▼
┌──────────────────────────┐         ┌────────────────────────┐
│ shmtu-terminal-desktop   │         │ 第三方 .NET 应用        │
│ (Avalonia 桌面 UI)        │         │ 自定义服务/工具         │
└──────────────────────────┘         └────────────────────────┘
```

## 关键设计原则

- **库不假设 UI**：库代码中**绝不**引用 `Avalonia` / `WPF` / `WinForms` 等 UI 框架
- **库不假设存储**：`IBillStore` 让调用方选择 Sqlite / JSON / 内存 / 其他
- **库不假设 OCR 方式**：`ICaptchaResolver` 允许手动 / 远程 / 本地三种实现
- **协议与实现分离**：HTTP/TCP 是可选的对外接口，核心是 `CasOcr`

## 模块依赖图

```
Core/
├── shmtu-dotnet-lib
│   ├── cas/                  CAS 认证
│   │   ├── auth/             Flurl.Http 调用
│   │   └── captcha/          ICaptchaResolver + 实现
│   ├── sync/                 BillSync 抽象
│   ├── parser/               HtmlAgilityPack
│   ├── datatype/             bill / auth 数据类型
│   ├── export/               CSV/JSON/钱迹
│   ├── Classifier/           关键字匹配
│   └── utils/                工具
│
└── shmtu-dotnet-demo         控制台 demo

ocr/
├── shmtu-ocr-onnx-lib        ONNX 推理核心
├── shmtu-ocr-onnx-server     HTTP 服务
├── shmtu-ocr-cli             TCP 服务 / CLI
├── shmtu-ocr-onnx-demo       控制台 demo
├── shmtu-ocr-onnx-demo-gui   Avalonia GUI demo
└── shmtu-ocr-onnx-tests      单元测试

Nuget/                        NuGet 打包配置
Scripts/                      Python 辅助脚本
docker-compose*.yml           Docker 编排
```

## 下一步

阅读 [模块结构](/advanced/module-structure) 了解详细的项目分解。
