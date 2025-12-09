using System;

namespace praktik.Models
{
    public class MaterialRequestStatusLog
    {
        public int LogId { get; set; }
        public int RequestId { get; set; }
        public string OldStatus { get; set; }
        public string NewStatus { get; set; }
        public int ChangedByUserId { get; set; }
        public DateTime ChangedAt { get; set; }
        public string Comment { get; set; }

        // Navigation properties
        public MaterialRequest Request { get; set; }
        public User ChangedByUser { get; set; }
    }
}

