using System;

namespace praktik.Models
{
    public class MaterialDeliveryDoc
    {
        public int DeliveryId { get; set; }
        public int RequestId { get; set; }
        public string Event { get; set; } // Issued, Delivered
        public DateTime EventAt { get; set; }
        public string DocNumber { get; set; }
        public string Note { get; set; }

        // Navigation properties
        public MaterialRequest Request { get; set; }
    }
}

