using System.Windows;
using praktik;

namespace praktik.Models.Patterns.Factories
{
    /// <summary>
    /// Фабрика для создания окон приложения в зависимости от роли пользователя.
    /// Реализует вариацию паттерна Factory Method 
    /// </summary>
    public static class RoleWindowFactory
    {
        /// <summary>
        /// Создает и возвращает окно, соответствующее указанной роли.
        /// </summary>
        /// <param name="role">Название роли пользователя .</param>
        /// <returns>Экземпляр окна, наследующего от <see cref="Window"/>.</returns>
        public static Window CreateWindow(string role)
        {
            var normalizedRole = role?.Trim().ToLowerInvariant();

            switch (normalizedRole)
            {
                case "администратор":
                    return new MainWindow();

                case "диспетчер":
                    return new MainWindow();

                case "бригадир":
                    return new MainWindow();

                default:
                    // По умолчанию открываем главное окно для всех, включая обычных пользователей
                    return new MainWindow();
            }
        }
    }
}
