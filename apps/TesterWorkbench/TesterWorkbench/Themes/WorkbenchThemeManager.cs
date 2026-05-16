using System.Windows;
using TesterWorkbench.Core.ViewModels;

namespace TesterWorkbench.Themes;

public static class WorkbenchThemeManager
{
    private const string ThemeResourcePrefix = "Themes/";
    private const string DarkThemeSource = "Themes/DarkTheme.xaml";
    private const string LightThemeSource = "Themes/LightTheme.xaml";

    public static void Apply(WorkbenchThemeMode themeMode)
    {
        Apply(themeMode, WorkbenchSystemTheme.PrefersDarkTheme());
    }

    internal static void Apply(WorkbenchThemeMode themeMode, bool systemPrefersDarkTheme)
    {
        var resources = System.Windows.Application.Current.Resources.MergedDictionaries;
        for (var index = resources.Count - 1; index >= 0; index--)
        {
            var source = resources[index].Source?.OriginalString;
            if (source is not null
                && source.StartsWith(ThemeResourcePrefix, StringComparison.OrdinalIgnoreCase)
                && !source.EndsWith("BaseWorkbenchStyles.xaml", StringComparison.OrdinalIgnoreCase))
            {
                resources.RemoveAt(index);
            }
        }

        resources.Insert(0, new ResourceDictionary
        {
            Source = new Uri(ResolveThemeSource(themeMode, systemPrefersDarkTheme), UriKind.Relative)
        });
    }

    private static string ResolveThemeSource(WorkbenchThemeMode themeMode, bool systemPrefersDarkTheme)
    {
        var resolvedTheme = WorkbenchThemeResolver.Resolve(themeMode, systemPrefersDarkTheme);
        return resolvedTheme switch
        {
            ResolvedWorkbenchTheme.Light => LightThemeSource,
            ResolvedWorkbenchTheme.Dark => DarkThemeSource,
            _ => DarkThemeSource
        };
    }
}
