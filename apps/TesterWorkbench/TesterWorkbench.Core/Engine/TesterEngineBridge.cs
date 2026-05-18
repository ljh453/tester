using System.Text.Json;

namespace TesterWorkbench.Core.Engine;

public sealed class TesterEngineBridge
{
    private static readonly JsonSerializerOptions DetailJsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _pythonExecutable;
    private readonly string _repositoryRoot;
    private readonly IEngineProcessRunner _processRunner;

    public TesterEngineBridge(
        string pythonExecutable,
        string repositoryRoot,
        IEngineProcessRunner processRunner)
    {
        _pythonExecutable = string.IsNullOrWhiteSpace(pythonExecutable)
            ? throw new ArgumentException("Python executable is required.", nameof(pythonExecutable))
            : pythonExecutable;
        _repositoryRoot = string.IsNullOrWhiteSpace(repositoryRoot)
            ? throw new ArgumentException("Repository root is required.", nameof(repositoryRoot))
            : repositoryRoot;
        _processRunner = processRunner;
    }

    public async Task<EngineCompileResult> CompileAsync(
        string yamlFile,
        CancellationToken cancellationToken = default)
    {
        var result = await _processRunner.RunAsync(
            _pythonExecutable,
            new[] { "-m", "embsw_tester.cli", "compile", yamlFile, "--json" },
            _repositoryRoot,
            cancellationToken).ConfigureAwait(false);

        return new EngineCompileResult(
            result.ExitCode,
            ParseDiagnostics(result.StandardOutput),
            result.StandardError,
            result.StandardOutput);
    }

    public async Task<EngineRunResult> RunAsync(
        string yamlFile,
        string runId,
        string reportsRoot,
        CancellationToken cancellationToken = default,
        Action<EngineRunEvent>? onEvent = null,
        string? controlFile = null,
        IReadOnlyCollection<int>? breakpointLines = null)
    {
        var arguments = new List<string>
        {
            "-m",
            "embsw_tester.cli",
            "run",
            yamlFile,
            "--json",
            "--run-id",
            runId,
            "--reports-root",
            reportsRoot,
            "--use-tool-profile-adapters"
        };
        if (onEvent is not null)
        {
            arguments.Add("--events-jsonl");
        }
        if (!string.IsNullOrWhiteSpace(controlFile))
        {
            arguments.Add("--control-file");
            arguments.Add(controlFile);
        }
        foreach (var breakpointLine in (breakpointLines ?? Array.Empty<int>())
            .Where(line => line > 0)
            .Distinct()
            .Order())
        {
            arguments.Add("--breakpoint-line");
            arguments.Add(breakpointLine.ToString());
        }

        var result = await _processRunner.RunAsync(
            _pythonExecutable,
            arguments,
            _repositoryRoot,
            cancellationToken,
            onEvent is null ? null : eventJsonLine => onEvent(ParseRunEventJson(eventJsonLine)))
            .ConfigureAwait(false);

        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        var status = root.TryGetProperty("status", out var statusElement)
            ? statusElement.GetString() ?? "unknown"
            : "unknown";
        string? reportDirectory = null;
        if (root.TryGetProperty("report", out var reportElement)
            && reportElement.TryGetProperty("report_dir", out var reportDirElement))
        {
            reportDirectory = reportDirElement.GetString();
        }

        return new EngineRunResult(
            result.ExitCode,
            status,
            reportDirectory,
            ParseRunEvents(root),
            ParseVariables(root),
            result.StandardError,
            result.StandardOutput);
    }

    public static EngineRunEvent ParseRunEventJson(string eventJsonLine)
    {
        using var document = JsonDocument.Parse(eventJsonLine);
        return ParseRunEvent(document.RootElement, GetString(document.RootElement, "testcase"));
    }

    private static IReadOnlyList<EngineDiagnostic> ParseDiagnostics(string stdout)
    {
        using var document = JsonDocument.Parse(stdout);
        if (!document.RootElement.TryGetProperty("diagnostics", out var diagnosticsElement)
            || diagnosticsElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<EngineDiagnostic>();
        }

        var diagnostics = new List<EngineDiagnostic>();
        foreach (var diagnostic in diagnosticsElement.EnumerateArray())
        {
            diagnostics.Add(new EngineDiagnostic(
                GetString(diagnostic, "severity"),
                GetString(diagnostic, "code"),
                GetString(diagnostic, "message")));
        }

        return diagnostics;
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static IReadOnlyList<EngineRunEvent> ParseRunEvents(JsonElement root)
    {
        if (!root.TryGetProperty("testcase_results", out var testcasesElement)
            || testcasesElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<EngineRunEvent>();
        }

        var events = new List<EngineRunEvent>();
        foreach (var testcase in testcasesElement.EnumerateArray())
        {
            var testcaseName = GetString(testcase, "name");
            if (!testcase.TryGetProperty("events", out var eventsElement)
                || eventsElement.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var runEvent in eventsElement.EnumerateArray())
            {
                events.Add(ParseRunEvent(runEvent, testcaseName));
            }
        }

        return events;
    }

    private static EngineRunEvent ParseRunEvent(JsonElement runEvent, string testcaseName)
    {
        var localVariables = ParseVariableObject(runEvent, "local_variables", testcaseName);
        return new EngineRunEvent(
            GetString(runEvent, "testcase", testcaseName),
            GetString(runEvent, "phase"),
            GetString(runEvent, "command_type"),
            GetString(runEvent, "status"),
            FormatCommandPath(runEvent),
            GetString(runEvent, "source_file"),
            GetInt(runEvent, "source_line"),
            localVariables,
            HasVariableObject(runEvent, "local_variables"),
            GetNullableString(runEvent, "error"),
            FormatEventDetail(runEvent),
            FormatLogText(runEvent),
            FormatJsonDetail(runEvent, "resolved_inputs"),
            FormatJsonDetail(runEvent, "outputs"),
            FormatRawEvidenceRefs(runEvent),
            FormatDurationDetail(runEvent));
    }

    private static IReadOnlyList<EngineVariableValue> ParseVariables(JsonElement root)
    {
        if (!root.TryGetProperty("testcase_results", out var testcasesElement)
            || testcasesElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<EngineVariableValue>();
        }

        var variables = new List<EngineVariableValue>();
        foreach (var testcase in testcasesElement.EnumerateArray())
        {
            var testcaseName = GetString(testcase, "name");
            if (!testcase.TryGetProperty("variables", out var variablesElement)
                || variablesElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            variables.AddRange(ParseVariableObject(testcase, "variables", testcaseName));
        }

        return variables;
    }

    private static IReadOnlyList<EngineVariableValue> ParseVariableObject(
        JsonElement element,
        string propertyName,
        string testcaseName)
    {
        if (!element.TryGetProperty(propertyName, out var variablesElement)
            || variablesElement.ValueKind != JsonValueKind.Object)
        {
            return Array.Empty<EngineVariableValue>();
        }

        var variables = new List<EngineVariableValue>();
        foreach (var variable in variablesElement.EnumerateObject())
        {
            variables.Add(new EngineVariableValue(
                testcaseName,
                variable.Name,
                FormatJsonValue(variable.Value)));
        }

        return variables;
    }

    private static bool HasVariableObject(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var variablesElement)
            && variablesElement.ValueKind == JsonValueKind.Object;
    }

    private static string FormatCommandPath(JsonElement runEvent)
    {
        if (!runEvent.TryGetProperty("command_path", out var pathElement)
            || pathElement.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        return string.Join(
            "/",
            pathElement.EnumerateArray().Select(FormatJsonValue));
    }

    private static string FormatEventDetail(JsonElement runEvent)
    {
        var commandType = GetString(runEvent, "command_type");
        if (!runEvent.TryGetProperty("outputs", out var outputsElement)
            || outputsElement.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        if (commandType == "log.text"
            && outputsElement.TryGetProperty("text", out var textElement))
        {
            return FormatJsonValue(textElement);
        }

        if (commandType == "log.value"
            && outputsElement.TryGetProperty("name", out var nameElement)
            && outputsElement.TryGetProperty("value", out var valueElement))
        {
            return $"{FormatJsonValue(nameElement)} = {FormatJsonValue(valueElement)}";
        }

        return string.Join(
            ", ",
            outputsElement.EnumerateObject()
                .Select(property => $"{property.Name}: {FormatJsonValue(property.Value)}"));
    }

    private static string FormatLogText(JsonElement runEvent)
    {
        if (GetString(runEvent, "status") != "passed")
        {
            return string.Empty;
        }

        var commandType = GetString(runEvent, "command_type");
        if (commandType is not ("log.text" or "log.value"))
        {
            return string.Empty;
        }

        var detail = FormatEventDetail(runEvent);
        if (string.IsNullOrWhiteSpace(detail))
        {
            return string.Empty;
        }

        var lineNumber = GetInt(runEvent, "source_line");
        var lineText = lineNumber > 0 ? $" L{lineNumber}" : string.Empty;
        return $"[{GetString(runEvent, "testcase")}/{GetString(runEvent, "phase")}{lineText}] {detail}";
    }

    private static string FormatJsonDetail(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property)
            || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return string.Empty;
        }

        return JsonSerializer.Serialize(property, DetailJsonOptions);
    }

    private static string FormatRawEvidenceRefs(JsonElement runEvent)
    {
        if (!runEvent.TryGetProperty("outputs", out var outputsElement))
        {
            return string.Empty;
        }

        var evidenceRefs = new List<string>();
        CollectStringPropertyValues(outputsElement, "raw_evidence_ref", evidenceRefs);
        return string.Join(Environment.NewLine, evidenceRefs.Distinct());
    }

    private static void CollectStringPropertyValues(
        JsonElement element,
        string propertyName,
        List<string> values)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals(propertyName))
                {
                    var value = FormatJsonValue(property.Value);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        values.Add(value);
                    }
                }

                CollectStringPropertyValues(property.Value, propertyName, values);
            }
            return;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                CollectStringPropertyValues(item, propertyName, values);
            }
        }
    }

    private static string FormatDurationDetail(JsonElement runEvent)
    {
        if (!runEvent.TryGetProperty("outputs", out var outputsElement))
        {
            return string.Empty;
        }

        var durations = new List<string>();
        CollectDurationValues(outputsElement, string.Empty, durations);
        return string.Join(Environment.NewLine, durations);
    }

    private static void CollectDurationValues(
        JsonElement element,
        string inheritedLabel,
        List<string> durations)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var label = inheritedLabel;
            if (element.TryGetProperty("command_type", out var commandTypeElement))
            {
                label = FormatJsonValue(commandTypeElement);
            }

            if (element.TryGetProperty("duration_ms", out var durationElement)
                && TryGetDurationMs(durationElement, out var durationMs))
            {
                var prefix = string.IsNullOrWhiteSpace(label) ? string.Empty : $"{label}: ";
                durations.Add($"{prefix}{durationMs} ms");
            }

            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals("duration_ms"))
                {
                    continue;
                }

                CollectDurationValues(property.Value, label, durations);
            }
            return;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                CollectDurationValues(item, inheritedLabel, durations);
            }
        }
    }

    private static bool TryGetDurationMs(JsonElement element, out int durationMs)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out durationMs))
        {
            return true;
        }

        return int.TryParse(FormatJsonValue(element), out durationMs);
    }

    private static string GetString(JsonElement element, string propertyName, string fallback)
    {
        var value = GetNullableString(element, propertyName);
        return string.IsNullOrEmpty(value) ? fallback : value;
    }

    private static string GetNullableString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property)
            || property.ValueKind == JsonValueKind.Null
            || property.ValueKind == JsonValueKind.Undefined)
        {
            return string.Empty;
        }

        return FormatJsonValue(property);
    }

    private static int GetInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property)
            || property.ValueKind == JsonValueKind.Null
            || property.ValueKind == JsonValueKind.Undefined)
        {
            return 0;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value))
        {
            return value;
        }

        return int.TryParse(FormatJsonValue(property), out var parsed) ? parsed : 0;
    }

    private static string FormatJsonValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Null => string.Empty,
            JsonValueKind.Undefined => string.Empty,
            _ => value.GetRawText()
        };
    }
}
