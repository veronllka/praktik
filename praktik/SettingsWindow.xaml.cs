using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using praktik.Models;
using praktik.Models.Patterns;

namespace praktik
{
    public partial class SettingsWindow : Window
    {
        private readonly WorkPlannerFacade facade = new WorkPlannerFacade();
        private User currentUser;

        public SettingsWindow()
        {
            InitializeComponent();
            LoadCurrentUser();
            UpdatePasswordValidation();
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

            if (DialogResult == true)
            {
                return;
            }

            if (LoginWindow.CurrentUser != null)
            {
                AppThemeManager.ApplyTheme(LoginWindow.CurrentUser);
            }
        }
    }
}
