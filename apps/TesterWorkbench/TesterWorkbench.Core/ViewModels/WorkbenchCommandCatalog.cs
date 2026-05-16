namespace TesterWorkbench.Core.ViewModels;

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
        SnippetBody = snippetBody;
    }

    public string CommandType { get; }

    public string Category { get; }

    public string Description { get; }

    public IReadOnlyList<string> RequiredArgs { get; }

    public IReadOnlyList<string> OptionalArgs { get; }

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
            Command("sent_usb.command", "device", "Control or transmit with a Mach Systems SENT-USB gateway command.", new[] { "device", "action" }, new[] { "channel", "data_nibbles", "slow_data", "timeout_ms", "save_as" },
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
            Command("canoe.measurement.stop", "adapter", "Stop a CANoe/CANalyzer measurement.", Array.Empty<string>(), Array.Empty<string>(),
                "{}"),
            Command("canoe.signal.read", "adapter", "Read a CANoe/CANalyzer signal.", new[] { "signal" }, new[] { "bus", "channel", "save_as" },
                """
                signal: SignalName
                save_as: signal_value
                """),
            Command("canoe.sysvar.read", "adapter", "Read a CANoe/CANalyzer system variable.", new[] { "namespace", "name" }, new[] { "save_as" },
                """
                namespace: Namespace
                name: VariableName
                save_as: sysvar_value
                """),
            Command("canoe.sysvar.set", "adapter", "Set a CANoe/CANalyzer system variable.", new[] { "namespace", "name", "value" }, Array.Empty<string>(),
                """
                namespace: Namespace
                name: VariableName
                value: null
                """)),
        Group(
            "INCA",
            Command("inca.measure.read", "adapter", "Read an INCA measurement variable.", new[] { "variable" }, new[] { "save_as", "timeout_ms" },
                """
                variable: EngineSpeed
                save_as: inca_value
                timeout_ms: 1000
                """),
            Command("inca.calibration.set", "adapter", "Set an INCA calibration parameter.", new[] { "parameter", "value" }, new[] { "timeout_ms" },
                """
                parameter: ParameterName
                value: null
                timeout_ms: 1000
                """),
            Command("inca.recording.start", "adapter", "Start INCA recording.", Array.Empty<string>(), new[] { "name", "output_dir", "timeout_ms" },
                """
                name: recording_name
                timeout_ms: 1000
                """),
            Command("inca.recording.stop", "adapter", "Stop INCA recording.", Array.Empty<string>(), new[] { "timeout_ms" },
                """
                timeout_ms: 1000
                """)),
        Group(
            "Trace32",
            Command("trace32.command", "adapter", "Execute a Trace32 command using RCL with optional UDP fallback.", new[] { "command" }, new[] { "timeout_ms", "transport", "fallback", "save_as" },
                """
                command: "PRINT"
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
