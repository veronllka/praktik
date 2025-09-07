namespace praktik.Models
{
    public class Crew
    {
        public int CrewId { get; set; }
        public string CrewName { get; set; }
        public int? BrigadierId { get; set; }
        public User Brigadier { get; set; }
    }
}
