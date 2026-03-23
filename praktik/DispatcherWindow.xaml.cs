using System.Windows;

namespace praktik
{
    /// <summary>
    /// Окно для роли "Диспетчер".
    /// Предоставляет доступ к управлению задачами, площадками, бригадами и материалами.
    /// Реализует паттерн Фабричный метод - создается через RoleWindowFactory.
    /// </summary>
    public partial class DispatcherWindow : Window
    {
        public DispatcherWindow()
        {
            InitializeComponent();
            MainView.ApplyRole("Диспетчер");
        }
    }
}
