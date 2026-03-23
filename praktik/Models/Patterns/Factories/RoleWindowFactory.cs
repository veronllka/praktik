using System.Windows;
using praktik;

namespace praktik.Models.Patterns.Factories
{
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
                case "админ":
                case "admin":
                case "administrator":
                    return new AdminWindow();

                case "диспетчер":
                case "dispatcher":
                    return new DispatcherWindow();

                case "бригадир":
                case "foreman":
                    return new BrigadierWindow();

                default:
                    return new BrigadierWindow();
            }
        }
    }
}
