# PA310 Power Studio

PA310 Power Studio 是一个面向致远电子 PA300/PA310 系列数字功率计的 Windows 上位机。项目使用 C# / WinForms / .NET 8 实现，重点支持 PA310 的电压、电流、功率、功率因数、ITHD 与多次电流谐波监测。

## 功能特性

- RS-232、TCP/IP、USB/WinUSB 通信
- 自动识别 PA300/PA310 USB 驱动状态
- 一键连接/断开，一键开始/停止采集
- 实时数值卡片、曲线和表格显示
- PA310 推荐默认配置：600V / 20A / 电压 / 电流 / 有功功率 / 功率因数
- 仪器 ABCD 屏显示项配置
- 电压、电流量程与自动量程配置
- 电流谐波与 ITHD 分析，支持 3/5/7/9/11... 次奇次谐波显示
- 采集记录自动保存到程序目录下的 `数据保存/`
- 停止记录后自动生成 CSV、原始通信日志、HTML 图表报告
- 关闭窗口时自动停止采集并释放串口/USB

## 目录结构

```text
.
├─ src/PA300PowerStudio/          # 主上位机 WinForms 项目
├─ tools/PA300UsbProbe/           # USB/串口探测辅助工具
├─ tools/scripts/                 # 驱动修复、WinUSB 探测脚本
├─ drivers/PA300-USB/             # PA300 USB 驱动安装文件
├─ docs/manuals/                  # SCPI 通信手册与 LabVIEW 示例说明
└─ PA310PowerStudio.sln           # 根目录解决方案
```

## 运行环境

- Windows 10 / Windows 11
- .NET 8 SDK 或更新版本
- Visual Studio 2022 推荐

## 编译

```powershell
dotnet restore
dotnet build PA310PowerStudio.sln -c Release
```

主程序输出位置：

```text
src/PA300PowerStudio/bin/Release/net8.0-windows/PA300UpperMachineFull.exe
```

## 使用流程

1. 打开 `PA310PowerStudio.sln`。
2. 编译并运行 `PA300UpperMachineFull`。
3. 选择连接方式：
   - RS-232：选择 COM 口与波特率，PA310 常用 `115200`。
   - USB：需要 WinUSB 驱动。
   - TCP/IP：填写 IP 与端口。
4. 点击首页“连接”。
5. 点击“恢复推荐默认：电压/电流/PF/THD”或进入“仪器配置”读取并修改配置。
6. 点击“开始采集”。
7. 如需保存数据，点击“开始记录”；再次点击后停止记录并生成报告。

## USB 驱动说明

PA300/PA310 的 USB 不是普通串口协议。若 Windows 将设备识别成 `usbser` 虚拟串口，程序会提示驱动模式不匹配。可在程序的“连接设置”中点击“修复驱动”，或手动运行：

```text
drivers/PA300-USB/Drivers/InstallAll.bat
```

安装后请重新插拔设备。

## 数据保存

记录功能不会要求用户选择路径。每次记录会自动在程序同目录下创建：

```text
数据保存/PA310_yyyyMMdd_HHmmss/
```

目录内包含：

- `data.csv`：表格数据
- `raw.txt`：原始 SCPI 返回
- `report.html`：HTML 图表报告
- `power.png`、`pf.png`、`thd.png`：可生成时自动输出

## 文档

相关手册位于：

- `docs/manuals/【通信命令】PA300系列高精度系列功率计SCPI通信命令手册V1.05.pdf`
- `docs/manuals/PA300系列labview驱动及例程使用说明V1.03.pdf`

## 说明

本项目是现场调试型上位机，已经针对 PA310 单通道 600V/20A 功率计做了界面与默认配置优化。不同固件版本可能存在个别 SCPI 命令不支持的情况；读取全部配置时，无法读取或不支持的控件会标红提示。
