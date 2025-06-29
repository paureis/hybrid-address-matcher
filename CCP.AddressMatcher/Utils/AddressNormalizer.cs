// CCP.AddressMatcher/Utils/AddressNormalizer.cs

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using CCP.AddressMatcher.Models;

namespace CCP.AddressMatcher.Utils
{
    public static class AddressNormalizer
    {
        private static readonly Dictionary<string, string> AbbreviationMap = new(StringComparer.OrdinalIgnoreCase)
        {
            {"st", "street"}, {"rd", "road"}, {"ave", "avenue"}, {"blvd", "boulevard"}, {"pkwy", "parkway"},
            {"dr", "drive"}, {"ln", "lane"}, {"ct", "court"}, {"hwy", "highway"}, {"apt", "unit"}
        };

        private static readonly Dictionary<string, string> StateMap = new(StringComparer.OrdinalIgnoreCase)
        {
            {"alabama", "al"}, {"alaska", "ak"}, {"arizona", "az"}, {"arkansas", "ar"}, {"california", "ca"},
            {"colorado", "co"}, {"connecticut", "ct"}, {"delaware", "de"}, {"florida", "fl"}, {"georgia", "ga"},
            {"hawaii", "hi"}, {"idaho", "id"}, {"illinois", "il"}, {"indiana", "in"}, {"iowa", "ia"},
            {"kansas", "ks"}, {"kentucky", "ky"}, {"louisiana", "la"}, {"maine", "me"}, {"maryland", "md"},
            {"massachusetts", "ma"}, {"michigan", "mi"}, {"minnesota", "mn"}, {"mississippi", "ms"}, {"missouri", "mo"},
            {"montana", "mt"}, {"nebraska", "ne"}, {"nevada", "nv"}, {"new hampshire", "nh"}, {"new jersey", "nj"},
            {"new mexico", "nm"}, {"new york", "ny"}, {"north carolina", "nc"}, {"north dakota", "nd"}, {"ohio", "oh"},
            {"oklahoma", "ok"}, {"oregon", "or"}, {"pennsylvania", "pa"}, {"rhode island", "ri"}, {"south carolina", "sc"},
            {"south dakota", "sd"}, {"tennessee", "tn"}, {"texas", "tx"}, {"utah", "ut"}, {"vermont", "vt"},
            {"virginia", "va"}, {"washington", "wa"}, {"west virginia", "wv"}, {"wisconsin", "wi"}, {"wyoming", "wy"},
            {"district of columbia", "dc"}
        };

        public static NormalizedAddress? Normalize(string address)
{
    if (string.IsNullOrWhiteSpace(address) || address.Trim().Split(' ').Length == 1)
        return null; // consider invalid

    address = address.ToLowerInvariant();
    address = Regex.Replace(address, "[.,]", "");
    address = Regex.Replace(address, "\\s+", " ").Trim();

    var tokens = address.Split(' ').ToList();

    for (int i = 0; i < tokens.Count; i++)
    {
        if (AbbreviationMap.TryGetValue(tokens[i], out var expanded))
            tokens[i] = expanded;

        if (StateMap.TryGetValue(tokens[i], out var stateAbbr))
            tokens[i] = stateAbbr;
    }

    var normalized = new NormalizedAddress();

    // Extract ZIP code
    var zipMatch = tokens.FirstOrDefault(t => Regex.IsMatch(t, @"^\d{5}(-\d{4})?$"));
    if (zipMatch != null)
    {
        normalized.Zip = zipMatch;
        tokens.Remove(zipMatch);
    }

    // Extract state
    var states = new HashSet<string>(StateMap.Values);
    var stateToken = tokens.FirstOrDefault(t => states.Contains(t));
    if (stateToken != null)
    {
        normalized.State = stateToken;
        tokens.Remove(stateToken);
    }

    // Extract house number (first numeric token with 1-4 digits)
    var houseNumberToken = tokens.FirstOrDefault(t => Regex.IsMatch(t, @"^\d{1,5}$"));
    if (houseNumberToken != null)
    {
        normalized.Street = houseNumberToken + " ";
        tokens.Remove(houseNumberToken);
    }

    // Attempt to extract city (after street tokens removed)
    if (string.IsNullOrEmpty(normalized.State) && tokens.Count >= 2)
    {
        normalized.City = tokens[^1];
        tokens.RemoveAt(tokens.Count - 1);
    }
    else if (!string.IsNullOrEmpty(normalized.State) && tokens.Count >= 1)
    {
        normalized.City = tokens[^1];
        tokens.RemoveAt(tokens.Count - 1);
    }

    // Remaining tokens are street
    normalized.Street += string.Join(" ", tokens);

    return normalized;
}

    }
}
