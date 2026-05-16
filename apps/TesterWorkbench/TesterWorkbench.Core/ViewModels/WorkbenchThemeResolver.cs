namespace TesterWorkbench.Core.ViewModels;

public static class WorkbenchThemeResolver
{
    public static ResolvedWorkbenchTheme Resolve(
        WorkbenchThemeMode themeMode,
        bool systemPrefersDarkTheme)
    {
        return themeMode switch
        {
            WorkbenchThemeMode.Light => ResolvedWorkbenchTheme.Light,
            WorkbenchThemeMode.Dark => ResolvedWorkbenchTheme.Dark,
            WorkbenchThemeMode.System => systemPrefersDarkTheme
                ? ResolvedWorkbenchTheme.Dark
                : ResolvedWorkbenchTheme.Light,
            _ => ResolvedWorkbenchTheme.Dark
        };
    }
}
