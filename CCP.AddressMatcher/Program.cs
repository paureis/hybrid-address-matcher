using CCP.AddressMatcher.Services;
using CCP.AddressMatcher.Utils;
using CCP.AddressMatcher.Models;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();
builder.Services.AddScoped<GoogleAddressValidatorService>();
builder.Services.AddScoped<GoogleGeocodingService>();
builder.Services.AddScoped<AddressMatchingService>();
builder.Services.AddScoped<USPSValidationService>();
builder.Services.AddScoped<LLMFallbackService>();  
builder.Services.AddScoped<EnhancedAddressMatchingService>();

// Add memory cache for geocoding results to reduce API calls
builder.Services.AddMemoryCache();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy => policy.WithOrigins(
            "https://ccp-address-matcher.vercel.app",
            "http://localhost:5173",  // Vite dev server
            "http://localhost:3000"   // Alternative React dev port
        )
        .AllowAnyMethod()
        .AllowAnyHeader());
});

var app = builder.Build();

app.UseCors("AllowFrontend");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// 🧠 Local-only address comparison (keep existing)
app.MapPost("/api/compare-addresses", (CompareRequest request) =>
{
    var normalized1 = AddressNormalizer.Normalize(request.Address1);
    var normalized2 = AddressNormalizer.Normalize(request.Address2);

    return Results.Ok(new
    {
        normalizedAddress1 = normalized1,
        normalizedAddress2 = normalized2
    });
});

// 🌐 Google-only validation (keep existing)
app.MapPost("/api/google-validate", async (GoogleAddressValidatorService validator, GoogleRequest request) =>
{
    var validated = await validator.NormalizeAddressAsync(request.Address);

    if (validated != null &&
        (validated.Any(char.IsDigit) &&
         (validated.Contains("Street") || validated.Contains("St") ||
          validated.Contains("Ave") || validated.Contains("Avenue") ||
          validated.Contains("Blvd") || validated.Contains("Road") || validated.Contains("Rd"))))
    {
        return Results.Ok(new { formatted = validated });
    }
    else
    {
        return Results.BadRequest(new { error = "Validation failed: Address is incomplete, malformed, or could not be verified by Google." });
    }
});

// 🪐 Hybrid comparison (keep existing)
app.MapPost("/api/hybrid-compare", (CompareRequest request) =>
{
    var normalized1 = AddressNormalizer.Normalize(request.Address1);
    var normalized2 = AddressNormalizer.Normalize(request.Address2);

    bool isMatch = string.Equals(normalized1.ToString(), normalized2.ToString(), StringComparison.OrdinalIgnoreCase);

    List<string> differences = new();
    if (!isMatch)
    {
        differences.Add($"Address1: {normalized1}");
        differences.Add($"Address2: {normalized2}");
    }

    return Results.Ok(new
    {
        normalizedAddress1 = normalized1.ToString(),
        normalizedAddress2 = normalized2.ToString(),
        match = isMatch,
        differences = differences
    });
});

// 🎯 Geocoding-based comparison endpoint (keep existing)
app.MapPost("/api/geocoding-compare", async (AddressMatchingService matchingService, CompareRequest request) =>
{
    try
    {
        var result = await matchingService.CompareAddressesAsync(request.Address1, request.Address2);
        
        return Results.Ok(new
        {
            match = result.Match,
            confidence = result.Confidence,
            reason = result.Reason,
            geocodedAddresses = new
            {
                address1 = result.GeocodedAddress1,
                address2 = result.GeocodedAddress2
            },
            distanceMeters = result.DistanceMeters,
            placeIdMatch = result.PlaceIdMatch,
            details = new
            {
                locationTypes = new
                {
                    address1 = result.LocationType1,
                    address2 = result.LocationType2
                },
                coordinates = new
                {
                    address1 = new { lat = result.Latitude1, lng = result.Longitude1 },
                    address2 = new { lat = result.Latitude2, lng = result.Longitude2 }
                }
            },
            rawAddresses = new
            {
                address1 = request.Address1,
                address2 = request.Address2
            }
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// 🔄 Smart comparison endpoint (keep existing)
app.MapPost("/api/smart-compare", async (AddressMatchingService matchingService, CompareRequest request) =>
{
    try
    {
        // Try geocoding first
        var geocodingResult = await matchingService.CompareAddressesAsync(request.Address1, request.Address2);
        
        // If geocoding worked well, return that result
        if (geocodingResult.Confidence >= 0.7)
        {
            return Results.Ok(new
            {
                method = "geocoding",
                match = geocodingResult.Match,
                confidence = geocodingResult.Confidence,
                reason = geocodingResult.Reason,
                geocodedAddresses = new
                {
                    address1 = geocodingResult.GeocodedAddress1,
                    address2 = geocodingResult.GeocodedAddress2
                },
                distanceMeters = geocodingResult.DistanceMeters
            });
        }
        
        // Fall back to local normalization if geocoding confidence is low
        var normalized1 = AddressNormalizer.Normalize(request.Address1);
        var normalized2 = AddressNormalizer.Normalize(request.Address2);
        bool localMatch = string.Equals(normalized1.ToString(), normalized2.ToString(), StringComparison.OrdinalIgnoreCase);
        
        return Results.Ok(new
        {
            method = "local_fallback",
            match = localMatch,
            confidence = localMatch ? 0.6 : 0.3, // Lower confidence for local matching
            reason = localMatch ? "Local normalization match" : "No match found with either method",
            normalizedAddresses = new
            {
                address1 = normalized1.ToString(),
                address2 = normalized2.ToString()
            },
            geocodingAttempt = new
            {
                lowConfidence = true,
                confidence = geocodingResult.Confidence,
                reason = geocodingResult.Reason
            }
        });
    }
    catch (Exception ex)
    {
        // If geocoding fails completely, fall back to local comparison
        var normalized1 = AddressNormalizer.Normalize(request.Address1);
        var normalized2 = AddressNormalizer.Normalize(request.Address2);
        bool localMatch = string.Equals(normalized1.ToString(), normalized2.ToString(), StringComparison.OrdinalIgnoreCase);
        
        return Results.Ok(new
        {
            method = "local_error_fallback",
            match = localMatch,
            confidence = localMatch ? 0.6 : 0.3,
            reason = localMatch ? "Local normalization match (geocoding failed)" : "No match found (geocoding failed)",
            error = ex.Message,
            normalizedAddresses = new
            {
                address1 = normalized1.ToString(),
                address2 = normalized2.ToString()
            }
        });
    }
});

// 🏆 NEW: Enhanced multi-layer comparison endpoint (ALL 5 LAYERS)
app.MapPost("/api/enhanced-compare", async (EnhancedAddressMatchingService enhancedService, CompareRequest request) =>
{
    try
    {
        var result = await enhancedService.CompareAddressesAsync(request.Address1, request.Address2);
        
        return Results.Ok(new
        {
            match = result.Match,
            confidence = result.Confidence,
            method = result.Method,
            reason = result.Reason,
            layersUsed = result.LayersUsed,
            distanceMeters = result.DistanceMeters,
            rawAddresses = new
            {
                address1 = request.Address1,
                address2 = request.Address2
            }
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// 🧪 NEW: USPS validation testing endpoint
app.MapPost("/api/usps-validate", async (USPSValidationService uspsService, GoogleRequest request) =>
{
    try
    {
        var validated = await uspsService.ValidateAddressAsync(request.Address);
        
        if (validated != null)
        {
            return Results.Ok(new 
            { 
                success = true,
                originalAddress = request.Address,
                validatedAddress = validated 
            });
        }
        else
        {
            return Results.BadRequest(new { success = false, error = "USPS validation failed" });
        }
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, error = ex.Message });
    }
});

// 🤖 NEW: LLM fallback testing endpoint
app.MapPost("/api/llm-evaluate", async (LLMFallbackService llmService, LLMRequest request) =>
{
    try
    {
        var result = await llmService.EvaluateAddressMatchAsync(request.Address1, request.Address2, request.Context ?? "");
        
        return Results.Ok(new
        {
            match = result.Match,
            confidence = result.Confidence,
            reasoning = result.Reasoning,
            rawAddresses = new
            {
                address1 = request.Address1,
                address2 = request.Address2
            }
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// 📊 Batch comparison endpoint (keep existing)
app.MapPost("/api/batch-compare", async (AddressMatchingService matchingService, BatchCompareRequest request) =>
{
    var results = new List<object>();
    
    foreach (var pair in request.AddressPairs)
    {
        try
        {
            var result = await matchingService.CompareAddressesAsync(pair.Address1, pair.Address2);
            results.Add(new
            {
                address1 = pair.Address1,
                address2 = pair.Address2,
                expected = pair.Expected,
                match = result.Match,
                confidence = result.Confidence,
                reason = result.Reason,
                distanceMeters = result.DistanceMeters,
                correct = pair.Expected == null ? (bool?)null : pair.Expected == result.Match
            });
        }
        catch (Exception ex)
        {
            results.Add(new
            {
                address1 = pair.Address1,
                address2 = pair.Address2,
                expected = pair.Expected,
                error = ex.Message,
                match = false,
                confidence = 0.0
            });
        }
        
        // Add small delay to respect API rate limits
        await Task.Delay(100);
    }
    
    var summary = new
    {
        totalPairs = results.Count,
        correctPredictions = results.Count(r => {
            var correctProp = r.GetType().GetProperty("correct")?.GetValue(r) as bool?;
            return correctProp == true;
        }),
        accuracy = CalculateAccuracy(results)
    };
    
    return Results.Ok(new
    {
        results = results,
        summary = summary
    });
});

// 🎯 NEW: Enhanced batch comparison with all layers
app.MapPost("/api/enhanced-batch-compare", async (EnhancedAddressMatchingService enhancedService, BatchCompareRequest request) =>
{
    var results = new List<object>();
    
    foreach (var pair in request.AddressPairs)
    {
        try
        {
            var result = await enhancedService.CompareAddressesAsync(pair.Address1, pair.Address2);
            results.Add(new
            {
                address1 = pair.Address1,
                address2 = pair.Address2,
                expected = pair.Expected,
                match = result.Match,
                confidence = result.Confidence,
                method = result.Method,
                reason = result.Reason,
                layersUsed = result.LayersUsed,
                correct = pair.Expected == null ? (bool?)null : pair.Expected == result.Match
            });
        }
        catch (Exception ex)
        {
            results.Add(new
            {
                address1 = pair.Address1,
                address2 = pair.Address2,
                expected = pair.Expected,
                error = ex.Message,
                match = false,
                confidence = 0.0,
                method = "error"
            });
        }
        
        // Add small delay to respect API rate limits
        await Task.Delay(150);
    }
    
    var summary = new
    {
        totalPairs = results.Count,
        correctPredictions = results.Count(r => {
            var correctProp = r.GetType().GetProperty("correct")?.GetValue(r) as bool?;
            return correctProp == true;
        }),
        accuracy = CalculateAccuracy(results),
        methodBreakdown = results.GroupBy(r => r.GetType().GetProperty("method")?.GetValue(r)?.ToString() ?? "unknown")
                                 .ToDictionary(g => g.Key, g => g.Count())
    };
    
    return Results.Ok(new
    {
        results = results,
        summary = summary
    });
});

// 🧪 Simple OpenAI test endpoint
app.MapPost("/api/test-openai-simple", async () =>
{
    try
    {
        var httpClient = new HttpClient();
        var apiKey = builder.Configuration["OpenAIApiKey"];
        
        Console.WriteLine($"Testing with API Key: {apiKey?.Substring(0, 20)}...");
        
        var request = new
        {
            model = "gpt-4o-mini",
            messages = new[] { new { role = "user", content = "Say hello" } },
            max_tokens = 10
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        
        httpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        var response = await httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
        var responseText = await response.Content.ReadAsStringAsync();
        
        Console.WriteLine($"Raw OpenAI Response: {responseText}");
        
        return Results.Ok(new { 
            status = response.StatusCode.ToString(), 
            response = responseText 
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
        return Results.BadRequest(new { error = ex.Message });
    }
});

// 🎯 Simple Enhanced Compare (working version)
app.MapPost("/api/simple-enhanced-compare", async (AddressMatchingService matchingService, LLMFallbackService llmService, CompareRequest request) =>
{
    try
    {
        Console.WriteLine($"Simple Enhanced: Starting comparison for {request.Address1} vs {request.Address2}");
        
        // Layer 0: Normalization
        var normalized1 = AddressNormalizer.Normalize(request.Address1);
        var normalized2 = AddressNormalizer.Normalize(request.Address2);
        
        if (normalized1?.ToString() == normalized2?.ToString())
        {
            Console.WriteLine("Simple Enhanced: Match found at normalization layer");
            return Results.Ok(new
            {
                match = true,
                confidence = 1.0,
                method = "normalization",
                reason = "Identical after normalization",
                layersUsed = new[] { "Layer 0: Normalize" }
            });
        }

        // Layer 2-3: Geocoding + Place ID
        try
        {
            var geoResult = await matchingService.CompareAddressesAsync(request.Address1, request.Address2);
            Console.WriteLine($"Simple Enhanced: Geocoding result - Match: {geoResult.Match}, Confidence: {geoResult.Confidence}");
            
            if (geoResult.Confidence >= 0.3)
            {
                Console.WriteLine($"Simple Enhanced: Using geocoding result");
                return Results.Ok(new
                {
                    match = geoResult.Match,
                    confidence = geoResult.Confidence,
                    method = "geocoding",
                    reason = geoResult.Reason,
                    layersUsed = new[] { "Layer 0: Normalize", "Layer 2: Geocoding" },
                    distanceMeters = geoResult.DistanceMeters
                });
            }
            else
            {
                Console.WriteLine($"Simple Enhanced: Geocoding confidence too low ({geoResult.Confidence}), going to LLM");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Simple Enhanced: Geocoding failed: {ex.Message}");
        }

        // Layer 4: LLM Fallback
        try
        {
            Console.WriteLine("Simple Enhanced: Using LLM fallback");
            var llmResult = await llmService.EvaluateAddressMatchAsync(request.Address1, request.Address2, "Previous layers failed or low confidence");
            
            return Results.Ok(new
            {
                match = llmResult.Match,
                confidence = llmResult.Confidence,
                method = "llm_fallback",
                reason = llmResult.Reasoning,
                layersUsed = new[] { "Layer 0: Normalize", "Layer 2: Geocoding (failed)", "Layer 4: LLM Fallback" }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Simple Enhanced: LLM failed: {ex.Message}");
            return Results.BadRequest(new { error = ex.Message });
        }
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// Helper function for calculating accuracy
static double CalculateAccuracy(List<object> results)
{
    var withExpected = results.Where(r => r.GetType().GetProperty("expected")?.GetValue(r) != null).ToList();
    if (withExpected.Count == 0) return 0.0;

    var correct = withExpected.Count(r =>
    {
        var correctProp = r.GetType().GetProperty("correct")?.GetValue(r) as bool?;
        return correctProp == true;
    });

    return (double)correct / withExpected.Count;
}

app.Run();

// Request types
record CompareRequest(string Address1, string Address2);
record GoogleRequest(string Address);
record LLMRequest(string Address1, string Address2, string? Context = null);
record BatchCompareRequest(List<AddressPair> AddressPairs);
record AddressPair(string Address1, string Address2, bool? Expected = null);