using System;
using System.Windows;

namespace BlacksmithStore.Models
{
    public class OrderReturnItem
    {
        private string GetString(string key) => Application.Current?.TryFindResource(key) as string ?? key;

        public int OrderId { get; set; }
        public int StockId { get; set; }
        public string ProductName { get; set; }
        public string Brand { get; set; }
        public string Size { get; set; }
        public int Quantity { get; set; }
        public decimal SalePrice { get; set; }
        public string OrderDate { get; set; }

        public bool IsReturnable
        {
            get
            {
                if (DateTime.TryParse(OrderDate, out DateTime date))
                {
                    return (DateTime.Now - date).TotalDays <= 14;
                }
                return false;
            }
        }

        public string ReturnStatus => IsReturnable ? GetString("txtReturnAllowed") : GetString("txtReturnExpired");
    }
}