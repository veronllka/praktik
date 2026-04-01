using System.Windows;

namespace praktik
{
    public partial class AdminWindow : Window
    {
        public AdminWindow()
        {
            InitializeComponent();
            ApplyCurrentRole("Администратор");
        }

        private void ApplyCurrentRole(string fallbackRole)
        {
            var role = LoginWindow.CurrentUser?.Role;
            if (string.IsNullOrWhiteSpace(role))
            {
                role = fallbackRole;
            }

            Title = $"Планировщик работ бригад - {role}";
            MainView.ApplyRole(role);
        }
    }
}
