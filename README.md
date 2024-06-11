# shmtu-cas-dotnet

上海海事大学CAS系统 登录流程(.Net Core版本)

## 移植说明

[本项目](https://github.com/a645162/shmtu-cas-dotnet)
移植自Kotlin版本，项目仓库：
[a645162/shmtu-cas-kotlin](https://github.com/a645162/shmtu-cas-kotlin)

这是一个使用.Net Core实现的上海海事大学CAS系统登录流程的项目。
为后面推出.Net Core+AvaloniaUI的项目做准备。

[a645162/shmtu-terminal-desktop](https://github.com/a645162/shmtu-terminal-desktop)

### 技术栈优势

- 使用.Net Core技术栈
- MVVM架构
- 跨平台
- 方便开发与**调试**
- 高性能

### 依赖移植

- OkHTTP -> Flurl.Http

#### Flurl.Http VS RestSharp

由于Flurl.Http的链式调用风格，与Kotlin版本的OkHTTP更为接近，所以选择了Flurl.Http。

RestSharp也是一个不错的选择，但是在链式调用方面不如Flurl.Http。
RestSharp开始于2009年，Flurl.Http开始于2014年。
虽然Flurl.Http是一个比较新的库，但是已经存在了10年(截止到2024年6月)之久。

**Ref**

https://code-maze.com/httpclient-vs-restsharp/

https://blog.csdn.net/qq_20984273/article/details/135799658

https://www.libhunt.com/compare-Flurl-vs-RestSharp

## 支持的功能

- 账单查询
- 热水查询

## 使用方法

本地运行`shmtu-cas-ocr-server`，然后再运行本项目。

您可以前往
[GitHub Release](https://github.com/a645162/shmtu-cas-ocr-server/releases)
下载`shmtu-cas-ocr-server`。

## 技术栈

- .Net Core 8
- C\#
- Flurl.Http

## 确定不做的功能

由于微信客户端有单独的登录逻辑，因此无法实现。

- 电费监控

## 贡献指南

1. Fork 本项目
2. 创建您的特性分支 (`git checkout -b feature/AmazingFeature`)
3. 提交您的更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到远程分支 (`git push origin feature/AmazingFeature`)

## 贡献者

- 孔昊旻(Haomin Kong)

期待您的加入~

## 本系列项目

### 客户端

* 桌面版客户端
  [a645162/shmtu-terminal-desktop](https://github.com/a645162/shmtu-terminal-desktop)

* 基础库
  [a645162/shmtu-cas-dotnet](https://github.com/a645162/shmtu-cas-dotnet)
  这是桌面版客户端的基础库。

* Android客户端(Google Play)
  推荐前往Google Play商店下载App体验验证码识别。
  [Play商店](https://play.google.com/store/apps/details?id=com.khm.shmtu.cas.ocr.demo)

### 服务器部署模型

验证码OCR识别系列项目今后将只会维护推理服务器(shmtu-cas-ocr-server)这一个项目。

[a645162/shmtu-cas-ocr-server](https://github.com/a645162/shmtu-cas-ocr-server)

注：这个项目为课程设计项目，**仅用作学习用途**！！！

- 王老师的研究生课程《机器视觉》的课程设计
- 鲜老师的研究生课程《人工智能》的课程设计

### 统一认证登录流程(数字平台+微信平台)

* Kotlin版(方便移植Android)
  [a645162/shmtu-cas-kotlin](https://github.com/a645162/shmtu-cas-kotlin)
* Go版(为Wails桌面客户端做准备)
  [a645162/shmtu-cas-go](https://github.com/a645162/shmtu-cas-go)
* .Net Core版(为AvaloniaUI桌面客户端做准备)
  [a645162/shmtu-cas-dotnet](https://github.com/a645162/shmtu-cas-dotnet)
* Rust版(未来想做Tauri桌面客户端可能会移植)
  ps.功能其实和Golang版本没啥区别，甚至可能实现地更费劲，Golang的移植已经让我比较抓狂了，虽然Rust我也是会的，但是或许不会做。。。

注：这个项目为王老师的研究生课程《机器视觉》的课程设计项目，**仅用作学习用途**！！！

### 模型训练

**神经网络图像分类模型训练**

使用PyTorch以及经典网络ResNet

[a645162/shmtu-cas-ocr-model](https://github.com/a645162/shmtu-cas-ocr-model)

**人工标注的数据集(2选1下载)**

* Hugging Face
  [a645162/shmtu_cas_validate_code](https://huggingface.co/datasets/a645162/shmtu_cas_validate_code)
* Gitee AI(国内较快)
  [a645162/shmtu_cas_validate_code](https://ai.gitee.com/datasets/a645162/shmtu_cas_validate_code)

训练代码中包含爬虫代码，以及自动测试识别结果代码。
您可以对其修改，对测试通过的图片进行标注，这样可以获得准确的标注。

注：这个项目为王老师的研究生课程《机器视觉》的课程设计项目，**仅用作学习用途**！！！

### 模型本地部署

* Windows客户端(包括VC Win32 GUI以及C# WPF)
  [a645162/shmtu-cas-ocr-demo-windows](https://github.com/a645162/shmtu-cas-ocr-demo-windows)
* Qt客户端(支持Windows/macOS/Linux)
  [a645162/shmtu-cas-ocr-demo-qt](https://github.com/a645162/shmtu-cas-ocr-demo-qt)
* Android客户端
  [a645162/shmtu-cas-demo-android](https://github.com/a645162/shmtu-cas-demo-android)
* Android客户端(Google Play)
  [Play商店](https://play.google.com/store/apps/details?id=com.khm.shmtu.cas.ocr.demo)

注：这3个项目为王老师的研究生课程《机器视觉》的课程设计项目，**仅用作学习用途**！！！

### 原型测试

Python+Selenium4自动化测试数字海大平台登录流程

[a645162/Digital-SHMTU-Tools](https://github.com/a645162/Digital-SHMTU-Tools)

注：本项目为付老师的研究生课程《Python程序设计与开发》的课程设计项目，**仅用作学习用途**！！！

### 废弃的项目

* Go Wails版(停止开发)
  [a645162/SHMTU-Terminal-Wails](https://github.com/a645162/SHMTU-Terminal-Wails)
* Rust Tauri版(不折腾了~)

这两个都是编译型语言做后端+调用本地浏览器的方案，由于数据传递较为复杂，软件架构复杂，因此不再开发。

## 免责声明

本(系列)项目**仅供学习交流使用**，不得用于商业用途，如有侵权请联系作者删除。

本(系列)项目为个人开发，与上海海事大学无关，**仅供学习参考，请勿用于非法用途**。

本(系列)项目为孔昊旻同学的**课程设计**项目，**仅用作学习用途**！！！
