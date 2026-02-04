using CCP.AddressMatcher.Models;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace CCP.AddressMatcher.Services
{
    public class GoogleGeocodingService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly IDistributedCache _cache;
        private readonly ILogger<GoogleGeocodingService> _logger;
        private readonly string _apiKey;

        public GoogleGeocodingService(
            HttpClient httpClient, 
            IConfiguration configuration, 
            IDistributedCache cache,
            ILogger<GoogleGeocodingService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _cache = cache;
            _logger = logger;
            _apiKey = _configuration["GoogleApiKey"] ?? throw new InvalidOperationException("Google API Key not configured");
        }

        public async Task<GoogleGeocodeResponse?> GeocodeAddressAsync(string address)
        {
            // Check cache first to avoid unnecessary API calls
            var cacheKey = $"geocode_{address}";
            
            try
            {
                var cachedJson = await _cache.GetStringAsync(cacheKey);
                if (!string.IsNullOrEmpty(cachedJson))
                {
                    _logger.LogDebug("Cache hit for address: {Address}", address);
                    return JsonSerializer.Deserialize<GoogleGeocodeResponse>(cachedJson);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cache retrieval failed for {Address}, proceeding without cache", address);
            }

            try
            {
                var encodedAddress = Uri.EscapeDataString(address);
                var url = $"https://maps.googleapis.com/maps/api/geocode/json?address={encodedAddress}&key={_apiKey}";

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var jsonContent = await response.Content.ReadAsStringAsync();
                var geocodeResponse = JsonSerializer.Deserialize<GoogleGeocodeResponse>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                // Cache the result for 1 hour to reduce API calls
                if (geocodeResponse != null)
                {
                    try
                    {
                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
                        };
                        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(geocodeResponse), cacheOptions);
                        _logger.LogDebug("Cached geocoding result for: {Address}", address);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to cache result for {Address}", address);
                    }
                }

                return geocodeResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Geocoding failed for address: {Address}", address);
                throw new Exception($"Geocoding failed for address '{address}': {ex.Message}", ex);
            }
        }

        public static double CalculateDistance(double lat1, double lng1, double lat2, double lng2)
        {
            const double R = 6371000; // Earth's radius in meters
            var dLat = (lat2 - lat1) * Math.PI / 180;
            var dLng = (lng2 - lng1) * Math.PI / 180;
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                    Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }
    }
}