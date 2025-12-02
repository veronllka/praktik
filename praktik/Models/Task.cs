using System;

namespace praktik.Models
{
    public class Task
    {
        public int TaskId { get; set; }
        public int SiteId { get; set; }
        public int? CrewId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int PriorityId { get; set; }
        public int TaskStatusId { get; set; }
        public int? LabelId { get; set; } 
        public int CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

        public Site Site { get; set; }
        public Crew Crew { get; set; }
        public Priority Priority { get; set; }
        public TaskStatus TaskStatus { get; set; }
        public User Creator { get; set; }

        public string GenerateQRData()
        {
            var siteName = Site?.SiteName?.Replace("\"", "'") ?? "";
            var crewName = Crew?.CrewName?.Replace("\"", "'") ?? "Не назначена";
            var priorityName = Priority?.PriorityName?.Replace("\"", "'") ?? "";
            var title = Title?.Replace("\"", "'") ?? "";
            
            return $"{{\"type\":\"TASK\",\"id\":{TaskId},\"title\":\"{title}\",\"site\":\"{siteName}\",\"crew\":\"{crewName}\",\"priority\":\"{priorityName}\",\"start\":\"{StartDate:yyyy-MM-dd}\",\"end\":\"{EndDate:yyyy-MM-dd}\"}}";
        }

        public TaskReport TaskReport
        {
            get => default;
            set
            {
            }
        }

        // For displaying notes in DataGrid
        public string LastNoteText { get; set; }
        public string LastNoteTooltip { get; set; }
    }
}
