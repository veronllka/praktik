using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using praktik.Models;
using praktik.Models.Patterns;
using praktik.Models.Patterns.Factories;

namespace praktik
{
    /// <summary>
    /// Окно аутентификации пользователя.
    /// Обрабатывает ввод логина/пароля и переход к регистрации.
    /// </summary>
    public partial class LoginWindow : Window
    {
        public static User CurrentUser { get; set; }
        private readonly WorkPlannerFacade facade = new WorkPlannerFacade();
        private bool isPasswordVisible = false;
        private string currentPassword = string.Empty;

        public LoginWindow()
        {
            InitializeComponent();
        }

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            var username = txtUsername.Text.Trim();
            var password = currentPassword;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ClearMessages();
                txtError.Text = "Введите логин или пароль";
                return;
            }

            ClearMessages();
            TryLogin(username, password);
        }

        private void BtnRegister_Click(object sender, RoutedEventArgs e)
        {
            ShowRegisterWindow();
        }

        private void RegisterLink_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ShowRegisterWindow();
        }

        private void InputField_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnLogin_Click(sender, e);
            }
        }

        private void ClearMessages()
        {
            txtSuccess.Text = "";
            txtError.Text = "";
        }

        private void ShowRegisterWindow()
        {
            var registerWindow = new RegisterWindow();
            if (registerWindow.ShowDialog() == true)
            {
                ClearMessages();
                txtSuccess.Text = "Пользователь успешно зарегистрирован";
            }
        }

        /// <param name="username">Имя пользователя.</param>
        /// <param name="password">Пароль.</param>
        private void TryLogin(string username, string password)
        {
            var user = facade.GetUser(username, password);
            if (user == null)
            {
                txtError.Text = "Неверный логин или пароль";
                return;
            }

            OpenMainWindow(user);
        }

        private void OpenMainWindow(User user)
        {
            CurrentUser = user;
            try
            {
                var mainWindow = RoleWindowFactory.CreateWindow(user.Role);
                mainWindow.Show();
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии главного окна: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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

        private void TxtPasswordVisible_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isPasswordVisible)
            {
                currentPassword = txtPasswordVisible.Text;
            }
        }

        /// <summary>
        /// Переключает видимость пароля (текст/звездочки) в интерфейсе.
        /// </summary>
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
