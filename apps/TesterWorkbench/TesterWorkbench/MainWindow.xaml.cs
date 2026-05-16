using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Forms = System.Windows.Forms;
using TesterWorkbench.Core.Engine;
using TesterWorkbench.Core.ViewModels;
using TesterWorkbench.Core.Workspace;
using TesterWorkbench.Themes;

namespace TesterWorkbench;

public partial class MainWindow : Window
{
    private const double GuiEditorCollapseThreshold = 150.0;

    private readonly MainWorkbenchViewModel _viewModel;
    private double _editorVerticalOffset;
    private GridLength _savedGuiEditorWidth = new(1.12, GridUnitType.Star);
    private GridLength _savedGuiPropertiesWidth = new(280);
    private bool _isGuiEditorVisible = true;
    private bool _isRefreshingGuiTestcaseSelection;

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
        ThemeModeComboBox.ItemsSource = Enum.GetValues<WorkbenchThemeMode>();
        ThemeModeComboBox.SelectedItem = _viewModel.ThemeMode;
        WorkbenchThemeManager.Apply(_viewModel.ThemeMode);
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

    private async void Run_Click(object sender, RoutedEventArgs e)
    {
        await RunUiAction(() => _viewModel.RunAsync(
            onExecutionChanged: QueueRuntimeRefresh));
    }

    private void AutoFocusLine_Changed(object sender, RoutedEventArgs e)
    {
        _viewModel.SetAutoFocusExecutionLine(AutoFocusLineCheckBox.IsChecked == true);
    }

    private void ThemeMode_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeModeComboBox.SelectedItem is not WorkbenchThemeMode themeMode)
        {
            return;
        }

        _viewModel.SetThemeMode(themeMode);
        WorkbenchThemeManager.Apply(themeMode);
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
        HighlightCurrentExecutionLine();
        RefreshGuiEditor();
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

    private void EditorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (EditorBox.Text != _viewModel.EditorText)
        {
            _viewModel.UpdateEditorText(EditorBox.Text);
        }

        LineNumbersTextBlock.Text = _viewModel.EditorLineNumbersText;
        SyncLineNumberScroll();
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

    private void EditorBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
        {
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
        AutoFocusLineCheckBox.IsChecked = _viewModel.AutoFocusExecutionLine;
        ThemeModeComboBox.SelectedItem = _viewModel.ThemeMode;
        ApplyEditorFontSize();
        SelectedFileText.Text = _viewModel.SelectedFilePath ?? "";
        RefreshRuntimeViews();
        SyncLineNumberScroll();
    }

    private void RefreshRuntimeViews(bool selectLatestTrace = true)
    {
        CurrentLineText.Text = _viewModel.CurrentLocationText;
        ProblemsGrid.ItemsSource = _viewModel.Problems;
        ExecutionTraceGrid.ItemsSource = null;
        ExecutionTraceGrid.ItemsSource = _viewModel.ExecutionTrace;
        if (selectLatestTrace && _viewModel.ExecutionTrace.Count > 0)
        {
            var latestEvent = _viewModel.ExecutionTrace[^1];
            ExecutionTraceGrid.SelectedItem = latestEvent;
            ExecutionTraceGrid.ScrollIntoView(latestEvent);
        }

        VariablesGrid.ItemsSource = _viewModel.Variables;
        RunStatusText.Text = _viewModel.RunStatus;
        ReportPathText.Text = _viewModel.ReportDirectory ?? "";
        ReportTabText.Text = _viewModel.ReportDirectory is null
            ? "No report generated yet."
            : $"Report directory: {_viewModel.ReportDirectory}";
        RefreshConsole();
        RefreshGuiEditor();
        HighlightCurrentExecutionLine();
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
        GuiCommandSourceBox.Text = command?.SourcePreview ?? "";
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

    private void HideGuiEditorGroup()
    {
        if (!_isGuiEditorVisible)
        {
            GuiEditorToggleButton.IsChecked = false;
            GuiEditorToggleButton.Content = "Show GUI Editor";
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
        GuiEditorToggleButton.Content = "Show GUI Editor";
        _isGuiEditorVisible = false;
    }

    private void ShowGuiEditorGroup()
    {
        if (_isGuiEditorVisible)
        {
            GuiEditorToggleButton.IsChecked = true;
            GuiEditorToggleButton.Content = "GUI Editor";
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
        GuiEditorToggleButton.Content = "GUI Editor";
        _isGuiEditorVisible = true;
    }

    private void QueueRuntimeRefresh()
    {
        if (Dispatcher.CheckAccess())
        {
            SafeRefreshRuntimeViews();
            return;
        }

        Dispatcher.BeginInvoke(SafeRefreshRuntimeViews);
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
