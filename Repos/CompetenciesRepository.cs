using CVAPI.Models;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CVAPI.Repos
{
    public class CompetenciesRepository
    {
        private readonly CosmosClient _cosmosClient;
        private readonly string _databaseName = "BEPAVEXA";

        public CompetenciesRepository(CosmosClient cosmosClient)
        {
            _cosmosClient = cosmosClient;
        }

        private Container GetContainer(string region = "DK")
        {
            var database = _cosmosClient.GetDatabase(_databaseName);
            return database.GetContainer(region); // Region "DK" or "VN"
        }

        // Hent alle predefinerede kompetencer (Kategorier, Subkategorier og Kompetencer)
        public async Task<PredefinedData> GetPredefinedDataAsync(string region = "DK")
        {
            try
            {
                var container = GetContainer(region);
                var query = "SELECT * FROM c WHERE c.id = 'predefinedData'";
                var iterator = container.GetItemQueryIterator<dynamic>(query);
                
                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    if (response.Count > 0)
                    {
                        var item = response.First();
                        
                        // Deserialize the dynamic object to PredefinedData
                        var json = JsonConvert.SerializeObject(item);
                        var predefinedData = JsonConvert.DeserializeObject<PredefinedData>(json);
                        return predefinedData;
                    }
                }
            }
            catch
            {
                // If deserialization fails, return empty object
            }

            return new PredefinedData 
            { 
                Id = "predefinedData",
                Competencies = new List<CompetencyCategory>(),
                Languages = new List<Language>()
            };
        }
    }
}
