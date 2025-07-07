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

            // Add debugging for street numbers
            Console.WriteLine($"Address 1 components: Street={components1["street_number"]}, Route={components1["route"]}");
            Console.WriteLine($"Address 2 components: Street={components2["street_number"]}, Route={components2["route"]}");

            if (ComponentsMatch(components1, components2))
            {
                return (true, 0.85, "All key address components match");
            }

            // Method 4: Enhanced distance-based comparison with stricter thresholds
            if (distance <= 5) // Very strict - same entrance/building
            {
                return (true, 0.80, $"Same building entrance ({Math.Round(distance)}m apart)");
            }
            
            if (distance <= 15) // Strict - likely same building but different entrances
            {
                // Additional check: same street number
                var sameStreetNum = SameStreetNumber(components1, components2);
                Console.WriteLine($"Same street number check: {sameStreetNum}");
                
                if (sameStreetNum)
                {
                    return (true, 0.75, $"Same building ({Math.Round(distance)}m apart)");
                }
                else
                {
                    // Add more detailed reasoning about why they don't match
                    var street1 = components1["street_number"];
                    var street2 = components2["street_number"];
                    return (false, 0.40, $"Different buildings - street numbers don't match ({street1} vs {street2}), {Math.Round(distance)}m apart");
                }
            }

            if (distance <= 100) // Medium - possibly same property/complex
            {
                // Only match if explicitly same address components
                if (SameStreetNumber(components1, components2) && SameStreet(components1, components2))
                {
                    return (true, 0.65, $"Same property complex ({Math.Round(distance)}m apart)");
                }
                else
                {
                    return (false, 0.25, $"Nearby different buildings ({Math.Round(distance)}m apart)");
                }
            }

            if (distance <= 500) // Large - corporate campus consideration
            {
                // Special handling for known corporate campuses
                if (IsPotentialCorporateCampus(components1, components2, distance))
                {
                    return (true, 0.60, $"Potential corporate campus ({Math.Round(distance)}m apart) - review recommended");
                }
                else
                {
                    return (false, 0.20, $"Different locations ({Math.Round(distance)}m apart)");
                }
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

        // Improved helper method to check if addresses have same street number
        private bool SameStreetNumber(Dictionary<string, string> comp1, Dictionary<string, string> comp2)
        {
            var streetNum1 = comp1["street_number"];
            var streetNum2 = comp2["street_number"];
            
            // If either is empty, try to extract from formatted address as fallback
            if (string.IsNullOrEmpty(streetNum1) || string.IsNullOrEmpty(streetNum2))
            {
                Console.WriteLine("Street number missing from components, trying fallback extraction");
                return false; // Conservative approach - if we can't extract street numbers, don't match
            }
            
            // Extract numeric part from street numbers (handles cases like 221B vs 223)
            var num1 = ExtractNumericPart(streetNum1);
            var num2 = ExtractNumericPart(streetNum2);
            
            Console.WriteLine($"Extracted numbers: {num1} vs {num2}");
            
            if (num1 == null || num2 == null)
                return false;
            
            // Must be exactly the same number
            return num1 == num2;
        }

        // Helper method to extract numeric part from street number
        private int? ExtractNumericPart(string streetNumber)
        {
            if (string.IsNullOrEmpty(streetNumber))
                return null;
            
            // Use regex to extract first number from string like "221B" -> 221
            var match = System.Text.RegularExpressions.Regex.Match(streetNumber, @"^\d+");
            if (match.Success && int.TryParse(match.Value, out int number))
            {
                return number;
            }
            
            return null;
        }

        // Helper method to check if addresses have same street
        private bool SameStreet(Dictionary<string, string> comp1, Dictionary<string, string> comp2)
        {
            return !string.IsNullOrEmpty(comp1["route"]) && 
                   !string.IsNullOrEmpty(comp2["route"]) &&
                   string.Equals(comp1["route"], comp2["route"], StringComparison.OrdinalIgnoreCase);
        }

        // Helper method for corporate campus detection
        private bool IsPotentialCorporateCampus(Dictionary<string, string> comp1, Dictionary<string, string> comp2, double distance)
        {
            // Same city and within reasonable campus distance
            var sameCity = string.Equals(comp1["locality"], comp2["locality"], StringComparison.OrdinalIgnoreCase);
            var reasonableDistance = distance <= 1000; // 1km max for campus
            
            // Known corporate campus patterns
            var address1 = $"{comp1["route"]} {comp1["locality"]}".ToLower();
            var address2 = $"{comp2["route"]} {comp2["locality"]}".ToLower();
            
            // Microsoft campus detection
            var microsoftCampus = (address1.Contains("microsoft") || address1.Contains("redmond")) &&
                                 (address2.Contains("microsoft") || address2.Contains("redmond")) &&
                                 comp1["locality"].Equals("redmond", StringComparison.OrdinalIgnoreCase);
            
            // Add other known corporate campuses here as needed
            // var appleCampus = (address1.Contains("infinite loop") || address1.Contains("cupertino")) &&
            //                   (address2.Contains("infinite loop") || address2.Contains("cupertino")) &&
            //                   comp1["locality"].Equals("cupertino", StringComparison.OrdinalIgnoreCase);
            
            // var googleCampus = (address1.Contains("amphitheatre") || address1.Contains("mountain view")) &&
            //                    (address2.Contains("amphitheatre") || address2.Contains("mountain view")) &&
            //                    comp1["locality"].Equals("mountain view", StringComparison.OrdinalIgnoreCase);
            
            return sameCity && reasonableDistance && microsoftCampus;
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