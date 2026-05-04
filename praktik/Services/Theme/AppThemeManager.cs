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
                    "Фиолетовый",
                    ColorFromHex("#7567F5"),
                    ColorFromHex("#C9C4FF")),
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
                : WithAlpha(isDark ? ColorFromHex("#121A29") : ColorFromHex("#C8DFFF"), glassAmount + 0.34);
            var surface = isClassic
                ? (isDark ? ColorFromHex("#FF18202B") : ColorFromHex("#FFFFFFFF"))
                : WithAlpha(isDark ? ColorFromHex("#172033") : ColorFromHex("#CCDEFF"), glassAmount + 0.36);
            var surfaceAlt = isClassic
                ? (isDark ? ColorFromHex("#FF202A36") : ColorFromHex("#FFF7F9FC"))
                : WithAlpha(isDark ? ColorFromHex("#1E2A40") : ColorFromHex("#C6D8FF"), glassAmount + 0.30);
            var panel = isClassic
                ? (isDark ? ColorFromHex("#FF222B36") : ColorFromHex("#FFF0F4F9"))
                : WithAlpha(isDark ? ColorFromHex("#24324B") : ColorFromHex("#BFCEFF"), glassAmount + 0.26);
            var glass = isClassic
                ? surfaceAlt
                : WithAlpha(isDark ? ColorFromHex("#182238") : ColorFromHex("#C5D8FF"), glassAmount + 0.24);
            var glassStrong = isClassic
                ? surface
                : WithAlpha(isDark ? ColorFromHex("#20304A") : ColorFromHex("#CADEFD"), glassAmount + 0.36);
            var glassSoft = isClassic
                ? (isDark ? ColorFromHex("#FF1B2330") : ColorFromHex("#FFF8FAFE"))
                : WithAlpha(isDark ? ColorFromHex("#22314A") : ColorFromHex("#DBF0FF"), glassAmount + 0.18);
            var glassBorder = isClassic
                ? (isDark ? ColorFromHex("#FF343E4C") : ColorFromHex("#FFD9E1EC"))
                : WithAlpha(Colors.White, isDark ? 0.40 : 0.72);
            var ambientCircle = isDark ? ColorFromHex("#5A76B6FF") : ColorFromHex("#9EA8FFFF");
            var ambientCircleAlt = isDark ? ColorFromHex("#4066D8FF") : ColorFromHex("#72C8F8FF");
            var wave = isDark ? ColorFromHex("#405B86FF") : ColorFromHex("#88A4E8FF");
            var input = isClassic
                ? (isDark ? ColorFromHex("#FF111720") : ColorFromHex("#FFFFFFFF"))
                : WithAlpha(isDark ? ColorFromHex("#20252D") : ColorFromHex("#FFFFFF"), glassAmount - 0.04);
            var border = isDark ? ColorFromHex("#6F7F98") : ColorFromHex("#D8E6F5");
            var text = isDark ? ColorFromHex("#F8FBFF") : ColorFromHex("#18223A");
            var mutedText = isDark ? ColorFromHex("#D6DEEC") : ColorFromHex("#4A5878");
            var subtleText = isDark ? ColorFromHex("#AEB9CB") : ColorFromHex("#6B7899");
            var buttonDark = isDark ? ColorFromHex("#151A21") : ColorFromHex("#FFFFFF");
            var buttonDarkHover = isDark ? ColorFromHex("#1D232C") : ColorFromHex("#F4F5FB");
            var accentBrush = accent.PrimaryColor;
            var selection = Blend(surfaceAlt, accent.PrimaryColor, isDark ? 0.38 : 0.18);
            var accentSoft = Blend(surfaceAlt, accent.SecondaryColor, isDark ? 0.28 : 0.22);
            var accentHover = isDark
                ? Blend(accent.PrimaryColor, Colors.White, 0.16)
                : Blend(accent.PrimaryColor, Colors.Black, 0.08);
            var primaryButton = accent.PrimaryColor;
            var primaryButtonHover = accentHover;
            var accentText = Colors.White;

            SetGradientResource(
                resources,
                "AppBackgroundGradientBrush",
                isDark ? (isClassic ? ColorFromHex("#10151D") : ColorFromHex("#0F1724")) : (isClassic ? ColorFromHex("#F8FAFC") : ColorFromHex("#D8EEFF")),
                isDark ? (isClassic ? ColorFromHex("#18202B") : ColorFromHex("#172338")) : (isClassic ? ColorFromHex("#EEF2F7") : ColorFromHex("#C8DAFF")),
                isDark ? (isClassic ? ColorFromHex("#121923") : ColorFromHex("#211B35")) : (isClassic ? ColorFromHex("#E8EDF4") : ColorFromHex("#D8D0FF")));

            SetGradientResource(
                resources,
                "GlassPrimaryGradientBrush",
                accent.PrimaryColor,
                Blend(accent.PrimaryColor, accent.SecondaryColor, 0.24),
                Blend(accent.PrimaryColor, Colors.White, isDark ? 0.10 : 0.18));
            SetGradientResource(
                resources,
                "GlassPrimaryHoverBrush",
                accentHover,
                Blend(accentHover, accent.SecondaryColor, 0.20),
                Blend(accentHover, Colors.White, isDark ? 0.10 : 0.16));
            SetGradientResource(
                resources,
                "GlassShellPanelBrush",
                isDark ? ColorFromHex("#A8121A29") : ColorFromHex("#72D8F0FF"),
                isDark ? ColorFromHex("#96182438") : ColorFromHex("#5AC8E8FF"),
                isDark ? ColorFromHex("#8420324C") : ColorFromHex("#4EC4EAFF"));
            SetGradientResource(
                resources,
                "GlassCardBrush",
                isDark ? ColorFromHex("#A8172235") : ColorFromHex("#78EEF4FF"),
                isDark ? ColorFromHex("#9022324B") : ColorFromHex("#58E6EFFF"),
                isDark ? ColorFromHex("#842A2443") : ColorFromHex("#50E2DCFF"));
            SetGradientResource(
                resources,
                "GlassIconTileBrush",
                isDark ? ColorFromHex("#B9223048") : ColorFromHex("#70D8EEFF"),
                isDark ? ColorFromHex("#9A304567") : ColorFromHex("#48CCDFFF"),
                isDark ? ColorFromHex("#8A334D72") : ColorFromHex("#48CCDFFF"));

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
            SetBrushResource(resources, "GlassShellChromeBrush", isDark ? ColorFromHex("#7A20304A") : ColorFromHex("#52D8F0FF"));
            SetBrushResource(resources, "GlassShellChromeHoverBrush", isDark ? ColorFromHex("#A02F4263") : ColorFromHex("#72DEEEFF"));
            SetBrushResource(resources, "GlassShellBorderBrush", isDark ? ColorFromHex("#76FFFFFF") : ColorFromHex("#98FFFFFF"));
            SetBrushResource(resources, "GlassTableRowBrush", isDark ? ColorFromHex("#2AFFFFFF") : ColorFromHex("#18FFFFFF"));
            SetBrushResource(resources, "GlassTableRowHoverBrush", isDark ? ColorFromHex("#3A8FB3FF") : ColorFromHex("#30FFFFFF"));
            SetBrushResource(resources, "GlassTableSelectedBrush", isDark ? ColorFromHex("#4A9AC2FF") : ColorFromHex("#38DCEBFF"));

            SetBrushResource(resources, "AppChartBrush1", accent.PrimaryColor);
            SetBrushResource(resources, "AppChartBrush2", Blend(accent.PrimaryColor, accent.SecondaryColor, 0.45));
            SetBrushResource(resources, "AppChartBrush3", Blend(accent.PrimaryColor, Colors.White, 0.18));
            SetBrushResource(resources, "AppChartBrush4", Blend(accent.PrimaryColor, Colors.Black, 0.18));
            SetBrushResource(resources, "AppChartBrush5", Blend(surfaceAlt, accent.SecondaryColor, 0.52));
            SetBrushResource(resources, "AppChartBrush6", Blend(accent.PrimaryColor, text, 0.28));

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
