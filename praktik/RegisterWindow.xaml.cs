using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using praktik.Models;
using praktik.Models.Patterns;

namespace praktik
{
    /// <summary>
    /// Окно регистрации нового пользователя.
    /// Позволяет создать новую учетную запись с выбором роли.
    /// </summary>
    public partial class RegisterWindow : Window
    {
        private readonly WorkPlannerFacade facade = new WorkPlannerFacade();
        private bool isPasswordVisible = false;
        private string currentPassword = string.Empty;

        public RegisterWindow()
        {
            InitializeComponent();
            LoadRoles();
        }

        private void LoadRoles()
        {
            var roles = facade.GetRoles();
            cbRoles.ItemsSource = roles;

            var normalizedCurrentRole = RolePermissionCatalog.NormalizeRoleName(LoginWindow.CurrentUser?.Role);
            var isAdminSession = normalizedCurrentRole == "администратор"
                || normalizedCurrentRole == "админ"
                || normalizedCurrentRole == "admin"
                || normalizedCurrentRole == "administrator";

            if (isAdminSession)
            {
                cbRoles.IsEnabled = true;
                cbRoles.SelectedItem = roles.FirstOrDefault();
                txtRoleHint.Text = "Администратор может назначить роль сразу при создании пользователя.";
                return;
            }

            var defaultRole = roles.FirstOrDefault(role =>
            {
                var normalizedRole = RolePermissionCatalog.NormalizeRoleName(role.RoleName);
                return normalizedRole != "администратор"
                    && normalizedRole != "админ"
                    && normalizedRole != "admin"
                    && normalizedRole != "administrator"
                    && normalizedRole != "диспетчер"
                    && normalizedRole != "dispatcher";
            })
            ?? roles.FirstOrDefault(role =>
            {
                var normalizedRole = RolePermissionCatalog.NormalizeRoleName(role.RoleName);
                return normalizedRole != "администратор"
                    && normalizedRole != "админ"
                    && normalizedRole != "admin"
                    && normalizedRole != "administrator";
            })
            ?? roles.FirstOrDefault();

            cbRoles.SelectedItem = defaultRole;
            cbRoles.IsEnabled = false;
            txtRoleHint.Text = defaultRole != null
                ? $"Роль при регистрации назначается автоматически: {defaultRole.RoleName}. Изменить её может администратор."
                : "Нет доступных ролей для регистрации.";
        }

        /// <summary>
        /// Обработчик нажатия кнопки регистрации.
        /// </summary>
        private void BtnRegister_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtUsername.Text) || 
                string.IsNullOrWhiteSpace(currentPassword) ||
                string.IsNullOrWhiteSpace(txtFullName.Text) ||
                cbRoles.SelectedItem == null)
            {
                txtError.Text = "Заполните все поля";
                return;
            }

            try
            {
                var selectedRole = cbRoles.SelectedItem as Role;
                facade.RegisterUser(txtUsername.Text.Trim(), currentPassword, txtFullName.Text.Trim(), selectedRole.RoleId);
                
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                txtError.Text = $"Ошибка регистрации: {ex.Message}";
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void InputField_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnRegister_Click(sender, e);
            }
        }

        private void BtnTogglePassword_Click(object sender, RoutedEventArgs e)
        {
            isPasswordVisible = !isPasswordVisible;
            UpdatePasswordVisibility();
        }

        private void TxtPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (!isPasswordVisible)
            {
                currentPassword = txtPassword.Password;
            }
        }

        private void TxtPasswordVisible_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (isPasswordVisible)
            {
                currentPassword = txtPasswordVisible.Text;
            }
        }

        private void UpdatePasswordVisibility()
        {
            if (isPasswordVisible)
            {
                txtPasswordVisible.Text = currentPassword;
                txtPasswordVisible.Visibility = Visibility.Visible;
                txtPassword.Visibility = Visibility.Collapsed;
                IconPasswordEye.Kind = MaterialDesignThemes.Wpf.PackIconKind.Eye;
            }
            else
            {
                txtPassword.Password = currentPassword;
                txtPassword.Visibility = Visibility.Visible;
                txtPasswordVisible.Visibility = Visibility.Collapsed;
                IconPasswordEye.Kind = MaterialDesignThemes.Wpf.PackIconKind.EyeOff;
            }
        }
    }
}
