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

public sealed record WorkbenchCommandDefaultSettings(
    string ProfileName,
    string CommandType,
    string ArgumentName,
    string Value);

public sealed record WorkbenchCommandDefaultSettingsUpdate(
    string ProfileName,
    string CommandType,
    string ArgumentName,
    string Value);

public sealed record WorkbenchSettingsSnapshot(
    WorkbenchThemeMode ThemeMode,
    string? ToolProfilePath,
    IReadOnlyList<WorkbenchSerialDeviceSettings> SerialDevices,
    IReadOnlyList<WorkbenchCommandDefaultSettings> CommandDefaults);

public sealed record WorkbenchSettingsUpdate(
    WorkbenchThemeMode ThemeMode,
    IReadOnlyList<WorkbenchSerialDeviceSettingsUpdate> SerialDevices,
    IReadOnlyList<WorkbenchCommandDefaultSettingsUpdate> CommandDefaults);

public static class WorkbenchToolProfileSettingsEditor
{
    public const string GlobalCommandDefaultsScope = "global";

    private static readonly Dictionary<string, IReadOnlyList<string>> ProfileCommandDefaultArguments = new(StringComparer.Ordinal)
    {
        ["sent_usb.read"] = new[] { "channel", "timeout_ms", "max_frames" },
        ["sent_usb.command"] = new[] { "channel", "timeout_ms", "read_ack" },
        ["power_supply.command"] = new[] { "channel", "timeout_ms" }
    };

    private static readonly Dictionary<string, IReadOnlyList<string>> GlobalCommandDefaultArguments = new(StringComparer.Ordinal)
    {
        ["serial.write"] = new[] { "timeout_ms" },
        ["serial.read"] = new[] { "timeout_ms" },
        ["serial.write_bytes"] = new[] { "timeout_ms" },
        ["serial.read_bytes"] = new[] { "timeout_ms" },
        ["canoe.measurement.stop"] = new[] { "timeout_ms" },
        ["canoe.signal.read"] = new[] { "bus", "channel", "message", "timeout_ms" },
        ["canoe.sysvar.read"] = new[] { "timeout_ms" },
        ["canoe.sysvar.set"] = new[] { "timeout_ms" },
        ["inca.measure.read"] = new[] { "acquisition_rate", "device", "timeout_ms" },
        ["inca.calibration.set"] = new[] { "device", "timeout_ms", "value_kind" },
        ["inca.recording.start"] = new[] { "file_format", "format", "output_dir", "timeout_ms" },
        ["inca.recording.stop"] = new[] { "discard", "file_format", "format", "timeout_ms" },
        ["trace32.command"] = new[] { "timeout_ms", "transport", "fallback" },
        ["trace32.command_sequence"] = new[] { "timeout_ms", "transport", "fallback" }
    };

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

    public static IReadOnlyList<WorkbenchCommandDefaultSettings> ReadCommandDefaults(string yamlText)
    {
        var lines = SplitLines(yamlText);
        var defaults = new List<WorkbenchCommandDefaultSettings>();
        ReadGlobalCommandDefaults(lines, defaults);

        var profilesLineIndex = FindCommandProfilesLineIndex(lines);
        if (profilesLineIndex < 0)
        {
            return defaults;
        }

        var profilesIndent = LeadingSpaceCount(lines[profilesLineIndex]);
        for (var profileIndex = profilesLineIndex + 1; profileIndex < lines.Count; profileIndex++)
        {
            if (string.IsNullOrWhiteSpace(lines[profileIndex]))
            {
                continue;
            }

            var profileIndent = LeadingSpaceCount(lines[profileIndex]);
            if (profileIndent <= profilesIndent)
            {
                break;
            }

            var profileTrimmed = lines[profileIndex].Trim();
            if (profileIndent != profilesIndent + 2
                || !profileTrimmed.EndsWith(":", StringComparison.Ordinal)
                || profileTrimmed.StartsWith("-", StringComparison.Ordinal))
            {
                continue;
            }

            var profileName = profileTrimmed[..^1].Trim();
            var profileEnd = FindBlockEnd(lines, profileIndex, profileIndent);
            var commandsLineIndex = FindChildBlockLineIndex(lines, profileIndex + 1, profileEnd, profileIndent, "commands");
            if (commandsLineIndex < 0)
            {
                profileIndex = profileEnd - 1;
                continue;
            }

            var commandsIndent = LeadingSpaceCount(lines[commandsLineIndex]);
            for (var commandIndex = commandsLineIndex + 1; commandIndex < profileEnd; commandIndex++)
            {
                if (string.IsNullOrWhiteSpace(lines[commandIndex]))
                {
                    continue;
                }

                var commandIndent = LeadingSpaceCount(lines[commandIndex]);
                if (commandIndent <= commandsIndent)
                {
                    break;
                }

                var commandTrimmed = lines[commandIndex].Trim();
                if (commandIndent != commandsIndent + 2
                    || !commandTrimmed.EndsWith(":", StringComparison.Ordinal)
                    || commandTrimmed.StartsWith("-", StringComparison.Ordinal))
                {
                    continue;
                }

                var commandType = commandTrimmed[..^1].Trim();
                if (!ProfileCommandDefaultArguments.TryGetValue(commandType, out var defaultArguments))
                {
                    continue;
                }

                var commandEnd = FindBlockEnd(lines, commandIndex, commandIndent);
                var values = ReadScalarValues(lines, commandIndex + 1, commandEnd, commandIndent);
                foreach (var argument in defaultArguments)
                {
                    defaults.Add(
                        new WorkbenchCommandDefaultSettings(
                            profileName,
                            commandType,
                            argument,
                            values.GetValueOrDefault(argument, "")));
                }

                commandIndex = commandEnd - 1;
            }

            profileIndex = profileEnd - 1;
        }

        return defaults;
    }

    private static void ReadGlobalCommandDefaults(
        IReadOnlyList<string> lines,
        List<WorkbenchCommandDefaultSettings> defaults)
    {
        var configuredValues = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        var defaultsLineIndex = FindCommandDefaultsLineIndex(lines);
        if (defaultsLineIndex >= 0)
        {
            var defaultsIndent = LeadingSpaceCount(lines[defaultsLineIndex]);
            for (var commandIndex = defaultsLineIndex + 1; commandIndex < lines.Count; commandIndex++)
            {
                if (string.IsNullOrWhiteSpace(lines[commandIndex]))
                {
                    continue;
                }

                var commandIndent = LeadingSpaceCount(lines[commandIndex]);
                if (commandIndent <= defaultsIndent)
                {
                    break;
                }

                var commandTrimmed = lines[commandIndex].Trim();
                if (commandIndent != defaultsIndent + 2
                    || !commandTrimmed.EndsWith(":", StringComparison.Ordinal)
                    || commandTrimmed.StartsWith("-", StringComparison.Ordinal))
                {
                    continue;
                }

                var commandType = commandTrimmed[..^1].Trim();
                if (!GlobalCommandDefaultArguments.ContainsKey(commandType))
                {
                    continue;
                }

                var commandEnd = FindBlockEnd(lines, commandIndex, commandIndent);
                configuredValues[commandType] = ReadScalarValues(lines, commandIndex + 1, commandEnd, commandIndent);
                commandIndex = commandEnd - 1;
            }
        }

        foreach (var (commandType, arguments) in GlobalCommandDefaultArguments)
        {
            configuredValues.TryGetValue(commandType, out var values);
            foreach (var argument in arguments)
            {
                defaults.Add(
                    new WorkbenchCommandDefaultSettings(
                        GlobalCommandDefaultsScope,
                        commandType,
                        argument,
                        values?.GetValueOrDefault(argument, "") ?? ""));
            }
        }
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

    public static Task<string> UpdateCommandDefaultsAsync(
        string yamlText,
        IReadOnlyList<WorkbenchCommandDefaultSettingsUpdate> updates)
    {
        var newline = yamlText.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = SplitLines(yamlText);
        foreach (var update in updates)
        {
            UpdateCommandDefault(lines, update);
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

    private static void UpdateCommandDefault(
        List<string> lines,
        WorkbenchCommandDefaultSettingsUpdate update)
    {
        if (string.Equals(update.ProfileName, GlobalCommandDefaultsScope, StringComparison.Ordinal))
        {
            UpdateGlobalCommandDefault(lines, update);
            return;
        }

        if (!ProfileCommandDefaultArguments.TryGetValue(update.CommandType, out var defaultArguments)
            || !defaultArguments.Contains(update.ArgumentName, StringComparer.Ordinal))
        {
            return;
        }

        var profilesLineIndex = FindCommandProfilesLineIndex(lines);
        if (profilesLineIndex < 0)
        {
            return;
        }

        var profilesIndent = LeadingSpaceCount(lines[profilesLineIndex]);
        for (var profileIndex = profilesLineIndex + 1; profileIndex < lines.Count; profileIndex++)
        {
            if (string.IsNullOrWhiteSpace(lines[profileIndex]))
            {
                continue;
            }

            var profileIndent = LeadingSpaceCount(lines[profileIndex]);
            if (profileIndent <= profilesIndent)
            {
                break;
            }

            if (profileIndent != profilesIndent + 2
                || !string.Equals(lines[profileIndex].Trim(), $"{update.ProfileName}:", StringComparison.Ordinal))
            {
                continue;
            }

            var profileEnd = FindBlockEnd(lines, profileIndex, profileIndent);
            var commandsLineIndex = FindChildBlockLineIndex(lines, profileIndex + 1, profileEnd, profileIndent, "commands");
            if (commandsLineIndex < 0)
            {
                return;
            }

            var commandsIndent = LeadingSpaceCount(lines[commandsLineIndex]);
            for (var commandIndex = commandsLineIndex + 1; commandIndex < profileEnd; commandIndex++)
            {
                if (string.IsNullOrWhiteSpace(lines[commandIndex]))
                {
                    continue;
                }

                var commandIndent = LeadingSpaceCount(lines[commandIndex]);
                if (commandIndent <= commandsIndent)
                {
                    break;
                }

                if (commandIndent != commandsIndent + 2
                    || !string.Equals(lines[commandIndex].Trim(), $"{update.CommandType}:", StringComparison.Ordinal))
                {
                    continue;
                }

                var commandEnd = FindBlockEnd(lines, commandIndex, commandIndent);
                var propertyIndent = ResolvePropertyIndent(lines, commandIndex + 1, commandEnd, commandIndent + 2);
                if (string.IsNullOrWhiteSpace(update.Value))
                {
                    RemoveScalar(lines, commandIndex + 1, commandEnd, propertyIndent, update.ArgumentName);
                }
                else
                {
                    SetScalar(lines, commandIndex + 1, commandEnd, propertyIndent, update.ArgumentName, update.Value.Trim(), out _);
                }

                return;
            }
        }
    }

    private static void UpdateGlobalCommandDefault(
        List<string> lines,
        WorkbenchCommandDefaultSettingsUpdate update)
    {
        if (!GlobalCommandDefaultArguments.TryGetValue(update.CommandType, out var defaultArguments)
            || !defaultArguments.Contains(update.ArgumentName, StringComparer.Ordinal))
        {
            return;
        }

        var defaultsLineIndex = FindCommandDefaultsLineIndex(lines);
        if (defaultsLineIndex < 0)
        {
            if (string.IsNullOrWhiteSpace(update.Value))
            {
                return;
            }

            if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1]))
            {
                lines.Add("");
            }

            lines.Add("command_defaults:");
            lines.Add($"  {update.CommandType}:");
            lines.Add($"    {update.ArgumentName}: {update.Value.Trim()}");
            return;
        }

        var defaultsIndent = LeadingSpaceCount(lines[defaultsLineIndex]);
        var defaultsEnd = FindBlockEnd(lines, defaultsLineIndex, defaultsIndent);
        for (var commandIndex = defaultsLineIndex + 1; commandIndex < defaultsEnd; commandIndex++)
        {
            if (string.IsNullOrWhiteSpace(lines[commandIndex]))
            {
                continue;
            }

            var commandIndent = LeadingSpaceCount(lines[commandIndex]);
            if (commandIndent <= defaultsIndent)
            {
                break;
            }

            if (commandIndent != defaultsIndent + 2
                || !string.Equals(lines[commandIndex].Trim(), $"{update.CommandType}:", StringComparison.Ordinal))
            {
                continue;
            }

            var commandEnd = FindBlockEnd(lines, commandIndex, commandIndent);
            var propertyIndent = ResolvePropertyIndent(lines, commandIndex + 1, commandEnd, commandIndent + 2);
            if (string.IsNullOrWhiteSpace(update.Value))
            {
                RemoveScalar(lines, commandIndex + 1, commandEnd, propertyIndent, update.ArgumentName);
            }
            else
            {
                SetScalar(lines, commandIndex + 1, commandEnd, propertyIndent, update.ArgumentName, update.Value.Trim(), out _);
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(update.Value))
        {
            return;
        }

        lines.Insert(defaultsEnd, $"{new string(' ', defaultsIndent + 2)}{update.CommandType}:");
        lines.Insert(defaultsEnd + 1, $"{new string(' ', defaultsIndent + 4)}{update.ArgumentName}: {update.Value.Trim()}");
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

    private static int FindCommandProfilesLineIndex(IReadOnlyList<string> lines)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            if (string.Equals(lines[index].Trim(), "command_profiles:", StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindCommandDefaultsLineIndex(IReadOnlyList<string> lines)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            if (string.Equals(lines[index].Trim(), "command_defaults:", StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindChildBlockLineIndex(
        IReadOnlyList<string> lines,
        int startIndex,
        int endIndex,
        int parentIndent,
        string key)
    {
        for (var index = startIndex; index < endIndex; index++)
        {
            if (string.IsNullOrWhiteSpace(lines[index]))
            {
                continue;
            }

            var indent = LeadingSpaceCount(lines[index]);
            if (indent <= parentIndent)
            {
                break;
            }

            if (indent == parentIndent + 2
                && string.Equals(lines[index].Trim(), $"{key}:", StringComparison.Ordinal))
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

    private static void RemoveScalar(
        List<string> lines,
        int startIndex,
        int endIndex,
        int indent,
        string key)
    {
        for (var index = startIndex; index < endIndex; index++)
        {
            if (LeadingSpaceCount(lines[index]) == indent
                && string.Equals(lines[index].Trim().Split(':')[0], key, StringComparison.Ordinal))
            {
                lines.RemoveAt(index);
                return;
            }
        }
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
