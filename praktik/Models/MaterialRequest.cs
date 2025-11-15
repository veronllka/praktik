using System;
using System.Collections.Generic;

namespace praktik.Models
{
    public class MaterialRequest
    {
        public int RequestId { get; set; }
        public int TaskId { get; set; }
        public int CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? RequiredDate { get; set; }
        public string Status { get; set; } // Draft, Submitted, Approved, Issued, Delivered, Closed, Rejected
        public string Comment { get; set; }

        // Navigation properties
        public Task Task { get; set; }
        public User CreatedByUser { get; set; }
        public List<MaterialRequestItem> Items { get; set; } = new List<MaterialRequestItem>();
    }
}

