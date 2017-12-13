namespace BitPay.Models.Settlements
{
    public class WithHoldings
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
        public string Code { get; set; }
        public string Description { get; set; }
        public string Notes { get; set; }
    }
}