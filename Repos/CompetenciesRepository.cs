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
        private readonly string _databaseName = "DK";

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
            var container = GetContainer(region);
            var query = "SELECT * FROM c WHERE c.id = 'predefinedData'"; // Filtrering efter id
            var iterator = container.GetItemQueryIterator<PredefinedData>(query);
            var result = new List<PredefinedData>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                result.AddRange(response);
            }

            return result.FirstOrDefault(); // Returnerer det første matchende dokument
        }
    }
}
