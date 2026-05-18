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
        string name,
        WorkbenchCommandArgumentKind kind,
        bool isRequired,
        WorkbenchCommandAutocompleteKind autocompleteKind,
        IReadOnlyList<string> suggestions)
    {
        Name = name;
        Kind = kind;
        IsRequired = isRequired;
        AutocompleteKind = autocompleteKind;
        Suggestions = suggestions;
    }

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
            or "data")
        {
            return WorkbenchCommandArgumentKind.Number;
        }

        if (commandType == "power_supply.command" && name == "value")
        {
            return WorkbenchCommandArgumentKind.Number;
        }

        if (name is "state" or "autostart" or "auto_start" or "crc"
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
            return new[] { "output", "set_voltage", "set_current", "measure", "query" };
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
            Command("sent_usb.command", "device", "Control or transmit with a Mach Systems SENT-USB gateway command.", new[] { "device", "action" }, new[] { "channel", "data_nibbles", "slow_message_id", "data", "buffer_index", "enabled", "enhanced_format", "timeout_ms", "save_as" },
                """
                device: sent_usb
                action: start
                channel: 1
                timeout_ms: 1000
                """),
            Command("power_supply.command", "device", "Execute a VUpower K USB power supply command profile action.", new[] { "device" }, new[] { "action", "channel", "voltage", "current", "state", "save_as", "timeout_ms" },
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
