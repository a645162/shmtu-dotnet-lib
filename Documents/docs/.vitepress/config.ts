import { defineConfig } from 'vitepress'

function resolveBase() {
  const repo = process.env.GITHUB_REPOSITORY?.split('/')[1]
  if (!process.env.GITHUB_ACTIONS || !repo) {
    return '/'
  }
  return repo.endsWith('.github.io') ? '/' : `/${repo}/`
}

export default defineConfig({
  base: resolveBase(),
  lang: 'zh-CN',
  title: 'shmtu-dotnet-lib 文档',
  description: '上海海事大学校园消费账单 .NET 基础库 — CAS 认证、ONNX 推理、HTTP/TCP OCR 服务',
  cleanUrls: true,
  lastUpdated: true,
  themeConfig: {
    nav: [
      { text: '使用指南', link: '/guide/quick-start' },
      { text: '高级文档', link: '/advanced/overview' },
      { text: 'Docker 部署', link: '/guide/docker-deployment' },
    ],
    sidebar: [
      {
        text: '使用指南',
        items: [
          { text: '快速开始', link: '/' },
          { text: '安装与配置', link: '/guide/quick-start' },
          { text: 'NuGet 集成', link: '/guide/nuget-integration' },
          { text: 'CAS 认证', link: '/guide/cas-auth' },
          { text: '账单同步 (BillSync)', link: '/guide/bill-sync' },
          { text: 'HTML 解析', link: '/guide/html-parser' },
          { text: '账单分类', link: '/guide/bill-classifier' },
          { text: 'ONNX 推理', link: '/guide/onnx-inference' },
          { text: 'OCR HTTP 服务', link: '/guide/ocr-server' },
          { text: 'OCR TCP 服务', link: '/guide/ocr-tcp-server' },
          { text: '数据导出', link: '/guide/export' },
          { text: 'Docker 部署', link: '/guide/docker-deployment' },
          { text: 'FAQ', link: '/guide/faq' },
        ],
      },
      {
        text: '高级文档',
        items: [
          { text: '总览', link: '/advanced/overview' },
          { text: '模块结构', link: '/advanced/module-structure' },
          { text: 'CAS 登录链路', link: '/advanced/cas-flow' },
          { text: '验证码解析器', link: '/advanced/captcha-resolver' },
          { text: '同步抽象与存储', link: '/advanced/sync-store' },
          { text: 'ONNX 模型格式', link: '/advanced/onnx-models' },
          { text: '多语言绑定', link: '/advanced/multi-language' },
          { text: 'NuGet 发布与 CI', link: '/advanced/nuget-ci' },
        ],
      },
    ],
    outline: [2, 3],
    search: {
      provider: 'local',
    },
    footer: {
      message: 'shmtu-dotnet-lib Docs',
      copyright: 'Copyright © SHMTU Terminal',
    },
  },
})
