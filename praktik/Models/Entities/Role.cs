namespace praktik.Models
{
    public class Role
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; }

        public User User
        {
            get => default;
            set
            {
            }
        }

        public Task Task
        {
            get => default;
            set
            {
            }
        }
    }
}



