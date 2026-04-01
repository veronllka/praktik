using System.Windows;

namespace praktik
{
    public partial class DispatcherWindow : Window
    {
        public DispatcherWindow()
        {
            InitializeComponent();
            ApplyCurrentRole("Диспетчер");
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
