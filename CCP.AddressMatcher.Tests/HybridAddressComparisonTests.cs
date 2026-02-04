// CCP.AddressMatcher.Tests/HybridAddressComparisonTests.cs

using System.Collections.Generic;
using Xunit;
using CCP.AddressMatcher.Utils;
using CCP.AddressMatcher.Models;

namespace CCP.AddressMatcher.Tests
{
    public class HybridAddressComparisonTests
    {
        [Theory]
        [InlineData("1600 Amphitheatre Parkway Mountain View, California", "1600 Amphitheatre Pkwy, Mountain View, CA 94043", true)]
        [InlineData("123 Main St, Springfield, IL", "124 Main St, Springfield, IL", false)]
        [InlineData("123 Main St, Springfield, IL", "123 Main St, Chicago, IL", false)]
        [InlineData("123 Main St, Springfield, IL", "123 Main St, Springfield, MO", false)]
        [InlineData("123 Main St, Springfield, IL 62704", "123 Main St, Springfield, IL 62701", false)]
        [InlineData("asdfghjkl", "asdfghjkl", false)] // Should fail as invalid
        [InlineData("500 E 4th St Apt 2, Austin, TX 78701", "500 E 4th St, Austin, TX 78701", true)] // Unit differences ignored
        [InlineData("Empire State Building, New York, NY", "1600 Amphitheatre Parkway, Mountain View, CA", false)]
        public void HybridComparison_ShouldMatchExpectedResults(string addr1, string addr2, bool expected)
        {
            var normalized1 = AddressNormalizer.Normalize(addr1);
            var normalized2 = AddressNormalizer.Normalize(addr2);

            if (normalized1 == null || normalized2 == null)
            {
                Assert.False(expected);
                return;
            }

            var differences = AddressComparer.Compare(normalized1, normalized2);
            var match = differences.Count == 0;

            Assert.Equal(expected, match);
        }

        [Fact]
        public void Normalize_ShouldHandleAbbreviations()
        {
            var address1 = "1600 Amphitheatre Parkway Mountain View, CA";
            var address2 = "1600 Amphitheatre Pkwy, Mountain View, CA 94043";

            var normalized1 = AddressNormalizer.Normalize(address1);
            var normalized2 = AddressNormalizer.Normalize(address2);

            Assert.NotNull(normalized1);
            Assert.NotNull(normalized2);

            Assert.Equal(normalized1.Street, normalized2.Street);
            Assert.Equal(normalized1.City, normalized2.City);
            Assert.Equal(normalized1.State, normalized2.State);
        }

        [Fact]
        public void Compare_ShouldDetectCityMismatch()
        {
            var address1 = "123 Main St, Springfield, IL";
            var address2 = "123 Main St, Chicago, IL";

            var normalized1 = AddressNormalizer.Normalize(address1);
            var normalized2 = AddressNormalizer.Normalize(address2);

            Assert.NotNull(normalized1);
            Assert.NotNull(normalized2);

            var differences = AddressComparer.Compare(normalized1, normalized2);

            Assert.Contains(differences, d => d.ToLower().Contains("city mismatch"));
        }

        [Fact]
        public void Compare_ShouldDetectZipMismatch()
        {
            var address1 = "123 Main St, Springfield, IL 62704";
            var address2 = "123 Main St, Springfield, IL 62701";

            var normalized1 = AddressNormalizer.Normalize(address1);
            var normalized2 = AddressNormalizer.Normalize(address2);

            Assert.NotNull(normalized1);
            Assert.NotNull(normalized2);

            var differences = AddressComparer.Compare(normalized1, normalized2);

            Assert.Contains(differences, d => d.ToLower().Contains("zip"));
        }
    }
}
