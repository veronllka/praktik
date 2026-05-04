using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using praktik.Models;
using praktik.Properties;

namespace praktik
{
    internal static class AppThemeManager
    {
        public const string DefaultBaseTheme = "Light";
        public const string DefaultAccentColor = "LightBlue";
        public const string DefaultVisualStyle = "Liquid";
        public const string ClassicVisualStyle = "Classic";
        public const string DefaultFontFamily = "Segoe UI";
        public const int DefaultGlassOpacity = 34;

        private const int MinGlassOpacity = 25;
        private const int MaxGlassOpacity = 92;

        private static readonly IReadOnlyDictionary<string, AccentPaletteOption> AccentOptions =
            new Dictionary<string, AccentPaletteOption>(StringComparer.OrdinalIgnoreCase)
            {
                ["Brown"] = new AccentPaletteOption(
                    "Brown",
                    "Синий",
                    ColorFromHex("#5E8DF4"),
                    ColorFromHex("#D7E5FF")),
                ["Pink"] = new AccentPaletteOption(
                    "Pink",
                    "Стальной",
                    ColorFromHex("#5E8DF4"),
                    ColorFromHex("#D7E5FF")),
                ["LightBlue"] = new AccentPaletteOption(
                    "LightBlue",
                    "Прозрачный",
                    ColorFromHex("#52627A"),
                    ColorFromHex("#34FFFFFF"))
            };

        public static IReadOnlyList<AccentPaletteOption> AvailableAccents { get; } =
            AccentOptions.Values.ToList().AsReadOnly();

        public static IReadOnlyList<string> AvailableFontFamilies { get; } = LoadFontFamilies();

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

            var normalized = accentColor.Trim();
            if (string.Equals(normalized, "Brown", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "Pink", StringComparison.OrdinalIgnoreCase))
            {
                return DefaultAccentColor;
            }

            return AccentOptions.TryGetValue(normalized, out var option)
                ? option.Key
                : DefaultAccentColor;
        }

        public static AccentPaletteOption GetAccentOption(string accentColor)
        {
            return AccentOptions[NormalizeAccent(accentColor)];
        }

        public static string NormalizeVisualStyle(string visualStyle)
        {
            return string.Equals(visualStyle, ClassicVisualStyle, StringComparison.OrdinalIgnoreCase)
                ? ClassicVisualStyle
                : DefaultVisualStyle;
        }

        public static string NormalizeFontFamily(string fontFamily)
        {
            return string.IsNullOrWhiteSpace(fontFamily)
                ? DefaultFontFamily
                : fontFamily.Trim();
        }

        public static bool IsFontFamilyAvailable(string fontFamily)
        {
            var normalized = NormalizeFontFamily(fontFamily);
            return AvailableFontFamilies.Count == 0 ||
                   AvailableFontFamilies.Any(font => string.Equals(font, normalized, StringComparison.OrdinalIgnoreCase));
        }

        public static int NormalizeGlassOpacity(int glassOpacity)
        {
            return Math.Max(MinGlassOpacity, Math.Min(MaxGlassOpacity, glassOpacity));
        }

        public static AppAppearanceSettings LoadAppearanceSettings()
        {
            var settings = Settings.Default;
            return new AppAppearanceSettings(
                NormalizeVisualStyle(settings.UiStyle),
                NormalizeFontFamily(settings.UiFontFamily),
                NormalizeGlassOpacity(settings.GlassOpacity));
        }

        public static void SaveAppearanceSettings(string visualStyle, string fontFamily, int glassOpacity)
        {
            var settings = Settings.Default;
            settings.UiStyle = NormalizeVisualStyle(visualStyle);
            settings.UiFontFamily = NormalizeFontFamily(fontFamily);
            settings.GlassOpacity = NormalizeGlassOpacity(glassOpacity);
            settings.Save();
        }

        public static void ApplyTheme(User user)
        {
            ApplyTheme(user?.PreferredTheme, user?.AccentColor);
        }

        public static void ApplyTheme(string baseTheme, string accentColor)
        {
            var appearance = LoadAppearanceSettings();
            ApplyTheme(baseTheme, accentColor, appearance.VisualStyle, appearance.FontFamily, appearance.GlassOpacity);
        }

        public static void ApplyTheme(
            string baseTheme,
            string accentColor,
            string visualStyle,
            string fontFamily,
            int glassOpacity)
        {
            var normalizedBaseTheme = NormalizeBaseTheme(baseTheme);
            var accent = GetAccentOption(accentColor);
            ApplyCustomPalette(
                normalizedBaseTheme,
                accent,
                NormalizeVisualStyle(visualStyle),
                NormalizeFontFamily(fontFamily),
                NormalizeGlassOpacity(glassOpacity));
        }

        public static SolidColorBrush ResolveBrush(string resourceKey, string fallbackHex)
        {
            if (Application.Current?.Resources[resourceKey] is SolidColorBrush solidBrush)
            {
                return solidBrush;
            }

            return CreateThemeBrush(ColorFromHex(fallbackHex));
        }

        private static void ApplyCustomPalette(
            string baseTheme,
            AccentPaletteOption accent,
            string visualStyle,
            string fontFamily,
            int glassOpacity)
        {
            var resources = Application.Current?.Resources;
            if (resources == null)
            {
                return;
            }

            bool isDark = IsDarkTheme(baseTheme);
            bool isClassic = string.Equals(visualStyle, ClassicVisualStyle, StringComparison.OrdinalIgnoreCase);
            var glassAmount = glassOpacity / 100d;

            var appBackground = isDark
                ? (isClassic ? ColorFromHex("#10151D") : ColorFromHex("#121824"))
                : (isClassic ? ColorFromHex("#F4F7FB") : ColorFromHex("#DFF0FF"));
            var navigation = isClassic
                ? (isDark ? ColorFromHex("#F018202B") : ColorFromHex("#FFFFFFFF"))
                : WithAlpha(isDark ? ColorFromHex("#1A2130") : ColorFromHex("#C8DFFF"), glassAmount + 0.18);
            var surface = isClassic
                ? (isDark ? ColorFromHex("#FF18202B") : ColorFromHex("#FFFFFFFF"))
                : WithAlpha(isDark ? ColorFromHex("#1E2635") : ColorFromHex("#CCDEFF"), glassAmount + 0.24);
            var surfaceAlt = isClassic
                ? (isDark ? ColorFromHex("#FF202A36") : ColorFromHex("#FFF7F9FC"))
                : WithAlpha(isDark ? ColorFromHex("#273142") : ColorFromHex("#C6D8FF"), glassAmount + 0.16);
            var panel = isClassic
                ? (isDark ? ColorFromHex("#FF222B36") : ColorFromHex("#FFF0F4F9"))
                : WithAlpha(isDark ? ColorFromHex("#313C52") : ColorFromHex("#BFCEFF"), glassAmount + 0.16);
            var glass = isClassic
                ? surfaceAlt
                : WithAlpha(isDark ? ColorFromHex("#242D3D") : ColorFromHex("#C5D8FF"), glassAmount + 0.10);
            var glassStrong = isClassic
                ? surface
                : WithAlpha(isDark ? ColorFromHex("#222B3A") : ColorFromHex("#CADEFD"), glassAmount + 0.22);
            var glassSoft = isClassic
                ? (isDark ? ColorFromHex("#FF1B2330") : ColorFromHex("#FFF8FAFE"))
                : WithAlpha(isDark ? ColorFromHex("#2E384A") : ColorFromHex("#DBF0FF"), glassAmount + 0.06);
            var glassBorder = isClassic
                ? (isDark ? ColorFromHex("#FF343E4C") : ColorFromHex("#FFD9E1EC"))
                : WithAlpha(Colors.White, isDark ? 0.28 : 0.72);
            var ambientCircle = isDark ? ColorFromHex("#6B5F8BFF") : ColorFromHex("#9EA8FFFF");
            var ambientCircleAlt = isDark ? ColorFromHex("#5068D8FF") : ColorFromHex("#72C8F8FF");
            var wave = isDark ? ColorFromHex("#526B7CFF") : ColorFromHex("#88A4E8FF");
            var input = isClassic
                ? (isDark ? ColorFromHex("#FF111720") : ColorFromHex("#FFFFFFFF"))
                : WithAlpha(isDark ? ColorFromHex("#20252D") : ColorFromHex("#FFFFFF"), glassAmount - 0.04);
            var border = isDark ? ColorFromHex("#343C49") : ColorFromHex("#D8E6F5");
            var text = isDark ? ColorFromHex("#F0F4FF") : ColorFromHex("#18223A");
            var mutedText = isDark ? ColorFromHex("#A8B3C5") : ColorFromHex("#4A5878");
            var subtleText = isDark ? ColorFromHex("#7A8799") : ColorFromHex("#6B7899");
            var buttonDark = isDark ? ColorFromHex("#151A21") : ColorFromHex("#FFFFFF");
            var buttonDarkHover = isDark ? ColorFromHex("#1D232C") : ColorFromHex("#F4F5FB");
            bool isGlassAccent = string.Equals(accent.Key, DefaultAccentColor, StringComparison.OrdinalIgnoreCase);
            var accentBrush = isGlassAccent
                ? (isDark ? ColorFromHex("#C8D4E5") : ColorFromHex("#52627A"))
                : accent.PrimaryColor;
            var selection = isGlassAccent
                ? WithAlpha(Colors.White, isDark ? 0.14 : 0.34)
                : Blend(surfaceAlt, accent.PrimaryColor, isDark ? 0.32 : 0.13);
            var accentSoft = isGlassAccent
                ? WithAlpha(Colors.White, isDark ? 0.12 : 0.28)
                : Blend(surfaceAlt, accent.PrimaryColor, isDark ? 0.18 : 0.12);
            var accentHover = isGlassAccent
                ? (isDark ? ColorFromHex("#E8EEF8") : ColorFromHex("#334155"))
                : (isDark
                    ? Blend(accent.PrimaryColor, Colors.White, 0.12)
                    : Blend(accent.PrimaryColor, Colors.Black, 0.08));
            var primaryButton = isGlassAccent
                ? WithAlpha(Colors.White, isDark ? 0.18 : 0.36)
                : accent.PrimaryColor;
            var primaryButtonHover = isGlassAccent
                ? WithAlpha(Colors.White, isDark ? 0.26 : 0.52)
                : accentHover;
            var accentText = isGlassAccent ? text : Colors.White;

            SetGradientResource(
                resources,
                "AppBackgroundGradientBrush",
                isDark ? (isClassic ? ColorFromHex("#10151D") : ColorFromHex("#151B26")) : (isClassic ? ColorFromHex("#F8FAFC") : ColorFromHex("#D8EEFF")),
                isDark ? (isClassic ? ColorFromHex("#18202B") : ColorFromHex("#202A3D")) : (isClassic ? ColorFromHex("#EEF2F7") : ColorFromHex("#C8DAFF")),
                isDark ? (isClassic ? ColorFromHex("#121923") : ColorFromHex("#2A2440")) : (isClassic ? ColorFromHex("#E8EDF4") : ColorFromHex("#D8D0FF")));

            resources["AppFontFamily"] = new FontFamily(fontFamily);
            SetCornerRadiusResource(resources, "AppControlCornerRadius", isClassic ? 9 : 14);
            SetCornerRadiusResource(resources, "AppInputCornerRadius", isClassic ? 8 : 12);
            SetCornerRadiusResource(resources, "AppCardCornerRadius", isClassic ? 10 : 16);
            SetCornerRadiusResource(resources, "AppPopupCornerRadius", isClassic ? 10 : 14);
            SetBrushResource(resources, "AppBackgroundBrush", appBackground);
            SetBrushResource(resources, "AppNavigationBrush", navigation);
            SetBrushResource(resources, "AppSurfaceBrush", surface);
            SetBrushResource(resources, "AppSurfaceAltBrush", surfaceAlt);
            SetBrushResource(resources, "AppPanelBrush", panel);
            SetBrushResource(resources, "AppGlassBrush", glass);
            SetBrushResource(resources, "AppGlassStrongBrush", glassStrong);
            SetBrushResource(resources, "AppGlassSoftBrush", glassSoft);
            SetBrushResource(resources, "AppGlassBorderBrush", glassBorder);
            SetBrushResource(resources, "AppAmbientCircleBrush", ambientCircle);
            SetBrushResource(resources, "AppAmbientCircleAltBrush", ambientCircleAlt);
            SetBrushResource(resources, "AppWaveBrush", wave);
            SetBrushResource(resources, "AppInputBackgroundBrush", input);
            SetBrushResource(resources, "AppBorderBrush", border);
            SetBrushResource(resources, "AppTextBrush", text);
            SetBrushResource(resources, "AppMutedTextBrush", mutedText);
            SetBrushResource(resources, "AppSubtleTextBrush", subtleText);
            SetBrushResource(resources, "AppAccentBrush", accentBrush);
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

            SetBrushResource(resources, "AppChartBrush1", isGlassAccent ? ColorFromHex("#5E8DF4") : accent.PrimaryColor);
            SetBrushResource(resources, "AppChartBrush2", isGlassAccent ? ColorFromHex("#63B38C") : Blend(accent.PrimaryColor, accent.SecondaryColor, 0.45));
            SetBrushResource(resources, "AppChartBrush3", isGlassAccent ? ColorFromHex("#F0B45A") : Blend(accent.PrimaryColor, Colors.White, 0.18));
            SetBrushResource(resources, "AppChartBrush4", isGlassAccent ? ColorFromHex("#E06A6A") : Blend(accent.PrimaryColor, Colors.Black, 0.18));
            SetBrushResource(resources, "AppChartBrush5", isGlassAccent ? ColorFromHex("#9AA7B8") : Blend(surfaceAlt, accent.SecondaryColor, 0.52));
            SetBrushResource(resources, "AppChartBrush6", isGlassAccent ? ColorFromHex("#6C7A92") : Blend(accent.PrimaryColor, text, 0.28));

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

        private static IReadOnlyList<string> LoadFontFamilies()
        {
            try
            {
                return Fonts.SystemFontFamilies
                    .Select(font => font.Source)
                    .Where(font => !string.IsNullOrWhiteSpace(font))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(font => font, StringComparer.CurrentCultureIgnoreCase)
                    .ToList()
                    .AsReadOnly();
            }
            catch
            {
                return new[] { DefaultFontFamily }.ToList().AsReadOnly();
            }
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

        private static void SetCornerRadiusResource(ResourceDictionary resources, string key, double radius)
        {
            resources[key] = new CornerRadius(radius);
        }

        private static void SetGradientResource(ResourceDictionary resources, string key, Color start, Color middle, Color end)
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1)
            };

            brush.GradientStops.Add(new GradientStop(start, 0));
            brush.GradientStops.Add(new GradientStop(middle, 0.47));
            brush.GradientStops.Add(new GradientStop(end, 1));
            brush.Freeze();

            resources[key] = brush;
        }

        private static SolidColorBrush CreateThemeBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private static Color WithAlpha(Color color, double opacity)
        {
            opacity = Math.Max(0d, Math.Min(1d, opacity));
            return Color.FromArgb(
                (byte)Math.Round(255 * opacity),
                color.R,
                color.G,
                color.B);
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

        internal sealed class AppAppearanceSettings
        {
            public AppAppearanceSettings(string visualStyle, string fontFamily, int glassOpacity)
            {
                VisualStyle = visualStyle;
                FontFamily = fontFamily;
                GlassOpacity = glassOpacity;
            }

            public string VisualStyle { get; }
            public string FontFamily { get; }
            public int GlassOpacity { get; }
        }
    }
}
