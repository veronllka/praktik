using System;

namespace praktik.Models
{
    public class CrewMember
    {
        public int CrewId { get; set; }
        public int UserId { get; set; }
        public DateTime JoinedAt { get; set; }
        public DateTime? LeftAt { get; set; }

        public User User { get; set; }

        public bool IsActive => !LeftAt.HasValue;
    }
}
