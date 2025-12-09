using System;

namespace praktik.Models
{
    public class MaterialCatalog
    {
        public int MaterialId { get; set; }
        public string Name { get; set; }
        public string Unit { get; set; }
        public string Code { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

