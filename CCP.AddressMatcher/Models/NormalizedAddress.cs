// CCP.AddressMatcher/Models/NormalizedAddress.cs
namespace CCP.AddressMatcher.Models
{
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
}
