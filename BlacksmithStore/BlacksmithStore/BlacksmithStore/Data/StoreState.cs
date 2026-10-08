using System;
using System.Collections.ObjectModel;
using System.IO;
using BlacksmithStore.Models;

namespace BlacksmithStore.Data
{
    public static class StoreState
    {
        public static UserModel CurrentUser { get; set; }

        public static ObservableCollection<Product> AllProducts { get; set; } = new ObservableCollection<Product>();
        public static ObservableCollection<Product> CartItems { get; set; } = new ObservableCollection<Product>();

        public static string DbPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Blacksmith_StoreBD");
        public static string ConnectionString => $"Data Source={DbPath}";
    }
}