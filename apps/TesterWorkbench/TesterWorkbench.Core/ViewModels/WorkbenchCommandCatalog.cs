namespace TesterWorkbench.Core.ViewModels;

public enum WorkbenchCommandArgumentKind
{
    Text,
    Number,
    Boolean,
    Identifier,
    VariableName,
    Expression,
    Value,
    Enum,
    Map,
    List,
    CommandList
}

public enum WorkbenchCommandAutocompleteKind
{
    None,
    Variables,
    Functions,
    ToolProfiles,
    Enum
}

public sealed class WorkbenchCommandArgumentDefinition
{
    public WorkbenchCommandArgumentDefinition(
        string commandType,
        string name,
        WorkbenchCommandArgumentKind kind,
        bool isRequired,
        WorkbenchCommandAutocompleteKind autocompleteKind,
        IReadOnlyList<string> suggestions)
    {
        CommandType = commandType;
        Name = name;
        Kind = kind;
        IsRequired = isRequired;
        AutocompleteKind = autocompleteKind;
        Suggestions = suggestions;
    }

    public string CommandType { get; }

    public string Name { get; }

    public WorkbenchCommandArgumentKind Kind { get; }

    public bool IsRequired { get; }

    public WorkbenchCommandAutocompleteKind AutocompleteKind { get; }

    public IReadOnlyList<string> Suggestions { get; }

    public bool IsScalarEditable =>
        Kind is not WorkbenchCommandArgumentKind.Map
            and not WorkbenchCommandArgumentKind.List
            and not WorkbenchCommandArgumentKind.CommandList;

    public bool HasSuggestions => Suggestions.Count > 0;

    public string RequirementText => IsRequired ? "required" : "optional";

    public static IReadOnlyList<WorkbenchCommandArgumentDefinition> Build(
        string commandType,
        IReadOnlyList<string> requiredArgs,
        IReadOnlyList<string> optionalArgs)
    {
        return requiredArgs
            .Select(argument => Create(commandType, argument, isRequired: true))
            .Concat(optionalArgs.Select(argument => Create(commandType, argument, isRequired: false)))
            .ToArray();
    }

    private static WorkbenchCommandArgumentDefinition Create(
        string commandType,
        string name,
        bool isRequired)
    {
        var kind = ResolveKind(commandType, name);
        var autocompleteKind = ResolveAutocompleteKind(commandType, name, kind);
        return new WorkbenchCommandArgumentDefinition(
            commandType,
            name,
            kind,
            isRequired,
            autocompleteKind,
            SuggestionsFor(commandType, name));
    }

    private static WorkbenchCommandArgumentKind ResolveKind(string commandType, string name)
    {
        if (name == "do")
        {
            return WorkbenchCommandArgumentKind.CommandList;
        }

        if (name is "args" or "out" or "slow_data")
        {
            return WorkbenchCommandArgumentKind.Map;
        }

        if (name is "data_nibbles" or "commands")
        {
            return WorkbenchCommandArgumentKind.List;
        }

        if (name is "ms" or "timeout_ms" or "channel" or "count" or "max_frames"
            or "voltage" or "current"
            or "unit_time_us" or "pulse_pause_frame_period_us" or "message_id"
            or "slow_message_id" or "status_nibble" or "crc_received" or "crc_calculated"
            or "data_nibble_count" or "buffer_index" or "slow_buffer_index" or "index"
            or "data" or "status")
        {
            return WorkbenchCommandArgumentKind.Number;
        }

        if (commandType == "power_supply.command" && name == "value")
        {
            return WorkbenchCommandArgumentKind.Number;
        }

        if (commandType == "sent_usb.command" && name == "crc")
        {
            return WorkbenchCommandArgumentKind.Number;
        }

        if (name is "state" or "autostart" or "auto_start" or "average" or "read" or "crc"
            or "pulse_pause_enabled" or "enhanced_format" or "enhanced_config_bit"
            or "enhanced_serial_format" or "slow_channel_tx_crc_fault"
            or "swap_fast_data_nibbles" or "read_ack" or "enabled" or "buffer_enabled")
        {
            return WorkbenchCommandArgumentKind.Boolean;
        }

        if (name is "action" or "transport" or "fallback" or "direction" or "frame_type"
            or "crc_mode" or "rx_forward_mode" or "tx_echo_mode" or "slow_channel_mode"
            or "slow_frame_type")
        {
            return WorkbenchCommandArgumentKind.Enum;
        }

        if (name is "var" or "as" or "save_as")
        {
            return WorkbenchCommandArgumentKind.VariableName;
        }

        if (name == "function")
        {
            return WorkbenchCommandArgumentKind.Identifier;
        }

        if (name is "each")
        {
            return WorkbenchCommandArgumentKind.Expression;
        }

        if (name is "left" or "right" or "value")
        {
            return WorkbenchCommandArgumentKind.Value;
        }

        if (name is "text" or "message" or "command" or "until" or "match"
            or "payload_hex" or "configuration" or "output_dir")
        {
            return WorkbenchCommandArgumentKind.Text;
        }

        return WorkbenchCommandArgumentKind.Identifier;
    }

    private static WorkbenchCommandAutocompleteKind ResolveAutocompleteKind(
        string commandType,
        string name,
        WorkbenchCommandArgumentKind kind)
    {
        if (name == "function")
        {
            return WorkbenchCommandAutocompleteKind.Functions;
        }

        if (kind is WorkbenchCommandArgumentKind.Expression or WorkbenchCommandArgumentKind.Value)
        {
            return WorkbenchCommandAutocompleteKind.Variables;
        }

        if (kind == WorkbenchCommandArgumentKind.Enum)
        {
            return WorkbenchCommandAutocompleteKind.Enum;
        }

        if (name is "port" or "device")
        {
            return WorkbenchCommandAutocompleteKind.ToolProfiles;
        }

        return WorkbenchCommandAutocompleteKind.None;
    }

    private static IReadOnlyList<string> SuggestionsFor(string commandType, string name)
    {
        if (name == "transport")
        {
            return new[] { "rcl", "udp" };
        }

        if (name == "fallback")
        {
            return new[] { "udp", "none" };
        }

        if (name == "action" && commandType == "sent_usb.command")
        {
            return new[]
            {
                "config",
                "start",
                "stop",
                "transmit_fast",
                "transmit_slow",
                "transmit_slow_buffer"
            };
        }

        if (name == "action" && commandType == "power_supply.command")
        {
            return new[]
            {
                "raw",
                "apply",
                "set_voltage",
                "set_current",
                "read_voltage",
                "read_current",
                "mode",
                "output",
                "read_output",
                "measure_voltage",
                "measure_current",
                "track",
                "read_track",
                "reset",
                "identify",
                "system_error"
            };
        }

        if (name == "direction")
        {
            return new[] { "rx", "tx" };
        }

        if (name == "frame_type")
        {
            return new[] { "fast", "slow" };
        }

        if (name == "crc_mode")
        {
            return new[] { "auto", "manual" };
        }

        return Array.Empty<string>();
    }
}

public sealed class WorkbenchCommandDefinition
{
    public WorkbenchCommandDefinition(
        string commandType,
        string category,
        string description,
        IReadOnlyList<string> requiredArgs,
        IReadOnlyList<string> optionalArgs,
        string snippetBody)
    {
        CommandType = commandType;
        Category = category;
        Description = description;
        RequiredArgs = requiredArgs;
        OptionalArgs = optionalArgs;
        Arguments = WorkbenchCommandArgumentDefinition.Build(commandType, requiredArgs, optionalArgs);
        SnippetBody = snippetBody;
    }

    public string CommandType { get; }

    public string Category { get; }

    public string Description { get; }

    public IReadOnlyList<string> RequiredArgs { get; }

    public IReadOnlyList<string> OptionalArgs { get; }

    public IReadOnlyList<WorkbenchCommandArgumentDefinition> Arguments { get; }

    public string SnippetBody { get; }

    public string RequiredArgsText => RequiredArgs.Count == 0
        ? "no required args"
        : string.Join(", ", RequiredArgs);
}

public sealed record WorkbenchCommandArgumentSelection(
    bool IsRelevant,
    bool IsRequired);

public static class WorkbenchCommandArgumentVisibility
{
    private static readonly HashSet<string> PowerQueryActions = new(StringComparer.Ordinal)
    {
        "identify",
        "measure_current",
        "measure_voltage",
        "mode",
        "read_current",
        "read_output",
        "read_track",
        "read_voltage",
        "system_error"
    };

    public static WorkbenchCommandArgumentSelection Resolve(
        string commandType,
        IReadOnlyDictionary<string, string> values,
        string argumentName)
    {
        return commandType switch
        {
            "power_supply.command" => ResolvePowerSupply(values, argumentName),
            "sent_usb.command" => ResolveSentUsb(values, argumentName),
            _ => new WorkbenchCommandArgumentSelection(true, false)
        };
    }

    private static WorkbenchCommandArgumentSelection ResolvePowerSupply(
        IReadOnlyDictionary<string, string> values,
        string argumentName)
    {
        var action = NormalizeAction(ValueOf(values, "action"));
        if (string.IsNullOrWhiteSpace(action))
        {
            return Selection(argumentName, new[] { "device", "action" }, new[] { "action" });
        }

        var relevant = new HashSet<string>(StringComparer.Ordinal) { "device", "action", "timeout_ms" };
        var required = new HashSet<string>(StringComparer.Ordinal) { "action" };

        switch (action)
        {
            case "raw":
                relevant.Add("text");
                required.Add("text");
                break;
            case "apply":
                relevant.UnionWith(new[] { "channel", "voltage", "current" });
                required.UnionWith(new[] { "voltage", "current" });
                break;
            case "set_voltage":
                relevant.UnionWith(new[] { "channel", "voltage" });
                required.Add("voltage");
                break;
            case "set_current":
                relevant.UnionWith(new[] { "channel", "current" });
                required.Add("current");
                break;
            case "output":
                relevant.UnionWith(new[] { "channel", "state", "value" });
                if (string.IsNullOrWhiteSpace(ValueOf(values, "value")))
                {
                    required.Add("state");
                }
                break;
            case "track":
                relevant.UnionWith(new[] { "state", "value" });
                if (string.IsNullOrWhiteSpace(ValueOf(values, "value")))
                {
                    required.Add("state");
                }
                break;
            case "read_voltage":
            case "read_current":
            case "mode":
            case "read_output":
                relevant.UnionWith(new[] { "channel", "save_as", "until", "match", "read" });
                break;
            case "measure_voltage":
            case "measure_current":
                relevant.UnionWith(new[] { "channel", "average", "save_as", "until", "match", "read" });
                break;
            case "identify":
            case "read_track":
            case "system_error":
                relevant.UnionWith(new[] { "save_as", "until", "match", "read" });
                break;
            case "reset":
                break;
            default:
                relevant.UnionWith(new[]
                {
                    "average", "channel", "current", "match", "read", "save_as",
                    "state", "text", "until", "value", "voltage"
                });
                break;
        }

        if (PowerQueryActions.Contains(action))
        {
            relevant.Add("save_as");
        }

        return new WorkbenchCommandArgumentSelection(
            relevant.Contains(argumentName),
            required.Contains(argumentName));
    }

    private static WorkbenchCommandArgumentSelection ResolveSentUsb(
        IReadOnlyDictionary<string, string> values,
        string argumentName)
    {
        var action = NormalizeSentAction(ValueOf(values, "action"));
        if (string.IsNullOrWhiteSpace(action))
        {
            return Selection(argumentName, new[] { "device", "action" }, Array.Empty<string>());
        }

        var relevant = new HashSet<string>(StringComparer.Ordinal)
        {
            "device",
            "action",
            "channel",
            "timeout_ms",
            "read_ack",
            "save_as"
        };
        var required = new HashSet<string>(StringComparer.Ordinal);

        switch (action)
        {
            case "config":
                relevant.UnionWith(new[]
                {
                    "autostart",
                    "auto_start",
                    "direction",
                    "crc_mode",
                    "data_nibble_count",
                    "pulse_pause_enabled",
                    "rx_forward_mode",
                    "tx_echo_mode",
                    "slow_channel_mode",
                    "slow_channel_tx_crc_fault",
                    "unit_time_us",
                    "pulse_pause_frame_period_us",
                    "swap_fast_data_nibbles"
                });
                break;
            case "transmit_fast":
                relevant.UnionWith(new[] { "data_nibbles", "status", "status_nibble", "crc", "crc_calculated" });
                required.Add("data_nibbles");
                break;
            case "transmit_slow":
                relevant.UnionWith(new[]
                {
                    "slow_message_id",
                    "message_id",
                    "data",
                    "slow_data",
                    "crc_received",
                    "crc",
                    "frame_type",
                    "slow_frame_type",
                    "enhanced_format",
                    "enhanced_config_bit",
                    "enhanced_serial_format",
                    "crc_calculated",
                    "slow_channel_mode"
                });
                if (string.IsNullOrWhiteSpace(ValueOf(values, "message_id")))
                {
                    required.Add("slow_message_id");
                }
                if (string.IsNullOrWhiteSpace(ValueOf(values, "slow_data")))
                {
                    required.Add("data");
                }
                break;
            case "transmit_slow_buffer":
                relevant.UnionWith(new[]
                {
                    "buffer_index",
                    "slow_buffer_index",
                    "index",
                    "enabled",
                    "buffer_enabled",
                    "slow_message_id",
                    "message_id",
                    "data",
                    "slow_data",
                    "enhanced_format",
                    "enhanced_config_bit",
                    "enhanced_serial_format"
                });
                if (string.IsNullOrWhiteSpace(ValueOf(values, "slow_buffer_index"))
                    && string.IsNullOrWhiteSpace(ValueOf(values, "index")))
                {
                    required.Add("buffer_index");
                }
                if (!IsFalse(ValueOf(values, "enabled")) && !IsFalse(ValueOf(values, "buffer_enabled")))
                {
                    if (string.IsNullOrWhiteSpace(ValueOf(values, "message_id")))
                    {
                        required.Add("slow_message_id");
                    }
                    if (string.IsNullOrWhiteSpace(ValueOf(values, "slow_data")))
                    {
                        required.Add("data");
                    }
                }
                break;
            case "start":
            case "stop":
                break;
            default:
                relevant.UnionWith(new[]
                {
                    "autostart", "auto_start", "buffer_enabled", "buffer_index", "crc",
                    "crc_calculated", "crc_mode", "crc_received", "data", "data_nibble_count",
                    "data_nibbles", "direction", "enabled", "enhanced_config_bit",
                    "enhanced_format", "enhanced_serial_format", "frame_type", "index",
                    "message_id", "pulse_pause_enabled", "pulse_pause_frame_period_us",
                    "rx_forward_mode", "slow_buffer_index", "slow_channel_mode",
                    "slow_channel_tx_crc_fault", "slow_data", "slow_frame_type",
                    "slow_message_id", "status", "status_nibble", "swap_fast_data_nibbles",
                    "tx_echo_mode", "unit_time_us"
                });
                break;
        }

        return new WorkbenchCommandArgumentSelection(
            relevant.Contains(argumentName),
            required.Contains(argumentName));
    }

    private static WorkbenchCommandArgumentSelection Selection(
        string argumentName,
        IReadOnlyList<string> relevant,
        IReadOnlyList<string> required)
    {
        return new WorkbenchCommandArgumentSelection(
            relevant.Contains(argumentName, StringComparer.Ordinal),
            required.Contains(argumentName, StringComparer.Ordinal));
    }

    private static string ValueOf(IReadOnlyDictionary<string, string> values, string name)
    {
        return values.TryGetValue(name, out var value) ? value : "";
    }

    private static string NormalizeAction(string value)
    {
        return value.Trim().Trim('"', '\'').ToLowerInvariant().Replace("-", "_", StringComparison.Ordinal);
    }

    private static string NormalizeSentAction(string value)
    {
        var normalized = NormalizeAction(value);
        return normalized switch
        {
            "slow_buffer" or "slow_buffers" or "write_slow_buffer"
                or "write_slow_buffers" or "transmit_slow_buffers"
                or "transmit_slow_multiplex" or "transmit_slow_multiplex_buffer" => "transmit_slow_buffer",
            _ => normalized
        };
    }

    private static bool IsFalse(string value)
    {
        var normalized = value.Trim().Trim('"', '\'').ToLowerInvariant();
        return normalized is "0" or "false" or "off" or "no";
    }
}

public sealed class WorkbenchCommandCatalogGroup
{
    public WorkbenchCommandCatalogGroup(string name, IReadOnlyList<WorkbenchCommandDefinition> commands)
    {
        Name = name;
        Commands = commands;
    }

    public string Name { get; }

    public IReadOnlyList<WorkbenchCommandDefinition> Commands { get; }
}

public static class WorkbenchCommandCatalog
{
    public static IReadOnlyList<WorkbenchCommandCatalogGroup> Groups { get; } = new[]
    {
        Group(
            "Runtime",
            Command("set", "runtime", "Store a value in the current local scope.", new[] { "var", "value" }, Array.Empty<string>(),
                """
                var: new_variable
                value: null
                """),
            Command("delay", "runtime", "Wait for a fixed duration.", new[] { "ms" }, Array.Empty<string>(),
                """
                ms: 1000
                """)),
        Group(
            "Control",
            Command("call", "control", "Call a function and optionally map returned values.", new[] { "function" }, new[] { "args", "out" },
                """
                function: function_name
                args: {}
                out: {}
                """),
            Command("for", "control", "Run nested commands for each item in a list.", new[] { "each", "as", "do" }, Array.Empty<string>(),
                """
                each: "${items}"
                as: item
                do:
                  - log.text:
                      text: "loop item"
                """)),
        Group(
            "Assert",
            Command("assert.eq", "assert", "Pass when left and right values are equal.", new[] { "left", "right" }, Array.Empty<string>(),
                """
                left: "${actual}"
                right: expected
                """),
            Command("assert.gt", "assert", "Pass when left is greater than right.", new[] { "left", "right" }, Array.Empty<string>(),
                """
                left: "${actual}"
                right: 0
                """),
            Command("assert.fail", "assert", "Fail the current testcase with a message.", new[] { "message" }, Array.Empty<string>(),
                """
                message: "failure reason"
                """)),
        Group(
            "Log",
            Command("log.text", "logging", "Write text to the execution log and Console tab.", new[] { "text" }, Array.Empty<string>(),
                """
                text: "message"
                """),
            Command("log.value", "logging", "Write a named value to the execution log and Console tab.", new[] { "name", "value" }, Array.Empty<string>(),
                """
                name: value_name
                value: "${value}"
                """)),
        Group(
            "Serial",
            Command("serial.write", "adapter", "Write one text line to a configured serial port.", new[] { "port", "text" }, new[] { "timeout_ms" },
                """
                port: serial_port
                text: "COMMAND"
                timeout_ms: 1000
                """),
            Command("serial.read", "adapter", "Read one text line from a configured serial port.", new[] { "port" }, new[] { "timeout_ms", "until", "match", "save_as" },
                """
                port: serial_port
                timeout_ms: 1000
                save_as: serial_response
                """),
            Command("serial.write_bytes", "adapter", "Write a hexadecimal byte payload to a serial port.", new[] { "port", "payload_hex" }, new[] { "timeout_ms" },
                """
                port: serial_port
                payload_hex: "02 01 00 03"
                timeout_ms: 1000
                """),
            Command("serial.read_bytes", "adapter", "Read a fixed number of bytes from a serial port.", new[] { "port", "count" }, new[] { "timeout_ms", "save_as" },
                """
                port: serial_port
                count: 1
                timeout_ms: 1000
                save_as: serial_bytes
                """)),
        Group(
            "Device",
            Command("sent_usb.read", "device", "Read a Mach Systems SENT-USB fast frame through the serial command profile.", new[] { "device" }, new[] { "channel", "timeout_ms", "until", "save_as", "max_frames" },
                """
                device: sent_usb
                channel: 1
                timeout_ms: 1000
                save_as: sent_frame
                """),
            Command("sent_usb.command", "device", "Control or transmit with a Mach Systems SENT-USB gateway command.", new[] { "device", "action" }, new[]
                {
                    "autostart",
                    "auto_start",
                    "buffer_enabled",
                    "buffer_index",
                    "channel",
                    "crc",
                    "crc_calculated",
                    "crc_mode",
                    "crc_received",
                    "data",
                    "data_nibble_count",
                    "data_nibbles",
                    "direction",
                    "enabled",
                    "enhanced_config_bit",
                    "enhanced_format",
                    "enhanced_serial_format",
                    "frame_type",
                    "index",
                    "message_id",
                    "pulse_pause_enabled",
                    "pulse_pause_frame_period_us",
                    "read_ack",
                    "rx_forward_mode",
                    "save_as",
                    "slow_buffer_index",
                    "slow_channel_mode",
                    "slow_channel_tx_crc_fault",
                    "slow_data",
                    "slow_frame_type",
                    "slow_message_id",
                    "status",
                    "status_nibble",
                    "swap_fast_data_nibbles",
                    "timeout_ms",
                    "tx_echo_mode",
                    "unit_time_us"
                },
                """
                device: sent_usb
                action: start
                channel: 1
                timeout_ms: 1000
                """),
            Command("power_supply.command", "device", "Execute a VUpower K USB power supply command profile action.", new[] { "device" }, new[]
                {
                    "action",
                    "average",
                    "channel",
                    "current",
                    "match",
                    "read",
                    "save_as",
                    "state",
                    "text",
                    "timeout_ms",
                    "until",
                    "value",
                    "voltage"
                },
                """
                device: psu
                action: output
                channel: 1
                state: true
                timeout_ms: 1000
                """)),
        Group(
            "CANoe",
            Command("canoe.measurement.start", "adapter", "Start a CANoe/CANalyzer measurement.", Array.Empty<string>(), new[] { "configuration" },
                """
                configuration: null
                """),
            Command("canoe.measurement.stop", "adapter", "Stop a CANoe/CANalyzer measurement.", Array.Empty<string>(), new[] { "timeout_ms" },
                "{}"),
            Command("canoe.signal.read", "adapter", "Read a CANoe/CANalyzer signal.", new[] { "signal" }, new[] { "bus", "channel", "message", "save_as", "timeout_ms" },
                """
                bus: CAN
                channel: 1
                message: MessageName
                signal: SignalName
                save_as: signal_value
                timeout_ms: 1000
                """),
            Command("canoe.sysvar.read", "adapter", "Read a CANoe/CANalyzer system variable.", new[] { "namespace", "name" }, new[] { "save_as", "timeout_ms" },
                """
                namespace: Namespace
                name: VariableName
                save_as: sysvar_value
                timeout_ms: 1000
                """),
            Command("canoe.sysvar.set", "adapter", "Set a CANoe/CANalyzer system variable.", new[] { "namespace", "name", "value" }, new[] { "timeout_ms" },
                """
                namespace: Namespace
                name: VariableName
                value: null
                timeout_ms: 1000
                """)),
        Group(
            "INCA",
            Command("inca.measure.read", "adapter", "Read an INCA measurement variable.", new[] { "variable" }, new[] { "acquisition_rate", "device", "save_as", "timeout_ms" },
                """
                variable: EngineSpeed
                device: ETKC
                acquisition_rate: 10ms
                save_as: inca_value
                timeout_ms: 1000
                """),
            Command("inca.calibration.set", "adapter", "Set an INCA calibration parameter.", new[] { "parameter", "value" }, new[] { "device", "timeout_ms", "value_kind" },
                """
                parameter: ParameterName
                value: null
                value_kind: phys
                timeout_ms: 1000
                """),
            Command("inca.recording.start", "adapter", "Start INCA recording.", Array.Empty<string>(), new[] { "file_format", "format", "name", "output_dir", "timeout_ms" },
                """
                name: recording_name
                output_dir: C:/reports
                file_format: MDF
                timeout_ms: 1000
                """),
            Command("inca.recording.stop", "adapter", "Stop INCA recording.", Array.Empty<string>(), new[] { "discard", "file_format", "file_name", "format", "name", "timeout_ms" },
                """
                file_name: recording_name
                file_format: MDF4
                timeout_ms: 1000
                """)),
        Group(
            "Trace32",
            Command("trace32.command", "adapter", "Execute a Trace32 command using RCL with optional UDP fallback.", new[] { "command" }, new[] { "timeout_ms", "transport", "fallback", "save_as" },
                """
                command: "PRINT"
                fallback: udp
                timeout_ms: 1000
                """),
            Command("trace32.command_sequence", "adapter", "Execute several Trace32 commands in order using the same transport policy.", new[] { "commands" }, new[] { "timeout_ms", "transport", "fallback", "save_as" },
                """
                commands:
                  - "SYStem.Up"
                  - "Data.List D:0x1000++0x10"
                fallback: udp
                timeout_ms: 1000
                """))
    };

    public static IReadOnlyList<WorkbenchCommandDefinition> AllCommands { get; } =
        Groups.SelectMany(group => group.Commands).ToArray();

    public static WorkbenchCommandDefinition? Find(string commandType)
    {
        return AllCommands.FirstOrDefault(command =>
            command.CommandType.Equals(commandType, StringComparison.Ordinal));
    }

    private static WorkbenchCommandCatalogGroup Group(
        string name,
        params WorkbenchCommandDefinition[] commands)
    {
        return new WorkbenchCommandCatalogGroup(name, commands);
    }

    private static WorkbenchCommandDefinition Command(
        string commandType,
        string category,
        string description,
        IReadOnlyList<string> requiredArgs,
        IReadOnlyList<string> optionalArgs,
        string snippetBody)
    {
        return new WorkbenchCommandDefinition(
            commandType,
            category,
            description,
            requiredArgs,
            optionalArgs,
            snippetBody.TrimEnd());
    }
}
