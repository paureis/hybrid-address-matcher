using System.Text.Json.Serialization;


public class GoogleGeocodeResponse
{
    [JsonPropertyName("results")]
    public GoogleResult[] Results { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; }
}

public class GoogleResult
{
    [JsonPropertyName("formatted_address")]
    public string FormattedAddress { get; set; }

    [JsonPropertyName("partial_match")]
    public bool? PartialMatch { get; set; }

    [JsonPropertyName("geometry")]
    public Geometry Geometry { get; set; }
}

public class Geometry
{
    [JsonPropertyName("location_type")]
    public string LocationType { get; set; }
}
