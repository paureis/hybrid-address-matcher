using CCP.AddressMatcher.Models;
using CCP.AddressMatcher.Utils;

namespace CCP.AddressMatcher.Services
{
    public class EnhancedAddressMatchingService
        {   
        private readonly GoogleGeocodingService _geocodingService;
        private readonly USPSValidationService _uspsService;
        private readonly LLMFallbackService _llmService;
        private readonly AddressMatchingService _addressMatchingService;
        private readonly ILogger<EnhancedAddressMatchingService> _logger;

        public EnhancedAddressMatchingService(
            GoogleGeocodingService geocodingService,
            USPSValidationService uspsService,
            LLMFallbackService llmService,
            AddressMatchingService addressMatchingService,
            ILogger<EnhancedAddressMatchingService> logger)
        {
            _geocodingService = geocodingService;
            _uspsService = uspsService;
            _llmService = llmService;
            _addressMatchingService = addressMatchingService;
            _logger = logger;
        }

        public async Task<EnhancedMatchResult> CompareAddressesAsync(string address1, string address2)
        {
            var layers = new List<LayerResult>();

            // Layer 0: Normalize
            var normalized1 = AddressNormalizer.Normalize(address1);
            var normalized2 = AddressNormalizer.Normalize(address2);
            
            if (normalized1?.ToString() == normalized2?.ToString())
            {
                return new EnhancedMatchResult
                {
                    Match = true,
                    Confidence = 1.0,
                    Method = "normalization",
                    Reason = "Identical after normalization",
                    LayersUsed = new[] { "Layer 0: Normalize" }
                };
            }

            // Layer 1: USPS Postal Canonicalisation
            try
            {
                var usps1 = await _uspsService.ValidateAddressAsync(address1);
                var usps2 = await _uspsService.ValidateAddressAsync(address2);

                if (usps1 != null && usps2 != null && usps1 == usps2)
                {
                    return new EnhancedMatchResult
                    {
                        Match = true,
                        Confidence = 0.99,
                        Method = "usps_validation",
                        Reason = "Identical USPS CASS-formatted addresses",
                        LayersUsed = new[] { "Layer 0: Normalize", "Layer 1: USPS Validation" }
                    };
                }
                layers.Add(new LayerResult { Layer = "USPS", Success = usps1 != null && usps2 != null });
            }
            catch (Exception ex)
            {
                _logger.LogWarning("USPS layer failed: {Message}", ex.Message);
                layers.Add(new LayerResult { Layer = "USPS", Success = false });
            }

            // Layer 2-3: Geocoding + Place ID
            try
            {
                // Fixed: Use the correct service and parameters
                var geoResult = await _addressMatchingService.CompareAddressesAsync(address1, address2);
                _logger.LogInformation("Enhanced: Geocoding result - Match: {Match}, Confidence: {Confidence}", geoResult.Match, geoResult.Confidence);
                
                if (geoResult.Confidence >= 0.3)
                {
                   _logger.LogInformation("Enhanced: Using geocoding result");
                    return new EnhancedMatchResult
                    {
                        Match = geoResult.Match,
                        Method = "geocoding",
                        Confidence = geoResult.Confidence,
                        Reason = geoResult.Reason,
                        LayersUsed = new[] { "Layer 0: Normalize", "Layer 1: USPS (attempted)", "Layer 2: Geocoding" },
                        DistanceMeters = geoResult.DistanceMeters
                    };
                }
                else
                {
                    _logger.LogDebug("Enhanced: Geocoding confidence too low, continuing to next layer");
                }
                layers.Add(new LayerResult { Layer = "Geocoding", Success = true, Result = geoResult });
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Enhanced: Geocoding failed: {Message}", ex.Message);
                layers.Add(new LayerResult { Layer = "Geocoding", Success = false });
            }

            // Layer 4: LLM Fallback
            try
            {
                var context = BuildContextFromPreviousLayers(layers);
                var llmResult = await _llmService.EvaluateAddressMatchAsync(address1, address2, context);
                
                return new EnhancedMatchResult
                {
                    Match = llmResult.Match,
                    Confidence = llmResult.Confidence,
                    Method = "llm_fallback",
                    Reason = llmResult.Reasoning,
                    LayersUsed = new[] { "Layer 0: Normalize", "Layer 1: USPS", "Layer 2: Geocoding", "Layer 4: LLM Fallback" }
                };
            }
            catch (Exception ex)
            {
                // If all layers fail, return undetermined
                return new EnhancedMatchResult
                {
                    Match = false,
                    Confidence = 0.01,
                    Method = "undetermined",
                    Reason = $"All validation layers failed: {ex.Message}",
                    LayersUsed = layers.Select(l => l.Layer).ToArray()
                };
            }
        }

        private string BuildContextFromPreviousLayers(List<LayerResult> layers)
        {
            var context = string.Join("; ", layers.Select(l => 
                $"{l.Layer}: {(l.Success ? "Success" : "Failed")}"));
            return context;
        }
    }

    public class EnhancedMatchResult
    {
        public bool Match { get; set; }
        public double Confidence { get; set; }
        public string Method { get; set; } = "";
        public string Reason { get; set; } = "";
        public string[] LayersUsed { get; set; } = Array.Empty<string>();
        public double? DistanceMeters { get; set; }
    }

    public class LayerResult
    {
        public string Layer { get; set; } = "";
        public bool Success { get; set; }
        public AddressMatchResult? Result { get; set; }
    }
}