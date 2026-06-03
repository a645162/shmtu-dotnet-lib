---
layout: home

hero:
  name: shmtu-dotnet-lib
  text: 文档
  tagline: 上海海事大学校园消费账单 .NET 基础库 — CAS 认证、ONNX 推理、HTTP/TCP OCR 服务
  actions:
    - theme: brand
      text: 快速开始
      link: /guide/quick-start
    - theme: alt
      text: Docker 部署
      link: /guide/docker-deployment

features:
  - title: CAS 统一认证
    details: 自动完成上海海事大学 CAS 登录流程，支持验证码自动识别，无需手动输入
  - title: ONNX 本地推理
    details: 基于 ResNet18/34 模型的本地 ONNX 推理引擎，无需外部服务即可识别验证码
  - title: OCR 服务部署
    details: 提供 HTTP API 与 TCP 两种形式的 OCR 服务，支持 base64 和文件上传接口，可独立部署
  - title: .NET 8 + 多平台
    details: 核心库基于 .NET 8 编写，可被 .NET 6+ 项目引用，跨平台运行 Windows / Linux / macOS
  - title: 账单同步抽象
    details: 通过 IBillStore 接口解耦存储后端，可与 Sqlite、JSON、内存或自定义存储对接
  - title: NuGet 包发布
    details: 核心库和 OCR 库均发布到 NuGet，方便其他项目集成使用
---

## 这是什么

`shmtu-dotnet-lib` 是上海海事大学终端应用系列的 **.NET 基础库**，提供：

- **CAS 认证流程** — 自动登录校园 CAS 系统
- **账单同步** — 通用 `BillSync` 抽象，与 `shmtu-terminal-desktop` 等 UI 层解耦
- **HTML/JSON 解析** — 把 CAS 响应转换为强类型对象
- **账单分类** — 13 个内置分类 + 可扩展的关键字匹配
- **ONNX 推理** — ResNet 模型本地推理引擎
- **OCR 服务** — HTTP 和 TCP 两种协议的可独立部署服务

> 本仓库不包含 UI 界面，是纯类库。`shmtu-terminal-desktop` 是基于本库构建的 Avalonia 桌面应用。

## 谁应该用

- 想**集成 CAS 自动登录**的 .NET 应用
- 想**自动同步校园卡账单**到自己的服务
- 想**部署独立的 OCR 服务**给其他客户端调用
- 想**复用 ONNX 模型**做自己的验证码识别

## 谁不应该用

- 想直接看账单界面 — 请用 [shmtu-terminal-desktop](https://github.com/a645162/shmtu-terminal-desktop)
- 想看 Tauri 版 — 请用 [shmtu-terminal-tauri](https://github.com/a645162/shmtu-terminal-tauri)
- 想看 Android 客户端 — 请用 [shmtu-terminal-android](https://github.com/a645162/shmtu-terminal-android)

## 5 分钟上手

```bash
dotnet add package shmtu-dotnet-lib
dotnet add package shmtu-ocr-onnx-lib
```

```csharp
// 1. 登录 CAS
var auth = new EpayAuth();
await auth.LoginAsync("学号", "密码", captchaAnswer);

// 2. 拉取账单
var bills = await BillSync.RunAsync(auth, account, store, options);

// 3. OCR 识别
var ocr = new CasOcr(modelDir);
var text = ocr.Recognize(imageBytes);
```

## 文档导航

### 使用指南

- [快速开始](/guide/quick-start) — 从 NuGet 引用到第一次拉取账单
- [NuGet 集成](/guide/nuget-integration) — 各类项目如何引用
- [CAS 认证](/guide/cas-auth) — 登录流程、Cookie 管理、续期
- [账单同步 (BillSync)](/guide/bill-sync) — 同步抽象、选项、去重与早停
- [HTML 解析](/guide/html-parser) — 把 CAS 返回的 HTML 转为强类型
- [账单分类](/guide/bill-classifier) — 13 个分类与匹配规则
- [ONNX 推理](/guide/onnx-inference) — 模型加载、推理、结果解析
- [OCR HTTP 服务](/guide/ocr-server) — 独立部署的 HTTP OCR
- [OCR TCP 服务](/guide/ocr-tcp-server) — 高性能 TCP 版
- [数据导出](/guide/export) — CSV / JSON / 钱迹
- [Docker 部署](/guide/docker-deployment) — CPU / GPU 镜像
- [FAQ](/guide/faq) — 常见问题

### 高级文档

- [总览](/advanced/overview) — 阅读指引
- [模块结构](/advanced/module-structure) — 项目分解与依赖
- [CAS 登录链路](/advanced/cas-flow) — 详细时序图
- [验证码解析器](/advanced/captcha-resolver) — 抽象与多实现
- [同步抽象与存储](/advanced/sync-store) — IBillStore 与适配
- [ONNX 模型格式](/advanced/onnx-models) — 输入输出与训练
- [多语言绑定](/advanced/multi-language) — 与其他生态的互操作
- [NuGet 发布与 CI](/advanced/nuget-ci) — 版本号策略与发布流
