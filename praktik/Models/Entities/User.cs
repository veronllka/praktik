namespace praktik.Models
{
    public class User
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Role { get; set; } // Admin, Dispatcher, Brigadier

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
