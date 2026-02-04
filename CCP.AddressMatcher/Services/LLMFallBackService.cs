using System.Text.Json;
using System.Text;
using System.Text.Json.Serialization;

namespace CCP.AddressMatcher.Services
{
    public class LLMFallbackService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly string _openAiApiKey;
    private readonly ILogger<LLMFallbackService> _logger;

    public LLMFallbackService(HttpClient httpClient, IConfiguration configuration, ILogger<LLMFallbackService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _openAiApiKey = _configuration["OpenAIApiKey"] ?? throw new InvalidOperationException("OpenAI API Key not configured");
        _logger = logger;
    }

        public async Task<LLMMatchResult> EvaluateAddressMatchAsync(string address1, string address2, string context = "")
        {
            try
            {
                _logger.LogInformation("LLM Evaluation - Address1: {Address1}, Address2: {Address2}", address1, address2);
                
                var prompt = BuildPrompt(address1, address2, context);
                var response = await CallOpenAI(prompt);
                return ParseLLMResponse(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LLM Error");
                throw new Exception($"LLM evaluation failed: {ex.Message}", ex);
            }
        }

        private string BuildPrompt(string address1, string address2, string context)
        {
            return $@"You are an expert address matching system. Determine if these two addresses refer to the same building/location.

Address 1: {address1}
Address 2: {address2}

Context from previous layers: {context}

Consider:
- Same building with different units/suites should match
- Different buildings on same street should NOT match
- Corporate campuses may have multiple valid addresses
- Typos and abbreviation differences should be handled

Respond with JSON only:
{{
    ""match"": true/false,
    ""confidence"": 0.0-1.0,
    ""reasoning"": ""explanation""
}}";
        }

        private async Task<string> CallOpenAI(string prompt)
        {
            try
            {
                var request = new
                {
                    model = "gpt-4o-mini",
                    messages = new[]
                    {
                        new { role = "system", content = "You are an expert address validation system. Always respond with valid JSON." },
                        new { role = "user", content = prompt }
                    },
                    temperature = 0.1,
                    max_tokens = 200
                };

                var json = JsonSerializer.Serialize(request);
                _logger.LogDebug("OpenAI Request: {Json}", json);
                
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                // Clear any existing auth headers
                _httpClient.DefaultRequestHeaders.Authorization = null;
                
                // Set the authorization header
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _openAiApiKey);

                var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
                
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("OpenAI Response Status: {StatusCode}", response.StatusCode);
                _logger.LogDebug("OpenAI Response: {ResponseContent}", responseContent);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"OpenAI API error: {response.StatusCode} - {responseContent}");
                }

                var responseData = JsonSerializer.Deserialize<OpenAIResponse>(responseContent);
                
                if (responseData?.Choices == null || responseData.Choices.Length == 0)
                {
                    throw new Exception($"No choices in OpenAI response: {responseContent}");
                }

                var messageContent = responseData.Choices[0]?.Message?.Content;
                if (string.IsNullOrEmpty(messageContent))
                {
                    throw new Exception($"Empty message content in OpenAI response: {responseContent}");
                }

                return messageContent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OpenAI Call Error");
                throw;
            }
        }

        private LLMMatchResult ParseLLMResponse(string response)
        {
            try
            {
                _logger.LogDebug("Parsing LLM Response: {Response}", response);
                
                var result = JsonSerializer.Deserialize<LLMMatchResult>(response, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });
                
                if (result == null)
                {
                    throw new Exception("Failed to deserialize LLM response");
                }

                return result;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "JSON Parse Error");
                return new LLMMatchResult 
                { 
                    Match = false, 
                    Confidence = 0.0, 
                    Reasoning = $"Failed to parse JSON response: {response}" 
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Parse Error");
                return new LLMMatchResult 
                { 
                    Match = false, 
                    Confidence = 0.0, 
                    Reasoning = $"Parsing error: {ex.Message}" 
                };
            }
        }
    }

    public class LLMMatchResult
    {
        public bool Match { get; set; }
        public double Confidence { get; set; }
        public string Reasoning { get; set; } = "";
    }

    public class OpenAIResponse
    {
        [JsonPropertyName("choices")]
        public Choice[]? Choices { get; set; }
        
        [JsonPropertyName("error")]
        public OpenAIError? Error { get; set; }
    }

    public class Choice
    {
        [JsonPropertyName("message")]
        public Message? Message { get; set; }
    }

    public class Message
{
    [JsonPropertyName("content")]
    public string? Content { get; set; }
    
    [JsonPropertyName("role")]
    public string? Role { get; set; }
}

    public class OpenAIError
    {
        [JsonPropertyName("message")]
        public string? Message { get; set; }
        
        [JsonPropertyName("type")]
        public string? Type { get; set; }
    }
}