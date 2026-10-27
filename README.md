# TCP 调试助手

一个轻量级 Windows TCP 网络调试工具，C# WinForms 单文件实现，**无任何第三方依赖**。

![Platform](https://img.shields.io/badge/platform-Windows-blue) ![.NET](https://img.shields.io/badge/.NET%20Framework-4.x-green) ![License](https://img.shields.io/badge/license-MIT-orange)

## ✨ 功能

- **双模式**：TCP 客户端（主动连接）/ TCP 服务器（监听，支持多客户端）
- **工业简洁界面**：扁平卡片 + 1px 描边、状态指示灯、等宽数据字体、灰蓝配色
- **文本 / HEX 收发**：HEX 输入自动兼容空格、逗号、0x 前缀，自动校验偶数位
- **HEX 显示** / **时间戳**（精确到毫秒）/ 编码选择（UTF-8、GBK、ASCII）
- **多客户端管理**：服务器模式左侧在线客户端列表，选中 = 定向发送，不选 = 广播
- **循环发送**：自定义间隔（ms）定时重发，适合工控设备轮询调试
- **颜色区分**：黑色 = 收到、蓝色 = 发出、灰色 = 系统消息、红色 = 错误
- **RX / TX 字节计数**、清空接收/发送、**保存日志**
- 快捷键：`Ctrl + Enter` 发送

## 🚀 使用

1. 到 [Releases](https://github.com/MM121202/tcp-debug-assistant/releases/latest) 下载 `TCP-Debug-Assistant.exe`，双击运行（Windows 7+，需 .NET Framework 4.x，系统自带）；
2. 顶部点「客户端」或「服务器」切换模式：
   - **客户端**：填服务器地址 + 端口 → 点「连 接」
   - **服务器**：填本机监听端口 → 点「开始监听」
3. 在下方输入框输入内容（或勾选 HEX 发送填 `01 03 00 00 00 01`）→ 点「发送」或按 `Ctrl+Enter`。

## 🔧 从源码编译

无需安装 Visual Studio / .NET SDK，Windows 自带 C# 编译器即可：

```bat
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe ^
  /target:winexe /codepage:65001 /out:TCP调试助手.exe ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll TCP调试助手.cs
```

或直接双击 `build.bat` 一键编译。

## 📁 文件说明

| 文件 | 说明 |
| --- | --- |
| `TCP调试助手.cs` | 全部源码（单文件，约 700 行） |
| `build.bat` | 一键编译脚本 |
| `LICENSE` | MIT 许可证 |

## 📄 License

[MIT](LICENSE) © 2026 MM121202
