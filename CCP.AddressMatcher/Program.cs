using CCP.AddressMatcher.Services;
using CCP.AddressMatcher.Utils;
using CCP.AddressMatcher.Models;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

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

// Register Redis distributed cache (falls back to in-memory if not configured)
var redisConnection = builder.Configuration["RedisConnection"];
if (!string.IsNullOrEmpty(redisConnection))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnection;
        options.InstanceName = "AddressMatcher:";
    });
}
else
{
    // Fallback to in-memory cache for local development
    builder.Services.AddDistributedMemoryCache();
}

// Rate limiting — protect expensive external API calls from abuse
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;

    // General limit: 30 requests/minute per IP for standard endpoints
    options.AddPolicy("general", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    // Strict limit: 10 requests/minute per IP for expensive API endpoints (Google, LLM)
    options.AddPolicy("expensive", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    // Batch limit: 3 requests/minute per IP — each batch multiplies costs
    options.AddPolicy("batch", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy => policy.WithOrigins(
            "https://ccp-address-matcher.vercel.app",
            "http://localhost:5173",  // Vite dev server
            "http://localhost:3000",  // Alternative React dev port
            "http://localhost:5000"   // .NET default dev port
        )
        .AllowAnyMethod()
        .AllowAnyHeader());
});

var app = builder.Build();

app.UseCors("AllowFrontend");
app.UseRateLimiter();

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
}).RequireRateLimiting("general");

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
}).RequireRateLimiting("expensive");

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
}).RequireRateLimiting("general");

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
}).RequireRateLimiting("expensive");

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
}).RequireRateLimiting("expensive");

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
}).RequireRateLimiting("expensive");

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
}).RequireRateLimiting("expensive");

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
}).RequireRateLimiting("expensive");

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
}).RequireRateLimiting("batch");

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
}).RequireRateLimiting("batch");

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