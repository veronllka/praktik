using System.Windows;
using praktik;

namespace praktik.Models.Patterns
{
    public static class RoleWindowFactory
    {
        public static Window CreateWindow(string role)
        {
            switch (role?.Trim())
            {
                case "Администратор":
                return new MainWindow();

                case "Диспетчер":
                return new MainWindow();

                case "Бригадир":
                return new MainWindow();

                default:
                    return new MainWindow();
            }
        }
    }
}
