using System;
using System.Collections.Generic;

namespace praktik.Models
{
    public class DailyPlan
    {
        public int PlanId { get; set; }
        public DateTime PlanDate { get; set; }
        public int CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Comment { get; set; }
        public string Status { get; set; }

        public List<DailyPlanItem> Items { get; set; } = new List<DailyPlanItem>();

        public bool IsApproved => Status == "Утвержден";
        public bool IsDraft => Status != "Утвержден";
    }

    public class DailyPlanItem
    {
        public int PlanItemId { get; set; }
        public int PlanId { get; set; }
        public int TaskId { get; set; }
        public int CrewId { get; set; }
        public int SortOrder { get; set; }
        public string Note { get; set; }
        public bool MaterialsReady { get; set; }
    }
}
