namespace praktik.Models
{
    /// <summary>
    /// Сущность "Пользователь".
    /// Представляет учетную запись пользователя в системе.
    /// </summary>
    public class User
    {
        public int UserId { get; set; }
        public int? RoleId { get; set; }

        /// <summary>
        /// Логин пользователя
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// Полное имя пользователя (ФИО)
        /// </summary>
        public string FullName { get; set; }

        /// <summary>
        /// Пароль пользователя
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// Роль пользователя в системе
        /// </summary>
        public string Role { get; set; } 

        /// <summary>
        /// Отображаемое имя в списках (ФИО + логин, если есть).
        /// </summary>
        public string DisplayName
        {
            get
            {
                return !string.IsNullOrWhiteSpace(FullName) ? FullName : (Username ?? string.Empty);
            }
        }

        public Crew Crew
        {
            get => default;
            set
            {
            }
        }

        public TaskReport TaskReport
        {
            get => default;
            set
            {
            }
        }
    }
}
