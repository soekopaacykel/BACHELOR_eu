using Newtonsoft.Json;
using CVAPI.Models;
using System;
using System.Collections.Generic;

namespace CVAPI.Services
{
    public class CompetencyConverter : JsonConverter<List<CompetencyCategory>>
    {
        public override List<CompetencyCategory> ReadJson(JsonReader reader, Type objectType, List<CompetencyCategory> existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var competencies = new List<CompetencyCategory>();

            if (reader.TokenType == JsonToken.StartArray)
            {
                var items = serializer.Deserialize<List<object>>(reader);

                foreach (var item in items)
                {
                    if (item is string str)
                    {
                        competencies.Add(new CompetencyCategory { CategoryName = str });
                    }
                    else if (item is CompetencyCategory category)
                    {
                        competencies.Add(category);
                    }
                }
            }

            return competencies;
        }

        public override void WriteJson(JsonWriter writer, List<CompetencyCategory> value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
    }
}