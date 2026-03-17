namespace CVAPI.Models
{
    public class Education
    {
        public int Id { get; set; } // Unique identifier for each education entry
        public string Degree { get; set; }
        public string Field { get; set; }
        public string Institution { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public string EndDateString
        {
            get => EndDate.HasValue ? EndDate.Value.ToString("MMM yyyy") : "Now";
        }
    }

    public class PreviousWorkPlace // Corrected class name to singular for consistency
    {
        public int Id { get; set; } // Unique identifier for each job entry
        public string Position { get; set; }
        public string Company { get; set; }
        public string Description { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public string EndDateString
        {
            get => EndDate.HasValue ? EndDate.Value.ToString("MMM yyyy") : "Now";
        }
    }

    public class References
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string? Company { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
    }
}