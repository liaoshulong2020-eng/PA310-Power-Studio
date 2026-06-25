using System.Runtime.InteropServices;
using System.Text;
using System.IO.Ports;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

string? instance = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\USB\VID_04CC&PID_121B")
    ?.GetSubKeyNames().FirstOrDefault(name =>
    {
        using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Enum\USB\VID_04CC&PID_121B\{name}");
        return string.Equals(key?.GetValue("Service") as string, "WinUSB", StringComparison.OrdinalIgnoreCase);
    });
string DeviceInstanceId = $@"USB\VID_04CC&PID_121B\{instance ?? "2408010006"}";
const string DefaultCommand = "*IDN?";

Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine("PA300 USB Probe");
Console.WriteLine(new string('=', 60));

try
{
    string interfaceGuid = ProbeRegistry(DeviceInstanceId) ?? "{0040D94D-BE36-48B1-9605-0EFB33D5C206}";
    var devicePaths = ProbeDevicePaths(interfaceGuid);
    if (devicePaths.Count == 0)
    {
        Console.WriteLine("未找到 WinUSB 设备路径。");
        return;
    }

    foreach (string devicePath in devicePaths)
    {
        Console.WriteLine();
        Console.WriteLine($"设备路径: {devicePath}");
        using var probe = WinUsbProbe.Open(devicePath);
        if (probe is null)
        {
            Console.WriteLine("打开 WinUSB 设备失败。");
            continue;
        }

        probe.PrintInterfaceInfo();
        probe.TryScpiRoundTrips(DefaultCommand);
        probe.TryUsbTmcRoundTrip(DefaultCommand, 0x01, 0x81);
        probe.TryUsbTmcRoundTrip(DefaultCommand, 0x01, 0x82);
    }
}
catch (Exception ex)
{
    Console.WriteLine("程序异常:");
    Console.WriteLine(ex);
}

static string? ProbeRegistry(string DeviceInstanceId)
{
    Console.WriteLine("注册表信息");
    Console.WriteLine(new string('-', 60));

    using var enumKey = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Enum\{DeviceInstanceId}");
    if (enumKey is null)
    {
        Console.WriteLine($"未找到设备实例: {DeviceInstanceId}");
        return null;
    }

    PrintValue(enumKey, "DeviceDesc");
    PrintValue(enumKey, "Mfg");
    object? serviceValue = enumKey.GetValue("Service");
    string serviceName = serviceValue?.ToString() ?? string.Empty;
    PrintValue(enumKey, "Service");
    PrintValue(enumKey, "Driver");
    PrintValue(enumKey, "ClassGUID");
    PrintValue(enumKey, "HardwareID");
    PrintValue(enumKey, "CompatibleIDs");

    using var deviceParams = enumKey.OpenSubKey("Device Parameters");
    string? portName = null;
    string? interfaceGuid = null;
    if (deviceParams is not null)
    {
        PrintValue(deviceParams, "DeviceInterfaceGUIDs");
        PrintValue(deviceParams, "SymbolicName");
        PrintValue(deviceParams, "PortName");
        portName = deviceParams.GetValue("PortName") as string;
        interfaceGuid = (deviceParams.GetValue("DeviceInterfaceGUIDs") as string[] ?? Array.Empty<string>()).FirstOrDefault();
    }

    using var serialClassKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e978-e325-11ce-bfc1-08002be10318}\0009");
    if (serialClassKey is not null)
    {
        Console.WriteLine("串口类驱动信息");
        PrintValue(serialClassKey, "DriverDesc");
        PrintValue(serialClassKey, "ProviderName");
        PrintValue(serialClassKey, "PortName");
        using var serialDeviceParams = serialClassKey.OpenSubKey("Device Parameters");
        if (serialDeviceParams is not null)
            PrintValue(serialDeviceParams, "PortName");
    }

    if (!string.IsNullOrWhiteSpace(portName) && serviceName.Equals("usbser", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine();
        SerialProbe.TryQuery(portName, DefaultCommand);
    }
    else if (!string.IsNullOrWhiteSpace(portName))
    {
        Console.WriteLine();
        Console.WriteLine($"检测到 PortName={portName}，但当前驱动服务为 {serviceName}，跳过串口直测。");
    }

    return interfaceGuid;
}

static List<string> ProbeDevicePaths(string interfaceGuid)
{
    Console.WriteLine();
    Console.WriteLine("设备接口路径");
    Console.WriteLine(new string('-', 60));
    Console.WriteLine($"使用接口 GUID: {interfaceGuid}");

    var paths = new List<string>();
    using var deviceClassKey = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Control\DeviceClasses\{interfaceGuid}");
    if (deviceClassKey is null)
    {
        Console.WriteLine($"未找到 DeviceClasses\\{interfaceGuid}");
        return paths;
    }

    foreach (string subKeyName in deviceClassKey.GetSubKeyNames())
    {
        string normalized = subKeyName.Replace("##?#", @"\\?\");
        Console.WriteLine($"接口项: {subKeyName}");
        Console.WriteLine($"推测路径: {normalized}");
        paths.Add(normalized);
    }

    return paths;
}

static void PrintValue(RegistryKey key, string name)
{
    object? value = key.GetValue(name);
    string rendered = value switch
    {
        null => "(null)",
        string s => s,
        string[] arr => string.Join(" | ", arr),
        byte[] bytes => BitConverter.ToString(bytes),
        _ => value.ToString() ?? string.Empty
    };

    Console.WriteLine($"{name}: {rendered}");
}

internal static class SerialProbe
{
    public static void TryQuery(string portName, string command)
    {
        Console.WriteLine("串口直测");
        Console.WriteLine(new string('-', 60));
        Console.WriteLine($"端口: {portName}");

        foreach (int baudRate in new[] { 9600, 19200, 38400, 57600, 115200 })
        {
            foreach (string newline in new[] { "\n", "\r\n", "\r" })
            {
                Console.WriteLine($"尝试 波特率={baudRate}, NewLine={Escape(newline)}");
                try
                {
                    using var serial = new SerialPort(portName, baudRate)
                    {
                        Encoding = Encoding.ASCII,
                        ReadTimeout = 1200,
                        WriteTimeout = 1200,
                        NewLine = newline,
                        DtrEnable = false,
                        RtsEnable = false
                    };

                    serial.Open();
                    serial.DiscardInBuffer();
                    serial.DiscardOutBuffer();
                    serial.Write(command + newline);
                    var responseBuilder = new StringBuilder();
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    while (watch.ElapsedMilliseconds < 1200)
                    {
                        if (serial.BytesToRead > 0)
                        {
                            responseBuilder.Append(serial.ReadExisting());
                            if (responseBuilder.ToString().Contains('\n')) break;
                        }
                        Thread.Sleep(20);
                    }

                    string response = responseBuilder.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(response))
                    {
                        Console.WriteLine($"收到响应: {response}");
                        return;
                    }

                    Console.WriteLine("无响应");
                }
                catch (TimeoutException)
                {
                    Console.WriteLine("超时");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"失败: {ex.Message}");
                }
            }
        }

        Console.WriteLine("未读到串口响应。");
    }

    private static string Escape(string value)
    {
        return value.Replace("\r", "\\r").Replace("\n", "\\n");
    }
}

internal sealed class WinUsbProbe : IDisposable
{
    private readonly SafeFileHandle _deviceHandle;
    private readonly IntPtr _winUsbHandle;
    private readonly string _devicePath;
    private bool _disposed;

    private WinUsbProbe(string devicePath, SafeFileHandle deviceHandle, IntPtr winUsbHandle)
    {
        _devicePath = devicePath;
        _deviceHandle = deviceHandle;
        _winUsbHandle = winUsbHandle;
    }

    public static WinUsbProbe? Open(string devicePath)
    {
        SafeFileHandle handle = NativeMethods.CreateFile(
            devicePath,
            NativeMethods.GENERIC_READ | NativeMethods.GENERIC_WRITE,
            NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
            IntPtr.Zero,
            NativeMethods.OPEN_EXISTING,
            NativeMethods.FILE_ATTRIBUTE_NORMAL | NativeMethods.FILE_FLAG_OVERLAPPED,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            Console.WriteLine($"CreateFile 失败，Win32={Marshal.GetLastWin32Error()}");
            handle.Dispose();
            return null;
        }

        if (!NativeMethods.WinUsb_Initialize(handle, out IntPtr winUsbHandle))
        {
            Console.WriteLine($"WinUsb_Initialize 失败，Win32={Marshal.GetLastWin32Error()}");
            handle.Dispose();
            return null;
        }

        return new WinUsbProbe(devicePath, handle, winUsbHandle);
    }

    public void PrintInterfaceInfo()
    {
        Console.WriteLine("接口描述");
        Console.WriteLine(new string('-', 60));

        if (!NativeMethods.WinUsb_QueryInterfaceSettings(_winUsbHandle, 0, out NativeMethods.UsbInterfaceDescriptor descriptor))
        {
            Console.WriteLine($"WinUsb_QueryInterfaceSettings 失败，Win32={Marshal.GetLastWin32Error()}");
            return;
        }

        Console.WriteLine($"Class=0x{descriptor.InterfaceClass:X2}, SubClass=0x{descriptor.InterfaceSubClass:X2}, Protocol=0x{descriptor.InterfaceProtocol:X2}, Endpoints={descriptor.NumEndpoints}");
        for (byte i = 0; i < descriptor.NumEndpoints; i++)
        {
            if (!NativeMethods.WinUsb_QueryPipe(_winUsbHandle, 0, i, out NativeMethods.WinUsbPipeInformation pipe))
            {
                Console.WriteLine($"Pipe {i}: 查询失败，Win32={Marshal.GetLastWin32Error()}");
                continue;
            }

            Console.WriteLine($"Pipe {i}: Type={pipe.PipeType}, Id=0x{pipe.PipeId:X2}, MaxPacket={pipe.MaximumPacketSize}, Interval={pipe.Interval}");
        }
    }

    public void TryScpiRoundTrips(string command)
    {
        Console.WriteLine();
        Console.WriteLine($"发送测试命令: {command}");
        Console.WriteLine(new string('-', 60));

        foreach (string probeCommand in new[] { command, ":SYSTem:MODel?", ":SYSTem?", ":STATus?", ":MEASure:NORMal:VALue?" })
        {
            Console.WriteLine($"尝试命令 {probeCommand} | Pipe OUT=0x01, IN=0x81");
            foreach (var frame in BuildFrames(probeCommand))
            {
                Console.WriteLine($"  封包: {frame.Name} | 长度={frame.Payload.Length}");
                TrySinglePair(frame.Payload, 0x01, 0x81);
            }
            Console.WriteLine();
        }
    }

    public void TryUsbTmcRoundTrip(string command, byte outPipe, byte inPipe)
    {
        Console.WriteLine($"USBTMC 探测 OUT=0x{outPipe:X2}, IN=0x{inPipe:X2}");
        uint timeoutMs = 1500;
        NativeMethods.WinUsb_SetPipePolicy(_winUsbHandle, inPipe, NativeMethods.PIPE_TRANSFER_TIMEOUT, sizeof(uint), ref timeoutMs);
        NativeMethods.WinUsb_SetPipePolicy(_winUsbHandle, outPipe, NativeMethods.PIPE_TRANSFER_TIMEOUT, sizeof(uint), ref timeoutMs);

        byte[] payload = Encoding.ASCII.GetBytes(command + "\n");
        int padded = (payload.Length + 3) & ~3;
        byte[] output = new byte[12 + padded];
        output[0] = 1; output[1] = 1; output[2] = 0xFE;
        BitConverter.GetBytes((uint)payload.Length).CopyTo(output, 4);
        output[8] = 1;
        payload.CopyTo(output, 12);

        byte[] request = new byte[12];
        request[0] = 2; request[1] = 2; request[2] = 0xFD;
        BitConverter.GetBytes((uint)4096).CopyTo(request, 4);

        if (!NativeMethods.WinUsb_WritePipe(_winUsbHandle, outPipe, output, (uint)output.Length, out uint w1, IntPtr.Zero))
        { Console.WriteLine($"  命令帧写失败 Win32={Marshal.GetLastWin32Error()}"); return; }
        Console.WriteLine($"  命令帧写入 {w1}: {BitConverter.ToString(output)}");
        if (!NativeMethods.WinUsb_WritePipe(_winUsbHandle, outPipe, request, (uint)request.Length, out uint w2, IntPtr.Zero))
        { Console.WriteLine($"  请求帧写失败 Win32={Marshal.GetLastWin32Error()}"); return; }
        byte[] response = new byte[4096];
        if (!NativeMethods.WinUsb_ReadPipe(_winUsbHandle, inPipe, response, (uint)response.Length, out uint read, IntPtr.Zero))
        { Console.WriteLine($"  响应读取失败 Win32={Marshal.GetLastWin32Error()}"); return; }
        Console.WriteLine($"  响应 {read}: {BitConverter.ToString(response, 0, (int)read)}");
        if (read > 12) Console.WriteLine("  数据: " + Encoding.ASCII.GetString(response, 12, (int)read - 12).Trim());
    }

    private void TrySinglePair(byte[] request, byte outPipe, byte inPipe)
    {
        uint timeoutMs = 500;
        NativeMethods.WinUsb_SetPipePolicy(_winUsbHandle, inPipe, NativeMethods.PIPE_TRANSFER_TIMEOUT, sizeof(uint), ref timeoutMs);
        NativeMethods.WinUsb_SetPipePolicy(_winUsbHandle, outPipe, NativeMethods.PIPE_TRANSFER_TIMEOUT, sizeof(uint), ref timeoutMs);

        if (!NativeMethods.WinUsb_WritePipe(_winUsbHandle, outPipe, request, (uint)request.Length, out uint written, IntPtr.Zero))
        {
            Console.WriteLine($"写失败，Win32={Marshal.GetLastWin32Error()}");
            return;
        }

        Console.WriteLine($"    写入 {written} 字节 | HEX={BitConverter.ToString(request)}");

        var chunks = new List<byte>();
        for (int attempt = 0; attempt < 3; attempt++)
        {
            byte[] response = new byte[2048];
            if (!NativeMethods.WinUsb_ReadPipe(_winUsbHandle, inPipe, response, (uint)response.Length, out uint read, IntPtr.Zero))
            {
                int err = Marshal.GetLastWin32Error();
                if (chunks.Count == 0)
                {
                    Console.WriteLine($"    读失败，Win32={err}");
                }
                break;
            }

            if (read == 0) break;
            chunks.AddRange(response.AsSpan(0, (int)read).ToArray());
            if (read < response.Length) break;
        }

        if (chunks.Count == 0)
        {
            Console.WriteLine("    设备未返回数据");
            return;
        }

        byte[] all = chunks.ToArray();
        string ascii = Encoding.ASCII.GetString(all).Trim();
        Console.WriteLine($"    读取 {all.Length} 字节");
        Console.WriteLine($"    ASCII: {(string.IsNullOrWhiteSpace(ascii) ? "(空)" : ascii)}");
        Console.WriteLine($"    HEX: {BitConverter.ToString(all)}");
    }

    private static IReadOnlyList<UsbFrame> BuildFrames(string command)
    {
        byte[] ascii = Encoding.ASCII.GetBytes(command);
        byte[] lf = Encoding.ASCII.GetBytes(command + "\n");
        byte[] crlf = Encoding.ASCII.GetBytes(command + "\r\n");
        byte[] nul = Encoding.ASCII.GetBytes(command + "\0");
        byte[] len16Le = BuildLengthPrefixed(ascii, littleEndian: true, width: 2);
        byte[] len16Be = BuildLengthPrefixed(ascii, littleEndian: false, width: 2);
        byte[] len32Le = BuildLengthPrefixed(ascii, littleEndian: true, width: 4);
        byte[] len32Be = BuildLengthPrefixed(ascii, littleEndian: false, width: 4);
        byte[] simpleHeader = BuildSimpleHeader(ascii);

        return new[] { new UsbFrame("ASCII+LF", lf) };
    }

    private static byte[] BuildLengthPrefixed(byte[] payload, bool littleEndian, int width)
    {
        byte[] buffer = new byte[width + payload.Length];
        uint len = (uint)payload.Length;
        byte[] lenBytes = width == 2
            ? BitConverter.GetBytes((ushort)len)
            : BitConverter.GetBytes(len);

        if (BitConverter.IsLittleEndian != littleEndian)
            Array.Reverse(lenBytes);

        Array.Copy(lenBytes, 0, buffer, 0, width);
        Array.Copy(payload, 0, buffer, width, payload.Length);
        return buffer;
    }

    private static byte[] BuildSimpleHeader(byte[] payload)
    {
        byte[] buffer = new byte[4 + payload.Length];
        buffer[0] = 0x55;
        buffer[1] = 0xAA;
        buffer[2] = (byte)(payload.Length & 0xFF);
        buffer[3] = (byte)((payload.Length >> 8) & 0xFF);
        Array.Copy(payload, 0, buffer, 4, payload.Length);
        return buffer;
    }

    public void Dispose()
    {
        if (_disposed) return;
        NativeMethods.WinUsb_Free(_winUsbHandle);
        _deviceHandle.Dispose();
        _disposed = true;
    }
}

internal sealed record UsbFrame(string Name, byte[] Payload);

internal static class NativeMethods
{
    public const uint GENERIC_READ = 0x80000000;
    public const uint GENERIC_WRITE = 0x40000000;
    public const uint FILE_SHARE_READ = 0x1;
    public const uint FILE_SHARE_WRITE = 0x2;
    public const uint OPEN_EXISTING = 3;
    public const uint FILE_ATTRIBUTE_NORMAL = 0x80;
    public const uint FILE_FLAG_OVERLAPPED = 0x40000000;
    public const uint PIPE_TRANSFER_TIMEOUT = 0x03;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("winusb.dll", SetLastError = true)]
    public static extern bool WinUsb_Initialize(SafeFileHandle deviceHandle, out IntPtr interfaceHandle);

    [DllImport("winusb.dll", SetLastError = true)]
    public static extern bool WinUsb_Free(IntPtr interfaceHandle);

    [DllImport("winusb.dll", SetLastError = true)]
    public static extern bool WinUsb_QueryInterfaceSettings(IntPtr interfaceHandle, byte alternateInterfaceNumber, out UsbInterfaceDescriptor descriptor);

    [DllImport("winusb.dll", SetLastError = true)]
    public static extern bool WinUsb_QueryPipe(IntPtr interfaceHandle, byte alternateInterfaceNumber, byte pipeIndex, out WinUsbPipeInformation pipeInformation);

    [DllImport("winusb.dll", SetLastError = true)]
    public static extern bool WinUsb_SetPipePolicy(IntPtr interfaceHandle, byte pipeId, uint policyType, uint valueLength, ref uint value);

    [DllImport("winusb.dll", SetLastError = true)]
    public static extern bool WinUsb_WritePipe(IntPtr interfaceHandle, byte pipeId, byte[] buffer, uint bufferLength, out uint lengthTransferred, IntPtr overlapped);

    [DllImport("winusb.dll", SetLastError = true)]
    public static extern bool WinUsb_ReadPipe(IntPtr interfaceHandle, byte pipeId, byte[] buffer, uint bufferLength, out uint lengthTransferred, IntPtr overlapped);

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

    public enum UsbdPipeType
    {
        Control,
        Isochronous,
        Bulk,
        Interrupt
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
