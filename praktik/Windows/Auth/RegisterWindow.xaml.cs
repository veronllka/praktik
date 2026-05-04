using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using praktik.Models;
using praktik.Models.Patterns;
using praktik.Controls;

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
            SourceInitialized += RegisterWindow_SourceInitialized;
            Loaded += RegisterWindow_Loaded;
            LoadRoles();
            ApplyPresentationMode();
            FillUserForm();
            UpdatePasswordVisibility();
            UpdatePasswordPlaceholder();
        }

        private bool IsEditMode => editedUser != null;

        private void RegisterWindow_SourceInitialized(object sender, EventArgs e)
        {
            EnableGlassBackdrop();
        }

        private void RegisterWindow_Loaded(object sender, RoutedEventArgs e)
        {
            FitWindowToWorkArea();
        }

        private void FitWindowToWorkArea()
        {
            const double WindowMargin = 24;
            var workArea = SystemParameters.WorkArea;
            var maxHeight = Math.Max(520, workArea.Height - WindowMargin);

            if (MinHeight > maxHeight)
            {
                MinHeight = maxHeight;
            }

            MaxHeight = maxHeight;
            if (Height > maxHeight)
            {
                Height = maxHeight;
            }

            var windowHeight = double.IsNaN(Height) ? ActualHeight : Height;
            var windowWidth = double.IsNaN(Width) ? ActualWidth : Width;
            Top = workArea.Top + Math.Max(WindowMargin / 2, (workArea.Height - windowHeight) / 2);
            Left = workArea.Left + Math.Max(WindowMargin / 2, (workArea.Width - windowWidth) / 2);
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleWindowState();
                e.Handled = true;
                return;
            }

            StartWindowDrag(e);
        }

        private void RegisterPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsInteractiveSource(e.OriginalSource))
            {
                StartWindowDrag(e);
            }
        }

        private void StartWindowDrag(MouseButtonEventArgs e)
        {
            if (e.ButtonState != MouseButtonState.Pressed)
            {
                return;
            }

            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
                return;
            }

            e.Handled = true;
        }

        private bool IsInteractiveSource(object source)
        {
            var current = source as DependencyObject;
            while (current != null && current != RegisterPanel)
            {
                if (current is ButtonBase || current is TextBox || current is PasswordBox || current is ComboBox)
                {
                    return true;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            ToggleWindowState();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ToggleWindowState()
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void ApplyPresentationMode()
        {
            btnRegister.MinWidth = 210;

            if (IsEditMode)
            {
                SetPresentation(
                    "Редактирование пользователя",
                    "Редактирование пользователя",
                    "Измените данные учетной записи и сохраните результат.",
                    "Сохранить",
                    "\uE70F",
                    "\uE74E");
                txtRoleHint.Text = "Измените данные пользователя и сохраните изменения.";
                return;
            }

            if (isAdminCreationMode)
            {
                SetPresentation(
                    "Добавление пользователя",
                    "Добавление пользователя",
                    "Заполните данные новой учетной записи для системы.",
                    "Добавить",
                    "\uE710",
                    "\uE710");
                txtRoleHint.Text = "Выберите роль нового пользователя.";
                return;
            }

            SetPresentation(
                "Регистрация пользователя",
                "Регистрация пользователя",
                "Заполните данные новой учетной записи.",
                "Зарегистрироваться",
                "\uE710",
                "\uE710");
        }

        private void SetPresentation(
            string windowTitle,
            string headerTitle,
            string subtitle,
            string buttonText,
            string headerIcon,
            string buttonIcon)
        {
            Title = windowTitle;
            txtHeaderTitle.Text = headerTitle;
            txtHeaderSubtitle.Text = subtitle;
            RegisterButtonText.Text = buttonText;
            HeaderIcon.Text = headerIcon;
            RegisterButtonIcon.Text = buttonIcon;
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

            if (string.IsNullOrWhiteSpace(txtUsername.Text))
            {
                txtError.Text = "Введите логин пользователя.";
                txtUsername.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(currentPassword))
            {
                txtError.Text = "Введите пароль пользователя.";
                txtPassword.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(txtFullName.Text))
            {
                txtError.Text = "Введите ФИО пользователя.";
                txtFullName.Focus();
                return;
            }

            if (!(cbRoles.SelectedItem is Role selectedRole))
            {
                txtError.Text = "Выберите роль пользователя.";
                cbRoles.Focus();
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
            catch (Exception)
            {
                txtError.Text = "Не удалось сохранить пользователя. Проверьте данные и повторите попытку.";
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

            UpdatePasswordPlaceholder();
        }

        private void TxtPasswordVisible_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isPasswordVisible)
            {
                currentPassword = txtPasswordVisible.Text;
            }

            UpdatePasswordPlaceholder();
        }

        private void UpdatePasswordVisibility()
        {
            if (isPasswordVisible)
            {
                txtPasswordVisible.Text = currentPassword;
                txtPasswordVisible.Visibility = Visibility.Visible;
                txtPassword.Visibility = Visibility.Collapsed;
                IconPasswordEye.Text = "\uE890";
                btnTogglePassword.ToolTip = "Скрыть пароль";
            }
            else
            {
                txtPassword.Password = currentPassword;
                txtPassword.Visibility = Visibility.Visible;
                txtPasswordVisible.Visibility = Visibility.Collapsed;
                IconPasswordEye.Text = "\uE8F5";
                btnTogglePassword.ToolTip = "Показать пароль";
            }

            UpdatePasswordPlaceholder();
        }

        private void UpdatePasswordPlaceholder()
        {
            if (PasswordPlaceholder == null)
            {
                return;
            }

            PasswordPlaceholder.Visibility = string.IsNullOrEmpty(currentPassword)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void EnableGlassBackdrop()
        {
            try
            {
                var accent = new AccentPolicy
                {
                    AccentState = AccentState.EnableBlurBehind,
                    AccentFlags = 0,
                    GradientColor = 0
                };

                var accentSize = Marshal.SizeOf(accent);
                var accentPtr = Marshal.AllocHGlobal(accentSize);

                try
                {
                    Marshal.StructureToPtr(accent, accentPtr, false);
                    var data = new WindowCompositionAttributeData
                    {
                        Attribute = WindowCompositionAttribute.AccentPolicy,
                        Data = accentPtr,
                        SizeOfData = accentSize
                    };

                    SetWindowCompositionAttribute(new WindowInteropHelper(this).Handle, ref data);
                }
                finally
                {
                    Marshal.FreeHGlobal(accentPtr);
                }
            }
            catch
            {
                // Acrylic is only a visual enhancement; the XAML glass tint is the fallback.
            }
        }

        private enum AccentState
        {
            Disabled = 0,
            EnableGradient = 1,
            EnableTransparentGradient = 2,
            EnableBlurBehind = 3,
            EnableAcrylicBlurBehind = 4
        }

        private enum WindowCompositionAttribute
        {
            AccentPolicy = 19
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AccentPolicy
        {
            public AccentState AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowCompositionAttributeData
        {
            public WindowCompositionAttribute Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        [DllImport("user32.dll")]
        private static extern int SetWindowCompositionAttribute(
            IntPtr hwnd,
            ref WindowCompositionAttributeData data);
    }
}
