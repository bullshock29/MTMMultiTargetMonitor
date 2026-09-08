using System.Windows;
using Microsoft.Win32;

namespace MultiTargetMonitor.Services;

public enum AppTheme
{
    Light,
    Dark,
    System,
}

/// <summary>
/// Swaps the active colour palette resource dictionary at runtime and, in <see cref="AppTheme.System"/>
/// mode, follows the Windows app theme.
/// </summary>
public static class ThemeManager
{
    private static bool _initialized;

    public static AppTheme Current { get; private set; } = AppTheme.System;

    /// <summary>Raised after the effective palette changes.</summary>
    public static event Action? Changed;

    public static bool IsDarkEffective =>
        Current == AppTheme.Dark || (Current == AppTheme.System && SystemPrefersDark());

    public static void Initialize(AppTheme theme)
    {
        Current = theme;

        if (!_initialized)
        {
            _initialized = true;
            SystemEvents.UserPreferenceChanged += (_, e) =>
            {
                if (e.Category == UserPreferenceCategory.General && Current == AppTheme.System)
                    Application.Current?.Dispatcher.Invoke(Apply);
            };
        }

        Apply();
    }

    public static void Set(AppTheme theme)
    {
        Current = theme;
        Apply();
    }

    public static AppTheme Parse(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "light" => AppTheme.Light,
        "dark" => AppTheme.Dark,
        _ => AppTheme.System,
    };

    private static void Apply()
    {
        if (Application.Current is null) return;

        string name = IsDarkEffective ? "Dark" : "Light";
        var uri = new Uri($"pack://application:,,,/Themes/Palette.{name}.xaml", UriKind.Absolute);

        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var existing = dictionaries.FirstOrDefault(
            d => d.Source is not null && d.Source.OriginalString.Contains("Palette.", StringComparison.OrdinalIgnoreCase));
        var replacement = new ResourceDictionary { Source = uri };

        if (existing is not null)
            dictionaries[dictionaries.IndexOf(existing)] = replacement;
        else
            dictionaries.Add(replacement);

        Changed?.Invoke();
    }

    private static bool SystemPrefersDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch
        {
            return false;
        }
    }
}
