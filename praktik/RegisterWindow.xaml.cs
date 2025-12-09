using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using praktik.Models;

namespace praktik
{
    public partial class RegisterWindow : Window
    {
        private WorkPlannerContext db = new WorkPlannerContext();

        public RegisterWindow()
        {
            InitializeComponent();
            LoadRoles();
        }

        private void LoadRoles()
        {
            cbRoles.ItemsSource = db.GetRoles();
        }

        private void BtnRegister_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtUsername.Text) || 
                string.IsNullOrWhiteSpace(txtPassword.Password) ||
                string.IsNullOrWhiteSpace(txtFullName.Text) ||
                cbRoles.SelectedItem == null)
            {
                txtError.Text = "Заполните все поля";
                return;
            }

            try
            {
                var selectedRole = cbRoles.SelectedItem as Role;
                db.RegisterUser(txtUsername.Text.Trim(), txtPassword.Password, txtFullName.Text.Trim(), selectedRole.RoleId);
                
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
    }
}
