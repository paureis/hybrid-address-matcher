// Program.cs (updated with detailed hybrid comparison)

using CCP.AddressMatcher.Services;
using CCP.AddressMatcher.Utils;
using CCP.AddressMatcher.Models; // For GoogleGeocodeResponse and GoogleResult
using System.Text.Json;


var builder = WebApplication.CreateBuilder(args);
Console.WriteLine("Google API Key: " + builder.Configuration["GoogleApiKey"]);

// Register Swagger and HTTP client
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

// Register the Google Address Validator Service
builder.Services.AddScoped<GoogleAddressValidatorService>();

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

// 🧠 Local-only address comparison
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

// 🌐 Google-only validation with stricter checks
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


app.Run();

// Request types
record CompareRequest(string Address1, string Address2);
record GoogleRequest(string Address);
