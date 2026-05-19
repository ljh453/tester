# Phase 31 A-Workbench UI/UX Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Apply the A안 Stabilized IDE Shell direction from `docs/mockups/workbench-ui-ux-comparison.html` to the WPF workbench so editing, execution, trace, and selection state read as one coherent IDE.

**Architecture:** Keep the current WPF layout and view model as the base. Add a thin UI state layer for context/status display and selection provenance, then refine existing panes without introducing a new docking framework yet. Treat future Eclipse-style docking as a layout boundary concern by naming panes consistently and keeping pane-specific UI logic isolated in `MainWindow.xaml.cs`.

**Tech Stack:** .NET 8 WPF, `TesterWorkbench.Core` view models, existing XAML theme resources, existing Python `pytest` UI structure tests, existing C# console-style workbench tests.

---

## File Structure

- Modify `apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml`
  - Add IDE context strip below the toolbar.
  - Group toolbar controls by intent.
  - Add pane title/status affordances for Project/Test Explorer, YAML Editor, GUI Editor, Properties, and bottom inspector.
  - Refine Execution Trace inspector controls.

- Modify `apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml.cs`
  - Add `RefreshWorkbenchContextStrip()`.
  - Route YAML, GUI, and Trace selection through one active-selection path.
  - Keep trace follow/pin behavior separate from active editor selection.
  - Keep new UI refresh helpers small and pane-specific.

- Modify `apps/TesterWorkbench/TesterWorkbench.Core/ViewModels/MainWorkbenchViewModel.cs`
  - Add selection origin state.
  - Add context-strip display properties.
  - Ensure Project/Test Explorer, GUI block editor, YAML editor, and Trace selection do not all look active at the same time.

- Modify `apps/TesterWorkbench/TesterWorkbench/Themes/BaseWorkbenchStyles.xaml`
  - Add reusable styles for context strip, status chips, pane headers, and inspector buttons.

- Modify `apps/TesterWorkbench/TesterWorkbench/Themes/DarkTheme.xaml`
  - Add dark token colors for context strip and status chips.

- Modify `apps/TesterWorkbench/TesterWorkbench/Themes/LightTheme.xaml`
  - Add light token colors for context strip and status chips.

- Modify `tests/test_workbench_xaml.py`
  - Add structural tests for A-shell UI: context strip, grouped toolbar, pane headers, trace inspector controls, and reusable styles.

- Modify `apps/TesterWorkbench/TesterWorkbench.Tests/Program.cs`
  - Add view-model tests for context text and active selection origin.

- Optional doc update: `docs/mockups/workbench-ui-ux-comparison.html`
  - Mark A안 as selected and add a short implementation note after the first implementation pass.

---

### Task 1: Add IDE Context Strip

**Files:**
- Modify: `tests/test_workbench_xaml.py`
- Modify: `apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml`
- Modify: `apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml.cs`
- Modify: `apps/TesterWorkbench/TesterWorkbench.Core/ViewModels/MainWorkbenchViewModel.cs`

- [ ] **Step 1: Write failing XAML test for the context strip**

Add this test to `tests/test_workbench_xaml.py`:

```python
def test_workbench_has_a_shell_context_strip():
    root = _load("apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml")

    strip = next(
        (
            element
            for element in root.iter(f"{PRESENTATION}Border")
            if element.attrib.get(f"{XAML}Name") == "WorkbenchContextStrip"
        ),
        None,
    )
    text_names = {
        element.attrib.get(f"{XAML}Name")
        for element in root.iter(f"{PRESENTATION}TextBlock")
    }

    assert strip is not None
    assert {
        "ContextSelectedYamlText",
        "ContextRunTargetText",
        "ContextSaveStateText",
        "ContextRunStateText",
        "ContextCurrentLineText",
    } <= text_names
```

- [ ] **Step 2: Run the failing test**

Run:

```bash
uv run --extra dev pytest -q tests/test_workbench_xaml.py::test_workbench_has_a_shell_context_strip
```

Expected: FAIL because `WorkbenchContextStrip` does not exist yet.

- [ ] **Step 3: Add context display properties to the view model**

In `apps/TesterWorkbench/TesterWorkbench.Core/ViewModels/MainWorkbenchViewModel.cs`, add these properties near `SelectedFileDisplayText` and `SelectedGuiTestcaseRunText`:

```csharp
public string ContextSelectedYamlText => string.IsNullOrWhiteSpace(SelectedFilePath)
    ? "Current YAML: none"
    : $"Current YAML: {Path.GetFileName(SelectedFilePath)}";

public string ContextRunTargetText => SelectedGuiTestcaseRunText;

public string ContextSaveStateText => IsDirty ? "Unsaved" : SaveStatusText;

public string ContextRunStateText => $"Run: {RunStatus}";

public string ContextCurrentLineText => CurrentLineNumber > 0
    ? CurrentLocationText
    : "Line: none";
```

- [ ] **Step 4: Add the context strip XAML**

In `apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml`, insert this `Border` after the `ToolBar` and before `WorkbenchRootGrid`:

```xml
<Border x:Name="WorkbenchContextStrip"
        DockPanel.Dock="Top"
        Style="{StaticResource WorkbenchContextStripStyle}">
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="*" />
            <ColumnDefinition Width="Auto" />
            <ColumnDefinition Width="Auto" />
            <ColumnDefinition Width="Auto" />
            <ColumnDefinition Width="Auto" />
        </Grid.ColumnDefinitions>
        <TextBlock x:Name="ContextSelectedYamlText"
                   TextTrimming="CharacterEllipsis"
                   ToolTip="Currently opened YAML file." />
        <TextBlock x:Name="ContextRunTargetText"
                   Grid.Column="1"
                   Style="{StaticResource WorkbenchStatusChipTextStyle}"
                   ToolTip="Testcases selected for the next run." />
        <TextBlock x:Name="ContextSaveStateText"
                   Grid.Column="2"
                   Style="{StaticResource WorkbenchStatusChipTextStyle}"
                   ToolTip="Saved or unsaved editor state." />
        <TextBlock x:Name="ContextRunStateText"
                   Grid.Column="3"
                   Style="{StaticResource WorkbenchStatusChipTextStyle}"
                   ToolTip="Current runtime state." />
        <TextBlock x:Name="ContextCurrentLineText"
                   Grid.Column="4"
                   Style="{StaticResource WorkbenchStatusChipTextStyle}"
                   ToolTip="Current editor, GUI, or execution line." />
    </Grid>
</Border>
```

- [ ] **Step 5: Add temporary simple styles if Task 6 has not run**

If `WorkbenchContextStripStyle` and `WorkbenchStatusChipTextStyle` do not exist yet, add minimal styles to `MainWindow.Resources` so this task compiles:

```xml
<Style x:Key="WorkbenchContextStripStyle" TargetType="{x:Type Border}">
    <Setter Property="Padding" Value="8,4" />
    <Setter Property="BorderBrush" Value="{DynamicResource Workbench.Brush.Border}" />
    <Setter Property="BorderThickness" Value="0,0,0,1" />
    <Setter Property="Background" Value="{DynamicResource Workbench.Brush.PanelBackgroundAlt}" />
</Style>

<Style x:Key="WorkbenchStatusChipTextStyle" TargetType="{x:Type TextBlock}">
    <Setter Property="Margin" Value="8,0,0,0" />
    <Setter Property="Padding" Value="8,2" />
    <Setter Property="Foreground" Value="{DynamicResource Workbench.Brush.Text}" />
</Style>
```

- [ ] **Step 6: Refresh the context strip from code-behind**

In `apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml.cs`, add:

```csharp
private void RefreshWorkbenchContextStrip()
{
    ContextSelectedYamlText.Text = _viewModel.ContextSelectedYamlText;
    ContextRunTargetText.Text = _viewModel.ContextRunTargetText;
    ContextSaveStateText.Text = _viewModel.ContextSaveStateText;
    ContextRunStateText.Text = _viewModel.ContextRunStateText;
    ContextCurrentLineText.Text = _viewModel.ContextCurrentLineText;
}
```

Call `RefreshWorkbenchContextStrip();` at the end of:

```csharp
RefreshView()
RefreshRuntimeViews(bool selectLatestTrace = true)
RefreshGuiRunSelectionText()
ExecutionTraceGrid_SelectionChanged(...)
EditorBox_SelectionChanged(...)
GuiBlockTree_SelectedItemChanged(...)
```

- [ ] **Step 7: Verify and commit**

Run:

```bash
uv run --extra dev pytest -q tests/test_workbench_xaml.py::test_workbench_has_a_shell_context_strip
dotnet build apps/TesterWorkbench/TesterWorkbench/TesterWorkbench.csproj
git add tests/test_workbench_xaml.py apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml.cs apps/TesterWorkbench/TesterWorkbench.Core/ViewModels/MainWorkbenchViewModel.cs
git commit -m "feat: add workbench context strip"
```

Expected: pytest PASS, build PASS.

---

### Task 2: Group Toolbar Commands By Intent

**Files:**
- Modify: `tests/test_workbench_xaml.py`
- Modify: `apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml`

- [ ] **Step 1: Write failing XAML test for toolbar groups**

Add:

```python
def test_toolbar_commands_are_grouped_by_intent():
    root = _load("apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml")

    group_names = {
        element.attrib.get(f"{XAML}Name")
        for element in root.iter(f"{PRESENTATION}StackPanel")
    }

    assert {
        "FileToolbarGroup",
        "BuildToolbarGroup",
        "RunToolbarGroup",
        "ViewToolbarGroup",
    } <= group_names
```

- [ ] **Step 2: Run the failing test**

Run:

```bash
uv run --extra dev pytest -q tests/test_workbench_xaml.py::test_toolbar_commands_are_grouped_by_intent
```

Expected: FAIL because toolbar groups are not named.

- [ ] **Step 3: Update toolbar XAML**

In `MainWindow.xaml`, replace the flat toolbar button sequence with grouped `StackPanel`s:

```xml
<StackPanel x:Name="FileToolbarGroup" Orientation="Horizontal">
    <Button Content="💾"
            Click="Save_Click"
            Style="{StaticResource IdeToolbarButtonStyle}"
            ToolTip="Save YAML file" />
</StackPanel>
<Separator />
<StackPanel x:Name="BuildToolbarGroup" Orientation="Horizontal">
    <Button Content="✓"
            Click="Compile_Click"
            Style="{StaticResource IdeToolbarButtonStyle}"
            ToolTip="Compile current YAML" />
</StackPanel>
<Separator />
<StackPanel x:Name="RunToolbarGroup" Orientation="Horizontal">
    <Button Content="▶"
            Click="Run_Click"
            Style="{StaticResource IdeToolbarButtonStyle}"
            ToolTip="Run selected YAML or selected GUI testcases" />
    <Button Content="⏸"
            Click="Pause_Click"
            Style="{StaticResource IdeToolbarButtonStyle}"
            ToolTip="Pause at next command boundary" />
    <Button Content="▌▶"
            Click="Resume_Click"
            Style="{StaticResource IdeToolbarButtonStyle}"
            ToolTip="Resume paused run" />
    <Button Content="■"
            Click="Stop_Click"
            Style="{StaticResource IdeToolbarButtonStyle}"
            ToolTip="Stop run safely" />
    <Button Content="●"
            Click="ToggleBreakpoint_Click"
            Style="{StaticResource IdeToolbarButtonStyle}"
            ToolTip="Toggle breakpoint at caret or selected command line" />
</StackPanel>
<Separator />
<StackPanel x:Name="ViewToolbarGroup" Orientation="Horizontal">
    <CheckBox x:Name="AutoFocusLineCheckBox"
              Content="Auto Focus Line"
              Checked="AutoFocusLine_Changed"
              Unchecked="AutoFocusLine_Changed"
              VerticalAlignment="Center" />
    <ToggleButton x:Name="GuiEditorToggleButton"
                  Content="◨"
                  IsChecked="True"
                  Click="GuiEditorToggle_Click"
                  Style="{StaticResource IdeToggleButtonStyle}"
                  ToolTip="Show or hide GUI Editor"
                  VerticalAlignment="Center" />
</StackPanel>
```

Keep `WorkspacePathBox` before these groups for now.

- [ ] **Step 4: Verify and commit**

Run:

```bash
uv run --extra dev pytest -q tests/test_workbench_xaml.py::test_toolbar_commands_are_grouped_by_intent
dotnet build apps/TesterWorkbench/TesterWorkbench/TesterWorkbench.csproj
git add tests/test_workbench_xaml.py apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml
git commit -m "refactor: group workbench toolbar commands"
```

Expected: pytest PASS, build PASS.

---

### Task 3: Add Active Selection Origin

**Files:**
- Modify: `apps/TesterWorkbench/TesterWorkbench.Tests/Program.cs`
- Modify: `apps/TesterWorkbench/TesterWorkbench.Core/ViewModels/MainWorkbenchViewModel.cs`
- Modify: `apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml.cs`

- [ ] **Step 1: Write failing C# view-model test**

In `Program.cs`, add a test method and invoke it from the top-level test list:

```csharp
static Task RunWorkbenchTracksActiveSelectionOriginTest()
{
    var viewModel = CreateWorkbenchViewModel();
    viewModel.SetEditorText(
        """
        testcases:
          - name: origin_case
            steps:
              - log.text:
                  text: hello
        """);

    AssertTrue(viewModel.SelectGuiCommandAtLine(4), "select gui command at line");
    AssertEqual(
        WorkbenchSelectionOrigin.GuiEditor,
        viewModel.ActiveSelectionOrigin,
        "GUI selection origin");

    viewModel.SelectExecutionTraceEvent(new EngineRunEvent(
        "origin_case",
        "steps",
        "log.text",
        "passed",
        "testcases[0].steps[0]",
        "origin.yaml",
        4,
        Array.Empty<EngineVariableValue>(),
        false,
        "",
        "hello",
        "hello",
        "{}",
        "{}",
        "",
        "log.text: 1 ms"));

    AssertEqual(
        WorkbenchSelectionOrigin.ExecutionTrace,
        viewModel.ActiveSelectionOrigin,
        "Trace selection origin");

    return Task.CompletedTask;
}
```

- [ ] **Step 2: Run the failing test**

Run:

```bash
DOTNET_ROLL_FORWARD=Major dotnet run --project apps/TesterWorkbench/TesterWorkbench.Tests/TesterWorkbench.Tests.csproj
```

Expected: FAIL because `WorkbenchSelectionOrigin` and `ActiveSelectionOrigin` do not exist.

- [ ] **Step 3: Add selection origin enum and state**

In `MainWorkbenchViewModel.cs`, add near `WorkbenchProjectExplorerNodeRole`:

```csharp
public enum WorkbenchSelectionOrigin
{
    None,
    YamlEditor,
    GuiEditor,
    ExecutionTrace
}
```

Add property:

```csharp
public WorkbenchSelectionOrigin ActiveSelectionOrigin { get; private set; } = WorkbenchSelectionOrigin.None;
```

- [ ] **Step 4: Route selection methods through origin**

Update methods:

```csharp
public void SelectExecutionTraceEvent(
    EngineRunEvent? runEvent,
    bool makeActiveSelection = true)
{
    if (makeActiveSelection)
    {
        ActiveSelectionOrigin = runEvent is null
            ? WorkbenchSelectionOrigin.None
            : WorkbenchSelectionOrigin.ExecutionTrace;
    }

    // keep existing body after this point
}

public void SelectGuiCommand(
    WorkbenchCommandBlock? commandBlock,
    WorkbenchSelectionOrigin origin = WorkbenchSelectionOrigin.GuiEditor)
{
    ActiveSelectionOrigin = commandBlock is null
        ? WorkbenchSelectionOrigin.None
        : origin;

    // keep existing body after this point
}
```

In `SelectGuiCommandAtLine`, call:

```csharp
SelectGuiCommandForBulkAction(commandBlock, replaceSelection: true);
ActiveSelectionOrigin = WorkbenchSelectionOrigin.YamlEditor;
```

- [ ] **Step 5: Avoid trace streaming stealing active selection**

In `MainWindow.xaml.cs`, when automatic follow-latest selects a trace row, call:

```csharp
_viewModel.SelectExecutionTraceEvent(runEvent, makeActiveSelection: _isTraceFollowLatestEnabled);
```

If the user manually clicks a trace row, keep `makeActiveSelection: true`.

- [ ] **Step 6: Verify and commit**

Run:

```bash
DOTNET_ROLL_FORWARD=Major dotnet run --project apps/TesterWorkbench/TesterWorkbench.Tests/TesterWorkbench.Tests.csproj
dotnet build apps/TesterWorkbench/TesterWorkbench/TesterWorkbench.csproj
git add apps/TesterWorkbench/TesterWorkbench.Tests/Program.cs apps/TesterWorkbench/TesterWorkbench.Core/ViewModels/MainWorkbenchViewModel.cs apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml.cs
git commit -m "feat: track active workbench selection origin"
```

Expected: C# tests PASS, build PASS.

---

### Task 4: Make Active And Linked Highlights Visually Distinct

**Files:**
- Modify: `tests/test_workbench_xaml.py`
- Modify: `apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml`
- Modify: `apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml.cs`
- Modify: `apps/TesterWorkbench/TesterWorkbench.Core/ViewModels/WorkbenchGuiModel.cs`

- [ ] **Step 1: Write failing XAML test for active/linked styles**

Add:

```python
def test_gui_blocks_have_distinct_active_and_linked_visual_states():
    root = _load("apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml")

    data_triggers = [
        trigger.attrib.get("Binding")
        for trigger in root.iter(f"{PRESENTATION}DataTrigger")
    ]

    assert "{Binding IsActiveSelection}" in data_triggers
    assert "{Binding IsLinkedSelection}" in data_triggers
    assert "{Binding IsCurrentExecutionBlock}" in data_triggers
```

- [ ] **Step 2: Run the failing test**

Run:

```bash
uv run --extra dev pytest -q tests/test_workbench_xaml.py::test_gui_blocks_have_distinct_active_and_linked_visual_states
```

Expected: FAIL until the new visual state bindings exist.

- [ ] **Step 3: Add block visual state properties**

In `WorkbenchGuiModel.cs`, add properties to `WorkbenchCommandBlock`:

```csharp
public bool IsActiveSelection { get; set; }

public bool IsLinkedSelection { get; set; }
```

Keep `IsCurrentExecutionBlock` for runtime execution line only.

- [ ] **Step 4: Update view-model selection marking**

In `MainWorkbenchViewModel.cs`, add a helper:

```csharp
private void RefreshGuiSelectionVisualState()
{
    foreach (var commandBlock in AllGuiCommandBlocks())
    {
        commandBlock.IsActiveSelection = commandBlock == SelectedGuiCommand
            && ActiveSelectionOrigin is WorkbenchSelectionOrigin.GuiEditor or WorkbenchSelectionOrigin.YamlEditor;
        commandBlock.IsLinkedSelection = commandBlock == SelectedGuiCommand
            && ActiveSelectionOrigin == WorkbenchSelectionOrigin.ExecutionTrace;
    }
}
```

Call it after:

```csharp
SelectExecutionTraceEvent(...)
SelectGuiCommand(...)
SelectGuiCommandAtLine(...)
SelectGuiCommandsForBulkAction(...)
DeleteSelectedGuiCommands()
MoveSelectedGuiCommands(...)
```

- [ ] **Step 5: Add XAML visual triggers**

In the GUI command block `Border.Style`, add these triggers:

```xml
<DataTrigger Binding="{Binding IsActiveSelection}" Value="True">
    <Setter Property="BorderBrush" Value="{DynamicResource Workbench.Brush.Accent}" />
    <Setter Property="BorderThickness" Value="2" />
</DataTrigger>
<DataTrigger Binding="{Binding IsLinkedSelection}" Value="True">
    <Setter Property="BorderBrush" Value="{DynamicResource Workbench.Brush.LinkedHighlight}" />
    <Setter Property="BorderThickness" Value="1" />
    <Setter Property="Opacity" Value="0.96" />
</DataTrigger>
<DataTrigger Binding="{Binding IsCurrentExecutionBlock}" Value="True">
    <Setter Property="Background" Value="{DynamicResource Workbench.Brush.ExecutionCurrentBackground}" />
</DataTrigger>
```

- [ ] **Step 6: Verify and commit**

Run:

```bash
uv run --extra dev pytest -q tests/test_workbench_xaml.py::test_gui_blocks_have_distinct_active_and_linked_visual_states
DOTNET_ROLL_FORWARD=Major dotnet run --project apps/TesterWorkbench/TesterWorkbench.Tests/TesterWorkbench.Tests.csproj
dotnet build apps/TesterWorkbench/TesterWorkbench/TesterWorkbench.csproj
git add tests/test_workbench_xaml.py apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml.cs apps/TesterWorkbench/TesterWorkbench.Core/ViewModels/MainWorkbenchViewModel.cs apps/TesterWorkbench/TesterWorkbench.Core/ViewModels/WorkbenchGuiModel.cs
git commit -m "feat: distinguish active and linked gui selection"
```

Expected: pytest PASS, C# tests PASS, build PASS.

---

### Task 5: Refine Execution Trace Into A Trace Inspector

**Files:**
- Modify: `tests/test_workbench_xaml.py`
- Modify: `apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml`
- Modify: `apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml.cs`

- [ ] **Step 1: Write failing XAML test for Trace Inspector controls**

Add:

```python
def test_execution_trace_has_inspector_controls_for_follow_and_pin():
    root = _load("apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml")

    names = {
        element.attrib.get(f"{XAML}Name")
        for element in root.iter()
    }

    assert "TraceFollowLatestCheckBox" in names
    assert "TracePinSelectedButton" in names
    assert "TraceSelectedEventText" in names
```

- [ ] **Step 2: Run the failing test**

Run:

```bash
uv run --extra dev pytest -q tests/test_workbench_xaml.py::test_execution_trace_has_inspector_controls_for_follow_and_pin
```

Expected: FAIL because `TracePinSelectedButton` and `TraceSelectedEventText` do not exist.

- [ ] **Step 3: Add inspector header controls**

In `MainWindow.xaml`, expand the existing Execution Trace header:

```xml
<Border Grid.Row="0"
        Padding="6,3"
        BorderBrush="{DynamicResource Workbench.Brush.Border}"
        BorderThickness="0,0,0,1"
        Background="{DynamicResource Workbench.Brush.PanelBackgroundAlt}">
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="Auto" />
            <ColumnDefinition Width="Auto" />
            <ColumnDefinition Width="*" />
        </Grid.ColumnDefinitions>
        <CheckBox x:Name="TraceFollowLatestCheckBox"
                  Content="Follow latest"
                  IsChecked="True"
                  Click="TraceFollowLatestCheckBox_Click"
                  ToolTip="Follow the latest streamed event while the run is active." />
        <Button x:Name="TracePinSelectedButton"
                Grid.Column="1"
                Content="Pin selected"
                Margin="8,0,0,0"
                Click="TracePinSelectedButton_Click"
                Style="{StaticResource IdeToolbarButtonStyle}"
                ToolTip="Keep the selected event visible while new events continue to stream." />
        <TextBlock x:Name="TraceSelectedEventText"
                   Grid.Column="2"
                   Margin="10,0,0,0"
                   VerticalAlignment="Center"
                   TextTrimming="CharacterEllipsis" />
    </Grid>
</Border>
```

- [ ] **Step 4: Add click handler and selected event refresh**

In `MainWindow.xaml.cs`, add:

```csharp
private void TracePinSelectedButton_Click(object sender, RoutedEventArgs e)
{
    _isTraceFollowLatestEnabled = false;
    TraceFollowLatestCheckBox.IsChecked = false;
    _selectedTraceEvent = ExecutionTraceGrid.SelectedItem as EngineRunEvent ?? _selectedTraceEvent;
    RefreshSelectedTraceDetails();
}

private void RefreshTraceSelectedEventText()
{
    var selectedEvent = ExecutionTraceGrid.SelectedItem as EngineRunEvent ?? _selectedTraceEvent;
    TraceSelectedEventText.Text = selectedEvent is null
        ? "No trace event selected"
        : $"{selectedEvent.Testcase} / {selectedEvent.Phase} / {selectedEvent.CommandType} / line {selectedEvent.SourceLine}";
}
```

Call `RefreshTraceSelectedEventText();` from `RefreshSelectedTraceDetails()`.

- [ ] **Step 5: Verify and commit**

Run:

```bash
uv run --extra dev pytest -q tests/test_workbench_xaml.py::test_execution_trace_has_inspector_controls_for_follow_and_pin
dotnet build apps/TesterWorkbench/TesterWorkbench/TesterWorkbench.csproj
git add tests/test_workbench_xaml.py apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml.cs
git commit -m "feat: refine execution trace inspector"
```

Expected: pytest PASS, build PASS.

---

### Task 6: Promote A-Shell Styles Into Theme Resources

**Files:**
- Modify: `tests/test_workbench_xaml.py`
- Modify: `apps/TesterWorkbench/TesterWorkbench/Themes/BaseWorkbenchStyles.xaml`
- Modify: `apps/TesterWorkbench/TesterWorkbench/Themes/DarkTheme.xaml`
- Modify: `apps/TesterWorkbench/TesterWorkbench/Themes/LightTheme.xaml`
- Modify: `apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml`

- [ ] **Step 1: Write failing style test**

Add:

```python
def test_a_shell_uses_shared_theme_styles():
    base = _load("apps/TesterWorkbench/TesterWorkbench/Themes/BaseWorkbenchStyles.xaml")
    dark = _load("apps/TesterWorkbench/TesterWorkbench/Themes/DarkTheme.xaml")
    light = _load("apps/TesterWorkbench/TesterWorkbench/Themes/LightTheme.xaml")

    style_keys = {
        element.attrib.get(f"{XAML}Key")
        for element in base.iter(f"{PRESENTATION}Style")
    }
    dark_brushes = {
        element.attrib.get(f"{XAML}Key")
        for element in dark.iter(f"{PRESENTATION}SolidColorBrush")
    }
    light_brushes = {
        element.attrib.get(f"{XAML}Key")
        for element in light.iter(f"{PRESENTATION}SolidColorBrush")
    }

    assert "WorkbenchContextStripStyle" in style_keys
    assert "WorkbenchStatusChipTextStyle" in style_keys
    assert "WorkbenchPaneHeaderStyle" in style_keys
    assert "Workbench.Brush.LinkedHighlight" in dark_brushes
    assert "Workbench.Brush.LinkedHighlight" in light_brushes
```

- [ ] **Step 2: Run the failing test**

Run:

```bash
uv run --extra dev pytest -q tests/test_workbench_xaml.py::test_a_shell_uses_shared_theme_styles
```

Expected: FAIL until shared styles and brushes exist.

- [ ] **Step 3: Add shared styles**

In `BaseWorkbenchStyles.xaml`, add:

```xml
<Style x:Key="WorkbenchContextStripStyle" TargetType="{x:Type Border}">
    <Setter Property="Padding" Value="8,4" />
    <Setter Property="BorderBrush" Value="{DynamicResource Workbench.Brush.Border}" />
    <Setter Property="BorderThickness" Value="0,0,0,1" />
    <Setter Property="Background" Value="{DynamicResource Workbench.Brush.ContextStripBackground}" />
</Style>

<Style x:Key="WorkbenchStatusChipTextStyle" TargetType="{x:Type TextBlock}">
    <Setter Property="Margin" Value="8,0,0,0" />
    <Setter Property="Padding" Value="8,2" />
    <Setter Property="Foreground" Value="{DynamicResource Workbench.Brush.Text}" />
    <Setter Property="Background" Value="{DynamicResource Workbench.Brush.StatusChipBackground}" />
</Style>

<Style x:Key="WorkbenchPaneHeaderStyle" TargetType="{x:Type Border}">
    <Setter Property="Padding" Value="8,5" />
    <Setter Property="BorderBrush" Value="{DynamicResource Workbench.Brush.Border}" />
    <Setter Property="BorderThickness" Value="0,0,0,1" />
    <Setter Property="Background" Value="{DynamicResource Workbench.Brush.PanelBackgroundAlt}" />
</Style>
```

- [ ] **Step 4: Add dark and light brushes**

In `DarkTheme.xaml`:

```xml
<SolidColorBrush x:Key="Workbench.Brush.ContextStripBackground" Color="#131820" />
<SolidColorBrush x:Key="Workbench.Brush.StatusChipBackground" Color="#1D2530" />
<SolidColorBrush x:Key="Workbench.Brush.LinkedHighlight" Color="#B8A0FF" />
<SolidColorBrush x:Key="Workbench.Brush.ExecutionCurrentBackground" Color="#263B52" />
```

In `LightTheme.xaml`:

```xml
<SolidColorBrush x:Key="Workbench.Brush.ContextStripBackground" Color="#F3F6FA" />
<SolidColorBrush x:Key="Workbench.Brush.StatusChipBackground" Color="#E8EEF6" />
<SolidColorBrush x:Key="Workbench.Brush.LinkedHighlight" Color="#7C5FD6" />
<SolidColorBrush x:Key="Workbench.Brush.ExecutionCurrentBackground" Color="#DCEBFF" />
```

- [ ] **Step 5: Remove temporary local styles**

If Task 1 added local copies of `WorkbenchContextStripStyle` or `WorkbenchStatusChipTextStyle` to `MainWindow.Resources`, delete those local copies so the app uses theme resources.

- [ ] **Step 6: Verify and commit**

Run:

```bash
uv run --extra dev pytest -q tests/test_workbench_xaml.py::test_a_shell_uses_shared_theme_styles
dotnet build apps/TesterWorkbench/TesterWorkbench/TesterWorkbench.csproj
git add tests/test_workbench_xaml.py apps/TesterWorkbench/TesterWorkbench/Themes/BaseWorkbenchStyles.xaml apps/TesterWorkbench/TesterWorkbench/Themes/DarkTheme.xaml apps/TesterWorkbench/TesterWorkbench/Themes/LightTheme.xaml apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml
git commit -m "style: add a-shell workbench theme resources"
```

Expected: pytest PASS, build PASS.

---

### Task 7: Add Pane Headers For Docking-Ready Layout

**Files:**
- Modify: `tests/test_workbench_xaml.py`
- Modify: `apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml`

- [ ] **Step 1: Write failing XAML test for docking-ready pane names**

Add:

```python
def test_primary_panes_have_docking_ready_headers():
    root = _load("apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml")

    names = {
        element.attrib.get(f"{XAML}Name")
        for element in root.iter()
    }

    assert {
        "ProjectExplorerPaneHeader",
        "YamlEditorPaneHeader",
        "GuiEditorPaneHeader",
        "GuiPropertiesPaneHeader",
        "BottomInspectorPaneHeader",
    } <= names
```

- [ ] **Step 2: Run the failing test**

Run:

```bash
uv run --extra dev pytest -q tests/test_workbench_xaml.py::test_primary_panes_have_docking_ready_headers
```

Expected: FAIL until headers are named.

- [ ] **Step 3: Add or name pane header borders**

For each major pane in `MainWindow.xaml`, wrap the existing header content in a named `Border` using `WorkbenchPaneHeaderStyle`:

```xml
<Border x:Name="YamlEditorPaneHeader"
        DockPanel.Dock="Top"
        Style="{StaticResource WorkbenchPaneHeaderStyle}">
    <TextBlock Text="YAML Editor" />
</Border>
```

Use these names:

```text
ProjectExplorerPaneHeader
YamlEditorPaneHeader
GuiEditorPaneHeader
GuiPropertiesPaneHeader
BottomInspectorPaneHeader
```

- [ ] **Step 4: Verify and commit**

Run:

```bash
uv run --extra dev pytest -q tests/test_workbench_xaml.py::test_primary_panes_have_docking_ready_headers
dotnet build apps/TesterWorkbench/TesterWorkbench/TesterWorkbench.csproj
git add tests/test_workbench_xaml.py apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml
git commit -m "refactor: prepare workbench panes for docking"
```

Expected: pytest PASS, build PASS.

---

### Task 8: Tighten Properties Pane For A-Shell Authoring

**Files:**
- Modify: `tests/test_workbench_xaml.py`
- Modify: `apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml`
- Modify: `apps/TesterWorkbench/TesterWorkbench.Core/ViewModels/WorkbenchCommandCatalog.cs`
- Modify: `apps/TesterWorkbench/TesterWorkbench.Core/ViewModels/MainWorkbenchViewModel.cs`

- [ ] **Step 1: Write failing XAML test for required/optional field separation**

Add:

```python
def test_properties_pane_separates_required_optional_and_insert_sections():
    root = _load("apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml")

    names = {
        element.attrib.get(f"{XAML}Name")
        for element in root.iter()
    }

    assert "GuiRequiredArgumentsPanel" in names
    assert "GuiOptionalArgumentsPanel" in names
    assert "GuiInsertCommandPanel" in names
```

- [ ] **Step 2: Run the failing test**

Run:

```bash
uv run --extra dev pytest -q tests/test_workbench_xaml.py::test_properties_pane_separates_required_optional_and_insert_sections
```

Expected: FAIL if panels are not separated by name.

- [ ] **Step 3: Split the properties pane layout**

In `MainWindow.xaml`, structure the Properties pane as:

```xml
<StackPanel x:Name="GuiRequiredArgumentsPanel">
    <TextBlock Text="Required" />
    <!-- existing required ItemsControl goes here -->
</StackPanel>

<Expander x:Name="GuiOptionalArgumentsPanel"
          Header="Optional"
          IsExpanded="{Binding ElementName=ShowOptionalArgumentsCheckBox, Path=IsChecked}">
    <!-- existing optional ItemsControl goes here -->
</Expander>

<StackPanel x:Name="GuiInsertCommandPanel">
    <TextBlock Text="Insert Command" />
    <!-- existing command palette goes here -->
</StackPanel>
```

Keep the existing `ShowOptionalArgumentsCheckBox` behavior.

- [ ] **Step 4: Keep action-dependent fields visible only when relevant**

In `MainWorkbenchViewModel.cs`, ensure the already existing conditional argument visibility path is called after every selected command argument update:

```csharp
UpdateGuiCommandArgumentVisibility();
```

Call it after:

```csharp
UpdateSelectedGuiCommandArgument(...)
SelectGuiCommand(...)
SelectGuiCommandAtLine(...)
RefreshGuiModelFromEditor()
```

- [ ] **Step 5: Verify and commit**

Run:

```bash
uv run --extra dev pytest -q tests/test_workbench_xaml.py::test_properties_pane_separates_required_optional_and_insert_sections
DOTNET_ROLL_FORWARD=Major dotnet run --project apps/TesterWorkbench/TesterWorkbench.Tests/TesterWorkbench.Tests.csproj
dotnet build apps/TesterWorkbench/TesterWorkbench/TesterWorkbench.csproj
git add tests/test_workbench_xaml.py apps/TesterWorkbench/TesterWorkbench/MainWindow.xaml apps/TesterWorkbench/TesterWorkbench.Core/ViewModels/WorkbenchCommandCatalog.cs apps/TesterWorkbench/TesterWorkbench.Core/ViewModels/MainWorkbenchViewModel.cs
git commit -m "feat: organize gui properties pane"
```

Expected: pytest PASS, C# tests PASS, build PASS.

---

### Task 9: Add Manual UX Smoke Checklist

**Files:**
- Create: `docs/ui-ux/a-workbench-smoke-checklist.md`
- Modify: `docs/mockups/workbench-ui-ux-comparison.html`

- [ ] **Step 1: Create manual smoke checklist**

Create `docs/ui-ux/a-workbench-smoke-checklist.md`:

```markdown
# A-Workbench UI/UX Smoke Checklist

## Workspace

- Open a workspace from File > Open.
- Confirm the current YAML file is marked in Project/Test Explorer.
- Confirm referenced YAML files are visually different from the current YAML file.
- Confirm the context strip shows current YAML, run target, save state, run state, and line state.

## Editing

- Select a YAML command line and confirm the matching GUI block is the active selection.
- Select a GUI block and confirm the YAML line moves or highlights without creating a second active selection.
- Edit a scalar property and save.
- Confirm the save state changes from Unsaved to Saved.

## Running

- Run `samples/workbench-demo.yaml`.
- Confirm the current command is highlighted in YAML and GUI.
- Confirm delay rows remain visually subtle.
- Confirm Variables, Console, and Execution Trace update during the run.

## Trace Inspector

- While events are streaming, confirm Follow latest moves to the newest event.
- Select an old trace row and confirm details stay pinned.
- Confirm tooltips explain each trace column and detail panel.

## Theme

- Test System, Light, and Dark.
- Confirm selected tabs, dropdowns, toolbar buttons, context chips, and pane headers remain readable.
```

- [ ] **Step 2: Mark A안 selected in comparison mockup**

In `docs/mockups/workbench-ui-ux-comparison.html`, add a short note under the conclusion card:

```html
<p class="note">
  Decision: A안 Stabilized IDE Shell is selected as the base direction. B안 Trace Inspector and C안 Properties improvements will be absorbed selectively.
</p>
```

- [ ] **Step 3: Verify and commit**

Run:

```bash
git diff --check
git add docs/ui-ux/a-workbench-smoke-checklist.md docs/mockups/workbench-ui-ux-comparison.html
git commit -m "docs: add a-workbench ux smoke checklist"
```

Expected: diff check PASS.

---

### Task 10: Full Regression And Push

**Files:**
- No source changes unless tests reveal a defect.

- [ ] **Step 1: Run full Python tests**

Run:

```bash
uv run --extra dev pytest -q
```

Expected: all tests PASS.

- [ ] **Step 2: Run full C# tests**

Run:

```bash
DOTNET_ROLL_FORWARD=Major dotnet run --project apps/TesterWorkbench/TesterWorkbench.Tests/TesterWorkbench.Tests.csproj
```

Expected: `TesterWorkbench core tests passed.`

- [ ] **Step 3: Build WPF app**

Run:

```bash
dotnet build apps/TesterWorkbench/TesterWorkbench/TesterWorkbench.csproj
```

Expected: 0 errors.

- [ ] **Step 4: Check git diff hygiene**

Run:

```bash
git diff --check
git status --short
```

Expected: no whitespace errors. Only intentional source/doc changes should remain before the final commit. If `uv.lock` appears as untracked generated output, remove it from the working tree before committing.

- [ ] **Step 5: Push**

Run:

```bash
git push
```

Expected: `main -> main` pushed to `https://github.com/ljh453/tester.git`.

---

## Self-Review

- Spec coverage: The plan maps the selected A안 to shell/context strip, toolbar grouping, active selection, Trace Inspector, Properties refinement, theme resources, docking-ready pane headers, and a manual smoke checklist.
- Placeholder scan: The plan intentionally avoids unresolved placeholder markers. Each task has concrete files, test snippets, implementation snippets, commands, and expected results.
- Type consistency: New names are consistent across tasks:
  - `WorkbenchSelectionOrigin`
  - `ActiveSelectionOrigin`
  - `WorkbenchContextStrip`
  - `ContextSelectedYamlText`
  - `ContextRunTargetText`
  - `ContextSaveStateText`
  - `ContextRunStateText`
  - `ContextCurrentLineText`
  - `TracePinSelectedButton`
  - `TraceSelectedEventText`
  - `WorkbenchContextStripStyle`
  - `WorkbenchStatusChipTextStyle`
  - `WorkbenchPaneHeaderStyle`
