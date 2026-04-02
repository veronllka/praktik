using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
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
            ApplyCustomPalette(normalizedBaseTheme, accent);
        }

        public static SolidColorBrush ResolveBrush(string resourceKey, string fallbackHex)
        {
            if (Application.Current?.Resources[resourceKey] is SolidColorBrush solidBrush)
            {
                return solidBrush;
            }

            return CreateThemeBrush(ColorFromHex(fallbackHex));
        }

        private static void ApplyCustomPalette(string baseTheme, AccentPaletteOption accent)
        {
            var resources = Application.Current?.Resources;
            if (resources == null)
            {
                return;
            }

            bool isDark = IsDarkTheme(baseTheme);

            var appBackground = isDark ? ColorFromHex("#111318") : ColorFromHex("#F3F6FA");
            var navigation = isDark ? ColorFromHex("#191D24") : ColorFromHex("#FCFDFE");
            var surface = isDark ? ColorFromHex("#1C2027") : ColorFromHex("#FFFFFF");
            var surfaceAlt = isDark ? ColorFromHex("#242A33") : ColorFromHex("#F6F8FB");
            var panel = isDark ? ColorFromHex("#2A313C") : ColorFromHex("#ECF1F7");
            var input = isDark ? ColorFromHex("#20252D") : ColorFromHex("#FFFFFF");
            var border = isDark ? ColorFromHex("#343C49") : ColorFromHex("#D5DDE7");
            var text = isDark ? ColorFromHex("#F5F7FB") : ColorFromHex("#18202B");
            var mutedText = isDark ? ColorFromHex("#ADB6C3") : ColorFromHex("#5B6573");
            var subtleText = isDark ? ColorFromHex("#7E8896") : ColorFromHex("#7D8793");
            var buttonDark = isDark ? ColorFromHex("#151A21") : ColorFromHex("#F7F9FC");
            var buttonDarkHover = isDark ? ColorFromHex("#1D232C") : ColorFromHex("#EDF2F7");
            var selection = Blend(surfaceAlt, accent.PrimaryColor, isDark ? 0.32 : 0.16);
            var accentSoft = Blend(surfaceAlt, accent.PrimaryColor, isDark ? 0.18 : 0.09);
            var accentHover = Blend(accent.PrimaryColor, Colors.White, isDark ? 0.12 : 0.05);
            var primaryButton = isDark
                ? accent.PrimaryColor
                : Blend(surface, accent.PrimaryColor, 0.18);
            var primaryButtonHover = isDark
                ? accentHover
                : Blend(surfaceAlt, accent.PrimaryColor, 0.28);
            var accentText = isDark ? Colors.White : text;

            SetBrushResource(resources, "AppBackgroundBrush", appBackground);
            SetBrushResource(resources, "AppNavigationBrush", navigation);
            SetBrushResource(resources, "AppSurfaceBrush", surface);
            SetBrushResource(resources, "AppSurfaceAltBrush", surfaceAlt);
            SetBrushResource(resources, "AppPanelBrush", panel);
            SetBrushResource(resources, "AppInputBackgroundBrush", input);
            SetBrushResource(resources, "AppBorderBrush", border);
            SetBrushResource(resources, "AppTextBrush", text);
            SetBrushResource(resources, "AppMutedTextBrush", mutedText);
            SetBrushResource(resources, "AppSubtleTextBrush", subtleText);
            SetBrushResource(resources, "AppAccentBrush", accent.PrimaryColor);
            SetBrushResource(resources, "AppAccentHoverBrush", accentHover);
            SetBrushResource(resources, "AppAccentSoftBrush", accentSoft);
            SetBrushResource(resources, "AppAccentTextBrush", accentText);
            SetBrushResource(resources, "AppPrimaryButtonBrush", primaryButton);
            SetBrushResource(resources, "AppPrimaryButtonHoverBrush", primaryButtonHover);
            SetBrushResource(resources, "AppPrimaryButtonTextBrush", accentText);
            SetBrushResource(resources, "AppButtonDarkBrush", buttonDark);
            SetBrushResource(resources, "AppButtonDarkHoverBrush", buttonDarkHover);
            SetBrushResource(resources, "AppSelectionBrush", selection);
            SetBrushResource(resources, "AppSelectionTextBrush", text);
            SetBrushResource(resources, "AppDangerBrush", isDark ? ColorFromHex("#FF6B6B") : ColorFromHex("#D9485C"));
            SetBrushResource(resources, "AppSuccessBrush", isDark ? ColorFromHex("#34C759") : ColorFromHex("#1E9F52"));
            SetBrushResource(resources, "AppWarningBrush", isDark ? ColorFromHex("#F5A524") : ColorFromHex("#C98512"));

            SetBrushResource(resources, "AppChartBrush1", accent.PrimaryColor);
            SetBrushResource(resources, "AppChartBrush2", Blend(accent.PrimaryColor, accent.SecondaryColor, 0.45));
            SetBrushResource(resources, "AppChartBrush3", Blend(accent.PrimaryColor, Colors.White, 0.18));
            SetBrushResource(resources, "AppChartBrush4", Blend(accent.PrimaryColor, Colors.Black, 0.18));
            SetBrushResource(resources, "AppChartBrush5", Blend(surfaceAlt, accent.SecondaryColor, 0.52));
            SetBrushResource(resources, "AppChartBrush6", Blend(accent.PrimaryColor, text, 0.28));

            SetBrushResource(resources, "MaterialDesignPaper", surface);
            SetBrushResource(resources, "MaterialDesignCardBackground", surface);
            SetBrushResource(resources, "MaterialDesignBody", text);
            SetBrushResource(resources, "MaterialDesignDivider", border);
            SetBrushResource(resources, "MaterialDesignToolBackground", panel);

            SetBrushResource(resources, SystemColors.WindowBrushKey, surface);
            SetBrushResource(resources, SystemColors.WindowTextBrushKey, text);
            SetBrushResource(resources, SystemColors.ControlBrushKey, surface);
            SetBrushResource(resources, SystemColors.ControlTextBrushKey, text);
            SetBrushResource(resources, SystemColors.HighlightBrushKey, selection);
            SetBrushResource(resources, SystemColors.HighlightTextBrushKey, text);
            SetBrushResource(resources, SystemColors.InactiveSelectionHighlightBrushKey, surfaceAlt);
            SetBrushResource(resources, SystemColors.InactiveSelectionHighlightTextBrushKey, text);
            SetBrushResource(resources, SystemColors.MenuBrushKey, surface);
            SetBrushResource(resources, SystemColors.MenuTextBrushKey, text);
            SetBrushResource(resources, SystemColors.GrayTextBrushKey, subtleText);
        }

        private static Color ColorFromHex(string color)
        {
            return (Color)ColorConverter.ConvertFromString(color);
        }

        private static void SetBrushResource(ResourceDictionary resources, string key, Color color)
        {
            resources[key] = CreateThemeBrush(color);
        }

        private static void SetBrushResource(ResourceDictionary resources, object key, Color color)
        {
            resources[key] = CreateThemeBrush(color);
        }

        private static SolidColorBrush CreateThemeBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private static Color Blend(Color background, Color foreground, double amount)
        {
            amount = Math.Max(0d, Math.Min(1d, amount));

            return Color.FromRgb(
                (byte)Math.Round(background.R + ((foreground.R - background.R) * amount)),
                (byte)Math.Round(background.G + ((foreground.G - background.G) * amount)),
                (byte)Math.Round(background.B + ((foreground.B - background.B) * amount)));
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
