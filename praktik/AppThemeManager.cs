using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using praktik.Models;

namespace praktik
{
    internal static class AppThemeManager
    {
        public const string DefaultBaseTheme = "Light";
        public const string DefaultAccentColor = "Brown";

        private static readonly IReadOnlyDictionary<string, AccentPaletteOption> AccentOptions =
            new Dictionary<string, AccentPaletteOption>(StringComparer.OrdinalIgnoreCase)
            {
                ["Brown"] = new AccentPaletteOption(
                    "Brown",
                    "Коричневый",
                    ColorFromHex("#8D6E63"),
                    ColorFromHex("#FFD54F")),
                ["Pink"] = new AccentPaletteOption(
                    "Pink",
                    "Розовый",
                    ColorFromHex("#EC407A"),
                    ColorFromHex("#F8BBD0")),
                ["LightBlue"] = new AccentPaletteOption(
                    "LightBlue",
                    "Голубой",
                    ColorFromHex("#03A9F4"),
                    ColorFromHex("#80DEEA"))
            };

        public static IReadOnlyList<AccentPaletteOption> AvailableAccents { get; } =
            AccentOptions.Values.ToList().AsReadOnly();

        public static string NormalizeBaseTheme(string baseTheme)
        {
            return string.Equals(baseTheme, "Dark", StringComparison.OrdinalIgnoreCase)
                ? "Dark"
                : DefaultBaseTheme;
        }

        public static bool IsDarkTheme(string baseTheme)
        {
            return string.Equals(NormalizeBaseTheme(baseTheme), "Dark", StringComparison.OrdinalIgnoreCase);
        }

        public static string ToggleBaseTheme(string baseTheme)
        {
            return IsDarkTheme(baseTheme) ? DefaultBaseTheme : "Dark";
        }

        public static string NormalizeAccent(string accentColor)
        {
            if (string.IsNullOrWhiteSpace(accentColor))
            {
                return DefaultAccentColor;
            }

            return AccentOptions.TryGetValue(accentColor.Trim(), out var option)
                ? option.Key
                : DefaultAccentColor;
        }

        public static AccentPaletteOption GetAccentOption(string accentColor)
        {
            return AccentOptions[NormalizeAccent(accentColor)];
        }

        public static void ApplyTheme(User user)
        {
            ApplyTheme(user?.PreferredTheme, user?.AccentColor);
        }

        public static void ApplyTheme(string baseTheme, string accentColor)
        {
            var normalizedBaseTheme = NormalizeBaseTheme(baseTheme);
            var accent = GetAccentOption(accentColor);
            var paletteHelper = new MaterialDesignThemes.Wpf.PaletteHelper();
            var theme = MaterialDesignThemes.Wpf.Theme.Create(
                IsDarkTheme(normalizedBaseTheme)
                    ? MaterialDesignThemes.Wpf.BaseTheme.Dark
                    : MaterialDesignThemes.Wpf.BaseTheme.Light,
                accent.PrimaryColor,
                accent.SecondaryColor);

            paletteHelper.SetTheme(theme);
        }

        private static Color ColorFromHex(string color)
        {
            return (Color)ColorConverter.ConvertFromString(color);
        }

        internal sealed class AccentPaletteOption
        {
            public AccentPaletteOption(string key, string displayName, Color primaryColor, Color secondaryColor)
            {
                Key = key;
                DisplayName = displayName;
                PrimaryColor = primaryColor;
                SecondaryColor = secondaryColor;
                PrimaryBrush = new SolidColorBrush(primaryColor);
                PrimaryBrush.Freeze();
                SecondaryBrush = new SolidColorBrush(secondaryColor);
                SecondaryBrush.Freeze();
            }

            public string Key { get; }
            public string DisplayName { get; }
            public Color PrimaryColor { get; }
            public Color SecondaryColor { get; }
            public Brush PrimaryBrush { get; }
            public Brush SecondaryBrush { get; }
        }
    }
}
