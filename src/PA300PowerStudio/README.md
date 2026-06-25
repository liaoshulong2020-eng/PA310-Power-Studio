# PA300 完整版 C# 上位机

## USB 连接说明（重要）

PA300 的 USB 接口使用厂商自定义协议，不是普通 USB 串口。应用会识别
`VID_04CC / PID_121B` 的当前驱动：

- 驱动服务为 `WinUSB`：直接使用 WinUSB Bulk 端点通信。
- 驱动服务为 `usbser`、设备显示为 COM 口：判定为不兼容驱动，不再假装连接成功。

若界面提示驱动不兼容，请在“设备连接”卡片点击“修复驱动”。程序会启动仓库
`PA300-USB/Drivers/InstallAll.bat` 中的厂商驱动安装器；安装后重新插拔设备并刷新。

新版连接层使用 SetupAPI 枚举设备接口，支持自动检测 Bulk IN/OUT 端点、3 秒管道超时、
SCPI I/O 串行化和断线后按实际驱动模式重连。命令终止符按手册使用 LF (`0x0A`)。

## 新版界面

界面重构为工作台布局：左侧集中管理设备、采集任务与运行健康度；右侧提供实时曲线与
数据表格、仪器参数配置以及深色通信控制台。内置 USB 连接诊断、驱动修复、参数方案、
单次查询、连续采集、CSV 记录、谐波配置与批量 SCPI 命令。

这是一个面向 PA300 系列功率计的 WinForms 上位机示例，重点是：

- 异步采集，UI 不阻塞
- 实时曲线 + 表格显示
- CSV 记录
- 串口 / TCP 双通信方式
- 参数组保存与切换
- 常规数值 / 谐波列表双模式
- 规格书对应的 `FORMAT` / `RATE` / `HOLD` / `PRESET` / `NUMber` 快速下发
- 批量 SCPI 配置命令执行
- 谐波列表表头自动同步
- 本地保存配置

## 运行环境

- Windows 10 / 11
- .NET 8 SDK 或更新版本

## 打开方式

直接用 Visual Studio 2022 打开 `PA300UpperMachineFull.csproj` 即可。
首次还原时会自动拉取：

- `System.IO.Ports`
- `System.Windows.Forms.DataVisualization`

## 已按规格书落地的命令

基于 `【通信命令】PA300系列高精度系列功率计SCPI通信命令手册V1.05.pdf` 的 7.9、7.10 章节，当前界面已支持：

- `:NUMeric:FORMat {ASCii|FLOat}`
- `:NUMeric:HOLD {ON|OFF}`
- `:RATE {100MS|250MS|500MS|1S|2S|5S|10S|20S}`
- `:NUMeric:NORMal:PRESet {1..4}`
- `:NUMeric:NORMal:NUMber {1..255}`
- `:NUMeric[:NORMal]:HEADer?`
- `:NUMeric[:NORMal]:VALue?`
- `:NUMeric:LIST:PRESet {1..4}`
- `:NUMeric:LIST:NUMber {1..32}`
- `:NUMeric:LIST:ORDer {1..50}`
- `:NUMeric:LIST:SELect {ALL|ODD|EVEN}`
- `:NUMeric:LIST:ITEM<x>?`
- `:NUMeric:LIST:VALue?`
- `*IDN?`

同时兼容：

- ASCII 返回值解析
- SCPI 二进制块 + IEEE 单精度浮点返回值解析

## 推荐使用流程

### 常规测量
1. 连接设备后，先在“基础配置”里设置 `FORMAT / RATE / HOLD`。
2. 在“常规输出”里设置 `PRESET` 和输出项数。
3. 如果需要自定义 `ITEM1~ITEMN`，在“批量配置”里直接填写 SCPI 命令。
4. 点击“读取表头”。
5. 使用 `:NUMeric:NORMal:VALue?` 开始采集。

### 谐波列表
1. 将采集命令改为 `:NUMeric:LIST:VALue?`。
2. 在“谐波输出”里设置 `PRESET / NUMber / ORDer / SELect`。
3. 点击“读取表头”，程序会自动查询 `LIST:ITEM<x>?` 并生成谐波列名。
4. 开始采集或记录 CSV。

### 批量命令
- 支持按行或用 `;` 分隔输入多条 SCPI 命令。
- 查询命令会自动显示返回值，设置命令会直接下发。
- 适合补充 `:NUMeric[:NORMal]:ITEM<x>` 等高级配置。

## 当前实现说明

- 图表默认只显示前 8 路数据，避免大量列时界面卡顿。
- 表格与 CSV 会保留完整返回列。
- 谐波列表表头优先从设备当前配置读取；如果读取失败，则按界面上的 PRESET/阶次设置推断。

## 后续可继续扩展

- `:NUMeric[:NORMal]:ITEM<x>` 的可视化编辑器
- `:NUMeric:LIST:ITEM<x>` 的可视化编辑器
- 积分、量程、滤波等专用配置页
- 报警上下限
- Excel 导出
- 多窗口波形
