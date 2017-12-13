namespace BitPay.Models.Rates
{
    /// <summary>
    /// Provides an interface to a single exchange rate.
    /// </summary>
    public class Rate
    {
        public string Name { get; set; }
        public string Code { get; set; }
        public decimal Value { get; set; }
    }
}
