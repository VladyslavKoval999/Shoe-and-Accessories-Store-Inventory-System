using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BlacksmithStore.Models
{
    public class Product : INotifyPropertyChanged
    {
        private string GetString(string key) => Application.Current?.TryFindResource(key) as string ?? key;

        public int Id { get; set; }
        public string Article { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string ProductType { get; set; }

        public string LocalizedProductType => ProductType == "Взуття" ? GetString("txtMenuShoes") : GetString("txtMenuAccessories");

        public string Subtype { get; set; }
        public string Brand { get; set; }
        public string Season { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public string ImageName { get; set; }
        public int TotalQuantity { get; set; }

        public List<string> AvailableSizes { get; set; } = new List<string>();
        public List<string> AvailableColors { get; set; } = new List<string>();

        private string _selectedSize;
        public string SelectedSize { get => _selectedSize; set { _selectedSize = value; OnPropertyChanged(); } }

        public BitmapImage ImagePath
        {
            get
            {
                try
                {
                    if (string.IsNullOrEmpty(ImageName)) return null;
                    string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PNG", "Product", ImageName);
                    return File.Exists(path) ? new BitmapImage(new Uri(path)) : null;
                }
                catch { return null; }
            }
        }

        private int _quantity = 1;
        public int Quantity { get => _quantity; set { _quantity = value; OnPropertyChanged(); OnPropertyChanged(nameof(TotalPrice)); } }

        public decimal TotalPrice => Price * Quantity;

        public string StockStatus => TotalQuantity == 0 ? GetString("txtOutOfStock") :
                                     TotalQuantity < 5 ? $"{GetString("txtDeficitBadge")}: {TotalQuantity}" :
                                     $"{GetString("txtStockIn")}: {TotalQuantity}";

        public SolidColorBrush StatusColor => TotalQuantity == 0 ? new SolidColorBrush(Colors.Gray) :
                                              TotalQuantity < 5 ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF5252")) :
                                              new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"));

        public bool IsDeficit => TotalQuantity > 0 && TotalQuantity < 5;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}