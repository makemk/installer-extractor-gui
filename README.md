# InstallerExtractor (安装包全能解压与降权安装套件)

[![Platform](https://img.shields.io/badge/Platform-Windows%207%2F8%2F10%2F11%20%7C%20Server-blue.svg)](https://github.com/makemk/installer-extractor-gui)
[![Privilege](https://img.shields.io/badge/Privilege-Zero--Admin%20%7C%20Non--Privileged-success.svg)](https://github.com/makemk/installer-extractor-gui)
[![AI-Ready](https://img.shields.io/badge/AI%20Integration-Native%20CLI%20%26%20JSON-orange.svg)](https://github.com/makemk/installer-extractor-gui)
[![Build](https://img.shields.io/badge/Build-Zero--Dependency%20(csc.exe)-brightgreen.svg)](https://github.com/makemk/installer-extractor-gui)
[![Release](https://img.shields.io/badge/Release-v1.1.0-blue.svg)](https://github.com/makemk/installer-extractor-gui/releases)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

> **专为「无管理员权限环境」与「AI Agent 自动化流水线」打造的 Windows 全能安装包物理脱壳与绿色部署套件。**
> 
> 包含面向人类用户的可视化桌面端 **`InstallerExtractorGUI.exe`**，以及专为 AI / 脚本自动化设计的标准命令行端 **`InstallerExtractorCLI.exe`**。

---

## 🌟 核心痛点与两大核心卖点

### 1. 🛡️ 适合无管理员权限的场景 (Zero-Admin / Non-Privileged Friendly)

在企业受限办公机、学校机房、公共实验室、测试台或云端沙箱中，使用者通常**没有 Windows Administrator（管理员）权限**。传统软件安装面临以下困境：
- **UAC 弹窗拦截**：双击 `.exe` 或 `.msi` 即触发凭据输入，直接被系统拒绝；
- **强行写入系统目录**：大多数安装包强行要求写入 `C:\Program Files` 或系统注册表，普通用户无法完成；
- **上位机硬件驱动混杂**：像 **Saleae Logic**、**SEGGER J-Link**、ST-Link 等工控上位机软件，其应用主体本身完全可以独立运行，却因打包了 USB 驱动而强行要求全局管理员权限。

#### 💡 InstallerExtractor 的解决方案：
1. **物理脱壳与便携化 (Make Portable)**：
   跳过 Windows Installer 服务与 UAC 提权检测，利用内置的多核心解压引擎（7-Zip、innounp、Asar、Py7z 等）直接将安装包的内部结构无损提取到当前用户目录，直接生成**绿色免安装便携版**，解压即可运行！
2. **环境降权欺骗 (RunAsInvoker)**：
   对于部分必须以自身逻辑静默释放的安装程序，内置注入 `__COMPAT_LAYER=RunAsInvoker` 兼容层，强制其在当前用户安全上下文中以普通非提权权限运行。
3. **硬件驱动与主体解耦隔离**：
   专设 `Core/DriverInstaller.cs` 引擎，将软件主体运行与底层驱动安装完全解耦。应用日常免权限随时使用；若真需要连接硬件，工具支持驱动深度扫描与精准按需请求提权（`pnputil /add-driver` 或官方驱动安装器），不再因驱动卡死整个软件的使用。

---

### 2. 🤖 原生支持 AI CLI 接入 (AI Agent First & CLI Automation)

现代自主 AI Agent（如 **Antigravity**、**Claude Code**、**Cursor**、**AutoGPT** 等）或 CI/CD 自动化脚本在终端执行任务时，若遇到需要配置外部工具的场景，传统交互式安装包会导致终端彻底挂起阻塞。

#### 💡 InstallerExtractor 的解决方案：
1. **纯净标准 `--json` 结构化输出**：
   CLI 模式下支持 `--json` 输出严格合规的 JSON 对象，包含 `success`、`format`、`outputDir`、`logFile`、`hasDrivers`、`drivers`、`error` 等结构化字段，机器解析 0 误差。
2. **严谨的进程退出码机制**：
   退出码明确（`0` 成功，非 `0` 为详细错误码），标准输入/输出/错误流重定向干净，绝不在无头（Headless）终端弹窗卡死。
3. **闭环诊断日志系统 (Self-Healing Friendly)**：
   - 默认记录细粒度运行追踪（子进程调用参数、环境变量、异常堆栈）；
   - JSON 结果自动回传 `"logFile": "<path>"`，当操作失败时，AI Agent 可直接读取并分析该诊断日志，自主完成报错归因与策略重试；
   - 提供 `--no-log` 零磁盘 I/O 模式，满足高吞吐场景。

---

## 🧰 格式支持矩阵

全套引擎已内置集成，**无需外部预装任何解压软件**：

| 安装包 / 归档格式 | 识别与解包机制 | 适用典型软件 |
| :--- | :--- | :--- |
| **Inno Setup (.exe)** | 内置 `innounp.exe` 专用解压内核 | VS Code 安装包、各类开源工具 |
| **NSIS (.exe)** | 7z 内核深度遍历与脚本偏移探测 | 微信、网易云音乐、Notepad++ 等 |
| **Windows Installer (.msi)** | 原生 `msiexec /a` 行政解包 + 7z Cabinet 拆解 | Python、Node.js、各类企业部署包 |
| **InstallShield (.exe / .cab)**| 7z InstallShield 专用 Filter 解构 | 各类工业上位机、硬件驱动包 |
| **Electron Asar (.asar)** | 内置 `Formats/Asar.64.dll` | 现代跨平台桌面端应用核心包 |
| **PyInstaller (.exe)** | 内置 `Formats/Py7z.64.dll` | Python 打包生成的单一独立可执行文件 |
| **7z / RAR / Zip SFX** | 7z SFX 内核物理分离头与载荷 | 7-Zip、WinRAR 生成的自解压程序 |
| **ISO 镜像 / 压缩包** | 内置 `Iso7z.64.dll` 及全量解压套件 | 系统镜像、`.tar.gz`、`.zip`、`.7z` 等 |
| **现代压缩算法** | 内置 Zstandard (zstd), LZ4, Fast-LZMA2 | 采用最新现代高压缩比算法的文件 |

---

## ⚠️ 核心避坑指南与典型实战场景

### 1. 🛡️ Electron-Builder / NSIS 双层嵌套封装（典型代表：DeepSeek Harness DSH Desktop）

- **🚨 故障现象**：
  使用通用解压工具或自动化流水线解包后，程序看似“解压完成”，但双击启动主程序直接抛出致命异常并弹窗崩溃：
  ```text
  A JavaScript error occurred in the main process
  Uncaught Exception:
  Error [ERR_MODULE_NOT_FOUND]: Cannot find module '...\resources\app\out\main\index.js'
  ```
- **🔍 深度根因剖析**：
  现代跨平台 Electron 应用广泛使用 `electron-builder` 构建 Windows 单文件安装包（常见为 `*-windows-x64-setup.exe`）。此类安装包本质是 **双层嵌套载荷结构（2-Stage Packaging）**：
  1. **外壳层（NSIS Wrapper）**：通常仅含 10 个安装向导临时辅助 DLL/BMP（如 `nsDialogs.dll`、`System.dll`、`modern-wizard.bmp` 等），其文件列表极小；
  2. **核心载荷层（Core Payload）**：**真正的全量程序主体（包括 Electron 渲染内核、数百个 Node 模块、以及 `resources\app\out\main\index.js` 等核心主进程业务脚本，往往达上万个文件、近 600MB）被全部封包压缩在内层的 `$PLUGINSDIR\app-64.7z`（或 `app-32.7z`）中**。
  
  **常见致命误区**：如果仅对外部 `.exe` 执行了一级解包，输出目录只会释放出空壳和残缺目录，导致主进程模块 `index.js` 物理缺失，Electron 引擎启动即死。

- **💡 闭环规避规范与标准处理流程**：
  1. **一级解压后自动探测**：每次解压执行完成后，必须主动探测目标目录中是否存在 `$PLUGINSDIR\app-64.7z`、`$PLUGINSDIR\app-32.7z` 或根目录残留的 `.7z` 核心压缩卷；
  2. **自动二级递归脱壳**：一旦发现嵌套载荷，无须人工干预，引擎自动调用 7-Zip 内核对该内嵌 `.7z` 启动二级深度物理解压，将万级核心文件完整平铺释放至程序主目录；
  3. **附属清理与整理**：
     - 将 `$R0\` 目录释放的官方 `Uninstall *.exe` 智能迁移至程序根目录；
     - 彻底清理 `$PLUGINSDIR` 与 `$R0` 等临时引导残留；
  4. **主进程入口完整性自检**：检查 `resources\app\package.json` 中的 `"main"` 指向字段（如 `./out/main/index.js`），确保目标文件物理存在且字节大于 0。

---

## 🚀 快速上手

### 方式一：面向人类用户的桌面端 (`InstallerExtractorGUI.exe`)

双击运行 **`InstallerExtractorGUI.exe`**：
1. **拖拽支持**：直接将待解压的 `.exe` 或 `.msi` 拖入窗口；
2. **格式秒测**：自动展示安装包类型（如 Inno Setup / NSIS / MSI 等）；
3. **一键解包**：点击【开始处理】，自动释放至同名绿色便携目录；
4. **驱动管理**：支持勾选“解压后自动安装驱动”，或点击【⚡ 安装目录驱动】单独分析上位机目录驱动。

---

### 方式二：面向 AI Agent / 自动化脚本的命令行端 (`InstallerExtractorCLI.exe`)

#### 1. 静态探测格式 (Detect Only)
```bash
InstallerExtractorCLI.exe -i "Logic-2.4.14-win.exe" --detect --json
```
**JSON 输出示例**：
```json
{
  "success": true,
  "action": "detect",
  "installerType": "InnoSetup",
  "displayName": "Inno Setup Installer",
  "inputFile": "C:\\Tools\\Logic-2.4.14-win.exe",
  "logFile": "C:\\Tools\\logs\\extractor_20260910_150000.log"
}
```

#### 2. 无特权物理脱壳 (Extract)
```bash
InstallerExtractorCLI.exe -i "dsh-desktop-windows-x64-setup.exe" -o "C:\Portable\DSH" --json
```
**JSON 输出示例（AI 专用，直接提供主执行程序，零额外猜测）：**
```json
{
  "success": true,
  "mode": "extract",
  "source": "C:\\Downloads\\dsh-desktop-windows-x64-setup.exe",
  "target": "C:\\Portable\\DSH",
  "fileCount": 15334,
  "totalBytes": 612202257,
  "mainExecutable": "C:\\Portable\\DSH\\DSH Desktop.exe",
  "executables": [
    "C:\\Portable\\DSH\\DSH Desktop.exe",
    "C:\\Portable\\DSH\\Uninstall DSH Desktop.exe"
  ],
  "nestedPayloadsDetected": true,
  "drivers": {
    "detected": false,
    "count": 0,
    "installAttempted": false,
    "installSuccess": false
  },
  "logFile": "C:\\Tools\\logs\\extractor_20260917_114712_10764.log",
  "exitCode": 0
}
```

#### 3. 硬件上位机自动解包并安装驱动
```bash
InstallerExtractorCLI.exe -i "SaleaeLogic.exe" -o "C:\Portable\Logic" --install-drivers --json
```

#### 4. 扫描已有目录是否含有硬件驱动 (Driver Scan)
```bash
InstallerExtractorCLI.exe --scan-drivers -o "C:\Portable\JLink" --json
```
**JSON 输出示例**：
```json
{
  "success": true,
  "action": "scan_drivers",
  "targetDir": "C:\\Portable\\JLink",
  "hasDrivers": true,
  "driverCount": 3,
  "drivers": [
    "C:\\Portable\\JLink\\USBDriver\\JLink.inf",
    "C:\\Portable\\JLink\\USBDriver\\InstDrivers.exe"
  ],
  "logFile": null
}
```

---

## 💻 CLI 完整参数速查表

```text
InstallerExtractorCLI.exe -i <installer_file> [选项]
```

| 参数 | 缩写 | 类型 | 说明 |
| :--- | :---: | :---: | :--- |
| `--input <file>` | `-i` | String | 待处理的安装包文件路径（使用 `--scan-drivers` 时可免填） |
| `--output <dir>` | `-o` | String | 目标输出目录（缺省自动命名为 `<文件名>_extracted`） |
| `--mode <mode>` | `-m` | String | 操作模式：`extract`（默认物理解压）或 `install`（降权静默部署） |
| `--detect` | `-d` | Switch | 仅静态特征探测，不执行解压 |
| `--install-drivers` | | Switch | **若解包后检测到驱动，请求系统提权调用 pnputil 安装** |
| `--scan-drivers` | | Switch | **仅扫描指定目标目录中的驱动信息并输出 JSON** |
| `--json` | `-j` | Switch | **输出纯净 JSON 格式（AI 自动化调用必选）** |
| `--quiet` | `-q` | Switch | 静默模式，抑制子进程实时输出 |
| `--no-log` | | Switch | **关闭诊断日志文件落盘**（零磁盘 I/O） |
| `--log-file <path>` | `-l` | String | 指定自定义诊断日志写入路径 |
| `--help` | `-h` | Switch | 输出帮助信息 |

---

## 🛠️ 极速零依赖构建 (Build from Source)

本项目采用纯粹的原生 C# 实现，**无需安装 Visual Studio，无需安装 .NET SDK**！只要有一台标准的 Windows 电脑，即可调用系统自带的 `csc.exe` 瞬间完成单文件编译：

```powershell
# 运行内置构建脚本（编译 GUI 与 CLI 双端）
powershell -ExecutionPolicy Bypass -File .\build.ps1
```
* 构建产物：直接生成约 8.4 MB 的独立单文件 `InstallerExtractorGUI.exe` 与 `InstallerExtractorCLI.exe`。
* 所有 7z 核心模块、解码插件均以二进制内嵌资源形式打包，开箱即用。

---

## 🧪 自动化全闭环测试套件

运行自带的自动化回归验证套件：
```powershell
powershell -ExecutionPolicy Bypass -File .\test_closed_loop.ps1
```
涵盖真实安装包识别、MSI 行政解包校验、SFX 生成与无损检验、日志开关、驱动扫描等 8 大核心闭环用例。

---

## 📄 开源许可证

本项目基于 [MIT 许可证](LICENSE) 开源。欢迎 Star、提交 Issue 或发起 PR！