using Microsoft.Win32;

namespace TesterWorkbench.Themes;

internal static class WorkbenchSystemTheme
{
    public static bool PrefersDarkTheme()
    {
        if (!OperatingSystem.IsWindows())
        {
            return true;
        }

        using var personalizationKey = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        var appsUseLightTheme = personalizationKey?.GetValue("AppsUseLightTheme");
        return appsUseLightTheme is int intValue
            ? intValue == 0
            : true;
    }
}
