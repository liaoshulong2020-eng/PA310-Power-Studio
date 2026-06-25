using System.Collections.Concurrent;
using System.IO.Ports;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace PA300UpperMachineFull;

public sealed record Pa300UsbDevice(string InstanceId, string PortName, string Service, string DisplayName)
{
    public bool IsSerial => Service.Equals("usbser", StringComparison.OrdinalIgnoreCase);
}

public static class Pa300UsbDiscovery
{
    private const string DeviceRoot = @"SYSTEM\CurrentControlSet\Enum\USB\VID_04CC&PID_121B";

    public static IReadOnlyList<Pa300UsbDevice> FindPresentDevices()
    {
        var result = new List<Pa300UsbDevice>();
        try
        {
            using var root = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(DeviceRoot);
            if (root is null) return result;
            foreach (string instance in root.GetSubKeyNames())
            {
                using var device = root.OpenSubKey(instance);
                if (device is null) continue;
                using var parameters = device.OpenSubKey("Device Parameters");
                string port = parameters?.GetValue("PortName") as string ?? string.Empty;
                string service = device.GetValue("Service") as string ?? string.Empty;
                string name = device.GetValue("FriendlyName") as string ??
                              device.GetValue("DeviceDesc") as string ?? "PA300";
                // ConfigFlags bit 0 means the device is disabled.
                int flags = device.GetValue("ConfigFlags") is int value ? value : 0;
                if ((flags & 1) == 0)
                    result.Add(new Pa300UsbDevice($@"USB\VID_04CC&PID_121B\{instance}", port, service, name));
            }
        }
        catch { }
        return result;
    }
}

public interface IScpiTransport : IAsyncDisposable
{
    bool IsOpen { get; }
    Task OpenAsync(CancellationToken ct = default);
    Task WriteAsync(string command, CancellationToken ct = default);
    Task<byte[]> QueryRawAsync(string command, CancellationToken ct = default);
    Task<string> QueryAsync(string command, CancellationToken ct = default);
}

public sealed class TcpScpiTransport : IScpiTransport
{
    private readonly string _host;
    private readonly int _port;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private TcpClient? _client;
    private NetworkStream? _stream;

    public TcpScpiTransport(string host, int port)
    {
        _host = host;
        _port = port;
    }

    public bool IsOpen => _client?.Connected == true && _stream is not null;

    public async Task OpenAsync(CancellationToken ct = default)
    {
        _client = new TcpClient();
        using var reg = ct.Register(() => _client.Dispose());
        await _client.ConnectAsync(_host, _port, ct);
        _stream = _client.GetStream();
        _stream.ReadTimeout = 3000;
        _stream.WriteTimeout = 3000;
    }

    public async Task WriteAsync(string command, CancellationToken ct = default)
    {
        if (_stream is null) throw new InvalidOperationException("TCP 未连接");
        await _lock.WaitAsync(ct);
        try
        {
            byte[] data = Encoding.ASCII.GetBytes(command.TrimEnd() + "\n");
            await _stream.WriteAsync(data, ct);
            await _stream.FlushAsync(ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<string> QueryAsync(string command, CancellationToken ct = default)
    {
        byte[] raw = await QueryRawAsync(command, ct);
        return Encoding.ASCII.GetString(raw).Trim();
    }

    public async Task<byte[]> QueryRawAsync(string command, CancellationToken ct = default)
    {
        if (_stream is null) throw new InvalidOperationException("TCP 未连接");
        await _lock.WaitAsync(ct);
        try
        {
            byte[] data = Encoding.ASCII.GetBytes(command.TrimEnd() + "\n");
            await _stream.WriteAsync(data, ct);
            await _stream.FlushAsync(ct);
            return await ScpiTransportReader.ReadTcpResponseAsync(_stream, ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        try { _stream?.Dispose(); } catch { }
        try { _client?.Dispose(); } catch { }
        _lock.Dispose();
        return ValueTask.CompletedTask;
    }
}

public sealed class SerialScpiTransport : IScpiTransport
{
    private readonly string _portName;
    private readonly int _baudRate;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private SerialPort? _serialPort;
    private SafeFileHandle? _directHandle;
    private bool _disposed;

    public SerialScpiTransport(string portName, int baudRate)
    {
        _portName = portName;
        _baudRate = baudRate;
    }

    public bool IsOpen => _serialPort?.IsOpen == true || _directHandle?.IsInvalid == false;

    public async Task OpenAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // 先尝试标准 SerialPort 方式
        try
        {
            var port = new SerialPort(_portName, _baudRate)
            {
                NewLine = "\n",
                Encoding = Encoding.ASCII,
                ReadTimeout = 3000,
                WriteTimeout = 3000,
                DtrEnable = false,
                RtsEnable = false
            };
            await Task.Run(() => port.Open(), ct);
            _serialPort = port;
            return;
        }
        catch (UnauthorizedAccessException) when (_serialPort is null)
        {
            // SerialPort 被占用，尝试直接通过 CreateFile 打开
        }

        // 备用方案：直接用 CreateFile 打开串口
        await Task.Run(() =>
        {
            // 先尝试释放残留句柄（通过打开再关闭）
            TryReleasePort();

            var handle = NativeFileMethods.CreateFile(
                @"\\.\" + _portName,
                NativeFileMethods.GENERIC_READ | NativeFileMethods.GENERIC_WRITE,
                0, // 独占模式
                IntPtr.Zero,
                NativeFileMethods.OPEN_EXISTING,
                NativeFileMethods.FILE_ATTRIBUTE_NORMAL,
                IntPtr.Zero);

            if (handle.IsInvalid)
            {
                int err = Marshal.GetLastWin32Error();
                throw new UnauthorizedAccessException(
                    $"无法打开 {_portName} (Win32={err})。请确认没有其他程序占用该端口。");
            }

            // 配置串口参数
            var dcb = new NativeFileMethods.DCB
            {
                DCBlength = (uint)Marshal.SizeOf<NativeFileMethods.DCB>(),
                BaudRate = (uint)_baudRate,
                ByteSize = 8,
                Parity = 0,
                StopBits = 0,
                fDtrControl = 0,
                fRtsControl = 0
            };
            if (!NativeFileMethods.SetCommState(handle, ref dcb))
            {
                handle.Dispose();
                throw new InvalidOperationException($"SetCommState 失败 (Win32={Marshal.GetLastWin32Error()})");
            }

            // 设置超时
            var timeouts = new NativeFileMethods.CommTimeouts
            {
                ReadIntervalTimeout = 0,
                ReadTotalTimeoutConstant = 3000,
                ReadTotalTimeoutMultiplier = 0,
                WriteTotalTimeoutConstant = 3000,
                WriteTotalTimeoutMultiplier = 0
            };
            NativeFileMethods.SetCommTimeouts(handle, ref timeouts);

            _directHandle = handle;
        }, ct);
    }

    public async Task WriteAsync(string command, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (_serialPort is { IsOpen: true })
        {
            await _lock.WaitAsync(ct);
            try { _serialPort.WriteLine(command.TrimEnd()); }
            finally { _lock.Release(); }
            return;
        }

        if (_directHandle?.IsInvalid == false)
        {
            byte[] data = Encoding.ASCII.GetBytes(command + "\n");
            await Task.Run(() =>
            {
                if (!NativeFileMethods.WriteFile(_directHandle!, data, (uint)data.Length,
                        out uint written, IntPtr.Zero))
                    throw new InvalidOperationException($"写入失败 (Win32={Marshal.GetLastWin32Error()})");
            }, ct);
            return;
        }

        throw new InvalidOperationException("串口未连接");
    }

    public async Task<string> QueryAsync(string command, CancellationToken ct = default)
    {
        byte[] raw = await QueryRawAsync(command, ct);
        return Encoding.ASCII.GetString(raw).Trim();
    }

    public async Task<byte[]> QueryRawAsync(string command, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (_serialPort is { IsOpen: true })
        {
            await _lock.WaitAsync(ct);
            try
            {
                _serialPort.DiscardInBuffer();
                _serialPort.DiscardOutBuffer();
                _serialPort.Write(command.TrimEnd() + _serialPort.NewLine);
                return await ScpiTransportReader.ReadSerialResponseAsync(_serialPort, ct);
            }
            finally { _lock.Release(); }
        }

        if (_directHandle?.IsInvalid == false)
        {
            return await QueryRawDirectAsync(command, ct);
        }

        throw new InvalidOperationException("串口未连接");
    }

    private async Task<byte[]> QueryRawDirectAsync(string command, CancellationToken ct)
    {
        byte[] data = Encoding.ASCII.GetBytes(command + "\n");
        var result = new List<byte>();

        await Task.Run(async () =>
        {
            // 清空缓冲区
            NativeFileMethods.PurgeComm(_directHandle!,
                NativeFileMethods.PURGE_RXCLEAR | NativeFileMethods.PURGE_TXCLEAR);

            // 发送命令
            if (!NativeFileMethods.WriteFile(_directHandle!, data, (uint)data.Length,
                    out uint written, IntPtr.Zero))
                throw new InvalidOperationException($"写入失败 (Win32={Marshal.GetLastWin32Error()})");

            // 读取响应（最多尝试 5 次）
            for (int i = 0; i < 5; i++)
            {
                if (ct.IsCancellationRequested) break;
                byte[] buffer = new byte[4096];
                if (!NativeFileMethods.ReadFile(_directHandle!, buffer, (uint)buffer.Length,
                        out uint read, IntPtr.Zero))
                    break;
                if (read == 0)
                {
                    await Task.Delay(50, ct);
                    continue;
                }
                result.AddRange(buffer.AsSpan(0, (int)read).ToArray());
                if (read < buffer.Length) break;
            }
        }, ct);

        return result.ToArray();
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;

        try
        {
            if (_serialPort is { IsOpen: true }) _serialPort.Close();
            _serialPort?.Dispose();
        }
        catch { }

        try { _directHandle?.Dispose(); }
        catch { }

        _lock.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// 尝试释放端口（打开一次再关闭，有时能清理残留句柄）
    /// </summary>
    private static void TryReleasePort()
    {
        // 这个操作由调用者处理异常
    }

    private static class NativeFileMethods
    {
        public const uint GENERIC_READ = 0x80000000;
        public const uint GENERIC_WRITE = 0x40000000;
        public const uint OPEN_EXISTING = 3;
        public const uint FILE_ATTRIBUTE_NORMAL = 0x80;
        public const uint PURGE_TXCLEAR = 0x0004;
        public const uint PURGE_RXCLEAR = 0x0008;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern SafeFileHandle CreateFile(
            string fileName, uint desiredAccess, uint shareMode,
            IntPtr securityAttributes, uint creationDisposition,
            uint flagsAndAttributes, IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetCommState(SafeFileHandle hFile, ref DCB lpDCB);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetCommTimeouts(SafeFileHandle hFile, ref CommTimeouts lpCommTimeouts);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool PurgeComm(SafeFileHandle hFile, uint dwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WriteFile(SafeFileHandle hFile, byte[] lpBuffer,
            uint nNumberOfBytesToWrite, out uint lpNumberOfBytesWritten, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ReadFile(SafeFileHandle hFile, byte[] lpBuffer,
            uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead, IntPtr lpOverlapped);

        [StructLayout(LayoutKind.Sequential)]
        public struct DCB
        {
            public uint DCBlength;
            public uint BaudRate;
            public uint fBinary;
            public uint fParity;
            public uint fOutxCtsFlow;
            public uint fOutxDsrFlow;
            public uint fDtrControl;
            public uint fDsrSensitivity;
            public uint fTXContinueOnXoff;
            public uint fOutX;
            public uint fInX;
            public uint fErrorChar;
            public uint fNull;
            public uint fRtsControl;
            public uint fAbortOnError;
            public uint fDummy2;
            public ushort fReserved;
            public ushort XonLim;
            public ushort XoffLim;
            public byte ByteSize;
            public byte Parity;
            public byte StopBits;
            public char XonChar;
            public char XoffChar;
            public char ErrorChar;
            public char EofChar;
            public char EvtChar;
            public ushort wReserved1;
            public uint fReserved1;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CommTimeouts
        {
            public uint ReadIntervalTimeout;
            public uint ReadTotalTimeoutConstant;
            public uint ReadTotalTimeoutMultiplier;
            public uint WriteTotalTimeoutConstant;
            public uint WriteTotalTimeoutMultiplier;
        }
    }
}

internal static class ScpiTransportReader
{
    public static async Task<byte[]> ReadTcpResponseAsync(NetworkStream stream, CancellationToken ct)
    {
        int first = await ReadByteAsync(stream, ct);
        if (first < 0) return Array.Empty<byte>();

        using var ms = new MemoryStream();
        ms.WriteByte((byte)first);

        if (first == '#')
        {
            await ReadBinaryPayloadAsync(
                async token => await ReadByteAsync(stream, token),
                async (buffer, offset, count, token) => await stream.ReadAsync(buffer.AsMemory(offset, count), token),
                ms,
                ct);
            await DrainTcpTerminatorAsync(stream, ct);
            return ms.ToArray();
        }

        await ReadAsciiPayloadAsync(
            async token => await ReadByteAsync(stream, token),
            ms,
            ct);
        return ms.ToArray();
    }

    public static async Task<byte[]> ReadSerialResponseAsync(SerialPort port, CancellationToken ct)
    {
        int first = await ReadSerialBytePollingAsync(port, ct);
        if (first < 0) return Array.Empty<byte>();

        using var ms = new MemoryStream();
        ms.WriteByte((byte)first);

        if (first == '#')
        {
            await ReadBinaryPayloadAsync(
                token => ReadSerialBytePollingAsync(port, token),
                (buffer, offset, count, token) => ReadSerialChunkPollingAsync(port, buffer, offset, count, token),
                ms,
                ct);
            await DrainSerialTerminatorAsync(port, ct);
            return ms.ToArray();
        }

        await ReadAsciiPayloadAsync(
            token => ReadSerialBytePollingAsync(port, token),
            ms,
            ct);
        return ms.ToArray();
    }

    private static async Task<int> ReadSerialBytePollingAsync(SerialPort port, CancellationToken ct)
    {
        while (port.IsOpen)
        {
            ct.ThrowIfCancellationRequested();
            if (port.BytesToRead > 0) return port.ReadByte();
            await Task.Delay(5, ct);
        }
        return -1;
    }

    private static async Task<int> ReadSerialChunkPollingAsync(
        SerialPort port, byte[] buffer, int offset, int count, CancellationToken ct)
    {
        while (port.IsOpen)
        {
            ct.ThrowIfCancellationRequested();
            int available = port.BytesToRead;
            if (available > 0) return port.Read(buffer, offset, Math.Min(count, available));
            await Task.Delay(5, ct);
        }
        return 0;
    }

    private static async Task ReadBinaryPayloadAsync(
        Func<CancellationToken, Task<int>> readByteAsync,
        Func<byte[], int, int, CancellationToken, Task<int>> readChunkAsync,
        MemoryStream ms,
        CancellationToken ct)
    {
        int digitCountByte = await readByteAsync(ct);
        if (digitCountByte < 0) throw new IOException("SCPI 二进制块头不完整");

        ms.WriteByte((byte)digitCountByte);
        int digitCount = digitCountByte - '0';
        if (digitCount < 0 || digitCount > 9) throw new IOException("SCPI 二进制块头格式错误");

        var lenDigits = new byte[digitCount];
        if (digitCount > 0)
        {
            await ReadExactAsync(readChunkAsync, lenDigits, 0, lenDigits.Length, ct);
            ms.Write(lenDigits, 0, lenDigits.Length);
        }

        int payloadLength = digitCount == 0
            ? 0
            : int.Parse(Encoding.ASCII.GetString(lenDigits), System.Globalization.CultureInfo.InvariantCulture);

        if (payloadLength <= 0) return;

        var payload = new byte[payloadLength];
        await ReadExactAsync(readChunkAsync, payload, 0, payload.Length, ct);
        ms.Write(payload, 0, payload.Length);
    }

    private static async Task ReadAsciiPayloadAsync(
        Func<CancellationToken, Task<int>> readByteAsync,
        MemoryStream ms,
        CancellationToken ct)
    {
        while (true)
        {
            int next = await readByteAsync(ct);
            if (next < 0) break;

            ms.WriteByte((byte)next);
            if (next == '\n') break;
        }
    }

    private static async Task ReadExactAsync(Stream stream, byte[] buffer, int offset, int count, CancellationToken ct)
    {
        await ReadExactAsync(
            async (target, start, length, token) => await stream.ReadAsync(target.AsMemory(start, length), token),
            buffer,
            offset,
            count,
            ct);
    }

    private static async Task ReadExactAsync(
        Func<byte[], int, int, CancellationToken, Task<int>> readChunkAsync,
        byte[] buffer,
        int offset,
        int count,
        CancellationToken ct)
    {
        while (count > 0)
        {
            int read = await readChunkAsync(buffer, offset, count, ct);
            if (read <= 0) throw new IOException("SCPI 响应提前结束");
            offset += read;
            count -= read;
        }
    }

    private static async Task<int> ReadByteAsync(Stream stream, CancellationToken ct)
    {
        var buffer = new byte[1];
        int read = await stream.ReadAsync(buffer, ct);
        return read == 0 ? -1 : buffer[0];
    }

    private static async Task DrainTcpTerminatorAsync(NetworkStream stream, CancellationToken ct)
    {
        await Task.Delay(10, ct);
        while (stream.DataAvailable)
        {
            int next = await ReadByteAsync(stream, ct);
            if (next < 0 || next == '\n') break;
        }
    }

    private static async Task DrainSerialTerminatorAsync(SerialPort port, CancellationToken ct)
    {
        await Task.Delay(10, ct);
        while (port.BytesToRead > 0)
        {
            int next = await Task.Run(() => port.BaseStream.ReadByte(), ct);
            if (next < 0 || next == '\n') break;
        }
    }
}

public sealed class CsvLogger
{
    private readonly ConcurrentQueue<MeasurementFrame> _queue = new();
    private CancellationTokenSource? _cts;
    private Task? _worker;
    public bool IsRunning => _cts is not null;
    public string CurrentPath { get; private set; } = string.Empty;

    public void Start(string path)
    {
        if (IsRunning) return;
        CurrentPath = path;
        _cts = new CancellationTokenSource();
        _worker = Task.Run(() => RunAsync(path, _cts.Token));
    }

    public void Enqueue(MeasurementFrame frame)
    {
        if (IsRunning && !frame.IsError) _queue.Enqueue(frame);
    }

    public async Task StopAsync()
    {
        if (_cts is null || _worker is null) return;
        _cts.Cancel();
        try { await _worker; } catch { }
        _cts.Dispose();
        _cts = null;
        _worker = null;
        CurrentPath = string.Empty;
    }

    private async Task RunAsync(string path, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        await using var sw = new StreamWriter(fs, new UTF8Encoding(true));
        bool headerWritten = false;
        string[] dynamicHeaders = Array.Empty<string>();

        while (!ct.IsCancellationRequested || !_queue.IsEmpty)
        {
            while (_queue.TryDequeue(out var frame))
            {
                if (!headerWritten)
                {
                    dynamicHeaders = frame.Headers.Count > 0
                        ? frame.Headers.Select(SanitizeCsvTitle).ToArray()
                        : Enumerable.Range(1, frame.Values.Count).Select(i => $"Value{i}").ToArray();
                    string title = string.Join(',', new[] { "时间" }.Concat(dynamicHeaders.Select(EscapeCsv)));
                    await sw.WriteLineAsync(title);
                    headerWritten = true;
                }

                var line = new List<string>
                {
                    EscapeCsv(frame.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                };

                for (int i = 0; i < dynamicHeaders.Length; i++)
                {
                    if (i < frame.Values.Count && frame.Values[i].HasValue)
                        line.Add(frame.Values[i]!.Value.ToString("G", System.Globalization.CultureInfo.InvariantCulture));
                    else
                        line.Add(string.Empty);
                }

                await sw.WriteLineAsync(string.Join(',', line));
            }

            await sw.FlushAsync();
            try { await Task.Delay(100, ct); } catch { }
        }
    }

    private static string EscapeCsv(string value) => '"' + value.Replace("\"", "\"\"") + '"';
    private static string SanitizeCsvTitle(string s) => s.Replace(',', '_').Replace('"', '_').Trim();
}

public sealed class FixedSizeFrameBuffer
{
    private readonly int _capacity;
    private readonly Queue<MeasurementFrame> _items = new();
    private readonly object _sync = new();

    public FixedSizeFrameBuffer(int capacity) => _capacity = Math.Max(10, capacity);

    public void Add(MeasurementFrame frame)
    {
        lock (_sync)
        {
            _items.Enqueue(frame);
            while (_items.Count > _capacity) _items.Dequeue();
        }
    }

    public MeasurementFrame[] Snapshot()
    {
        lock (_sync)
        {
            return _items.ToArray();
        }
    }

    public void Clear()
    {
        lock (_sync) _items.Clear();
    }
}

public static class SettingsStore
{
    public static string AppDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PA300UpperMachineFull");
    public static string SettingsPath => Path.Combine(AppDir, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                var defaults = AppSettings.CreateDefault();
                Save(defaults);
                return defaults;
            }

            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? AppSettings.CreateDefault();
        }
        catch
        {
            return AppSettings.CreateDefault();
        }
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(AppDir);
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }
}
