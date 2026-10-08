using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;

namespace BlacksmithStore.Models
{
    public class WebOrderItem
    {
        public string Article { get; set; }
        public string ProductName { get; set; }
        public string Brand { get; set; }
        public string Size { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }

    public class WebOrder : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string OrderDate { get; set; }
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public string DeliveryAddress { get; set; }
        public string Comment { get; set; }
        public bool HasComment => !string.IsNullOrWhiteSpace(Comment);
        public decimal TotalAmount { get; set; }
        public string PaymentMethod { get; set; }

        private string _status;
        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public bool IsOverLimit => Items != null && Items.Sum(i => i.Quantity) > 4;

        public ObservableCollection<WebOrderItem> Items { get; set; } = new ObservableCollection<WebOrderItem>();

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}