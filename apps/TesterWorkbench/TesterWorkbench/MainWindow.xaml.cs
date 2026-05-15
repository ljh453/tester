using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Forms = System.Windows.Forms;
using TesterWorkbench.Core.Engine;
using TesterWorkbench.Core.ViewModels;
using TesterWorkbench.Core.Workspace;

namespace TesterWorkbench;

public partial class MainWindow : Window
{
    private readonly MainWorkbenchViewModel _viewModel;
    private double _editorVerticalOffset;

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
    }

    private void EditorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (EditorBox.Text != _viewModel.EditorText)
        {
            _viewModel.UpdateEditorText(EditorBox.Text);
        }

        LineNumbersTextBlock.Text = _viewModel.EditorLineNumbersText;
        SyncLineNumberScroll();
    }

    private void EditorBox_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        _editorVerticalOffset = e.VerticalOffset;
        SyncLineNumberScroll();
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
        ApplyEditorFontSize();
        SelectedFileText.Text = _viewModel.SelectedFilePath ?? "";
        RefreshRuntimeViews();
        ConsoleBox.Text = _viewModel.ConsoleText;
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
        HighlightCurrentExecutionLine();
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
        if (!_viewModel.AutoFocusExecutionLine)
        {
            return;
        }

        var lineNumber = _viewModel.CurrentLineNumber;
        if (lineNumber <= 0 || string.IsNullOrEmpty(EditorBox.Text))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(_viewModel.CurrentSourceFile)
            && !string.IsNullOrWhiteSpace(_viewModel.SelectedFilePath)
            && !Path.GetFullPath(_viewModel.CurrentSourceFile).Equals(
                Path.GetFullPath(_viewModel.SelectedFilePath),
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var lineIndex = lineNumber - 1;
        if (lineIndex < 0 || lineIndex >= EditorBox.LineCount)
        {
            return;
        }

        var start = EditorBox.GetCharacterIndexFromLineIndex(lineIndex);
        var length = EditorBox.GetLineLength(lineIndex);
        EditorBox.Focus();
        EditorBox.Select(start, length);
        EditorBox.ScrollToLine(lineIndex);
        SyncLineNumberScroll();
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
    }

    private void ApplyEditorFontSize()
    {
        EditorBox.FontSize = _viewModel.EditorFontSize;
        LineNumbersTextBlock.FontSize = _viewModel.EditorFontSize;
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
