using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
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
        private bool _suppressAnimSettingsChanged;

        public SettingsWindow()
        {
            InitializeComponent();
            LoadCurrentUser();
            UpdatePasswordValidation();
            LoadAnimationSettings();
            StartLiveStatsTimer();
            AnimationService.Instance.AnimationStateChanged += OnAnimationStateChanged;
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

                ApplyAppearanceSelection(
                    AppThemeManager.NormalizeBaseTheme(currentUser.PreferredTheme),
                    AppThemeManager.NormalizeAccent(currentUser.AccentColor));
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

        private void ApplyAppearanceSelection(string baseTheme, string accentColor)
        {
            rbLightTheme.IsChecked = !AppThemeManager.IsDarkTheme(baseTheme);
            rbDarkTheme.IsChecked = AppThemeManager.IsDarkTheme(baseTheme);

            rbAccentBrown.IsChecked = string.Equals(accentColor, "Brown", StringComparison.OrdinalIgnoreCase);
            rbAccentPink.IsChecked = string.Equals(accentColor, "Pink", StringComparison.OrdinalIgnoreCase);
            rbAccentLightBlue.IsChecked = string.Equals(accentColor, "LightBlue", StringComparison.OrdinalIgnoreCase);
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

        private void BaseTheme_Checked(object sender, RoutedEventArgs e)
        {
            PreviewAppearance();
        }

        private void AccentOption_Checked(object sender, RoutedEventArgs e)
        {
            PreviewAppearance();
        }

        private void PreviewAppearance()
        {
            if (!IsLoaded)
            {
                return;
            }

            AppThemeManager.ApplyTheme(GetSelectedBaseTheme(), GetSelectedAccentColor());
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

        private static void SetNeutralValidationState(dynamic icon, TextBlock hint, string text)
        {
            icon.Kind = MaterialDesignThemes.Wpf.PackIconKind.CircleOutline;
            icon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9CA3AF"));
            hint.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"));
            hint.Text = text;
        }

        private static void SetValidationState(dynamic icon, TextBlock hint, bool isValid, string text)
        {
            icon.Kind = isValid
                ? MaterialDesignThemes.Wpf.PackIconKind.CheckCircleOutline
                : MaterialDesignThemes.Wpf.PackIconKind.CloseCircleOutline;
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

            try
            {
                facade.UpdateUserSettings(
                    currentUser.UserId,
                    IsPasswordChangeRequested ? newPassword : null,
                    selectedBaseTheme,
                    selectedAccentColor);

                currentUser = facade.GetUserById(currentUser.UserId) ?? currentUser;
                currentUser.PreferredTheme = selectedBaseTheme;
                currentUser.AccentColor = selectedAccentColor;

                if (IsPasswordChangeRequested)
                {
                    currentUser.Password = newPassword;
                }

                LoginWindow.CurrentUser = currentUser;
                AppThemeManager.ApplyTheme(currentUser);
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
            chkAutoDisableBattery.IsChecked = svc.AutoDisableOnLowBattery;
            sliderCpuThreshold.Value = svc.CpuThreshold;
            sliderBatteryThreshold.Value = svc.BatteryThreshold;

            _suppressAnimSettingsChanged = false;
            UpdateAnimationControlsState();
            UpdateAnimStatusIndicator();
        }

        private void AnimationSettings_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressAnimSettingsChanged) return;
            UpdateAnimationControlsState();
        }

        private void SliderCpuThreshold_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (runCpuThreshold != null)
                runCpuThreshold.Text = ((int)e.NewValue).ToString();
        }

        private void SliderBatteryThreshold_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (runBatteryThreshold != null)
                runBatteryThreshold.Text = ((int)e.NewValue).ToString();
        }

        private void UpdateAnimationControlsState()
        {
            bool animOn = chkAnimationsEnabled.IsChecked == true;
            bool cpuOn = chkAutoDisableCpu.IsChecked == true;
            bool batOn = chkAutoDisableBattery.IsChecked == true;

            pnlCpuSettings.IsEnabled = animOn;
            pnlBatterySettings.IsEnabled = animOn;
            pnlCpuThreshold.IsEnabled = cpuOn;
            pnlBatteryThreshold.IsEnabled = batOn;

            UpdateAnimStatusIndicator();
        }

        private void OnAnimationStateChanged(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke((Action)UpdateAnimStatusIndicator);
        }

        private void UpdateAnimStatusIndicator()
        {
            var svc = AnimationService.Instance;
            bool enabled = svc.AnimationsEnabled;

            string status;
            string dotStyle;

            if (!svc.UserEnabledAnimations)
            {
                status = "Анимации отключены пользователем";
                dotStyle = "StatusDotRed";
            }
            else if (!enabled && svc.AutoDisableOnHighCpu && svc.CurrentCpuUsage > svc.CpuThreshold)
            {
                status = $"Отключены — высокая нагрузка CPU ({svc.CurrentCpuUsage:F0}%)";
                dotStyle = "StatusDotYellow";
            }
            else if (!enabled && svc.IsOnBattery)
            {
                status = $"Отключены — низкий заряд ({svc.CurrentBatteryLevel:F0}%)";
                dotStyle = "StatusDotYellow";
            }
            else
            {
                status = "Анимации включены";
                dotStyle = "StatusDotGreen";
            }

            txtAnimStatus.Text = status;
            dotAnimStatus.Style = (Style)TryFindResource(dotStyle);
        }

        private void StartLiveStatsTimer()
        {
            _liveStatsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _liveStatsTimer.Tick += (_, __) => UpdateLiveStats();
            _liveStatsTimer.Start();
            UpdateLiveStats();
        }

        private void UpdateLiveStats()
        {
            var svc = AnimationService.Instance;

            txtLiveCpu.Text = $"{svc.CurrentCpuUsage:F0}%";
            txtLiveCpu.Foreground = svc.CurrentCpuUsage > svc.CpuThreshold
                ? (Brush)TryFindResource("AppDangerBrush") ?? Brushes.Red
                : (Brush)TryFindResource("AppAccentBrush") ?? Brushes.CornflowerBlue;

            if (svc.CurrentBatteryLevel < 100 || svc.IsOnBattery)
            {
                txtLiveBattery.Text = $"{svc.CurrentBatteryLevel:F0}%";
                txtLiveBattery.Foreground = svc.CurrentBatteryLevel < svc.BatteryThreshold
                    ? (Brush)TryFindResource("AppDangerBrush") ?? Brushes.Red
                    : (Brush)TryFindResource("AppSuccessBrush") ?? Brushes.Green;
            }
            else
            {
                txtLiveBattery.Text = "AC";
                txtLiveBattery.Foreground = (Brush)TryFindResource("AppSuccessBrush") ?? Brushes.Green;
            }

            UpdateAnimStatusIndicator();
        }

        private void ApplyAnimationSettingsToService()
        {
            var svc = AnimationService.Instance;
            svc.UserEnabledAnimations = chkAnimationsEnabled.IsChecked == true;
            svc.AutoDisableOnHighCpu = chkAutoDisableCpu.IsChecked == true;
            svc.CpuThreshold = (int)sliderCpuThreshold.Value;
            svc.AutoDisableOnLowBattery = chkAutoDisableBattery.IsChecked == true;
            svc.BatteryThreshold = (int)sliderBatteryThreshold.Value;
            svc.SaveAndApply();
        }
    }
}
