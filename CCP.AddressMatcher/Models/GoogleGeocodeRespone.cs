using System.Text.Json.Serialization;

namespace CCP.AddressMatcher.Models
{
    // Keep your EXISTING NormalizedAddress class exactly as your Utils expect it
    public class NormalizedAddress
    {
        public string Street { get; set; } = "";
        public string Unit { get; set; } = "";
        public string City { get; set; } = "";
        public string State { get; set; } = "";
        public string Zip { get; set; } = "";
        public string Country { get; set; } = "";

        public override string ToString()
        {
            return $"{Street} {(string.IsNullOrEmpty(Unit) ? "" : $"Unit {Unit} ")}{City} {State} {Zip} {Country}".Trim();
        }
    }

    // Google API Models - completely separate namespace to avoid conflicts
    public class GoogleGeocodeResponse
    {
        [JsonPropertyName("results")]
        public GoogleResult[] Results { get; set; } = Array.Empty<GoogleResult>();

        [JsonPropertyName("status")]
        public string Status { get; set; } = "";

        [JsonPropertyName("error_message")]
        public string? ErrorMessage { get; set; }
    }

    public class GoogleResult
    {
        [JsonPropertyName("formatted_address")]
        public string FormattedAddress { get; set; } = "";

        [JsonPropertyName("partial_match")]
        public bool? PartialMatch { get; set; }

        [JsonPropertyName("geometry")]
        public GoogleGeometry? Geometry { get; set; }

        [JsonPropertyName("place_id")]
        public string PlaceId { get; set; } = "";

        [JsonPropertyName("address_components")]
        public GoogleAddressComponent[]? AddressComponents { get; set; }

        [JsonPropertyName("types")]
        public string[] Types { get; set; } = Array.Empty<string>();
    }

    public class GoogleGeometry
    {
        [JsonPropertyName("location")]
        public GoogleLocation? Location { get; set; }

        [JsonPropertyName("location_type")]
        public string LocationType { get; set; } = "";

        [JsonPropertyName("viewport")]
        public GoogleViewport? Viewport { get; set; }

        [JsonPropertyName("bounds")]
        public GoogleBounds? Bounds { get; set; }
    }

    public class GoogleLocation
    {
        [JsonPropertyName("lat")]
        public double Lat { get; set; }

        [JsonPropertyName("lng")]
        public double Lng { get; set; }
    }

    public class GoogleViewport
    {
        [JsonPropertyName("northeast")]
        public GoogleLocation? Northeast { get; set; }

        [JsonPropertyName("southwest")]
        public GoogleLocation? Southwest { get; set; }
    }

    public class GoogleBounds
    {
        [JsonPropertyName("northeast")]
        public GoogleLocation? Northeast { get; set; }

        [JsonPropertyName("southwest")]
        public GoogleLocation? Southwest { get; set; }
    }

    // Renamed to avoid conflicts with any existing AddressComponent
    public class GoogleAddressComponent
    {
        [JsonPropertyName("long_name")]
        public string LongName { get; set; } = "";

        [JsonPropertyName("short_name")]
        public string ShortName { get; set; } = "";

        [JsonPropertyName("types")]
        public string[] Types { get; set; } = Array.Empty<string>();
    }

    // Result model for the new geocoding comparison functionality
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