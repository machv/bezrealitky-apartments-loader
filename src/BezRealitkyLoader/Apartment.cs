using System;

namespace BezRealitkyLoader
{
    public class Apartment : IEquatable<Apartment>
    {
        public int Id { get; set; }
        public string Address { get; set; }
        public string DetailsLink { get; set; }
        public string Description { get; set; }
        public decimal Rent { get; set; }
        public decimal Fees { get; set; }
        public string Disposition { get; set; }
        public decimal Area { get; set; }
        public string District { get; set; }
        public bool IsPremiumOffer { get; set; }
        public Status Status { get; set; }
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }
        public decimal PurchasePrice { get; internal set; }

        public bool Equals(Apartment other)
        {
            return Id == other.Id;
        }

        public override string ToString()
        {
            return string.Format("{0}, {1}, {2} m²", Address, Disposition, Area);
        }
    }
}
