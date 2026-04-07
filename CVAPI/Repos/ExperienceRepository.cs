using CVAPI.Services;
using Newtonsoft.Json;

namespace CVAPI.Repos
{
    public class ExperienceRepository
    {
        private readonly CellarStorageService _storage;

        public ExperienceRepository(CellarStorageService storage)
        {
            _storage = storage;
        }

        private static string GetBucket(string region) =>
            region.ToUpper() == "VN" ? "bachelor-vn" : "bachelor-dk";

        public async Task<List<string>> GetDegreesAsync(string region)
        {
            try
            {
                var json = await _storage.GetObjectAsync(GetBucket(region), "predefined/degrees.json");
                if (json == null) return new List<string>();

                var doc = JsonConvert.DeserializeObject<DegreesDocument>(json);
                return doc?.Degrees?.Select(d => d.DegreeName).Where(n => n != null).ToList()
                       ?? new List<string>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetDegreesAsync: {ex.Message}");
                return new List<string>();
            }
        }

        public async Task<List<string>> GetFieldsAsync(string region)
        {
            try
            {
                var json = await _storage.GetObjectAsync(GetBucket(region), "predefined/fields.json");
                if (json == null) return new List<string>();

                var doc = JsonConvert.DeserializeObject<FieldsDocument>(json);
                return doc?.Fields?.Select(f => f.FieldName).Where(n => n != null).ToList()
                       ?? new List<string>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetFieldsAsync: {ex.Message}");
                return new List<string>();
            }
        }

        public async Task<List<string>> GetEngineeringFieldsAsync(string region)
        {
            try
            {
                var json = await _storage.GetObjectAsync(GetBucket(region), "predefined/engineeringFields.json");
                if (json == null) return new List<string>();

                var doc = JsonConvert.DeserializeObject<FieldsDocument>(json);
                return doc?.Fields?.Select(f => f.FieldName).Where(n => n != null).ToList()
                       ?? new List<string>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetEngineeringFieldsAsync: {ex.Message}");
                return new List<string>();
            }
        }

        private class DegreesDocument
        {
            public List<DegreeEntry> Degrees { get; set; } = new();
        }

        private class DegreeEntry
        {
            public string DegreeName { get; set; } = "";
        }

        private class FieldsDocument
        {
            public List<FieldEntry> Fields { get; set; } = new();
        }

        private class FieldEntry
        {
            public string FieldName { get; set; } = "";
        }
    }
}
