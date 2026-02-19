using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace CVAPI.Models
{
    // These roles are used as admin-access for the system
    public enum Role
    {
        Applicant = 0,
        Consultant = 1,
        Manager = 2,
        Admin = 3
    }

    public class User
    {
        [JsonProperty("id")]
        public string Id { get; set; } // This is CosmosDB's unique identifier

        [JsonProperty("UserId")]
        public string UserId { get; set; } // This is set as the Partition Key for CosmosDB
        public Role UserRole { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public int? PostalCode { get; set; }
        public string Country { get; set; }
        public string? ProfilePicture { get; set; } // Nullable, if the picture is not available
        public bool IsDeleted { get; set; } = false; 
    }

    public class Consultant : User
    {
        public string? Summary { get; set; }
        public string? Interests { get; set; }
        public string? Linkedin { get; set; }
        public string? CV { get; set; }
        public DateTime DateAdded { get; set; }
        public List<CompetencyCategory> Competencies { get; set; } = new List<CompetencyCategory>();
        public List<Language> Languages { get; set; } = new List<Language>();
        public List<Education> Education { get; set; } = new List<Education>();
        public List<PreviousWorkPlace> PreviousWorkPlaces { get; set; } =
            new List<PreviousWorkPlace>();
        public List<References>? References { get; set; } = new List<References>();
        public List<string>? AdditionalNotes { get; set; } = new List<string>();
        public bool IsAvailable { get; set; }
        public DateTime? AvailableWhen { get; set; }
        public int? AvailableInterval { get; set; } // Number of days until available (optional)
        public bool Confirmed { get; set; }
        public bool Bepa { get; set; }
        public List<PrivateNote>? PrivateNotes { get; set; } = new List<PrivateNote>(); // Updated to List<PrivateNote>
        public int PrivateRating { get; set; }
        public int TechRating { get; set; }
    }

    public class Applicant : User
    {
        public string? Summary { get; set; }
        public string? Interests { get; set; }
        public string? Linkedin { get; set; }
        public string? CV { get; set; }
        public DateTime DateAdded { get; set; }
        public List<CompetencyCategory>? Competencies { get; set; } =
            new List<CompetencyCategory>();
        public List<Language> Languages { get; set; } = new List<Language>();
        public List<Education> Education { get; set; } = new List<Education>();
        public List<PreviousWorkPlace> PreviousWorkPlaces { get; set; } =
            new List<PreviousWorkPlace>();
        public List<References>? References { get; set; } = new List<References>();
        public List<string>? AdditionalNotes { get; set; } = new List<string>();
        public bool IsAvailable { get; set; }
        public DateTime? AvailableWhen { get; set; }
        public int? AvailableInterval { get; set; } // Number of days until available (optional)
        public bool Confirmed { get; set; }
        public List<PrivateNote>? PrivateNotes { get; set; } = new List<PrivateNote>(); // Updated to List<PrivateNote>
        public int PrivateRating { get; set; }
        public int TechRating { get; set; }
        /*public string OneTimeToken { get; set; }
        public DateTime TokenExpiration { get; set; }*/ //this is for the one time link setup
    }

    public class PrivateNote
    {
        public string Text { get; set; }
        public string AdminInitials { get; set; }
    }

    public class Manager : User
    {
        public string AdminInitials { get; set; } // Only Managers have this property
        // You can add stuff here, if the Manager needs more properties
    }

    public class Admin : User
    {
        public string AdminInitials { get; set; } // Only Admins have this property
        // You can add stuff here, if the Manager needs more properties
    }
}
