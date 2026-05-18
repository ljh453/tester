using System.Collections.ObjectModel;
using System.Windows;
using TesterWorkbench.Core.ViewModels;

namespace TesterWorkbench;

public partial class SettingsWindow : Window
{
    private static readonly HashSet<string> AllowedParities = new(StringComparer.OrdinalIgnoreCase)
    {
        "none",
        "even",
        "odd",
        "mark",
        "space"
    };

    public static IReadOnlyList<string> ParityOptions { get; } = new[]
    {
        "none",
        "even",
        "odd",
        "mark",
        "space"
    };

    public static IReadOnlyList<double> StopBitsOptions { get; } = new[] { 1.0, 1.5, 2.0 };

    public static IReadOnlyList<int> ByteSizeOptions { get; } = new[] { 5, 6, 7, 8 };

    private readonly ObservableCollection<SerialDeviceSettingsRow> _serialDeviceRows;

    public SettingsWindow(WorkbenchSettingsSnapshot settings)
    {
        InitializeComponent();
        ThemeModeComboBox.ItemsSource = Enum.GetValues<WorkbenchThemeMode>();
        ThemeModeComboBox.SelectedItem = settings.ThemeMode;
        ToolProfilePathTextBlock.Text = string.IsNullOrWhiteSpace(settings.ToolProfilePath)
            ? "No tool_profile is referenced by the current YAML file."
            : settings.ToolProfilePath;
        _serialDeviceRows = new ObservableCollection<SerialDeviceSettingsRow>(
            settings.SerialDevices.Select(SerialDeviceSettingsRow.FromSettings));
        SerialDevicesGrid.ItemsSource = _serialDeviceRows;
    }

    public WorkbenchSettingsUpdate CreateUpdate()
    {
        var themeMode = ThemeModeComboBox.SelectedItem is WorkbenchThemeMode selectedThemeMode
            ? selectedThemeMode
            : WorkbenchThemeMode.System;
        return new WorkbenchSettingsUpdate(
            themeMode,
            _serialDeviceRows
                .Select(row => new WorkbenchSerialDeviceSettingsUpdate(
                    row.Name,
                    row.Parity.Trim().ToLowerInvariant(),
                    row.StopBits,
                    row.ByteSize))
                .ToArray());
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateSerialSettings())
        {
            return;
        }

        DialogResult = true;
    }

    private bool ValidateSerialSettings()
    {
        foreach (var row in _serialDeviceRows)
        {
            if (!AllowedParities.Contains(row.Parity.Trim()))
            {
                System.Windows.MessageBox.Show(
                    this,
                    $"Serial device '{row.Name}' parity must be one of none, even, odd, mark, or space.",
                    "Invalid Serial Setting",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (row.StopBits is not (1.0 or 1.5 or 2.0))
            {
                System.Windows.MessageBox.Show(
                    this,
                    $"Serial device '{row.Name}' stop bits must be 1, 1.5, or 2.",
                    "Invalid Serial Setting",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (row.ByteSize is < 5 or > 8)
            {
                System.Windows.MessageBox.Show(
                    this,
                    $"Serial device '{row.Name}' byte size must be between 5 and 8.",
                    "Invalid Serial Setting",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }
        }

        return true;
    }
}

public sealed class SerialDeviceSettingsRow
{
    public string Name { get; set; } = "";

    public string Port { get; set; } = "";

    public int Baudrate { get; set; }

    public string Parity { get; set; } = "none";

    public double StopBits { get; set; } = 1.0;

    public int ByteSize { get; set; } = 8;

    public static SerialDeviceSettingsRow FromSettings(WorkbenchSerialDeviceSettings settings)
    {
        return new SerialDeviceSettingsRow
        {
            Name = settings.Name,
            Port = settings.Port,
            Baudrate = settings.Baudrate,
            Parity = settings.Parity,
            StopBits = settings.StopBits,
            ByteSize = settings.ByteSize
        };
    }
}
