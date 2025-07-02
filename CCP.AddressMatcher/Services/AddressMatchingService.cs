using CCP.AddressMatcher.Models;

namespace CCP.AddressMatcher.Services
{
    public class AddressMatchingService
    {
        private readonly GoogleGeocodingService _geocodingService;

        public AddressMatchingService(GoogleGeocodingService geocodingService)
        {
            _geocodingService = geocodingService;
        }

        public async Task<AddressMatchResult> CompareAddressesAsync(string address1, string address2)
        {
            // Geocode both addresses
            var geo1Task = _geocodingService.GeocodeAddressAsync(address1);
            var geo2Task = _geocodingService.GeocodeAddressAsync(address2);

            var geo1 = await geo1Task;
            var geo2 = await geo2Task;

            if (geo1?.Status != "OK" || geo1.Results?.Length == 0)
            {
                throw new Exception($"Failed to geocode address 1: '{address1}' - Status: {geo1?.Status}");
            }

            if (geo2?.Status != "OK" || geo2.Results?.Length == 0)
            {
                throw new Exception($"Failed to geocode address 2: '{address2}' - Status: {geo2?.Status}");
            }

            var result1 = geo1.Results[0];
            var result2 = geo2.Results[0];

            // Extract key information
            var placeId1 = result1.PlaceId;
            var placeId2 = result2.PlaceId;
            var formatted1 = result1.FormattedAddress;
            var formatted2 = result2.FormattedAddress;
            var location1 = result1.Geometry?.Location;
            var location2 = result2.Geometry?.Location;

            if (location1 == null || location2 == null)
            {
                throw new Exception("Unable to extract coordinates from geocoding results");
            }

            // Calculate distance
            var distance = GoogleGeocodingService.CalculateDistance(
                location1.Lat, location1.Lng,
                location2.Lat, location2.Lng);

            // Determine match using multiple criteria
            var matchResult = DetermineMatch(placeId1, placeId2, formatted1, formatted2, 
                                           result1, result2, distance);

            return new AddressMatchResult
            {
                Match = matchResult.Match,
                Confidence = matchResult.Confidence,
                Reason = matchResult.Reason,
                PlaceIdMatch = placeId1 == placeId2,
                GeocodedAddress1 = formatted1,
                GeocodedAddress2 = formatted2,
                DistanceMeters = Math.Round(distance, 2),
                Latitude1 = location1.Lat,
                Longitude1 = location1.Lng,
                Latitude2 = location2.Lat,
                Longitude2 = location2.Lng,
                LocationType1 = result1.Geometry?.LocationType,
                LocationType2 = result2.Geometry?.LocationType
            };
        }

        private (bool Match, double Confidence, string Reason) DetermineMatch(
            string placeId1, string placeId2, 
            string formatted1, string formatted2,
            GoogleResult result1, GoogleResult result2, 
            double distance)
        {
            // Method 1: Place ID comparison (most reliable)
            if (placeId1 == placeId2)
            {
                return (true, 0.95, "Identical Place ID");
            }

            // Method 2: Formatted address comparison
            if (formatted1 == formatted2)
            {
                return (true, 0.90, "Identical formatted address");
            }

            // Method 3: Address component comparison
            var components1 = ExtractAddressComponents(result1.AddressComponents);
            var components2 = ExtractAddressComponents(result2.AddressComponents);

            if (ComponentsMatch(components1, components2))
            {
                return (true, 0.85, "All key address components match");
            }

            // Method 4: Distance-based comparison
            if (distance <= 50) // Same building threshold
            {
                return (true, 0.75, $"Same building ({Math.Round(distance)}m apart)");
            }
            
            if (distance <= 100) // Very close, possibly same property
            {
                return (true, 0.65, $"Very close addresses ({Math.Round(distance)}m apart)");
            }

            if (distance <= 200) // Nearby, manual review suggested
            {
                return (false, 0.30, $"Nearby addresses ({Math.Round(distance)}m apart) - manual review suggested");
            }

            return (false, 0.10, $"Different locations ({Math.Round(distance)}m apart)");
        }

        private Dictionary<string, string> ExtractAddressComponents(GoogleAddressComponent[]? components)
        {
            var extracted = new Dictionary<string, string>
            {
                ["street_number"] = "",
                ["route"] = "",
                ["locality"] = "",
                ["administrative_area_level_1"] = "",
                ["postal_code"] = "",
                ["country"] = ""
            };

            if (components == null) return extracted;

            foreach (var component in components)
            {
                if (component.Types.Contains("street_number"))
                    extracted["street_number"] = component.LongName;
                else if (component.Types.Contains("route"))
                    extracted["route"] = component.LongName;
                else if (component.Types.Contains("locality"))
                    extracted["locality"] = component.LongName;
                else if (component.Types.Contains("administrative_area_level_1"))
                    extracted["administrative_area_level_1"] = component.ShortName;
                else if (component.Types.Contains("postal_code"))
                    extracted["postal_code"] = component.LongName;
                else if (component.Types.Contains("country"))
                    extracted["country"] = component.ShortName;
            }

            return extracted;
        }

        private bool ComponentsMatch(Dictionary<string, string> comp1, Dictionary<string, string> comp2)
        {
            // Key components that must match for same building
            var keyComponents = new[] { "street_number", "route", "locality", "administrative_area_level_1", "postal_code" };
            
            return keyComponents.All(key => 
                comp1.ContainsKey(key) && comp2.ContainsKey(key) &&
                string.Equals(comp1[key], comp2[key], StringComparison.OrdinalIgnoreCase));
        }
    }

    public class AddressMatchResult
    {
        public bool Match { get; set; }
        public double Confidence { get; set; }
        public string Reason { get; set; } = "";
        public bool PlaceIdMatch { get; set; }
        public string GeocodedAddress1 { get; set; } = "";
        public string GeocodedAddress2 { get; set; } = "";
        public double DistanceMeters { get; set; }
        public double Latitude1 { get; set; }
        public double Longitude1 { get; set; }
        public double Latitude2 { get; set; }
        public double Longitude2 { get; set; }
        public string? LocationType1 { get; set; }
        public string? LocationType2 { get; set; }
    }
}