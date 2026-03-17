namespace CVAPI.Models
{
    // Model for the main competency category
    public class PredefinedData
    {
        public string Id { get; set; }  // "predefinedData"
        
        public List<CompetencyCategory> Competencies { get; set; }
        public List<Language> Languages { get; set; }
    }
    public class CompetencyCategory
    {
        public string CategoryName { get; set; }
        public int? CategoryLevel { get; set; } = 0;
        public List<SubCategory> SubCategories { get; set; } = new List<SubCategory>(); // Sub-categories related to the category
    }

    // Model to represent a sub-category under a competency category
    public class SubCategory
    {
        public string SubCategoryName { get; set; } // Name of the sub-category (e.g., Control Systems Design)
        public int? SubCategoryLevel {get; set;} = 0;
        public List<Competency> Competencies { get; set; } = new List<Competency>(); // Competencies under this sub-category
    }

    // Model to represent an individual competency
    public class Competency
    {
        public string CompetencyName { get; set; } // Name of the competency (e.g., Siemens TIA Portal)
        public int? CompetencyLevel {get; set;} = 0;
    }

    // Model to represent a language
    public class Language
    {
        public string LanguageName { get; set; } // Name of the language (e.g., English, Danish)
        public int? LanguageLevel {get; set;} = 0;
    }
}
