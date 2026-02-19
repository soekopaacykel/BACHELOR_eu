using System.Threading.Tasks;
using CVAPI.Repos;
using Microsoft.AspNetCore.Mvc;

namespace CVAPI.Controllers
{
    [Route("api/[controller]")]
    public class CompetenciesController : Controller
    {
        private readonly CompetenciesRepository _competenciesRepository;
        private readonly ExperienceRepository _experienceRepository; // Brug ExperienceRepository

        public CompetenciesController(
            CompetenciesRepository competenciesRepository,
            ExperienceRepository experienceRepository
        )
        {
            _competenciesRepository = competenciesRepository;
            _experienceRepository = experienceRepository; // Injicer ExperienceRepository
        }

        // Vis alle kompetencer (denne kan være til at vise hele dataen)
        public async Task<IActionResult> Index(string region = "DK")
        {
            var predefinedData = await _competenciesRepository.GetPredefinedDataAsync(region);
            return View(predefinedData);
        }

        // Hent subkategorier for en valgt kategori (kaldes via Ajax i Razor Page)
        [HttpGet("{region}/subcategories")]
        public async Task<IActionResult> GetSubCategories(string region, int categoryIndex)
        {
            var predefinedData = await _competenciesRepository.GetPredefinedDataAsync(region);
            var category = predefinedData?.Competencies?[categoryIndex];
            if (category == null)
                return Json(new { success = false });

            return Json(category.SubCategories);
        }

        // Hent kompetencer for en valgt subkategori
        [HttpGet("{region}/competencies")]
        public async Task<IActionResult> GetCompetencies(string region, int categoryIndex, int subCategoryIndex)
        {
            var predefinedData = await _competenciesRepository.GetPredefinedDataAsync(region);
            var category = predefinedData?.Competencies?[categoryIndex];
            var subCategory = category?.SubCategories?[subCategoryIndex];
            if (subCategory == null)
                return Json(new { success = false });

            return Json(subCategory.Competencies);
        }

        // Hent sprogene
        [HttpGet("{region}/GetLanguages")]
        public async Task<IActionResult> GetLanguages(string region)
        {
            try
            {
                var predefinedData = await _competenciesRepository.GetPredefinedDataAsync(region);
                
                if (predefinedData?.Languages != null)
                {
                    return Ok(predefinedData.Languages);
                }

                return Ok(new List<object>());
            }
            catch
            {
                return Ok(new List<object>());
            }
        }

        [HttpGet("{region}/GetDegrees")]
        public async Task<IActionResult> GetDegrees(string region)
        {
            try
            {
                var degrees = await _experienceRepository.GetDegreesAsync(region);
                return Ok(degrees ?? new List<string>());
            }
            catch
            {
                return Ok(new List<string>());
            }
        }

        // Hent Fields (fra CosmosDB)
        [HttpGet("{region}/GetFields")]
        public async Task<IActionResult> GetFields(string region)
        {
            try
            {
                var fields = await _experienceRepository.GetFieldsAsync(region);
                return Ok(fields ?? new List<string>());
            }
            catch
            {
                return Ok(new List<string>());
            }
        }

        // Hent Engineering Fields (fra CosmosDB)
        [HttpGet("{region}/GetEngineeringFields")]
        public async Task<IActionResult> GetEngineeringFields(string region)
        {
            try
            {
                var engineeringFields = await _experienceRepository.GetEngineeringFieldsAsync(region);
                return Ok(engineeringFields ?? new List<string>());
            }
            catch
            {
                return Ok(new List<string>());
            }
        }

        [HttpGet("{region}/predefined")]
        public async Task<IActionResult> GetPredefinedCompetencies(string region)
        {
            var predefinedData = await _competenciesRepository.GetPredefinedDataAsync(region);

            if (predefinedData == null)
            {
                return NotFound("No predefined competencies found.");
            }

            return Ok(predefinedData); // Returnerer som JSON
        }

        [HttpGet("{region}/fullcompetencystructure")]
        public async Task<IActionResult> GetFullCompetencyStructure(string region)
        {
            // Hent prædefinerede data (inkl. kategorier, subkategorier og kompetencer)
            var predefinedData = await _competenciesRepository.GetPredefinedDataAsync(region);

            if (predefinedData == null)
            {
                return NotFound("No predefined competencies found.");
            }

            // Skab en struktur med alle kategorier, subkategorier og kompetencer
            var fullStructure = new List<object>();

            foreach (var category in predefinedData.Competencies)
            {
                var categoryObject = new
                {
                    CategoryName = category.CategoryName,
                    CategoryLevel = category.CategoryLevel,
                    SubCategories = new List<object>(),
                };

                foreach (var subCategory in category.SubCategories)
                {
                    var subCategoryObject = new
                    {
                        SubCategoryName = subCategory.SubCategoryName,
                        SubCategoryLevel = subCategory.SubCategoryLevel,
                        Competencies = new List<object>(),
                    };

                    // Hent kompetencer for hver subkategori
                    foreach (var competency in subCategory.Competencies)
                    {
                        subCategoryObject.Competencies.Add(
                            new
                            {
                                CompetencyName = competency.CompetencyName,
                                CompetencyLevel = competency.CompetencyLevel,
                            }
                        );
                    }

                    categoryObject.SubCategories.Add(subCategoryObject);
                }

                fullStructure.Add(categoryObject);
            }

            return Ok(fullStructure); // Returnér den samlede struktur
        }

        // Backward compatibility endpoints (with region parameter)
        [HttpGet("GetLanguages")]
        public async Task<IActionResult> GetLanguagesDefault(string region = "DK")
        {
            return await GetLanguages(region);
        }

        [HttpGet("GetDegrees")]
        public async Task<IActionResult> GetDegreesDefault(string region = "DK")
        {
            return await GetDegrees(region);
        }

        [HttpGet("GetFields")]
        public async Task<IActionResult> GetFieldsDefault(string region = "DK")
        {
            return await GetFields(region);
        }

        [HttpGet("GetEngineeringFields")]
        public async Task<IActionResult> GetEngineeringFieldsDefault(string region = "DK")
        {
            return await GetEngineeringFields(region);
        }

        [HttpGet("predefined")]
        public async Task<IActionResult> GetPredefinedCompetenciesDefault(string region = "DK")
        {
            return await GetPredefinedCompetencies(region);
        }

        [HttpGet("fullcompetencystructure")]
        public async Task<IActionResult> GetFullCompetencyStructureDefault(string region = "DK")
        {
            return await GetFullCompetencyStructure(region);
        }
    }
}
