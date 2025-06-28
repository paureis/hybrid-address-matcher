// CCP.AddressMatcher/Utils/AddressComparer.cs

using System;
using System.Collections.Generic;
using CCP.AddressMatcher.Models;

namespace CCP.AddressMatcher.Utils
{
    public static class AddressComparer
    {
        public static List<string> Compare(NormalizedAddress? addr1, NormalizedAddress? addr2)
        {
            var differences = new List<string>();

            if (addr1 == null || addr2 == null)
            {
                differences.Add("One or both addresses are invalid or incomplete.");
                return differences;
            }

            // Compare street ignoring "unit" differences
            string street1 = RemoveUnit(addr1.Street);
            string street2 = RemoveUnit(addr2.Street);

            if (!string.Equals(street1, street2, StringComparison.OrdinalIgnoreCase))
                differences.Add($"Street mismatch: {addr1.Street} vs {addr2.Street}");

            if (!string.Equals(addr1.City, addr2.City, StringComparison.OrdinalIgnoreCase))
                differences.Add($"City mismatch: {addr1.City} vs {addr2.City}");

            if (!string.Equals(addr1.State, addr2.State, StringComparison.OrdinalIgnoreCase))
                differences.Add($"State mismatch: {addr1.State} vs {addr2.State}");

            if (!string.IsNullOrEmpty(addr1.Zip) && !string.IsNullOrEmpty(addr2.Zip) &&
                !string.Equals(addr1.Zip, addr2.Zip, StringComparison.OrdinalIgnoreCase))
                differences.Add($"ZIP code mismatch: {addr1.Zip} vs {addr2.Zip}");

            return differences;
        }

        private static string RemoveUnit(string street)
        {
            if (string.IsNullOrEmpty(street)) return street;

            var tokens = street.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var cleanedTokens = new List<string>();

            for (int i = 0; i < tokens.Length; i++)
            {
                if (tokens[i].Equals("unit", StringComparison.OrdinalIgnoreCase))
                {
                    // Skip "unit" and the next token (unit number) if it exists
                    i++;
                    continue;
                }
                cleanedTokens.Add(tokens[i]);
            }

            return string.Join(" ", cleanedTokens);
        }
    }
}
