namespace BlacksmithStore.Models
{
    public class ReportDetailItem
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string Brand { get; set; }
        public string ProductType { get; set; }
        public string Size { get; set; }
        public int Quantity { get; set; }
        public decimal SalePrice { get; set; }
        public string SaleDate { get; set; }
    }
}