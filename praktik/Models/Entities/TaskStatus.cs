namespace praktik.Models
{
    public class TaskStatus
    {
        public int TaskStatusId { get; set; }
        public string TaskStatusName { get; set; } // New, In Progress, Completed, Overdue

        public Task Task
        {
            get => default;
            set
            {
            }
        }
    }
}
