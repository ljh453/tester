using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using TesterWorkbench.Core.Engine;
using TesterWorkbench.Core.ViewModels;
using TesterWorkbench.Core.Workspace;
using TesterWorkbench.Themes;

namespace TesterWorkbench;

public partial class MainWindow : Window
{
    private const double GuiEditorCollapseThreshold = 150.0;
    private const string CommandDragDataFormat = "TesterWorkbench.CommandType";
    private const string CommandBlockDragDataFormat = "TesterWorkbench.CommandBlock";
    private const string CommandBlockGroupDragDataFormat = "TesterWorkbench.CommandBlocks";

    private readonly MainWorkbenchViewModel _viewModel;
    private double _editorVerticalOffset;
    private GridLength _savedGuiEditorWidth = new(1.12, GridUnitType.Star);
    private GridLength _savedGuiPropertiesWidth = new(280);
    private bool _isGuiEditorVisible = true;
    private bool _isRefreshingGuiTestcaseSelection;
    private bool _isApplyingGuiCommandArgumentEdit;
    private System.Windows.Point? _commandDragStartPoint;
    private WorkbenchCommandDefinition? _dragCommandDefinition;
    private System.Windows.Point? _commandBlockDragStartPoint;
    private WorkbenchCommandBlock? _dragCommandBlock;
    private System.Windows.Point? _guiSelectionDragStartPoint;
    private bool _isGuiBulkSelectingWithLeftButton;
    private bool _isGuiBulkSelectingWithRightButton;
    private bool _isRuntimeRefreshQueued;
    private string? _lastReportSummaryPath;

    public MainWindow()
    {
        InitializeComponent();
        var repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory)
            ?? Environment.CurrentDirectory;
        var pythonExecutable = Environment.GetEnvironmentVariable("EMBSW_TESTER_PYTHON") ?? "python";
        _viewModel = new MainWorkbenchViewModel(
            new WorkspaceScanner(),
            new TesterEngineBridge(
                pythonExecutable,
                repositoryRoot,
                new ProcessEngineRunner()));
        WorkspacePathBox.Text = repositoryRoot;
        AutoFocusLineCheckBox.IsChecked = _viewModel.AutoFocusExecutionLine;
        WorkbenchThemeManager.Apply(_viewModel.ThemeMode);
        CommandCatalogGroupsControl.ItemsSource = _viewModel.CommandCatalogGroups;
        ApplyEditorFontSize();
        RefreshView();
    }

    private async void OpenWorkspace_Click(object sender, RoutedEventArgs e)
    {
        var selectedPath = WorkspacePathBox.Text;
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "Select Embedded SW Tester workspace",
            SelectedPath = Directory.Exists(selectedPath) ? selectedPath : Environment.CurrentDirectory,
            UseDescriptionForTitle = true
        };
        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            selectedPath = dialog.SelectedPath;
            WorkspacePathBox.Text = selectedPath;
        }

        await RunUiAction(() => _viewModel.OpenWorkspaceAsync(selectedPath));
    }

    private async void ProjectTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (ProjectTree.SelectedItem is not TreeViewItem { Tag: WorkspaceNode node }
            || node.Kind != WorkspaceNodeKind.File
            || !IsYamlFile(node.FullPath))
        {
            return;
        }

        await RunUiAction(() => _viewModel.OpenFileAsync(node.FullPath));
    }

    private async void Compile_Click(object sender, RoutedEventArgs e)
    {
        await RunUiAction(() => _viewModel.CompileAsync());
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        await RunUiAction(() => _viewModel.SaveAsync());
    }

    private async void Run_Click(object sender, RoutedEventArgs e)
    {
        await RunUiAction(() => _viewModel.RunAsync(
            onExecutionChanged: QueueRuntimeRefresh,
            dispatchExecutionUpdate: DispatchRuntimeViewModelUpdate));
    }

    private async void Pause_Click(object sender, RoutedEventArgs e)
    {
        await RunUiAction(() => _viewModel.PauseRunAsync());
    }

    private async void Resume_Click(object sender, RoutedEventArgs e)
    {
        await RunUiAction(() => _viewModel.ResumeRunAsync());
    }

    private void OpenReportSummary_Click(object sender, RoutedEventArgs e)
    {
        var summaryPath = _viewModel.ReportSummaryPath;
        if (string.IsNullOrWhiteSpace(summaryPath) || !File.Exists(summaryPath))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(summaryPath)
        {
            UseShellExecute = true
        });
    }

    private void ToggleBreakpoint_Click(object sender, RoutedEventArgs e)
    {
        var lineNumber = EditorBox.GetLineIndexFromCharacterIndex(EditorBox.CaretIndex) + 1;
        _viewModel.ToggleBreakpointAtLine(lineNumber);
        RefreshBreakpointViews();
    }

    private void AutoFocusLine_Changed(object sender, RoutedEventArgs e)
    {
        _viewModel.SetAutoFocusExecutionLine(AutoFocusLineCheckBox.IsChecked == true);
    }

    private void GuiEditorToggle_Click(object sender, RoutedEventArgs e)
    {
        if (GuiEditorToggleButton.IsChecked == true)
        {
            ShowGuiEditorGroup();
        }
        else
        {
            HideGuiEditorGroup();
        }
    }

    private async void Settings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var snapshot = await _viewModel.GetSettingsSnapshotAsync();
            var dialog = new SettingsWindow(snapshot)
            {
                Owner = this
            };
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            await RunUiAction(() => _viewModel.ApplySettingsAsync(dialog.CreateUpdate()));
            WorkbenchThemeManager.Apply(_viewModel.ThemeMode);
        }
        catch (Exception ex)
        {
            ConsoleBox.Text = ex.Message;
        }
    }

    private void YamlGuiSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (_isGuiEditorVisible && GuiEditorColumn.ActualWidth <= GuiEditorCollapseThreshold)
        {
            HideGuiEditorGroup();
        }
    }

    private void ExecutionTraceGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ExecutionTraceGrid.SelectedItem is not EngineRunEvent runEvent)
        {
            return;
        }

        _viewModel.SelectExecutionTraceEvent(runEvent);
        VariablesGrid.ItemsSource = _viewModel.Variables;
        CurrentLineText.Text = _viewModel.CurrentLocationText;
        FocusYamlLine(_viewModel.CurrentLineNumber);
        HighlightCurrentExecutionLine();
        RefreshSelectedTraceDetails();
    }

    private void EditorBox_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (!EditorBox.IsKeyboardFocusWithin)
        {
            return;
        }

        var lineNumber = EditorBox.GetLineIndexFromCharacterIndex(EditorBox.CaretIndex) + 1;
        if (!_viewModel.SelectGuiCommandAtLine(lineNumber))
        {
            return;
        }

        CurrentLineText.Text = _viewModel.CurrentLocationText;
        RefreshGuiCommandProperties();
        UpdateCurrentExecutionLineMarker();
    }

    private void GuiTestcaseComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshingGuiTestcaseSelection)
        {
            return;
        }

        _viewModel.SelectGuiTestcase(GuiTestcaseComboBox.SelectedItem as WorkbenchGuiTestcase);
        RefreshGuiEditor();
    }

    private void GuiBlockTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not WorkbenchCommandBlock commandBlock)
        {
            return;
        }

        _viewModel.SelectGuiCommand(commandBlock);
        RefreshGuiCommandProperties();
        CurrentLineText.Text = _viewModel.CurrentLocationText;
        FocusYamlLine(commandBlock.SourceLineStart);
        UpdateCurrentExecutionLineMarker();
    }

    private void GuiExpandAll_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ExpandAllGuiBlocks();
        RefreshGuiEditor();
    }

    private void GuiFoldAll_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.FoldAllGuiBlocks();
        RefreshGuiEditor();
    }

    private void GuiFoldLevel2_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.FoldGuiBlocksFromLevel(2);
        RefreshGuiEditor();
    }

    private void GuiFoldLevel3_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.FoldGuiBlocksFromLevel(3);
        RefreshGuiEditor();
    }

    private void GuiCommandArgumentTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox textBox)
        {
            ApplyGuiCommandArgumentEdit(textBox, textBox.Text);
        }
    }

    private void GuiCommandArgumentTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not System.Windows.Controls.TextBox textBox)
        {
            return;
        }

        textBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        e.Handled = true;
    }

    private void GuiCommandArgumentComboBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.ComboBox comboBox)
        {
            ApplyGuiCommandArgumentEdit(comboBox, comboBox.Text);
        }
    }

    private void GuiCommandArgumentComboBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not System.Windows.Controls.ComboBox comboBox)
        {
            return;
        }

        comboBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        e.Handled = true;
    }

    private void GuiCommandComplexArgumentTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox textBox)
        {
            ApplyGuiCommandArgumentEdit(textBox, textBox.Text);
        }
    }

    private void CommandCatalogItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: WorkbenchCommandDefinition commandDefinition })
        {
            return;
        }

        _dragCommandDefinition = commandDefinition;
        _commandDragStartPoint = e.GetPosition(null);
    }

    private void CommandCatalogItem_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed
            || _dragCommandDefinition is null
            || _commandDragStartPoint is null
            || sender is not DependencyObject dragSource)
        {
            return;
        }

        var position = e.GetPosition(null);
        var diff = _commandDragStartPoint.Value - position;
        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var dragData = new System.Windows.DataObject(CommandDragDataFormat, _dragCommandDefinition.CommandType);
        DragDrop.DoDragDrop(dragSource, dragData, System.Windows.DragDropEffects.Copy);
        _viewModel.ClearGuiCommandInsertionPreview();
        _dragCommandDefinition = null;
        _commandDragStartPoint = null;
        e.Handled = true;
    }

    private void GuiCommandBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: WorkbenchCommandBlock commandBlock })
        {
            return;
        }

        if (IsCommandDragHandle(e.OriginalSource as DependencyObject))
        {
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            _viewModel.ToggleGuiCommandForBulkAction(commandBlock);
            RefreshGuiCommandProperties();
            CurrentLineText.Text = _viewModel.CurrentLocationText;
            UpdateCurrentExecutionLineMarker();
            e.Handled = true;
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            _viewModel.SelectGuiCommandRangeForBulkAction(commandBlock);
            RefreshGuiCommandProperties();
            CurrentLineText.Text = _viewModel.CurrentLocationText;
            UpdateCurrentExecutionLineMarker();
            e.Handled = true;
            return;
        }

        _viewModel.SelectGuiCommandForBulkAction(commandBlock, replaceSelection: true);
        RefreshGuiCommandProperties();
        CurrentLineText.Text = _viewModel.CurrentLocationText;
        BeginGuiBulkSelection(e.GetPosition(GuiSelectionHost));
        e.Handled = true;
    }

    private void GuiCommandBlock_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        EndGuiBulkSelection();
    }

    private void GuiCommandDragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: WorkbenchCommandBlock commandBlock })
        {
            return;
        }

        if (commandBlock.IsSelectedForBulkAction && _viewModel.SelectedGuiCommandCount > 1)
        {
            _viewModel.SelectGuiCommand(commandBlock);
        }
        else
        {
            _viewModel.SelectGuiCommandForBulkAction(commandBlock, replaceSelection: true);
        }

        RefreshGuiCommandProperties();
        CurrentLineText.Text = _viewModel.CurrentLocationText;
        _isGuiBulkSelectingWithLeftButton = false;
        _dragCommandBlock = commandBlock;
        _commandBlockDragStartPoint = e.GetPosition(null);
        e.Handled = true;
    }

    private void GuiCommandBlock_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        var isRangeSelecting =
            _isGuiBulkSelectingWithRightButton && e.RightButton == MouseButtonState.Pressed
            || _isGuiBulkSelectingWithLeftButton && e.LeftButton == MouseButtonState.Pressed;
        if (!isRangeSelecting
            || sender is not FrameworkElement { DataContext: WorkbenchCommandBlock commandBlock })
        {
            return;
        }

        _viewModel.SelectGuiCommandRangeForBulkAction(commandBlock);
        RefreshGuiCommandProperties();
        CurrentLineText.Text = _viewModel.CurrentLocationText;
        UpdateCurrentExecutionLineMarker();
    }

    private void GuiCommandBlock_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: WorkbenchCommandBlock commandBlock })
        {
            return;
        }

        if (!commandBlock.IsSelectedForBulkAction)
        {
            _viewModel.SelectGuiCommandForBulkAction(commandBlock, replaceSelection: true);
        }
        else
        {
            _viewModel.SelectGuiCommand(commandBlock);
        }

        _isGuiBulkSelectingWithRightButton = true;
        RefreshGuiCommandProperties();
        CurrentLineText.Text = _viewModel.CurrentLocationText;
        UpdateCurrentExecutionLineMarker();
    }

    private void GuiCommandBlock_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isGuiBulkSelectingWithRightButton = false;
    }

    private void GuiSelectionSurface_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindDataContext<WorkbenchCommandBlock>(e.OriginalSource as DependencyObject) is not null)
        {
            return;
        }

        _viewModel.ClearGuiCommandBulkSelection();
        BeginGuiBulkSelection(e.GetPosition(GuiSelectionHost));
        e.Handled = true;
    }

    private void GuiSelectionSurface_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        EndGuiBulkSelection();
    }

    private void GuiSelectionSurface_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isGuiBulkSelectingWithLeftButton || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        UpdateGuiDragSelectionRectangle(e.GetPosition(GuiSelectionHost));
        var commandBlock =
            FindDataContext<WorkbenchCommandBlock>(Mouse.DirectlyOver as DependencyObject)
            ?? FindDataContext<WorkbenchCommandBlock>(e.OriginalSource as DependencyObject);
        if (commandBlock is null)
        {
            return;
        }

        _viewModel.SelectGuiCommandRangeForBulkAction(commandBlock);
        RefreshGuiCommandProperties();
        CurrentLineText.Text = _viewModel.CurrentLocationText;
        UpdateCurrentExecutionLineMarker();
        e.Handled = true;
    }

    private void GuiCommandBlock_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed
            || _dragCommandBlock is null
            || _commandBlockDragStartPoint is null
            || sender is not DependencyObject dragSource)
        {
            return;
        }

        var position = e.GetPosition(null);
        var diff = _commandBlockDragStartPoint.Value - position;
        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var movingCommands = _viewModel.GetGuiCommandsForDrag(_dragCommandBlock);
        var dragData = new System.Windows.DataObject();
        if (movingCommands.Count > 1)
        {
            dragData.SetData(CommandBlockGroupDragDataFormat, movingCommands.ToArray());
        }
        else
        {
            dragData.SetData(CommandBlockDragDataFormat, movingCommands[0]);
        }

        DragDrop.DoDragDrop(dragSource, dragData, System.Windows.DragDropEffects.Move);
        _viewModel.ClearGuiCommandInsertionPreview();
        _dragCommandBlock = null;
        _commandBlockDragStartPoint = null;
        e.Handled = true;
    }

    private void BeginGuiBulkSelection(System.Windows.Point startPoint)
    {
        _isGuiBulkSelectingWithLeftButton = true;
        _guiSelectionDragStartPoint = startPoint;
        _dragCommandBlock = null;
        _commandBlockDragStartPoint = null;
        UpdateGuiDragSelectionRectangle(startPoint);
        Mouse.Capture(GuiSelectionHost, CaptureMode.SubTree);
    }

    private void EndGuiBulkSelection()
    {
        _isGuiBulkSelectingWithLeftButton = false;
        _guiSelectionDragStartPoint = null;
        GuiDragSelectionRectangle.Visibility = Visibility.Collapsed;
        if (Mouse.Captured == GuiSelectionHost)
        {
            Mouse.Capture(null);
        }
    }

    private void UpdateGuiDragSelectionRectangle(System.Windows.Point currentPoint)
    {
        if (_guiSelectionDragStartPoint is null)
        {
            return;
        }

        var startPoint = _guiSelectionDragStartPoint.Value;
        var left = Math.Min(startPoint.X, currentPoint.X);
        var top = Math.Min(startPoint.Y, currentPoint.Y);
        var width = Math.Abs(currentPoint.X - startPoint.X);
        var height = Math.Abs(currentPoint.Y - startPoint.Y);
        Canvas.SetLeft(GuiDragSelectionRectangle, left);
        Canvas.SetTop(GuiDragSelectionRectangle, top);
        GuiDragSelectionRectangle.Width = width;
        GuiDragSelectionRectangle.Height = height;
        GuiDragSelectionRectangle.Visibility = Visibility.Visible;
    }

    private void GuiDeleteSelectedCommands_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.DeleteSelectedGuiCommands();
        _dragCommandBlock = null;
        _commandBlockDragStartPoint = null;
        EndGuiBulkSelection();
        _isGuiBulkSelectingWithRightButton = false;
        RefreshEditorAfterGuiEdit();
        e.Handled = true;
    }

    private void GuiPhase_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        var commandDefinition = GetDraggedCommand(e.Data);
        var movingCommands = GetDraggedCommandBlocks(e.Data);
        var target = ResolveCommandInsertionTarget(sender, e.OriginalSource);
        if (commandDefinition is not null && target is not null)
        {
            _viewModel.ShowGuiCommandInsertionPreview(commandDefinition, target);
        }
        else if (movingCommands.Count > 0
            && target is not null
            && IsMoveTargetAllowed(movingCommands, target))
        {
            _viewModel.ShowGuiCommandMovePreview(movingCommands, target);
        }

        e.Effects = commandDefinition is not null && target is not null
            ? System.Windows.DragDropEffects.Copy
            : movingCommands.Count > 0
                && target is not null
                && IsMoveTargetAllowed(movingCommands, target)
                    ? System.Windows.DragDropEffects.Move
                    : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void GuiPhase_DragLeave(object sender, System.Windows.DragEventArgs e)
    {
        if (sender is FrameworkElement element && IsPointerInside(element, e.GetPosition(element)))
        {
            return;
        }

        _viewModel.ClearGuiCommandInsertionPreview();
    }

    private void GuiPhase_Drop(object sender, System.Windows.DragEventArgs e)
    {
        var commandDefinition = GetDraggedCommand(e.Data);
        var movingCommands = GetDraggedCommandBlocks(e.Data);
        var target = ResolveCommandInsertionTarget(sender, e.OriginalSource);
        if (target is null
            || commandDefinition is null
            && movingCommands.Count == 0)
        {
            e.Effects = System.Windows.DragDropEffects.None;
            e.Handled = true;
            return;
        }

        _viewModel.ClearGuiCommandInsertionPreview();
        if (commandDefinition is not null)
        {
            _viewModel.InsertGuiCommand(commandDefinition, target);
            e.Effects = System.Windows.DragDropEffects.Copy;
        }
        else if (movingCommands.Count > 0 && IsMoveTargetAllowed(movingCommands, target))
        {
            _viewModel.MoveGuiCommands(movingCommands, target);
            e.Effects = System.Windows.DragDropEffects.Move;
        }
        else
        {
            e.Effects = System.Windows.DragDropEffects.None;
        }

        RefreshEditorAfterGuiEdit();
        e.Handled = true;
    }

    private void EditorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (EditorBox.Text != _viewModel.EditorText)
        {
            _viewModel.UpdateEditorText(EditorBox.Text);
        }

        LineNumbersTextBlock.Text = _viewModel.EditorLineNumbersText;
        BreakpointsTextBlock.Text = _viewModel.BreakpointsText;
        SyncLineNumberScroll();
        SelectedFileText.Text = _viewModel.SelectedFileDisplayText;
        SaveStatusTextBlock.Text = _viewModel.SaveStatusText;
        RefreshGuiEditor();
        UpdateCurrentExecutionLineMarker();
    }

    private void EditorBox_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        _editorVerticalOffset = e.VerticalOffset;
        SyncLineNumberScroll();
        UpdateCurrentExecutionLineMarker();
    }

    private void EditorBox_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateCurrentExecutionLineMarker();
    }

    private async void EditorBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
        {
            return;
        }

        if (e.Key == Key.S)
        {
            await RunUiAction(() => _viewModel.SaveAsync());
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Add or Key.OemPlus)
        {
            ZoomEditor(increase: true);
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Subtract or Key.OemMinus)
        {
            ZoomEditor(increase: false);
            e.Handled = true;
        }
    }

    private void YamlEditor_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
        {
            return;
        }

        ZoomEditor(e.Delta > 0);
        e.Handled = true;
    }

    private void LineNumbers_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var lineNumber = GetEditorLineNumberFromPoint(e.GetPosition(EditorBox));
        _viewModel.ToggleBreakpointAtLine(lineNumber);
        RefreshBreakpointViews();
        e.Handled = true;
    }

    private async Task RunUiAction(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            ConsoleBox.Text = ex.Message;
        }
        finally
        {
            RefreshView();
        }
    }

    private void RefreshView()
    {
        ProjectTree.Items.Clear();
        if (_viewModel.WorkspaceRoot is not null)
        {
            foreach (var child in _viewModel.WorkspaceRoot.Children)
            {
                ProjectTree.Items.Add(CreateTreeItem(child));
            }
        }

        EditorBox.Text = _viewModel.EditorText;
        LineNumbersTextBlock.Text = _viewModel.EditorLineNumbersText;
        CommandCatalogGroupsControl.ItemsSource = _viewModel.CommandCatalogGroups;
        AutoFocusLineCheckBox.IsChecked = _viewModel.AutoFocusExecutionLine;
        ApplyEditorFontSize();
        SelectedFileText.Text = _viewModel.SelectedFileDisplayText;
        SaveStatusTextBlock.Text = _viewModel.SaveStatusText;
        BreakpointsTextBlock.Text = _viewModel.BreakpointsText;
        RefreshRuntimeViews();
        SyncLineNumberScroll();
    }

    private void RefreshRuntimeViews(bool selectLatestTrace = true)
    {
        CurrentLineText.Text = _viewModel.CurrentLocationText;
        ProblemsGrid.ItemsSource = _viewModel.Problems;
        ExecutionTraceGrid.ItemsSource = _viewModel.ExecutionTrace;
        if (selectLatestTrace && _viewModel.ExecutionTrace.Count > 0)
        {
            var latestEvent = _viewModel.ExecutionTrace[^1];
            ExecutionTraceGrid.SelectedItem = latestEvent;
            ExecutionTraceGrid.ScrollIntoView(latestEvent);
        }

        VariablesGrid.ItemsSource = _viewModel.Variables;
        RunStatusText.Text = _viewModel.RunStatus;
        BreakpointsTextBlock.Text = _viewModel.BreakpointsText;
        ReportPathText.Text = _viewModel.ReportDirectory ?? "";
        RefreshReportViewer();
        RefreshSelectedTraceDetails();
        RefreshConsole();
        HighlightCurrentExecutionLine();
    }

    private void RefreshSelectedTraceDetails()
    {
        var selectedEvent = ExecutionTraceGrid.SelectedItem as EngineRunEvent;
        TraceResolvedInputsBox.Text = selectedEvent?.ResolvedInputsDetail ?? string.Empty;
        TraceOutputsBox.Text = selectedEvent?.OutputsDetail ?? string.Empty;
        TraceEvidenceBox.Text = selectedEvent is null
            ? string.Empty
            : string.Join(
                Environment.NewLine,
                new[] { selectedEvent.RawEvidenceRefs, selectedEvent.DurationDetail }
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
        TraceErrorBox.Text = selectedEvent?.Error ?? string.Empty;
    }

    private void RefreshReportViewer()
    {
        var reportDirectory = _viewModel.ReportDirectory;
        var summaryPath = _viewModel.ReportSummaryPath;
        ReportTabText.Text = reportDirectory is null
            ? "No report generated yet."
            : $"Report directory: {reportDirectory}";
        ReportSummaryPathText.Text = summaryPath ?? string.Empty;

        var summaryExists = !string.IsNullOrWhiteSpace(summaryPath) && File.Exists(summaryPath);
        ReportSummaryButton.IsEnabled = summaryExists;
        if (!summaryExists)
        {
            if (_lastReportSummaryPath is not null)
            {
                try
                {
                    ReportBrowser.NavigateToString(string.Empty);
                }
                catch
                {
                    // Clearing the embedded browser is best-effort UI cleanup.
                }
            }

            _lastReportSummaryPath = null;
            return;
        }

        if (_lastReportSummaryPath == summaryPath)
        {
            return;
        }

        try
        {
            ReportBrowser.Navigate(new Uri(summaryPath!));
            _lastReportSummaryPath = summaryPath;
        }
        catch (Exception ex)
        {
            ReportTabText.Text = $"Report generated, but the viewer could not open summary.html: {ex.Message}";
        }
    }

    private void RefreshEditorAfterGuiEdit()
    {
        if (EditorBox.Text != _viewModel.EditorText)
        {
            EditorBox.Text = _viewModel.EditorText;
        }

        LineNumbersTextBlock.Text = _viewModel.EditorLineNumbersText;
        CurrentLineText.Text = _viewModel.CurrentLocationText;
        BreakpointsTextBlock.Text = _viewModel.BreakpointsText;
        SelectedFileText.Text = _viewModel.SelectedFileDisplayText;
        SaveStatusTextBlock.Text = _viewModel.SaveStatusText;
        RefreshGuiEditor();
        FocusYamlLine(_viewModel.CurrentLineNumber);
        UpdateCurrentExecutionLineMarker();
    }

    private void RefreshGuiEditor()
    {
        _isRefreshingGuiTestcaseSelection = true;
        try
        {
            GuiTestcaseComboBox.ItemsSource = _viewModel.GuiModel.Testcases;
            GuiTestcaseComboBox.SelectedItem = _viewModel.SelectedGuiTestcase;
        }
        finally
        {
            _isRefreshingGuiTestcaseSelection = false;
        }

        var selectedTestcase = _viewModel.SelectedGuiTestcase;
        GuiTestcaseLineText.Text = selectedTestcase?.LineRangeText ?? "";
        GuiTestcaseMetadataText.Text = selectedTestcase is null
            ? "No testcase in this YAML file."
            : $"description: {EmptyToDash(selectedTestcase.Description)}  |  tags: {EmptyToDash(selectedTestcase.TagsText)}  |  failure: {selectedTestcase.FailurePolicy}";
        GuiPhaseItems.ItemsSource = null;
        GuiPhaseItems.ItemsSource = selectedTestcase?.Phases ?? Array.Empty<WorkbenchGuiPhase>();
        RefreshGuiCommandProperties();
    }

    private void RefreshGuiCommandProperties()
    {
        var command = _viewModel.SelectedGuiCommand;
        GuiCommandTypeBox.Text = command?.CommandType ?? "";
        GuiCommandSummaryBox.Text = command?.Summary ?? "";
        GuiCommandLineBox.Text = command?.LineRangeText ?? "";
        GuiCommandValidationText.Text = command?.ValidationSummary ?? "";
        GuiCommandValidationText.Foreground = command?.HasValidationIssues == true
            ? System.Windows.Media.Brushes.DarkOrange
            : TryFindResource("Workbench.Brush.TextSecondary") as System.Windows.Media.Brush
                ?? System.Windows.Media.Brushes.Gray;
        GuiCommandArgumentsControl.ItemsSource = command?.Arguments
            .Where(argument => argument.IsScalarEditable)
            .ToArray() ?? Array.Empty<WorkbenchCommandArgument>();
        GuiCommandComplexArgumentsControl.ItemsSource = command?.Arguments
            .Where(argument => !argument.IsScalarEditable)
            .ToArray() ?? Array.Empty<WorkbenchCommandArgument>();
        GuiCommandSourceBox.Text = command?.SourcePreview ?? "";
    }

    private void ApplyGuiCommandArgumentEdit(FrameworkElement editor, string value)
    {
        if (_isApplyingGuiCommandArgumentEdit
            || editor.Tag is not WorkbenchCommandArgument argument
            || value == argument.Value)
        {
            return;
        }

        try
        {
            _isApplyingGuiCommandArgumentEdit = true;
            _viewModel.UpdateSelectedGuiCommandArgument(argument.Name, value);
            RefreshEditorAfterGuiEdit();
        }
        catch (Exception ex)
        {
            ConsoleBox.Text = ex.Message;
        }
        finally
        {
            _isApplyingGuiCommandArgumentEdit = false;
        }
    }

    private void RefreshConsole()
    {
        if (ConsoleBox.Text == _viewModel.ConsoleText)
        {
            return;
        }

        ConsoleBox.Text = _viewModel.ConsoleText;
        ConsoleBox.ScrollToEnd();
    }

    private void RefreshBreakpointViews()
    {
        LineNumbersTextBlock.Text = _viewModel.EditorLineNumbersText;
        BreakpointsTextBlock.Text = _viewModel.BreakpointsText;
        UpdateCurrentExecutionLineMarker();
    }

    private void HideGuiEditorGroup()
    {
        if (!_isGuiEditorVisible)
        {
            GuiEditorToggleButton.IsChecked = false;
            GuiEditorToggleButton.Content = "◧";
            return;
        }

        if (GuiEditorColumn.ActualWidth > 0)
        {
            _savedGuiEditorWidth = new GridLength(GuiEditorColumn.ActualWidth);
        }

        if (GuiPropertiesColumn.ActualWidth > 0)
        {
            _savedGuiPropertiesWidth = new GridLength(GuiPropertiesColumn.ActualWidth);
        }

        GuiEditorColumn.MinWidth = 0;
        GuiPropertiesColumn.MinWidth = 0;
        GuiEditorColumn.Width = new GridLength(0);
        GuiPropertiesColumn.Width = new GridLength(0);
        YamlGuiSplitterColumn.Width = new GridLength(0);
        GuiPropertiesSplitterColumn.Width = new GridLength(0);
        GuiEditorPane.Visibility = Visibility.Collapsed;
        GuiPropertiesPane.Visibility = Visibility.Collapsed;
        YamlGuiSplitter.Visibility = Visibility.Collapsed;
        GuiPropertiesSplitter.Visibility = Visibility.Collapsed;
        GuiEditorToggleButton.IsChecked = false;
        GuiEditorToggleButton.Content = "◧";
        _isGuiEditorVisible = false;
    }

    private void ShowGuiEditorGroup()
    {
        if (_isGuiEditorVisible)
        {
            GuiEditorToggleButton.IsChecked = true;
            GuiEditorToggleButton.Content = "◨";
            return;
        }

        GuiEditorColumn.MinWidth = 300;
        GuiPropertiesColumn.MinWidth = 220;
        YamlGuiSplitterColumn.Width = new GridLength(5);
        GuiPropertiesSplitterColumn.Width = new GridLength(5);
        GuiEditorColumn.Width = IsUsableGridLength(_savedGuiEditorWidth)
            ? _savedGuiEditorWidth
            : new GridLength(1.12, GridUnitType.Star);
        GuiPropertiesColumn.Width = IsUsableGridLength(_savedGuiPropertiesWidth)
            ? _savedGuiPropertiesWidth
            : new GridLength(280);
        GuiEditorPane.Visibility = Visibility.Visible;
        GuiPropertiesPane.Visibility = Visibility.Visible;
        YamlGuiSplitter.Visibility = Visibility.Visible;
        GuiPropertiesSplitter.Visibility = Visibility.Visible;
        GuiEditorToggleButton.IsChecked = true;
        GuiEditorToggleButton.Content = "◨";
        _isGuiEditorVisible = true;
    }

    private void QueueRuntimeRefresh()
    {
        if (_isRuntimeRefreshQueued)
        {
            return;
        }

        _isRuntimeRefreshQueued = true;
        Dispatcher.BeginInvoke(
            () =>
            {
                _isRuntimeRefreshQueued = false;
                SafeRefreshRuntimeViews();
            },
            DispatcherPriority.Background);
    }

    private void DispatchRuntimeViewModelUpdate(Action update)
    {
        if (Dispatcher.CheckAccess())
        {
            update();
            return;
        }

        Dispatcher.Invoke(update);
    }

    private void SafeRefreshRuntimeViews()
    {
        try
        {
            RefreshRuntimeViews();
        }
        catch (Exception ex)
        {
            ConsoleBox.Text = ex.Message;
        }
    }

    private void HighlightCurrentExecutionLine()
    {
        if (!TryGetCurrentExecutionLineIndex(out var lineIndex))
        {
            HideCurrentExecutionLineMarker();
            return;
        }

        if (_viewModel.AutoFocusExecutionLine)
        {
            var start = EditorBox.GetCharacterIndexFromLineIndex(lineIndex);
            EditorBox.Focus();
            EditorBox.Select(start, 0);
            EditorBox.ScrollToLine(lineIndex);
            SyncLineNumberScroll();
        }

        UpdateCurrentExecutionLineMarker(lineIndex);
    }

    private void FocusYamlLine(int sourceLine)
    {
        var lineIndex = sourceLine - 1;
        if (lineIndex < 0 || lineIndex >= EditorBox.LineCount)
        {
            return;
        }

        var start = EditorBox.GetCharacterIndexFromLineIndex(lineIndex);
        EditorBox.Focus();
        EditorBox.Select(start, 0);
        EditorBox.ScrollToLine(lineIndex);
        SyncLineNumberScroll();
    }

    private bool TryGetCurrentExecutionLineIndex(out int lineIndex)
    {
        lineIndex = -1;
        var lineNumber = _viewModel.CurrentLineNumber;
        if (lineNumber <= 0 || string.IsNullOrEmpty(EditorBox.Text))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(_viewModel.CurrentSourceFile)
            && !string.IsNullOrWhiteSpace(_viewModel.SelectedFilePath)
            && !Path.GetFullPath(_viewModel.CurrentSourceFile).Equals(
                Path.GetFullPath(_viewModel.SelectedFilePath),
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        lineIndex = lineNumber - 1;
        if (lineIndex < 0 || lineIndex >= EditorBox.LineCount)
        {
            return false;
        }

        return true;
    }

    private int GetEditorLineNumberFromPoint(System.Windows.Point editorPoint)
    {
        var characterIndex = EditorBox.GetCharacterIndexFromPoint(editorPoint, snapToText: true);
        if (characterIndex < 0)
        {
            return 0;
        }

        return EditorBox.GetLineIndexFromCharacterIndex(characterIndex) + 1;
    }

    private void SyncLineNumberScroll()
    {
        if (EditorBox.LineCount <= 0)
        {
            return;
        }

        LineNumbersScrollViewer.ScrollToVerticalOffset(_editorVerticalOffset);
    }

    private void ZoomEditor(bool increase)
    {
        if (increase)
        {
            _viewModel.ZoomEditorIn();
        }
        else
        {
            _viewModel.ZoomEditorOut();
        }

        ApplyEditorFontSize();
        SyncLineNumberScroll();
        UpdateCurrentExecutionLineMarker();
    }

    private void ApplyEditorFontSize()
    {
        EditorBox.FontSize = _viewModel.EditorFontSize;
        LineNumbersTextBlock.FontSize = _viewModel.EditorFontSize;
    }

    private void UpdateCurrentExecutionLineMarker()
    {
        if (TryGetCurrentExecutionLineIndex(out var lineIndex))
        {
            UpdateCurrentExecutionLineMarker(lineIndex);
            return;
        }

        HideCurrentExecutionLineMarker();
    }

    private void UpdateCurrentExecutionLineMarker(int lineIndex)
    {
        ShowCurrentExecutionLineBadge();
        var blockRange = YamlExecutionBlockRange.Find(EditorBox.Text, lineIndex + 1)
            ?? new YamlExecutionBlockRange(lineIndex, lineIndex);
        var startRectangle = GetLineRectangle(blockRange.StartLineIndex);
        var endRectangle = GetLineRectangle(blockRange.EndLineIndex);
        if (startRectangle.IsEmpty || endRectangle.IsEmpty)
        {
            HideCurrentExecutionLineHighlight();
            return;
        }

        var top = startRectangle.Top;
        var bottom = endRectangle.Bottom;
        if (bottom < 0 || top > EditorBox.ActualHeight)
        {
            HideCurrentExecutionLineHighlight();
            return;
        }

        top = Math.Max(0, top);
        bottom = Math.Min(EditorBox.ActualHeight, bottom);
        if (bottom <= top)
        {
            HideCurrentExecutionLineHighlight();
            return;
        }

        CurrentExecutionLineMarker.Width = Math.Max(0, EditorBox.ActualWidth);
        CurrentExecutionLineMarker.Height = bottom - top;
        Canvas.SetLeft(CurrentExecutionLineMarker, 0);
        Canvas.SetTop(CurrentExecutionLineMarker, top);
        CurrentExecutionLineMarker.Visibility = Visibility.Visible;
    }

    private Rect GetLineRectangle(int lineIndex)
    {
        var characterIndex = EditorBox.GetCharacterIndexFromLineIndex(lineIndex);
        var rectangle = EditorBox.GetRectFromCharacterIndex(characterIndex);
        if (rectangle.IsEmpty)
        {
            return rectangle;
        }

        if (rectangle.Height > 0)
        {
            return rectangle;
        }

        return new Rect(
            rectangle.X,
            rectangle.Y,
            Math.Max(0, EditorBox.ActualWidth),
            EditorBox.FontSize * 1.4);
    }

    private void HideCurrentExecutionLineMarker()
    {
        HideCurrentExecutionLineHighlight();
        CurrentExecutionLineBadge.Visibility = Visibility.Collapsed;
    }

    private void HideCurrentExecutionLineHighlight()
    {
        CurrentExecutionLineMarker.Visibility = Visibility.Collapsed;
    }

    private void ShowCurrentExecutionLineBadge()
    {
        CurrentExecutionLineBadgeText.Text = _viewModel.CurrentLocationText;
        CurrentExecutionLineBadge.Visibility = Visibility.Visible;
    }

    private static TreeViewItem CreateTreeItem(WorkspaceNode node)
    {
        var item = new TreeViewItem
        {
            Header = node.Name,
            Tag = node,
            IsExpanded = node.RelativePath is "tests" or "libs" or "tool-profiles"
        };
        foreach (var child in node.Children)
        {
            item.Items.Add(CreateTreeItem(child));
        }

        return item;
    }

    private static bool IsYamlFile(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".yml", StringComparison.OrdinalIgnoreCase);
    }

    private static WorkbenchCommandDefinition? GetDraggedCommand(System.Windows.IDataObject data)
    {
        if (!data.GetDataPresent(CommandDragDataFormat))
        {
            return null;
        }

        var commandType = data.GetData(CommandDragDataFormat) as string;
        return string.IsNullOrWhiteSpace(commandType)
            ? null
            : WorkbenchCommandCatalog.Find(commandType);
    }

    private static WorkbenchCommandBlock? GetDraggedCommandBlock(System.Windows.IDataObject data)
    {
        return data.GetDataPresent(CommandBlockDragDataFormat)
            ? data.GetData(CommandBlockDragDataFormat) as WorkbenchCommandBlock
            : null;
    }

    private static IReadOnlyList<WorkbenchCommandBlock> GetDraggedCommandBlocks(System.Windows.IDataObject data)
    {
        if (data.GetDataPresent(CommandBlockGroupDragDataFormat)
            && data.GetData(CommandBlockGroupDragDataFormat) is WorkbenchCommandBlock[] commandBlocks)
        {
            return commandBlocks;
        }

        var commandBlock = GetDraggedCommandBlock(data);
        return commandBlock is null
            ? Array.Empty<WorkbenchCommandBlock>()
            : new[] { commandBlock };
    }

    private WorkbenchCommandInsertionTarget? ResolveCommandInsertionTarget(
        object sender,
        object originalSource)
    {
        var explicitDropTarget = FindDropTarget(sender as DependencyObject)
            ?? FindDropTarget(originalSource as DependencyObject);
        if (explicitDropTarget is WorkbenchPhaseStartDropTarget startTarget)
        {
            return new WorkbenchCommandInsertionTarget(
                startTarget.Phase,
                WorkbenchCommandInsertPlacement.BeforeFirstInPhase);
        }

        if (explicitDropTarget is WorkbenchPhaseEndDropTarget endTarget)
        {
            return new WorkbenchCommandInsertionTarget(
                endTarget.Phase,
                WorkbenchCommandInsertPlacement.AtPhaseEnd);
        }

        if (explicitDropTarget is WorkbenchCommandInsideDropTarget insideTarget
            && insideTarget.CommandBlock.CanInsertInside)
        {
            var phase = FindPhaseContainingCommand(insideTarget.CommandBlock)
                ?? FindPhaseFromDropTarget(sender, originalSource);
            return phase is null
                ? null
                : new WorkbenchCommandInsertionTarget(
                    phase,
                    WorkbenchCommandInsertPlacement.InsideCommand,
                    insideTarget.CommandBlock);
        }

        var commandTarget = FindCommandBlockFromDropTarget(originalSource as DependencyObject);
        if (commandTarget is { CanInsertInside: true })
        {
            var commandPhase = FindPhaseContainingCommand(commandTarget)
                ?? FindPhaseFromDropTarget(sender, originalSource);
            return commandPhase is null
                ? null
                : new WorkbenchCommandInsertionTarget(
                    commandPhase,
                    WorkbenchCommandInsertPlacement.InsideCommand,
                    commandTarget);
        }

        var phaseTarget = FindPhaseFromDropTarget(sender, originalSource);
        if (phaseTarget is null)
        {
            return null;
        }

        return commandTarget is null
            ? new WorkbenchCommandInsertionTarget(phaseTarget, WorkbenchCommandInsertPlacement.AtPhaseEnd)
            : new WorkbenchCommandInsertionTarget(
                phaseTarget,
                WorkbenchCommandInsertPlacement.AfterCommand,
                commandTarget);
    }

    private WorkbenchGuiPhase? FindPhaseContainingCommand(WorkbenchCommandBlock commandBlock)
    {
        return _viewModel.SelectedGuiTestcase?.Phases.FirstOrDefault(phase =>
            FlattenCommands(phase.Blocks).Contains(commandBlock));
    }

    private static bool IsMoveTargetAllowed(
        WorkbenchCommandBlock movingCommand,
        WorkbenchCommandInsertionTarget target)
    {
        if (target.ReferenceCommand is null)
        {
            return true;
        }

        return target.ReferenceCommand != movingCommand
            && (target.ReferenceCommand.SourceLineStart < movingCommand.SourceLineStart
                || target.ReferenceCommand.SourceLineStart > movingCommand.SourceLineEnd);
    }

    private static bool IsMoveTargetAllowed(
        IReadOnlyList<WorkbenchCommandBlock> movingCommands,
        WorkbenchCommandInsertionTarget target)
    {
        if (movingCommands.Count == 0)
        {
            return false;
        }

        return movingCommands.All(movingCommand => IsMoveTargetAllowed(movingCommand, target));
    }

    private static WorkbenchGuiPhase? FindPhaseFromDropTarget(object sender, object originalSource)
    {
        if (sender is FrameworkElement { DataContext: WorkbenchGuiPhase senderPhase })
        {
            return senderPhase;
        }

        return FindDataContext<WorkbenchGuiPhase>(originalSource as DependencyObject);
    }

    private static WorkbenchCommandBlock? FindCommandBlockFromDropTarget(DependencyObject? originalSource)
    {
        return FindDataContext<WorkbenchCommandBlock>(originalSource);
    }

    private static object? FindDropTarget(DependencyObject? source)
    {
        var current = source;
        while (current is not null)
        {
            if (current is FrameworkElement { Tag: WorkbenchCommandInsideDropTarget insideDropTarget })
            {
                return insideDropTarget;
            }

            if (current is FrameworkElement
                {
                    DataContext: WorkbenchPhaseStartDropTarget or WorkbenchPhaseEndDropTarget
                } element)
            {
                return element.DataContext;
            }

            current = GetParent(current);
        }

        return null;
    }

    private static T? FindDataContext<T>(DependencyObject? source)
        where T : class
    {
        var current = source;
        while (current is not null)
        {
            if (current is FrameworkElement { DataContext: T dataContext })
            {
                return dataContext;
            }

            current = GetParent(current);
        }

        return null;
    }

    private static DependencyObject? GetParent(DependencyObject current)
    {
        try
        {
            var visualParent = VisualTreeHelper.GetParent(current);
            if (visualParent is not null)
            {
                return visualParent;
            }
        }
        catch (InvalidOperationException)
        {
        }

        return LogicalTreeHelper.GetParent(current);
    }

    private static bool IsPointerInside(FrameworkElement element, System.Windows.Point position)
    {
        return position.X >= 0
            && position.Y >= 0
            && position.X <= element.ActualWidth
            && position.Y <= element.ActualHeight;
    }

    private static bool IsCommandDragHandle(DependencyObject? source)
    {
        var current = source;
        while (current is not null)
        {
            if (current is FrameworkElement { Tag: "CommandDragHandle" })
            {
                return true;
            }

            current = GetParent(current);
        }

        return false;
    }

    private static IEnumerable<WorkbenchCommandBlock> FlattenCommands(
        IEnumerable<WorkbenchCommandBlock> commandBlocks)
    {
        foreach (var commandBlock in commandBlocks)
        {
            yield return commandBlock;
            foreach (var childCommandBlock in FlattenCommands(commandBlock.Children))
            {
                yield return childCommandBlock;
            }
        }
    }

    private static string EmptyToDash(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    private static bool IsUsableGridLength(GridLength gridLength)
    {
        return gridLength.Value > 0;
    }

    private static string? FindRepositoryRoot(string startPath)
    {
        var directory = Directory.Exists(startPath)
            ? new DirectoryInfo(startPath)
            : new DirectoryInfo(Path.GetDirectoryName(startPath) ?? Environment.CurrentDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "pyproject.toml"))
                && Directory.Exists(Path.Combine(directory.FullName, "src", "embsw_tester")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
