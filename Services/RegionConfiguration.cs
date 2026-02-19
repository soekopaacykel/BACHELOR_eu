namespace CVAPI.Services
{
    public class RegionConfiguration
    {
        public static readonly List<string> SupportedRegions = new() { "DK", "VN" };
        
        public static string DefaultRegion => "DK";
        
        public static bool IsValidRegion(string region)
        {
            return !string.IsNullOrEmpty(region) && SupportedRegions.Contains(region.ToUpper());
        }
        
        public static string NormalizeRegion(string region)
        {
            if (string.IsNullOrEmpty(region))
                return DefaultRegion;
                
            var normalizedRegion = region.ToUpper();
            return IsValidRegion(normalizedRegion) ? normalizedRegion : DefaultRegion;
        }
        
        public static class RegionNames
        {
            public const string Denmark = "DK";
            public const string Vietnam = "VN";
        }
    }
}