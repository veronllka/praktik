namespace praktik.Models
{
    /// <summary>
    /// Сущность "Пользователь".
    /// Представляет учетную запись пользователя в системе.
    /// </summary>
    public class User
    {
        public int UserId { get; set; }

        /// <summary>
        /// Логин пользователя
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// Пароль пользователя
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// Роль пользователя в системе
        /// </summary>
        public string Role { get; set; } 

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
