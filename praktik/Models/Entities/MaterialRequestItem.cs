namespace praktik.Models
{
    public class MaterialRequestItem
    {
        public int RequestItemId { get; set; }
        public int RequestId { get; set; }
        public int MaterialId { get; set; }
        public decimal Qty { get; set; }
        public string Comment { get; set; }

        // Navigation properties
        public MaterialRequest Request { get; set; }
        public MaterialCatalog Material { get; set; }
    }
}

