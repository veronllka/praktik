using System.Windows;

namespace praktik
{
    /// <summary>
    /// Окно для роли "Бригадир".
    /// Предоставляет доступ только к просмотру и обновлению задач своей бригады.
    /// Реализует паттерн Фабричный метод - создается через RoleWindowFactory.
    /// </summary>
    public partial class BrigadierWindow : Window
    {
        public BrigadierWindow()
        {
            InitializeComponent();
            MainView.ApplyRole("Бригадир");
        }
    }
}
