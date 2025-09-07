using System;

namespace praktik.Models
{
    public class TaskReport
    {
        public int ReportId { get; set; }
        public int TaskId { get; set; }
        public int ReportedByUserId { get; set; }
        public DateTime ReportedAt { get; set; }
        public string ReportText { get; set; }
        public int? ProgressPercent { get; set; }

        // Дополнительные свойства для отображения
        public string TaskTitle { get; set; }
        public string ReporterName { get; set; }
        
        public Task Task { get; set; }
        public User User { get; set; }
    }
}
