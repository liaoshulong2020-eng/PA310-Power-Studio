using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace PA300UpperMachineFull;

/// <summary>
/// PA300 WinUSB 直通传输层。
/// 设备 VID_04CC &amp; PID_121B，使用 WinUSB 驱动直接通信。
/// </summary>
public sealed class WinUsbScpiTransport : IScpiTransport
{
    // 来自 PA300 INF 文件的设备接口 GUID
    private const string DeviceInterfaceGuid = "{0040D94D-BE36-48B1-9605-0EFB33D5C206}";

    // 备选枚举路径（当 GUID 方式找不到时）
    private const string VidPidPath = @"SYSTEM\CurrentControlSet\Enum\USB\VID_04CC&PID_121B";

    private SafeFileHandle? _deviceHandle;
    private IntPtr _winUsbHandle = IntPtr.Zero;
    private byte _outPipeId;
    private byte _inPipeId;
    private readonly SemaphoreSlim _ioLock = new(1, 1);
    private bool _disposed;
    private string? _lastError;

    public bool IsOpen => _deviceHandle?.IsInvalid == false && _winUsbHandle != IntPtr.Zero;
    public string? LastError => _lastError;

    public async Task OpenAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _lastError = null;

        // 1. 查找设备路径
        string? devicePath = await Task.Run(() => FindDevicePath(), ct);
        if (devicePath is null)
            throw new InvalidOperationException("未找到 PA300 设备，请确认 USB 已连接且驱动已安装");

        // 2. 打开设备
        var handle = NativeMethods.CreateFile(
            devicePath,
            NativeMethods.GENERIC_READ | NativeMethods.GENERIC_WRITE,
            NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
            IntPtr.Zero,
            NativeMethods.OPEN_EXISTING,
            // WinUsb_ReadPipe/WritePipe below are synchronous (OVERLAPPED = NULL).
            // Opening an overlapped handle and then issuing synchronous requests is invalid.
            NativeMethods.FILE_ATTRIBUTE_NORMAL,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            int err = Marshal.GetLastWin32Error();
            _lastError = $"CreateFile 错误: {err}";
            throw new InvalidOperationException($"无法打开 PA300 设备 (Win32={err})。请尝试重新插拔 USB。");
        }

        // 3. 初始化 WinUSB
        if (!NativeMethods.WinUsb_Initialize(handle, out IntPtr winUsbHandle))
        {
            int err = Marshal.GetLastWin32Error();
            handle.Dispose();
            _lastError = $"WinUsb_Initialize 错误: {err}";
            throw new InvalidOperationException($"WinUSB 初始化失败 (Win32={err})。请确认已安装 PA300 驱动。");
        }

        _deviceHandle = handle;
        _winUsbHandle = winUsbHandle;

        // 4. 自动检测端点
        try
        {
            DetectEndpoints();
            ConfigurePipes();
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public async Task WriteAsync(string command, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        EnsureOpen();
        await _ioLock.WaitAsync(ct);
        try
        {
            byte[] data = Encoding.ASCII.GetBytes(command.TrimEnd() + "\n");
            await Task.Run(() =>
            {
            if (!NativeMethods.WinUsb_WritePipe(_winUsbHandle, _outPipeId, data,
                    (uint)data.Length, out uint written, IntPtr.Zero))
            {
                int err = Marshal.GetLastWin32Error();
                throw new InvalidOperationException($"写入失败 (Win32={err})");
            }
            if (written != data.Length)
                throw new InvalidOperationException($"写入不完整: {written}/{data.Length}");
            }, ct);
        }
        finally { _ioLock.Release(); }
    }

    public async Task<byte[]> QueryRawAsync(string command, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        EnsureOpen();

        await _ioLock.WaitAsync(ct);
        try
        {

        byte[] data = Encoding.ASCII.GetBytes(command.TrimEnd() + "\n");
        await Task.Run(() =>
        {
            if (!NativeMethods.WinUsb_WritePipe(_winUsbHandle, _outPipeId, data,
                    (uint)data.Length, out uint written, IntPtr.Zero))
            {
                int err = Marshal.GetLastWin32Error();
                throw new InvalidOperationException($"写入失败 (Win32={err})");
            }
        }, ct);

        // 读取响应
        var chunks = new List<byte>();
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        try
        {
            await Task.Run(() =>
            {
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    byte[] buffer = new byte[4096];
                    if (!NativeMethods.WinUsb_ReadPipe(_winUsbHandle, _inPipeId, buffer,
                            (uint)buffer.Length, out uint read, IntPtr.Zero))
                    {
                        int err = Marshal.GetLastWin32Error();
                        if (err == 0x1F) // ERROR_GEN_FAILURE - 设备已断开
                            throw new InvalidOperationException("设备已断开连接");
                        if (chunks.Count == 0)
                            throw new InvalidOperationException($"读取失败 (Win32={err})");
                        break; // 已有部分数据，返回
                    }
                    if (read == 0) break;
                    chunks.AddRange(buffer.AsSpan(0, (int)read).ToArray());
                    if (read < buffer.Length) break; // 读完
                }
            }, linkedCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // 读取超时但有部分数据
            if (chunks.Count == 0)
                throw new TimeoutException("读取响应超时");
        }

        return chunks.ToArray();
        }
        finally { _ioLock.Release(); }
    }

    public async Task<string> QueryAsync(string command, CancellationToken ct = default)
    {
        byte[] raw = await QueryRawAsync(command, ct);
        return Encoding.ASCII.GetString(raw).Trim();
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;

        try
        {
            if (_winUsbHandle != IntPtr.Zero)
                NativeMethods.WinUsb_Free(_winUsbHandle);
        }
        catch { }

        try
        {
            _deviceHandle?.Dispose();
        }
        catch { }

        _winUsbHandle = IntPtr.Zero;
        _deviceHandle = null;
        _ioLock.Dispose();
        return ValueTask.CompletedTask;
    }

    // ======================= 私有方法 =======================

    private void EnsureOpen()
    {
        if (!IsOpen)
            throw new InvalidOperationException("设备未连接");
    }

    /// <summary>
    /// 从注册表找到 PA300 设备路径。
    /// </summary>
    private static string? FindDevicePath()
    {
        // SetupAPI is the supported way to enumerate enabled device interfaces.
        IntPtr infoSet = NativeMethods.SetupDiGetClassDevs(
            ref NativeMethods.Pa300InterfaceGuid, null, IntPtr.Zero,
            NativeMethods.DIGCF_PRESENT | NativeMethods.DIGCF_DEVICEINTERFACE);
        if (infoSet != NativeMethods.INVALID_HANDLE_VALUE)
        {
            try
            {
                var interfaceData = new NativeMethods.SpDeviceInterfaceData
                {
                    CbSize = (uint)Marshal.SizeOf<NativeMethods.SpDeviceInterfaceData>()
                };
                for (uint index = 0; NativeMethods.SetupDiEnumDeviceInterfaces(
                         infoSet, IntPtr.Zero, ref NativeMethods.Pa300InterfaceGuid, index, ref interfaceData); index++)
                {
                    NativeMethods.SetupDiGetDeviceInterfaceDetail(
                        infoSet, ref interfaceData, IntPtr.Zero, 0, out uint required, IntPtr.Zero);
                    if (required == 0) continue;
                    IntPtr detail = Marshal.AllocHGlobal((int)required);
                    try
                    {
                        Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                        if (NativeMethods.SetupDiGetDeviceInterfaceDetail(
                                infoSet, ref interfaceData, detail, required, out _, IntPtr.Zero))
                            return Marshal.PtrToStringUni(detail + 4);
                    }
                    finally { Marshal.FreeHGlobal(detail); }
                }
            }
            finally { NativeMethods.SetupDiDestroyDeviceInfoList(infoSet); }
        }

        // 方式1：通过接口 GUID 查找
        try
        {
            using var deviceClassesKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                $@"SYSTEM\CurrentControlSet\Control\DeviceClasses\{DeviceInterfaceGuid}");
            if (deviceClassesKey != null)
            {
                foreach (string subKeyName in deviceClassesKey.GetSubKeyNames())
                {
                    // 有些路径带 ##?# 前缀 => 转换为 \\?\
                    string normalized = subKeyName.Replace("##?#", @"\\?\");
                    int guidSuffix = normalized.LastIndexOf("#{", StringComparison.Ordinal);
                    if (guidSuffix > 0) normalized = normalized[..guidSuffix];
                    if (normalized.Contains("VID_04CC", StringComparison.OrdinalIgnoreCase) &&
                        normalized.Contains("PID_121B", StringComparison.OrdinalIgnoreCase))
                        return normalized;
                }
            }
        }
        catch { }

        // 方式2：通过 VID/PID 枚举 USB 设备树
        try
        {
            using var usbKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(VidPidPath);
            if (usbKey != null)
            {
                foreach (string subKey in usbKey.GetSubKeyNames())
                {
                    using var deviceKey = usbKey.OpenSubKey(subKey);
                    if (deviceKey == null) continue;

                    // 找 Device Parameters 里的 SymbolicName
                    using var deviceParams = deviceKey.OpenSubKey("Device Parameters");
                    string? symbolicName = deviceParams?.GetValue("SymbolicName") as string;
                    if (!string.IsNullOrWhiteSpace(symbolicName))
                        return symbolicName;

                    // 通过 Driver 键找关联的 DeviceClasses
                    string? driver = deviceKey.GetValue("Driver") as string;
                    if (!string.IsNullOrWhiteSpace(driver))
                    {
                        string driverGuid = driver.Replace(
                            @"USB\VID_04CC&PID_121B\", "");
                        try
                        {
                            using var classKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                                $@"SYSTEM\CurrentControlSet\Control\DeviceClasses\{DeviceInterfaceGuid}\{symbolicName ?? subKey}");
                            if (classKey != null)
                            {
                                // 读取设备路径
                                string? path = classKey.GetValue("DeviceInstance") as string;
                                if (!string.IsNullOrWhiteSpace(path))
                                    return @"\\?\" + path;
                            }
                        }
                        catch { }
                    }

                    // HardwareID 不是可传给 CreateFile 的设备接口路径，不能据此拼造。
                }
            }
        }
        catch { }

        return null;
    }

    /// <summary>
    /// 自动检测 USB 端点（OUT 写 / IN 读）。
    /// </summary>
    private void DetectEndpoints()
    {
        if (!NativeMethods.WinUsb_QueryInterfaceSettings(_winUsbHandle, 0,
                out NativeMethods.UsbInterfaceDescriptor descriptor))
        {
            int err = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"查询接口失败 (Win32={err})");
        }

        bool foundOut = false, foundIn = false;
        for (byte i = 0; i < descriptor.NumEndpoints; i++)
        {
            if (!NativeMethods.WinUsb_QueryPipe(_winUsbHandle, 0, i,
                    out NativeMethods.WinUsbPipeInformation pipe))
                continue;

            if (pipe.PipeType == NativeMethods.UsbdPipeType.Bulk)
            {
                if ((pipe.PipeId & 0x80) == 0 && !foundOut)
                {
                    _outPipeId = pipe.PipeId;
                    foundOut = true;
                }
                else if ((pipe.PipeId & 0x80) != 0 && !foundIn)
                {
                    _inPipeId = pipe.PipeId;
                    foundIn = true;
                }
            }
        }

        if (!foundOut || !foundIn)
            throw new InvalidOperationException(
                $"未找到 Bulk 端点 (OUT={foundOut}, IN={foundIn})");
    }

    private void ConfigurePipes()
    {
        uint timeout = 3000;
        NativeMethods.WinUsb_SetPipePolicy(_winUsbHandle, _inPipeId,
            NativeMethods.PIPE_TRANSFER_TIMEOUT, sizeof(uint), ref timeout);
        NativeMethods.WinUsb_SetPipePolicy(_winUsbHandle, _outPipeId,
            NativeMethods.PIPE_TRANSFER_TIMEOUT, sizeof(uint), ref timeout);
    }

    // ======================= P/Invoke =======================

    private static class NativeMethods
    {
        public const uint GENERIC_READ = 0x80000000;
        public const uint GENERIC_WRITE = 0x40000000;
        public const uint FILE_SHARE_READ = 0x1;
        public const uint FILE_SHARE_WRITE = 0x2;
        public const uint OPEN_EXISTING = 3;
        public const uint FILE_ATTRIBUTE_NORMAL = 0x80;
        public const uint FILE_FLAG_OVERLAPPED = 0x40000000;
        public const uint PIPE_TRANSFER_TIMEOUT = 0x03;
        public const uint DIGCF_PRESENT = 0x02;
        public const uint DIGCF_DEVICEINTERFACE = 0x10;
        public static readonly IntPtr INVALID_HANDLE_VALUE = new(-1);
        public static Guid Pa300InterfaceGuid = new(DeviceInterfaceGuid);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern SafeFileHandle CreateFile(
            string fileName, uint desiredAccess, uint shareMode,
            IntPtr securityAttributes, uint creationDisposition,
            uint flagsAndAttributes, IntPtr templateFile);

        [DllImport("winusb.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WinUsb_Initialize(SafeFileHandle deviceHandle,
            out IntPtr interfaceHandle);

        [DllImport("winusb.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WinUsb_Free(IntPtr interfaceHandle);

        [DllImport("winusb.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WinUsb_QueryInterfaceSettings(
            IntPtr interfaceHandle, byte alternateInterfaceNumber,
            out UsbInterfaceDescriptor descriptor);

        [DllImport("winusb.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WinUsb_QueryPipe(
            IntPtr interfaceHandle, byte alternateInterfaceNumber,
            byte pipeIndex, out WinUsbPipeInformation pipeInformation);

        [DllImport("winusb.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WinUsb_SetPipePolicy(
            IntPtr interfaceHandle, byte pipeId, uint policyType,
            uint valueLength, ref uint value);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, string? enumerator,
            IntPtr hwndParent, uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetupDiEnumDeviceInterfaces(IntPtr deviceInfoSet, IntPtr deviceInfoData,
            ref Guid interfaceClassGuid, uint memberIndex, ref SpDeviceInterfaceData deviceInterfaceData);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr deviceInfoSet,
            ref SpDeviceInterfaceData deviceInterfaceData, IntPtr detailData, uint detailDataSize,
            out uint requiredSize, IntPtr deviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        [StructLayout(LayoutKind.Sequential)]
        public struct SpDeviceInterfaceData
        {
            public uint CbSize;
            public Guid InterfaceClassGuid;
            public uint Flags;
            public UIntPtr Reserved;
        }

        [DllImport("winusb.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WinUsb_WritePipe(
            IntPtr interfaceHandle, byte pipeId, byte[] buffer,
            uint bufferLength, out uint lengthTransferred,
            IntPtr overlapped);

        [DllImport("winusb.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WinUsb_ReadPipe(
            IntPtr interfaceHandle, byte pipeId, byte[] buffer,
            uint bufferLength, out uint lengthTransferred,
            IntPtr overlapped);

        [StructLayout(LayoutKind.Sequential)]
        public struct UsbInterfaceDescriptor
        {
            public byte Length;
            public byte DescriptorType;
            public byte InterfaceNumber;
            public byte AlternateSetting;
            public byte NumEndpoints;
            public byte InterfaceClass;
            public byte InterfaceSubClass;
            public byte InterfaceProtocol;
            public byte Interface;
        }

        public enum UsbdPipeType : byte
        {
            Control = 0,
            Isochronous = 1,
            Bulk = 2,
            Interrupt = 3
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WinUsbPipeInformation
        {
            public UsbdPipeType PipeType;
            public byte PipeId;
            public ushort MaximumPacketSize;
            public byte Interval;
        }
    }
}
