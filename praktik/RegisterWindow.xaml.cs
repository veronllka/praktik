using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using praktik.Models;
using praktik.Models.Patterns;

namespace praktik
{
    /// <summary>
    /// Окно создания и редактирования пользователя.
    /// </summary>
    public partial class RegisterWindow : Window
    {
        private readonly WorkPlannerFacade facade = new WorkPlannerFacade();
        private readonly bool isAdminCreationMode;
        private readonly User editedUser;
        private bool isPasswordVisible;
        private string currentPassword = string.Empty;

        public string CreatedUsername { get; private set; }
        public User SavedUser { get; private set; }

        public RegisterWindow(bool adminCreationMode = false, User userToEdit = null)
        {
            isAdminCreationMode = adminCreationMode;
            editedUser = userToEdit;

            InitializeComponent();
            LoadRoles();
            ApplyPresentationMode();
            FillUserForm();
            UpdatePasswordVisibility();
        }

        private bool IsEditMode => editedUser != null;

        private void ApplyPresentationMode()
        {
            btnRegister.MinWidth = 200;

            if (IsEditMode)
            {
                Title = "Редактирование пользователя";
                btnRegister.Content = "Сохранить";
                txtRoleHint.Text = "Измените данные пользователя и сохраните изменения.";
                return;
            }

            if (isAdminCreationMode)
            {
                Title = "Добавление пользователя";
                btnRegister.Content = "Добавить";
                txtRoleHint.Text = "Выберите роль нового пользователя.";
                return;
            }

            Title = "Регистрация пользователя";
            btnRegister.Content = "Зарегистрироваться";
        }

        private void LoadRoles()
        {
            var roles = facade.GetRoles();
            cbRoles.SelectedValuePath = "RoleId";
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
                ? $"Роль при регистрации назначается автоматически: {defaultRole.RoleName}."
                : "Нет доступных ролей для регистрации.";
        }

        private void FillUserForm()
        {
            if (!IsEditMode)
            {
                return;
            }

            txtUsername.Text = editedUser.Username ?? string.Empty;
            txtFullName.Text = editedUser.FullName ?? string.Empty;
            currentPassword = editedUser.Password ?? string.Empty;
            txtPassword.Password = currentPassword;
            txtPasswordVisible.Text = currentPassword;
            cbRoles.SelectedValue = editedUser.RoleId;

            var isCurrentSessionUser = LoginWindow.CurrentUser != null &&
                                       LoginWindow.CurrentUser.UserId == editedUser.UserId;

            if (isCurrentSessionUser)
            {
                cbRoles.IsEnabled = false;
                txtRoleHint.Text = "Свою роль в текущем сеансе менять нельзя.";
            }
        }

        private void BtnRegister_Click(object sender, RoutedEventArgs e)
        {
            txtError.Text = string.Empty;

            if (string.IsNullOrWhiteSpace(txtUsername.Text) ||
                string.IsNullOrWhiteSpace(currentPassword) ||
                string.IsNullOrWhiteSpace(txtFullName.Text) ||
                !(cbRoles.SelectedItem is Role selectedRole))
            {
                txtError.Text = "Заполните все поля.";
                return;
            }

            var username = txtUsername.Text.Trim();
            var fullName = txtFullName.Text.Trim();

            try
            {
                if (IsEditMode)
                {
                    var isCurrentSessionUser = LoginWindow.CurrentUser != null &&
                                               LoginWindow.CurrentUser.UserId == editedUser.UserId;

                    if (isCurrentSessionUser && editedUser.RoleId != selectedRole.RoleId)
                    {
                        txtError.Text = "Нельзя менять собственную роль в текущем сеансе.";
                        return;
                    }

                    facade.UpdateUser(editedUser.UserId, username, currentPassword, fullName, selectedRole.RoleId);
                }
                else
                {
                    facade.RegisterUser(username, currentPassword, fullName, selectedRole.RoleId);
                }

                CreatedUsername = username;
                SavedUser = new User
                {
                    UserId = editedUser?.UserId ?? 0,
                    Username = username,
                    FullName = fullName,
                    Password = currentPassword,
                    RoleId = selectedRole.RoleId,
                    Role = selectedRole.RoleName
                };

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                txtError.Text = $"Ошибка сохранения: {ex.Message}";
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
                btnTogglePassword.Content = "Скрыть";
            }
            else
            {
                txtPassword.Password = currentPassword;
                txtPassword.Visibility = Visibility.Visible;
                txtPasswordVisible.Visibility = Visibility.Collapsed;
                btnTogglePassword.Content = "Показать";
            }
        }
    }
}
