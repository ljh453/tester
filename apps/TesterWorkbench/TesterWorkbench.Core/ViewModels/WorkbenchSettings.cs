using System.Globalization;

namespace TesterWorkbench.Core.ViewModels;

public sealed record WorkbenchSerialDeviceSettings(
    string Name,
    string Port,
    int Baudrate,
    string Parity,
    double StopBits,
    int ByteSize);

public sealed record WorkbenchSerialDeviceSettingsUpdate(
    string Name,
    string Parity,
    double StopBits,
    int ByteSize);

public sealed record WorkbenchSettingsSnapshot(
    WorkbenchThemeMode ThemeMode,
    string? ToolProfilePath,
    IReadOnlyList<WorkbenchSerialDeviceSettings> SerialDevices);

public sealed record WorkbenchSettingsUpdate(
    WorkbenchThemeMode ThemeMode,
    IReadOnlyList<WorkbenchSerialDeviceSettingsUpdate> SerialDevices);

public static class WorkbenchToolProfileSettingsEditor
{
    public static IReadOnlyList<WorkbenchSerialDeviceSettings> ReadSerialDevices(string yamlText)
    {
        var lines = SplitLines(yamlText);
        var devicesLineIndex = FindDevicesLineIndex(lines);
        if (devicesLineIndex < 0)
        {
            return Array.Empty<WorkbenchSerialDeviceSettings>();
        }

        var devicesIndent = LeadingSpaceCount(lines[devicesLineIndex]);
        var devices = new List<WorkbenchSerialDeviceSettings>();
        for (var index = devicesLineIndex + 1; index < lines.Count; index++)
        {
            var line = lines[index];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var indent = LeadingSpaceCount(line);
            if (indent <= devicesIndent)
            {
                break;
            }

            var trimmed = line.Trim();
            if (indent != devicesIndent + 2
                || !trimmed.EndsWith(":", StringComparison.Ordinal)
                || trimmed.StartsWith("-", StringComparison.Ordinal))
            {
                continue;
            }

            var name = trimmed[..^1].Trim();
            var blockEnd = FindBlockEnd(lines, index, indent);
            var values = ReadScalarValues(lines, index + 1, blockEnd, indent);
            devices.Add(
                new WorkbenchSerialDeviceSettings(
                    name,
                    values.GetValueOrDefault("port", ""),
                    ParseInt(values.GetValueOrDefault("baudrate"), 9600),
                    NormalizeParity(values.GetValueOrDefault("parity", "none")),
                    ParseDouble(values.GetValueOrDefault("stop_bits"), 1.0),
                    ParseInt(values.GetValueOrDefault("byte_size"), 8)));
            index = blockEnd - 1;
        }

        return devices;
    }

    public static Task<string> UpdateSerialDevicesAsync(
        string yamlText,
        IReadOnlyList<WorkbenchSerialDeviceSettingsUpdate> updates)
    {
        var newline = yamlText.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = SplitLines(yamlText);
        foreach (var update in updates)
        {
            UpdateSerialDevice(lines, update);
        }

        return Task.FromResult(string.Join(newline, lines));
    }

    private static void UpdateSerialDevice(
        List<string> lines,
        WorkbenchSerialDeviceSettingsUpdate update)
    {
        var devicesLineIndex = FindDevicesLineIndex(lines);
        if (devicesLineIndex < 0)
        {
            return;
        }

        var devicesIndent = LeadingSpaceCount(lines[devicesLineIndex]);
        for (var index = devicesLineIndex + 1; index < lines.Count; index++)
        {
            if (string.IsNullOrWhiteSpace(lines[index]))
            {
                continue;
            }

            var indent = LeadingSpaceCount(lines[index]);
            if (indent <= devicesIndent)
            {
                break;
            }

            var trimmed = lines[index].Trim();
            if (indent != devicesIndent + 2 || !string.Equals(trimmed, $"{update.Name}:", StringComparison.Ordinal))
            {
                continue;
            }

            var blockEnd = FindBlockEnd(lines, index, indent);
            var propertyIndent = ResolvePropertyIndent(lines, index + 1, blockEnd, indent + 2);
            SetScalar(lines, index + 1, blockEnd, propertyIndent, "parity", NormalizeParity(update.Parity), out blockEnd);
            SetScalar(lines, index + 1, blockEnd, propertyIndent, "stop_bits", FormatDouble(update.StopBits), out blockEnd);
            SetScalar(lines, index + 1, blockEnd, propertyIndent, "byte_size", update.ByteSize.ToString(CultureInfo.InvariantCulture), out _);
            return;
        }
    }

    private static int FindDevicesLineIndex(IReadOnlyList<string> lines)
    {
        var serialLineIndex = -1;
        var serialIndent = 0;
        for (var index = 0; index < lines.Count; index++)
        {
            if (!string.Equals(lines[index].Trim(), "serial:", StringComparison.Ordinal))
            {
                continue;
            }

            serialLineIndex = index;
            serialIndent = LeadingSpaceCount(lines[index]);
            break;
        }

        if (serialLineIndex < 0)
        {
            return -1;
        }

        for (var index = serialLineIndex + 1; index < lines.Count; index++)
        {
            if (string.IsNullOrWhiteSpace(lines[index]))
            {
                continue;
            }

            var indent = LeadingSpaceCount(lines[index]);
            if (indent <= serialIndent)
            {
                return -1;
            }

            if (string.Equals(lines[index].Trim(), "devices:", StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static Dictionary<string, string> ReadScalarValues(
        IReadOnlyList<string> lines,
        int startIndex,
        int endIndex,
        int parentIndent)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = startIndex; index < endIndex; index++)
        {
            var line = lines[index];
            if (string.IsNullOrWhiteSpace(line) || LeadingSpaceCount(line) <= parentIndent)
            {
                continue;
            }

            var trimmed = line.Trim();
            var separatorIndex = trimmed.IndexOf(':');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = trimmed[..separatorIndex].Trim();
            var value = trimmed[(separatorIndex + 1)..].Trim().Trim('"', '\'');
            values[key] = value;
        }

        return values;
    }

    private static void SetScalar(
        List<string> lines,
        int startIndex,
        int endIndex,
        int indent,
        string key,
        string value,
        out int newEndIndex)
    {
        for (var index = startIndex; index < endIndex; index++)
        {
            var trimmed = lines[index].Trim();
            if (!trimmed.StartsWith($"{key}:", StringComparison.Ordinal))
            {
                continue;
            }

            lines[index] = $"{new string(' ', LeadingSpaceCount(lines[index]))}{key}: {value}";
            newEndIndex = endIndex;
            return;
        }

        lines.Insert(endIndex, $"{new string(' ', indent)}{key}: {value}");
        newEndIndex = endIndex + 1;
    }

    private static int FindBlockEnd(IReadOnlyList<string> lines, int startIndex, int indent)
    {
        for (var index = startIndex + 1; index < lines.Count; index++)
        {
            if (string.IsNullOrWhiteSpace(lines[index]))
            {
                continue;
            }

            if (LeadingSpaceCount(lines[index]) <= indent)
            {
                return index;
            }
        }

        return lines.Count;
    }

    private static int ResolvePropertyIndent(
        IReadOnlyList<string> lines,
        int startIndex,
        int endIndex,
        int fallbackIndent)
    {
        for (var index = startIndex; index < endIndex; index++)
        {
            if (!string.IsNullOrWhiteSpace(lines[index]))
            {
                return LeadingSpaceCount(lines[index]);
            }
        }

        return fallbackIndent;
    }

    private static List<string> SplitLines(string text)
    {
        return text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .ToList();
    }

    private static int LeadingSpaceCount(string line)
    {
        var count = 0;
        while (count < line.Length && line[count] == ' ')
        {
            count++;
        }

        return count;
    }

    private static int ParseInt(string? value, int defaultValue)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : defaultValue;
    }

    private static double ParseDouble(string? value, double defaultValue)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : defaultValue;
    }

    private static string NormalizeParity(string? value)
    {
        var normalized = (value ?? "none").Trim().ToLowerInvariant();
        return normalized switch
        {
            "n" => "none",
            "e" => "even",
            "o" => "odd",
            "m" => "mark",
            "s" => "space",
            "none" or "even" or "odd" or "mark" or "space" => normalized,
            _ => "none"
        };
    }

    private static string FormatDouble(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
