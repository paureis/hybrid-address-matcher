// GoogleAddressValidatorService.cs with advanced validation filtering
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using CCP.AddressMatcher.Models;

namespace CCP.AddressMatcher.Services
{
    public class GoogleAddressValidatorService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        public GoogleAddressValidatorService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["GoogleApiKey"];
        }

        public async Task<string?> NormalizeAddressAsync(string rawAddress)
        {
            var requestUri = $"https://maps.googleapis.com/maps/api/geocode/json?address={Uri.EscapeDataString(rawAddress)}&key={_apiKey}";

            try
            {
                var response = await _httpClient.GetAsync(requestUri);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var geocodeResponse = JsonSerializer.Deserialize<GoogleGeocodeResponse>(content, _jsonOptions);

                if (geocodeResponse?.Status == "OK" && geocodeResponse.Results.Length > 0)
                {
                    var result = geocodeResponse.Results[0];

                    // Advanced validation filtering:
                    if (result.PartialMatch == true)
                    {
                        Console.WriteLine("Rejected due to partial match.");
                        return null;
                    }

                    if (result.Geometry.LocationType != "ROOFTOP")
                    {
                        Console.WriteLine($"Rejected due to low confidence: {result.Geometry.LocationType}");
                        return null;
                    }

                    return result.FormattedAddress;
                }
                else
                {
                    Console.WriteLine($"Google API returned status: {geocodeResponse?.Status}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during Google address validation: {ex.Message}");
            }

            return null;
        }
    }
}
