using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;

namespace CCP.AddressMatcher.Services
{
    public class USPSValidationService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly string? _apiKey;
        private readonly string? _clientSecret;
        private readonly bool _isConfigured;
        private readonly bool _mockMode;
        private string? _accessToken;
        private DateTime _tokenExpiry;

        public USPSValidationService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _apiKey = _configuration["USPSApiKey"] ?? _configuration["USPSUserId"];
            _clientSecret = _configuration["USPSClientSecret"]; // You might need this too
            _isConfigured = !string.IsNullOrEmpty(_apiKey) && _apiKey != "your-usps-user-id" && _apiKey.Length > 10;
            _mockMode = !_isConfigured;
            
            if (_mockMode)
            {
                Console.WriteLine("USPS running in MOCK MODE - using simulated validation");
            }
            else
            {
                Console.WriteLine($"USPS OAuth2 configured with API Key: {_apiKey?.Substring(0, 10)}...");
            }
        }

        public async Task<string?> ValidateAddressAsync(string address)
        {
            if (_mockMode)
            {
                return await MockUSPSValidation(address);
            }
            
            return await OAuth2USPSValidation(address);
        }

        private async Task<string?> OAuth2USPSValidation(string address)
        {
            try
            {
                Console.WriteLine($"OAuth2 USPS validation for: {address}");
                
                // Skip international addresses
                if (IsInternationalAddress(address))
                {
                    Console.WriteLine("USPS: Skipping international address");
                    return null;
                }

                // Get OAuth2 access token first
                var accessToken = await GetAccessTokenAsync();
                if (string.IsNullOrEmpty(accessToken))
                {
                    Console.WriteLine("USPS: Failed to get access token");
                    return null;
                }

                var addressParts = ParseAddress(address);
                
                var requestData = new
                {
                    streetAddress = addressParts.Street,
                    city = addressParts.City,
                    state = addressParts.State,
                    zipCode = addressParts.Zip
                };

                var json = JsonSerializer.Serialize(requestData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                // Use Bearer token authentication
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
                
                var response = await _httpClient.PostAsync("https://api.usps.com/addresses/v3/address", content);
                
                var responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"USPS OAuth2 Response Status: {response.StatusCode}");
                Console.WriteLine($"USPS OAuth2 Response: {responseContent}");

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"USPS OAuth2 error: {response.StatusCode} - {responseContent}");
                    return null;
                }

                return ParseUSPSResponse(responseContent);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OAuth2 USPS validation failed: {ex.Message}");
                return null;
            }
        }

        private async Task<string?> GetAccessTokenAsync()
        {
            try
            {
                // Check if we have a valid token
                if (!string.IsNullOrEmpty(_accessToken) && DateTime.Now < _tokenExpiry)
                {
                    return _accessToken;
                }

                Console.WriteLine("USPS: Getting new OAuth2 access token...");

                // Prepare OAuth2 request
                var tokenRequest = new List<KeyValuePair<string, string>>
                {
                    new("grant_type", "client_credentials"),
                    new("client_id", _apiKey!),
                    new("scope", "addresses") // Might need to adjust scope
                };

                // Add client secret if available
                if (!string.IsNullOrEmpty(_clientSecret))
                {
                    tokenRequest.Add(new("client_secret", _clientSecret));
                }

                var formContent = new FormUrlEncodedContent(tokenRequest);

                // OAuth2 token endpoint
                var response = await _httpClient.PostAsync("https://api.usps.com/oauth2/v3/token", formContent);
                var responseContent = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"OAuth2 Token Response Status: {response.StatusCode}");
                Console.WriteLine($"OAuth2 Token Response: {responseContent}");

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"OAuth2 token request failed: {response.StatusCode} - {responseContent}");
                    return null;
                }

                var tokenResponse = JsonSerializer.Deserialize<USPSTokenResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (tokenResponse?.AccessToken != null)
                {
                    _accessToken = tokenResponse.AccessToken;
                    _tokenExpiry = DateTime.Now.AddSeconds(tokenResponse.ExpiresIn - 60); // Refresh 1 minute early
                    Console.WriteLine($"USPS: Got access token, expires in {tokenResponse.ExpiresIn} seconds");
                    return _accessToken;
                }

                Console.WriteLine("USPS: Failed to parse token response");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OAuth2 token request failed: {ex.Message}");
                return null;
            }
        }

        private string? ParseUSPSResponse(string responseJson)
        {
            try
            {
                var response = JsonSerializer.Deserialize<USPSAddressResponse>(responseJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (response?.Address != null)
                {
                    var standardized = $"{response.Address.StreetAddress}, {response.Address.City}, {response.Address.State} {response.Address.ZipCode}".ToUpper();
                    Console.WriteLine($"USPS standardized: {standardized}");
                    return standardized;
                }

                Console.WriteLine("USPS: No valid address in response");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to parse USPS response: {ex.Message}");
                return null;
            }
        }

        private async Task<string?> MockUSPSValidation(string address)
        {
            Console.WriteLine($"MOCK USPS validation for: {address}");
            await Task.Delay(100);
            
            if (IsInternationalAddress(address))
            {
                Console.WriteLine("MOCK USPS: Skipping international address");
                return null;
            }
            
            var normalized = NormalizeAddressForUSPS(address);
            if (!string.IsNullOrEmpty(normalized))
            {
                Console.WriteLine($"MOCK USPS result: {normalized}");
                return normalized;
            }
            
            Console.WriteLine("MOCK USPS: Address could not be validated");
            return null;
        }

        private bool IsInternationalAddress(string address)
        {
            var lowerAddress = address.ToLower();
            return lowerAddress.Contains("uk") || 
                   lowerAddress.Contains("london") ||
                   lowerAddress.Contains("united kingdom") ||
                   lowerAddress.Contains("canada");
        }

        private string? NormalizeAddressForUSPS(string address)
        {
            try
            {
                var parts = ParseAddress(address);
                var standardized = StandardizeForUSPS(parts);
                return $"{standardized.Street.ToUpper()}, {standardized.City.ToUpper()}, {standardized.State.ToUpper()} {standardized.Zip}";
            }
            catch
            {
                return null;
            }
        }

        private AddressParts StandardizeForUSPS(AddressParts parts)
        {
            return new AddressParts
            {
                Street = StandardizeStreet(parts.Street),
                City = parts.City?.Trim() ?? "",
                State = StandardizeState(parts.State),
                Zip = StandardizeZip(parts.Zip)
            };
        }

        private string StandardizeStreet(string street)
        {
            if (string.IsNullOrEmpty(street)) return street;
            
            var standardized = street
                .Replace("St.", "ST").Replace("Street", "ST")
                .Replace("Ave.", "AVE").Replace("Avenue", "AVE")
                .Replace("Blvd.", "BLVD").Replace("Boulevard", "BLVD");
                
            standardized = System.Text.RegularExpressions.Regex.Replace(standardized, @"\s+(Suite|Apt|Unit|#)\s*\d+.*", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return standardized.Trim();
        }

        private string StandardizeState(string state)
        {
            if (string.IsNullOrEmpty(state)) return state;
            
            var stateMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                {"california", "CA"}, {"florida", "FL"}, {"new york", "NY"},
                {"washington", "WA"}, {"texas", "TX"}
            };
            
            return stateMap.TryGetValue(state.Trim(), out var abbr) ? abbr : state.ToUpper().Trim();
        }

        private string StandardizeZip(string zip)
        {
            if (string.IsNullOrEmpty(zip)) return zip;
            var match = System.Text.RegularExpressions.Regex.Match(zip, @"\d{5}");
            return match.Success ? match.Value : zip;
        }

        private AddressParts ParseAddress(string address)
        {
            var parts = address.Split(',').Select(p => p.Trim()).ToArray();
            return new AddressParts
            {
                Street = parts.Length > 0 ? parts[0] : "",
                City = parts.Length > 1 ? parts[1] : "",
                State = parts.Length > 2 ? parts[2].Split(' ').FirstOrDefault() ?? "" : "",
                Zip = parts.Length > 2 ? parts[2].Split(' ').LastOrDefault() ?? "" : ""
            };
        }
    }

    // OAuth2 Token Response
    public class USPSTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = "";
        
        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = "";
        
        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
        
        [JsonPropertyName("scope")]
        public string Scope { get; set; } = "";
    }

    // Address Response Models
    public class USPSAddressResponse
    {
        [JsonPropertyName("address")]
        public USPSAddress? Address { get; set; }
    }

    public class USPSAddress
    {
        [JsonPropertyName("streetAddress")]
        public string StreetAddress { get; set; } = "";
        
        [JsonPropertyName("city")]
        public string City { get; set; } = "";
        
        [JsonPropertyName("state")]
        public string State { get; set; } = "";
        
        [JsonPropertyName("zipCode")]
        public string ZipCode { get; set; } = "";
    }

    public class AddressParts
    {
        public string Street { get; set; } = "";
        public string City { get; set; } = "";
        public string State { get; set; } = "";
        public string Zip { get; set; } = "";
    }
}