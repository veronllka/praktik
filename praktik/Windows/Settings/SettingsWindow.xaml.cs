using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using praktik.Models;
using praktik.Models.Patterns;
using praktik.Services;

namespace praktik
{
    public partial class SettingsWindow : Window
    {
        private readonly WorkPlannerFacade facade = new WorkPlannerFacade();
        private User currentUser;
        private DispatcherTimer _liveStatsTimer;
        private bool _suppressAppearancePreview;
        private bool _suppressAnimSettingsChanged;
        private int _cpuThreshold;
        private int _ramThreshold;
        private int _batteryThreshold;

        public SettingsWindow()
        {
            InitializeComponent();
            SourceInitialized += (_, __) =>
            {
                GlassBackdropService.Apply(this);
                WindowCornerService.Apply(this);
            };
            LoadFontOptions();
            LoadCurrentUser();
            UpdatePasswordValidation();
            LoadAnimationSettings();
            StartLiveStatsTimer();
            AnimationService.Instance.AnimationStateChanged += OnAnimationStateChanged;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleWindowState();
                e.Handled = true;
                return;
            }

            if (e.ButtonState != MouseButtonState.Pressed)
            {
                return;
            }

            RestoreWindowUnderCursor(e);

            try
            {
                DragMove();
                e.Handled = true;
            }
            catch (InvalidOperationException)
            {
                // DragMove can throw if the mouse button was released during routing.
            }
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            SystemCommands.MinimizeWindow(this);
            e.Handled = true;
        }

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            ToggleWindowState();
            e.Handled = true;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ToggleWindowState()
        {
            if (WindowState == WindowState.Maximized)
            {
                SystemCommands.RestoreWindow(this);
                return;
            }

            SystemCommands.MaximizeWindow(this);
        }

        private void RestoreWindowUnderCursor(MouseButtonEventArgs e)
        {
            if (WindowState != WindowState.Maximized)
            {
                return;
            }

            var mousePosition = e.GetPosition(this);
            var screenPosition = PointToScreen(mousePosition);
            var restoreWidth = RestoreBounds.Width > 0 ? RestoreBounds.Width : Width;
            var restoreHeight = RestoreBounds.Height > 0 ? RestoreBounds.Height : Height;
            var horizontalRatio = ActualWidth > 0 ? mousePosition.X / ActualWidth : 0.5;

            SystemCommands.RestoreWindow(this);
            Width = restoreWidth;
            Height = restoreHeight;
            Left = screenPosition.X - restoreWidth * horizontalRatio;
            Top = Math.Max(0, screenPosition.Y - 18);
        }

        private bool IsPasswordChangeRequested =>
            !string.IsNullOrWhiteSpace(txtNewPassword.Password) ||
            !string.IsNullOrWhiteSpace(txtConfirmPassword.Password);

        private void LoadCurrentUser()
        {
            try
            {
                if (LoginWindow.CurrentUser == null)
                {
                    throw new InvalidOperationException("Пользователь не авторизован.");
                }

                currentUser = facade.GetUserById(LoginWindow.CurrentUser.UserId) ?? LoginWindow.CurrentUser;

                txtCurrentFullName.Text = string.IsNullOrWhiteSpace(currentUser.FullName)
                    ? currentUser.Username
                    : currentUser.FullName;
                txtCurrentLogin.Text = currentUser.Username ?? "-";
                txtCurrentRole.Text = currentUser.Role ?? "-";

                var appearance = AppThemeManager.LoadAppearanceSettings();
                ApplyAppearanceSelection(
                    AppThemeManager.NormalizeBaseTheme(currentUser.PreferredTheme),
                    AppThemeManager.NormalizeAccent(currentUser.AccentColor),
                    appearance.VisualStyle,
                    appearance.FontFamily,
                    appearance.GlassOpacity);
            }
            catch (Exception ex)
            {
                btnSave.IsEnabled = false;
                MessageBox.Show(
                    $"Не удалось загрузить данные текущего пользователя: {ex.Message}",
                    "Настройки",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void LoadFontOptions()
        {
            cbFontFamily.ItemsSource = AppThemeManager.AvailableFontFamilies;
        }

        private void ApplyAppearanceSelection(
            string baseTheme,
            string accentColor,
            string visualStyle,
            string fontFamily,
            int glassOpacity)
        {
            _suppressAppearancePreview = true;
            try
            {
                rbStyleLiquid.IsChecked = !string.Equals(
                    AppThemeManager.NormalizeVisualStyle(visualStyle),
                    AppThemeManager.ClassicVisualStyle,
                    StringComparison.OrdinalIgnoreCase);
                rbStyleClassic.IsChecked = string.Equals(
                    AppThemeManager.NormalizeVisualStyle(visualStyle),
                    AppThemeManager.ClassicVisualStyle,
                    StringComparison.OrdinalIgnoreCase);

                rbLightTheme.IsChecked = !AppThemeManager.IsDarkTheme(baseTheme);
                rbDarkTheme.IsChecked = AppThemeManager.IsDarkTheme(baseTheme);

                rbAccentBrown.IsChecked = string.Equals(accentColor, "Brown", StringComparison.OrdinalIgnoreCase);
                rbAccentPink.IsChecked = string.Equals(accentColor, "Pink", StringComparison.OrdinalIgnoreCase);
                rbAccentLightBlue.IsChecked = string.Equals(accentColor, "LightBlue", StringComparison.OrdinalIgnoreCase);

                var normalizedFont = AppThemeManager.NormalizeFontFamily(fontFamily);
                var selectedFont = AppThemeManager.AvailableFontFamilies
                    .FirstOrDefault(font => string.Equals(font, normalizedFont, StringComparison.OrdinalIgnoreCase));
                cbFontFamily.SelectedItem = selectedFont ?? AppThemeManager.DefaultFontFamily;

                sldGlassOpacity.Value = AppThemeManager.NormalizeGlassOpacity(glassOpacity);
                UpdateGlassOpacityLabel();
                txtAppearanceError.Text = string.Empty;
            }
            finally
            {
                _suppressAppearancePreview = false;
            }
        }

        private string GetSelectedBaseTheme()
        {
            return rbDarkTheme.IsChecked == true ? "Dark" : AppThemeManager.DefaultBaseTheme;
        }

        private string GetSelectedAccentColor()
        {
            if (rbAccentPink.IsChecked == true)
            {
                return "Pink";
            }

            if (rbAccentLightBlue.IsChecked == true)
            {
                return "LightBlue";
            }

            return AppThemeManager.DefaultAccentColor;
        }

        private string GetSelectedVisualStyle()
        {
            return rbStyleClassic.IsChecked == true
                ? AppThemeManager.ClassicVisualStyle
                : AppThemeManager.DefaultVisualStyle;
        }

        private string GetSelectedFontFamily()
        {
            return cbFontFamily.SelectedItem as string ?? AppThemeManager.DefaultFontFamily;
        }

        private int GetSelectedGlassOpacity()
        {
            return AppThemeManager.NormalizeGlassOpacity((int)Math.Round(sldGlassOpacity.Value));
        }

        private void AppearanceOption_Checked(object sender, RoutedEventArgs e)
        {
            PreviewAppearance();
        }

        private void BaseTheme_Checked(object sender, RoutedEventArgs e)
        {
            PreviewAppearance();
        }

        private void AccentOption_Checked(object sender, RoutedEventArgs e)
        {
            PreviewAppearance();
        }

        private void FontFamily_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PreviewAppearance();
        }

        private void GlassOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateGlassOpacityLabel();
            PreviewAppearance();
        }

        private void PreviewAppearance()
        {
            if (!IsLoaded || _suppressAppearancePreview)
            {
                return;
            }

            if (!TryValidateAppearance(out var errorMessage))
            {
                txtAppearanceError.Text = errorMessage;
                return;
            }

            txtAppearanceError.Text = string.Empty;
            AppThemeManager.ApplyTheme(
                GetSelectedBaseTheme(),
                GetSelectedAccentColor(),
                GetSelectedVisualStyle(),
                GetSelectedFontFamily(),
                GetSelectedGlassOpacity());
        }

        private void UpdateGlassOpacityLabel()
        {
            if (txtGlassOpacityValue != null && sldGlassOpacity != null)
            {
                txtGlassOpacityValue.Text = $"{GetSelectedGlassOpacity()}%";
            }
        }

        private bool TryValidateAppearance(out string errorMessage)
        {
            var fontFamily = GetSelectedFontFamily();
            if (!AppThemeManager.IsFontFamilyAvailable(fontFamily))
            {
                errorMessage = "Выберите установленный шрифт из списка.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        private void PasswordFields_Changed(object sender, RoutedEventArgs e)
        {
            UpdatePasswordValidation();
        }

        private void UpdatePasswordValidation(bool forceValidation = false)
        {
            var newPassword = txtNewPassword.Password ?? string.Empty;
            var confirmPassword = txtConfirmPassword.Password ?? string.Empty;

            if (!forceValidation && !IsPasswordChangeRequested)
            {
                SetNeutralValidationState(
                    iconNewPasswordStatus,
                    txtNewPasswordHint,
                    "Оставьте поле пустым, если пароль менять не нужно.");
                SetNeutralValidationState(
                    iconConfirmPasswordStatus,
                    txtConfirmPasswordHint,
                    "Повторите новый пароль для проверки совпадения.");
                txtPasswordError.Text = string.Empty;
                return;
            }

            var isNewPasswordValid = !string.IsNullOrWhiteSpace(newPassword);
            SetValidationState(
                iconNewPasswordStatus,
                txtNewPasswordHint,
                isNewPasswordValid,
                isNewPasswordValid ? "Новый пароль заполнен." : "Введите новый пароль.");

            var isConfirmPasswordValid = !string.IsNullOrWhiteSpace(confirmPassword) &&
                                         string.Equals(newPassword, confirmPassword, StringComparison.Ordinal);
            var confirmHint = string.IsNullOrWhiteSpace(confirmPassword)
                ? "Подтвердите новый пароль."
                : isConfirmPasswordValid
                    ? "Пароли совпадают."
                    : "Пароли не совпадают.";

            SetValidationState(
                iconConfirmPasswordStatus,
                txtConfirmPasswordHint,
                isConfirmPasswordValid,
                confirmHint);

            txtPasswordError.Text = string.Empty;
        }

        private static void SetNeutralValidationState(TextBlock icon, TextBlock hint, string text)
        {
            icon.Text = "\uE91F";
            icon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9CA3AF"));
            hint.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"));
            hint.Text = text;
        }

        private static void SetValidationState(TextBlock icon, TextBlock hint, bool isValid, string text)
        {
            icon.Text = isValid ? "\uE73E" : "\uE711";
            icon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isValid ? "#2E7D32" : "#D32F2F"));
            hint.Foreground = icon.Foreground;
            hint.Text = text;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (currentUser == null)
            {
                return;
            }

            UpdatePasswordValidation(true);

            var newPassword = txtNewPassword.Password ?? string.Empty;
            var confirmPassword = txtConfirmPassword.Password ?? string.Empty;

            if (IsPasswordChangeRequested)
            {
                if (string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(confirmPassword))
                {
                    txtPasswordError.Text = "Для смены пароля заполните оба поля.";
                    return;
                }

                if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
                {
                    txtPasswordError.Text = "Новый пароль и подтверждение должны совпадать.";
                    return;
                }
            }

            var selectedBaseTheme = GetSelectedBaseTheme();
            var selectedAccentColor = GetSelectedAccentColor();
            var selectedVisualStyle = GetSelectedVisualStyle();
            var selectedFontFamily = GetSelectedFontFamily();
            var selectedGlassOpacity = GetSelectedGlassOpacity();

            if (!TryValidateAppearance(out var appearanceError))
            {
                txtAppearanceError.Text = appearanceError;
                return;
            }

            try
            {
                facade.UpdateUserSettings(
                    currentUser.UserId,
                    IsPasswordChangeRequested ? newPassword : null,
                    selectedBaseTheme,
                    selectedAccentColor);
                AppThemeManager.SaveAppearanceSettings(selectedVisualStyle, selectedFontFamily, selectedGlassOpacity);

                currentUser = facade.GetUserById(currentUser.UserId) ?? currentUser;
                currentUser.PreferredTheme = selectedBaseTheme;
                currentUser.AccentColor = selectedAccentColor;

                if (IsPasswordChangeRequested)
                {
                    currentUser.Password = newPassword;
                }

                LoginWindow.CurrentUser = currentUser;
                AppThemeManager.ApplyTheme(
                    selectedBaseTheme,
                    selectedAccentColor,
                    selectedVisualStyle,
                    selectedFontFamily,
                    selectedGlassOpacity);
                ApplyAnimationSettingsToService();

                MessageBox.Show("Настройки сохранены.", "Настройки", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                txtPasswordError.Text = $"Ошибка сохранения: {ex.Message}";
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            _liveStatsTimer?.Stop();
            AnimationService.Instance.AnimationStateChanged -= OnAnimationStateChanged;

            if (DialogResult == true) return;

            if (LoginWindow.CurrentUser != null)
                AppThemeManager.ApplyTheme(LoginWindow.CurrentUser);
        }

        // ── Animation settings ──────────────────────────────────────────────

        private void LoadAnimationSettings()
        {
            _suppressAnimSettingsChanged = true;
            var svc = AnimationService.Instance;

            chkAnimationsEnabled.IsChecked = svc.UserEnabledAnimations;
            chkAutoDisableCpu.IsChecked = svc.AutoDisableOnHighCpu;
            chkAutoDisableRam.IsChecked = svc.AutoDisableOnHighRam;
            chkAutoDisableBattery.IsChecked = svc.AutoDisableOnLowBattery;
            SetThresholdValues(svc.CpuThreshold, svc.RamThreshold, svc.BatteryThreshold);

            switch (svc.Mode)
            {
                case AnimationService.PerformanceMode.AlwaysOn:
                    segModeAlways.IsChecked = true; break;
                case AnimationService.PerformanceMode.Reduced:
                    segModeReduced.IsChecked = true; break;
                case AnimationService.PerformanceMode.Off:
                    segModeOff.IsChecked = true; break;
                default:
                    segModeAuto.IsChecked = true; break;
            }

            _suppressAnimSettingsChanged = false;
            UpdateAnimationControlsState();
            UpdateAnimStatusIndicator();
        }

        private void AnimationSettings_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressAnimSettingsChanged) return;
            PushSettingsToService();
            UpdateAnimationControlsState();
        }

        private void PerformanceMode_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressAnimSettingsChanged) return;
            PushSettingsToService();
            UpdateAnimationControlsState();
        }

        private void BtnCpuThresholdDown_Click(object sender, RoutedEventArgs e) => ChangeCpuThreshold(-5);

        private void BtnCpuThresholdUp_Click(object sender, RoutedEventArgs e) => ChangeCpuThreshold(5);

        private void BtnRamThresholdDown_Click(object sender, RoutedEventArgs e) => ChangeRamThreshold(-5);

        private void BtnRamThresholdUp_Click(object sender, RoutedEventArgs e) => ChangeRamThreshold(5);

        private void BtnBatteryThresholdDown_Click(object sender, RoutedEventArgs e) => ChangeBatteryThreshold(-5);

        private void BtnBatteryThresholdUp_Click(object sender, RoutedEventArgs e) => ChangeBatteryThreshold(5);

        private void ChangeCpuThreshold(int delta)
        {
            _cpuThreshold = Clamp(_cpuThreshold + delta, 50, 95);
            UpdateThresholdLabels();
            PushThresholdSettings();
        }

        private void ChangeRamThreshold(int delta)
        {
            _ramThreshold = Clamp(_ramThreshold + delta, 60, 95);
            UpdateThresholdLabels();
            PushThresholdSettings();
        }

        private void ChangeBatteryThreshold(int delta)
        {
            _batteryThreshold = Clamp(_batteryThreshold + delta, 5, 50);
            UpdateThresholdLabels();
            PushThresholdSettings();
        }

        private void SetThresholdValues(int cpuThreshold, int ramThreshold, int batteryThreshold)
        {
            _cpuThreshold = Clamp(cpuThreshold, 50, 95);
            _ramThreshold = Clamp(ramThreshold, 60, 95);
            _batteryThreshold = Clamp(batteryThreshold, 5, 50);
            UpdateThresholdLabels();
        }

        private void UpdateThresholdLabels()
        {
            if (runCpuThreshold != null) runCpuThreshold.Text = _cpuThreshold.ToString();
            if (runRamThreshold != null) runRamThreshold.Text = _ramThreshold.ToString();
            if (runBatteryThreshold != null) runBatteryThreshold.Text = _batteryThreshold.ToString();

            if (txtCpuThresholdValue != null) txtCpuThresholdValue.Text = $"{_cpuThreshold}%";
            if (txtRamThresholdValue != null) txtRamThresholdValue.Text = $"{_ramThreshold}%";
            if (txtBatteryThresholdValue != null) txtBatteryThresholdValue.Text = $"{_batteryThreshold}%";
        }

        private void PushThresholdSettings()
        {
            if (_suppressAnimSettingsChanged) return;
            PushSettingsToService();
            UpdateLiveStats();
        }

        private static int Clamp(int value, int min, int max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        private AnimationService.PerformanceMode GetSelectedMode()
        {
            if (segModeAlways.IsChecked == true) return AnimationService.PerformanceMode.AlwaysOn;
            if (segModeReduced.IsChecked == true) return AnimationService.PerformanceMode.Reduced;
            if (segModeOff.IsChecked == true) return AnimationService.PerformanceMode.Off;
            return AnimationService.PerformanceMode.Auto;
        }

        /// <summary>Live-apply preferences to the service so preview reflects reality.</summary>
        private void PushSettingsToService()
        {
            var svc = AnimationService.Instance;
            svc.UserEnabledAnimations = chkAnimationsEnabled.IsChecked == true;
            svc.Mode = GetSelectedMode();
            svc.AutoDisableOnHighCpu = chkAutoDisableCpu.IsChecked == true;
            svc.AutoDisableOnHighRam = chkAutoDisableRam.IsChecked == true;
            svc.AutoDisableOnLowBattery = chkAutoDisableBattery.IsChecked == true;
            svc.CpuThreshold = _cpuThreshold;
            svc.RamThreshold = _ramThreshold;
            svc.BatteryThreshold = _batteryThreshold;
            svc.EvaluateState();
        }

        private void UpdateAnimationControlsState()
        {
            bool masterOn = chkAnimationsEnabled.IsChecked == true;
            bool autoMode = segModeAuto.IsChecked == true;
            bool canTrigger = masterOn && autoMode;

            pnlCpuSettings.IsEnabled = canTrigger;
            pnlRamSettings.IsEnabled = canTrigger;
            pnlBatterySettings.IsEnabled = canTrigger;
            pnlCpuThreshold.IsEnabled = canTrigger && chkAutoDisableCpu.IsChecked == true;
            pnlRamThreshold.IsEnabled = canTrigger && chkAutoDisableRam.IsChecked == true;
            pnlBatteryThreshold.IsEnabled = canTrigger && chkAutoDisableBattery.IsChecked == true;

            UpdateAnimStatusIndicator();
        }

        private void OnAnimationStateChanged(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke((Action)UpdateAnimStatusIndicator);
        }

        private void UpdateAnimStatusIndicator()
        {
            var svc = AnimationService.Instance;

            string statusText;
            string badgeText;
            string dotStyle;
            Brush badgeFg;

            switch (svc.CurrentLevel)
            {
                case AnimationService.AnimationLevel.Full:
                    statusText = "Анимации работают в полном режиме";
                    badgeText = "Полные";
                    dotStyle = "StatusDotGreen";
                    badgeFg = TryGetBrush("AppSuccessBrush");
                    break;
                case AnimationService.AnimationLevel.Reduced:
                    statusText = "Анимации упрощены для экономии ресурсов";
                    badgeText = "Упрощённые";
                    dotStyle = "StatusDotYellow";
                    badgeFg = TryGetBrush("AppWarningBrush");
                    break;
                default:
                    statusText = "Анимации отключены";
                    badgeText = "Выключены";
                    dotStyle = "StatusDotRed";
                    badgeFg = TryGetBrush("AppDangerBrush");
                    break;
            }

            txtAnimStatus.Text = statusText;
            txtAnimReason.Text = svc.LastTriggerReason;
            txtAnimStatusBadge.Text = badgeText;
            txtAnimStatusBadge.Foreground = badgeFg;
            dotAnimStatus.Style = (Style)TryFindResource(dotStyle);
        }

        private Brush TryGetBrush(string key)
        {
            return (Brush)TryFindResource(key) ?? Brushes.Gray;
        }

        private void StartLiveStatsTimer()
        {
            _liveStatsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _liveStatsTimer.Tick += (_, __) => UpdateLiveStats();
            _liveStatsTimer.Start();
            UpdateLiveStats();
        }

        private void UpdateLiveStats()
        {
            var svc = AnimationService.Instance;

            gaugeCpu.Max = Math.Max(svc.CpuThreshold, 100);
            gaugeCpu.Value = svc.CurrentCpuUsage;
            txtLiveCpu.Text = $"{svc.CurrentCpuUsage:F0}%";
            txtCpuHint.Text = svc.CurrentCpuUsage > svc.CpuThreshold ? "перегрузка"
                            : svc.CurrentCpuUsage > svc.CpuThreshold * 0.7f ? "повышенная"
                            : "в норме";

            txtLiveRam.Text = $"{svc.CurrentRamUsage:F0}%";
            barRam.Value = svc.CurrentRamUsage;
            barRam.Foreground = svc.CurrentRamUsage > svc.RamThreshold
                ? TryGetBrush("AppDangerBrush")
                : svc.CurrentRamUsage > svc.RamThreshold * 0.85f
                    ? TryGetBrush("AppWarningBrush")
                    : TryGetBrush("AppAccentBrush");

            iconBattery.IsCharging = svc.IsCharging;
            iconBattery.LowThreshold = svc.BatteryThreshold / 100.0;

            if (svc.IsOnBattery || svc.CurrentBatteryLevel < 100)
            {
                iconBattery.Level = svc.CurrentBatteryLevel / 100.0;
                txtLiveBattery.Text = $"{svc.CurrentBatteryLevel:F0}%";
                txtBatteryHint.Text = svc.IsCharging ? "заряжается" : (svc.IsOnBattery ? "от аккумулятора" : "от сети");
            }
            else
            {
                iconBattery.Level = 1.0;
                txtLiveBattery.Text = "AC";
                txtBatteryHint.Text = "от сети";
            }

            UpdateAnimStatusIndicator();
        }

        private void BtnPreview_Click(object sender, RoutedEventArgs e)
        {
            // Trigger a burst of preview animations so the user can see the
            // current animation level in action.
            iconPreviewBell.Pulse();
            iconPreviewRefresh.IsBusy = true;

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
            timer.Tick += (_, __) =>
            {
                iconPreviewRefresh.IsBusy = false;
                timer.Stop();
            };
            timer.Start();
        }

        private void ApplyAnimationSettingsToService()
        {
            PushSettingsToService();
            AnimationService.Instance.SaveAndApply();
        }
    }
}
