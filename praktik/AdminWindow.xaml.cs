using System.Windows;

namespace praktik
{
    /// <summary>
    /// Окно для роли "Администратор".
    /// Предоставляет полный доступ ко всем функциям системы.
    /// Реализует паттерн Фабричный метод - создается через RoleWindowFactory.
    /// </summary>
    public partial class AdminWindow : Window
    {
        public AdminWindow()
        {
            InitializeComponent();
            MainView.ApplyRole("Администратор");
        }
    }
}
