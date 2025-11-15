namespace praktik.Models
{
    public class Priority
    {
        public int PriorityId { get; set; }
        public string PriorityName { get; set; } // Critical, High, Normal, Low

        public Task Task
        {
            get => default;
            set
            {
            }
        }
    }
}
