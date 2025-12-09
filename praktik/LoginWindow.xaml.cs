using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using praktik.Models;
using praktik.Models.Patterns.Factories;

namespace praktik
{
    public partial class LoginWindow : Window
    {
        public static User CurrentUser { get; set; }

        public LoginWindow()
        {
            InitializeComponent();
        }

        private void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            string username = txtUsername.Text.Trim();
            string password = txtPassword.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ClearMessages();
                txtError.Text = "Введите логин и пароль";
                return;
            }

            ClearMessages();

            using (var context = new WorkPlannerContext())
            {
                var user = context.GetUser(username, password);
                if (user != null)
                {
                    CurrentUser = user;
                    try
                    {
                        Window mainWindow = RoleWindowFactory.CreateWindow(user.Role);
                        mainWindow.Show();
                        this.Close();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при открытии главного окна: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    txtError.Text = "Неверный логин или пароль";
                }
            }
        }

        private void btnRegister_Click(object sender, RoutedEventArgs e)
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
                btnLogin_Click(sender, e);
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

        private void TryLogin(string username, string password)
        {
            using var context = new WorkPlannerContext();
            var user = context.GetUser(username, password);
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
            catch (System.Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии главного окна: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
