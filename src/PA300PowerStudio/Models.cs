using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;

namespace PA300UpperMachineFull;

public sealed class AppSettings
{
    public string ConnectionType { get; set; } = "USB";
    public string Ip { get; set; } = "192.168.1.100";
    public int Port { get; set; } = 5025;
    public string ComPort { get; set; } = string.Empty;
    public int BaudRate { get; set; } = 115200;
    public int PollIntervalMs { get; set; } = 200;
    public int ChartCapacity { get; set; } = 300;
    public int RawTextMaxChars { get; set; } = 120000;
    public int GridMaxRows { get; set; } = 300;
    public bool AutoReconnect { get; set; } = true;
    public int ReconnectDelayMs { get; set; } = 1500;
    public string DefaultQueryCommand { get; set; } = ":NUMeric:NORMal:VALue?";
    public string DefaultHeaderCommand { get; set; } = ":NUMeric:NORMal:HEADer?";
    public string NumericFormat { get; set; } = "ASCii";
    public string Rate { get; set; } = "250MS";
    public bool NumericHoldEnabled { get; set; }
    public int NormalPresetMode { get; set; } = 2;
    public int NormalItemCount { get; set; } = 10;
    public int ListPresetMode { get; set; } = 2;
    public int ListItemCount { get; set; } = 1;
    public int ListOrder { get; set; } = 50;
    public string ListSelect { get; set; } = "ALL";
    public string SetupCommands { get; set; } = string.Empty;
    public List<CommandPreset> Presets { get; set; } = new();

    public static AppSettings CreateDefault()
    {
        return new AppSettings
        {
            Presets = new List<CommandPreset>
            {
                new() { Name = "常规测量：电压/电流/功率/功率因数/频率", QueryCommand = ":NUMeric:NORMal:VALue?", HeaderCommand = ":NUMeric:NORMal:HEADer?", Description = "读取 PA300 当前常规输出列；常见为 U/I/P/S/Q/PF/相位/频率等" },
                new() { Name = "谐波测量：各阶电压/电流/功率谐波", QueryCommand = ":NUMeric:LIST:VALue?", HeaderCommand = string.Empty, Description = "读取谐波列表，列含义由谐波输出设置决定" },
                new() { Name = "设备信息：型号/序列号/固件版本", QueryCommand = "*IDN?", HeaderCommand = string.Empty, Description = "读取仪器身份信息，不是测量数据" },
                new() { Name = "谐波失真：THD/总谐波失真", QueryCommand = ":NUMeric:LIST:VALue?", HeaderCommand = string.Empty, Description = "结合谐波 LIST 预设读取 THD 等失真项目" }
            }
        };
    }
}

public sealed class CommandPreset
{
    public string Name { get; set; } = string.Empty;
    public string QueryCommand { get; set; } = string.Empty;
    public string HeaderCommand { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public override string ToString() => Name;
}

public sealed class MeasurementFrame
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public string Raw { get; init; } = string.Empty;
    public IReadOnlyList<string> Headers { get; init; } = Array.Empty<string>();
    public IReadOnlyList<double?> Values { get; init; } = Array.Empty<double?>();
    public bool IsError { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;

    [JsonIgnore]
    public int ValueCount => Values.Count;

    public static MeasurementFrame FromValues(string raw, IReadOnlyList<string> headers, IReadOnlyList<double?> values)
        => new() { Timestamp = DateTime.Now, Raw = raw, Headers = headers, Values = values };

    public static MeasurementFrame FromError(string error)
        => new() { Timestamp = DateTime.Now, IsError = true, ErrorMessage = error };
}

public static class ScpiValueParser
{
    private const float BinaryNanSentinel = 9.91e37f;

    public static List<double?> ParseValues(string raw)
    {
        return ParseValues(Encoding.ASCII.GetBytes(raw ?? string.Empty));
    }

    public static List<double?> ParseValues(byte[] raw)
    {
        if (raw.Length == 0) return new List<double?>();

        if (TryExtractBinaryFloatPayload(raw, out var payload))
        {
            return ParseFloatPayload(payload);
        }

        string ascii = Encoding.ASCII.GetString(raw).Trim();
        var parts = ascii.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var values = new List<double?>(parts.Length);

        foreach (var part in parts)
            values.Add(ParseAsciiToken(part));

        return values;
    }

    public static List<string> ParseHeaders(string raw)
    {
        return raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                  .Select(x => x.Trim())
                  .Where(x => !string.IsNullOrWhiteSpace(x))
                  .ToList();
    }

    public static string FormatRawPreview(byte[] raw)
    {
        if (raw.Length == 0) return string.Empty;

        if (TryExtractBinaryFloatPayload(raw, out var payload))
        {
            int count = payload.Length / 4;
            return $"#BINARY_FLOAT[{count}]";
        }

        return Encoding.ASCII.GetString(raw).Trim();
    }

    public static int? ParseIntValue(string raw)
    {
        string token = ExtractLastToken(raw);
        if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        return null;
    }

    public static string ParseKeywordValue(string raw)
    {
        return ExtractLastToken(raw).ToUpperInvariant();
    }

    public static string? ParseItemValue(string raw)
    {
        int firstSpace = raw.IndexOf(' ');
        if (firstSpace < 0 || firstSpace == raw.Length - 1) return null;
        return raw[(firstSpace + 1)..].Trim();
    }

    private static double? ParseAsciiToken(string token)
    {
        token = token.Trim();
        if (token.Equals("NAN", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("INF", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("-INF", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("------------", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("---OL---", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("---OF---", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("ERROR", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            return value;

        return null;
    }

    private static bool TryExtractBinaryFloatPayload(byte[] raw, out byte[] payload)
    {
        payload = Array.Empty<byte>();
        if (raw.Length < 3 || raw[0] != (byte)'#') return false;

        int digitCount = raw[1] - (byte)'0';
        if (digitCount < 0 || digitCount > 9 || raw.Length < 2 + digitCount) return false;

        int payloadLength = 0;
        if (digitCount > 0)
        {
            string lengthText = Encoding.ASCII.GetString(raw, 2, digitCount);
            if (!int.TryParse(lengthText, NumberStyles.None, CultureInfo.InvariantCulture, out payloadLength))
                return false;
        }

        int payloadStart = 2 + digitCount;
        if (payloadLength < 0 || raw.Length < payloadStart + payloadLength) return false;

        payload = new byte[payloadLength];
        Buffer.BlockCopy(raw, payloadStart, payload, 0, payloadLength);
        return payloadLength % 4 == 0;
    }

    private static List<double?> ParseFloatPayload(byte[] payload)
    {
        var values = new List<double?>(payload.Length / 4);
        for (int offset = 0; offset < payload.Length; offset += 4)
        {
            var slice = payload.AsSpan(offset, 4);
            byte[] bytes = new byte[4];
            slice.CopyTo(bytes);
            if (BitConverter.IsLittleEndian) bytes.Reverse();

            float value = BitConverter.ToSingle(bytes);
            values.Add(IsInvalidFloatValue(value) ? null : value);
        }

        return values;
    }

    private static bool IsInvalidFloatValue(float value)
    {
        return float.IsNaN(value) || float.IsInfinity(value) || Math.Abs(value - BinaryNanSentinel) < 1e33f;
    }

    private static string ExtractLastToken(string raw)
    {
        raw = raw.Trim();
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        int lastSpace = raw.LastIndexOf(' ');
        return lastSpace >= 0 ? raw[(lastSpace + 1)..].Trim() : raw;
    }
}

public sealed class HarmonicListDescriptor
{
    public string Function { get; init; } = string.Empty;
    public int Element { get; init; } = 1;
}

public static class HarmonicHeaderBuilder
{
    public static List<string> Build(IReadOnlyList<HarmonicListDescriptor> descriptors, int order, string selectMode)
    {
        var harmonicLabels = BuildHarmonicLabels(order, selectMode);
        var headers = new List<string>(descriptors.Count * harmonicLabels.Count);

        foreach (var descriptor in descriptors)
        {
            string prefix = BuildItemPrefix(descriptor);
            headers.AddRange(harmonicLabels.Select(label => $"{prefix}-{label}"));
        }

        return headers;
    }

    private static List<string> BuildHarmonicLabels(int order, string selectMode)
    {
        order = Math.Clamp(order, 1, 50);
        string mode = selectMode.ToUpperInvariant();

        IEnumerable<int> orders = Enumerable.Range(1, order);
        if (mode is "ODD3" or "ODD_FROM_3" or "ODD")
            orders = orders.Where(x => x % 2 == 1 && (mode == "ODD" || x >= 3));
        else if (mode == "EVEN") orders = orders.Where(x => x % 2 == 0);

        if (mode is "ODD3" or "ODD_FROM_3")
            return orders.Select(x => $"H{x:00}").ToList();

        var labels = new List<string> { "TOT", "DC" };
        labels.AddRange(orders.Select(x => $"H{x:00}"));
        return labels;
    }

    private static string BuildItemPrefix(HarmonicListDescriptor descriptor)
    {
        string func = descriptor.Function.ToUpperInvariant();
        return $"{func}-E{descriptor.Element}";
    }
}
