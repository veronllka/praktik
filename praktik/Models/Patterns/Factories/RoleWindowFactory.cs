using System.Windows;
using praktik;

namespace praktik.Models.Patterns.Factories
{
    public static class RoleWindowFactory
    {
        public static Window CreateWindow(string role)
        {
            var normalizedRole = role?.Trim().ToLowerInvariant();

            switch (normalizedRole)
            {
                case "администратор":
                case "админ":
                case "admin":
                    return new MainWindow();

                case "диспетчер":
                case "dispatcher":
                    return new MainWindow();

                case "бригадир":
                case "brigadier":
                    return new MainWindow();

                default:
                    return new MainWindow();
            }
        }
    }
}
