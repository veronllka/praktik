using System.Windows;

namespace praktik
{
    public partial class BrigadierWindow : Window
    {
        public BrigadierWindow()
        {
            InitializeComponent();
            ApplyCurrentRole("Бригадир");
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
