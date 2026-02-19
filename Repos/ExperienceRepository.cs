using CVAPI.Models;
using Microsoft.Azure.Cosmos;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CVAPI.Repos
{
    public class ExperienceRepository
    {
        private readonly CosmosClient _cosmosClient;
        private readonly string _databaseName = "DK"; // Samme database som UserRepository

        public ExperienceRepository(CosmosClient cosmosClient)
        {
            _cosmosClient = cosmosClient;
        }

        private Container GetContainer(string region)
        {
            var database = _cosmosClient.GetDatabase(_databaseName);
            return database.GetContainer(region); // Region, f.eks. "DK"
        }

        // Hent Degrees
        public async Task<List<string>> GetDegreesAsync(string region)
        {
            try
            {
                var container = GetContainer(region);
                var query = container.GetItemQueryIterator<dynamic>(
                    new QueryDefinition("SELECT c.Degrees FROM c WHERE c.id = 'predefinedDegrees'")
                );

                var degrees = new List<string>();

                while (query.HasMoreResults)
                {
                    var response = await query.ReadNextAsync();
                    foreach (var item in response)
                    {
                        // Hvis Degrees findes og er et objekt
                        if (item.Degrees != null)
                        {
                            // Hvis Degrees er et array eller en liste
                            foreach (var degree in item.Degrees)
                            {
                                if (degree.DegreeName != null)
                                {
                                    degrees.Add(degree.DegreeName.ToString());
                                }
                            }
                        }
                    }
                }

                return degrees;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetDegreesAsync: {ex.Message}");
                return new List<string>();
            }
        }

        // Hent Fields
        public async Task<List<string>> GetFieldsAsync(string region)
        {
            try
            {
                var container = GetContainer(region);
                var query = container.GetItemQueryIterator<dynamic>(
                    new QueryDefinition("SELECT c.Fields FROM c WHERE c.id = 'predefinedFields'")
                );

                var fields = new List<string>();

                while (query.HasMoreResults)
                {
                    var response = await query.ReadNextAsync();
                    foreach (var item in response)
                    {
                        // Hvis Fields findes og er et objekt
                        if (item.Fields != null)
                        {
                            // Hvis Fields er et array eller en liste
                            foreach (var field in item.Fields)
                            {
                                if (field.FieldName != null)
                                {
                                    fields.Add(field.FieldName.ToString());
                                }
                            }
                        }
                    }
                }

                return fields;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetFieldsAsync: {ex.Message}");
                return new List<string>();
            }
        }

        // Hent Engineering Fields
        public async Task<List<string>> GetEngineeringFieldsAsync(string region)
        {
            try
            {
                var container = GetContainer(region);
                var query = container.GetItemQueryIterator<dynamic>(
                    new QueryDefinition("SELECT c.Fields FROM c WHERE c.id = 'predefinedEngineeringFields'")
                );

                var engineeringFields = new List<string>();

                while (query.HasMoreResults)
                {
                    var response = await query.ReadNextAsync();
                    foreach (var item in response)
                    {
                        // Hvis Fields findes og er et objekt
                        if (item.Fields != null)
                        {
                            // Hvis Fields er et array eller en liste
                            foreach (var field in item.Fields)
                            {
                                if (field.FieldName != null)
                                {
                                    engineeringFields.Add(field.FieldName.ToString());
                                }
                            }
                        }
                    }
                }

                return engineeringFields;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetEngineeringFieldsAsync: {ex.Message}");
                return new List<string>();
            }
        }
    }
}
