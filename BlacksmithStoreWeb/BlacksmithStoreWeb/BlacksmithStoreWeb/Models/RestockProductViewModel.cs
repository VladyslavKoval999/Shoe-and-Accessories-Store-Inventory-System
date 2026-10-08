using System.Collections.Generic;

namespace BlacksmithStoreWeb.Models
{
    public class RestockProductDetailsViewModel
    {
        public Product Product { get; set; } = new Product();
        public List<string> OutOfStockSizes { get; set; } = new List<string>();
    }
}