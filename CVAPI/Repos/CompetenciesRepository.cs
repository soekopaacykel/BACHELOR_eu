using CVAPI.Models;
using CVAPI.Services;
using Newtonsoft.Json;

namespace CVAPI.Repos
{
    public class CompetenciesRepository
    {
        private readonly CellarStorageService _storage;

        public CompetenciesRepository(CellarStorageService storage)
        {
            _storage = storage;
        }

        private static string GetBucket(string region) =>
            region.ToUpper() == "VN" ? "bachelor-vn" : "bachelor-dk";

        public async Task<PredefinedData?> GetPredefinedDataAsync(string region = "DK")
        {
            var json = await _storage.GetObjectAsync(GetBucket(region), "predefined/data.json");
            if (json == null) return null;
            return JsonConvert.DeserializeObject<PredefinedData>(json);
        }
    }
}
