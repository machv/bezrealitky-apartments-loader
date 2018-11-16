namespace BezRealitkyLoader
{
    public class Apartment
    {
        public string Address { get; set; }
        public string DetailsLink { get; set; }
        public string Description { get; set; }
        public decimal Rent { get; set; }
        public decimal Fees { get; set; }
        public string Disposition { get; set; }
        public decimal Area { get; set; }
        public string District { get; set; }

        public override string ToString()
        {
            return string.Format("{0}, {1}, {2} m²", Address, Disposition, Area);
        }
    }
}
