using CCP.AddressMatcher.Services;
using CCP.AddressMatcher.Utils;
using CCP.AddressMatcher.Models;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
Console.WriteLine("Google API Key: " + builder.Configuration["GoogleApiKey"]);

// Register services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

// Register existing services
builder.Services.AddScoped<GoogleAddressValidatorService>();

// Register new geocoding comparison service
builder.Services.AddScoped<GoogleGeocodingService>();
builder.Services.AddScoped<AddressMatchingService>();

// Add memory cache for geocoding results to reduce API calls
builder.Services.AddMemoryCache();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder => builder.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader());
});

var app = builder.Build();

app.UseCors("AllowAll");

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

// 🎯 NEW: Geocoding-based comparison endpoint
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

// 🔄 NEW: Smart comparison endpoint (tries geocoding first, falls back to local)
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

// 📊 NEW: Batch comparison endpoint for testing multiple addresses
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

// Helper function for calculating accuracy
static double CalculateAccuracy(List<object> results)
{
    var withExpected = results.Where(r => r.GetType().GetProperty("expected")?.GetValue(r) != null).ToList();
    if (withExpected.Count == 0) return 0.0;
    
    var correct = withExpected.Count(r => {
        var correctProp = r.GetType().GetProperty("correct")?.GetValue(r) as bool?;
        return correctProp == true;
    });
    
    return (double)correct / withExpected.Count;
}

app.Run();

// Request types
record CompareRequest(string Address1, string Address2);
record GoogleRequest(string Address);
record BatchCompareRequest(List<AddressPair> AddressPairs);
record AddressPair(string Address1, string Address2, bool? Expected = null);