using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using BlacksmithStore.Models;
using BlacksmithStore.Data;
using System.Linq;

namespace BlacksmithStore.Views
{
    public class ProductSizeInfo
    {
        public string SizeValue { get; set; }
        public int StockQuantity { get; set; }
    }

    public partial class ProductDetailsView : UserControl
    {
        private Product _product;
        private string _sourceTab;
        private UserControl _previousView;

        public ProductDetailsView(Product product, string sourceTab = "txtMenuCatalog", UserControl previousView = null)
        {
            InitializeComponent();
            _product = product;
            _sourceTab = sourceTab;
            _previousView = previousView;

            _product.Quantity = 1;
            this.DataContext = _product;

            if (_product.ProductType != "Взуття")
            {
                SizesPanel.Visibility = Visibility.Collapsed;
                _product.SelectedSize = "-";
            }
            else
            {
                LoadSizesFromDB();
            }
        }

        private void LoadSizesFromDB()
        {
            var sizesList = new List<ProductSizeInfo>();
            try
            {
                using (var conn = new SqliteConnection(StoreState.ConnectionString))
                {
                    conn.Open();
                    string sql = @"
                        SELECT s.value, st.quantity 
                        FROM Stock st
                        JOIN Sizes s ON st.size_id = s.size_id
                        WHERE st.product_id = @pid AND st.quantity > 0
                        ORDER BY CAST(s.value AS INTEGER)";

                    using (var cmd = new SqliteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@pid", _product.Id);
                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                sizesList.Add(new ProductSizeInfo { SizeValue = r.GetString(0), StockQuantity = r.GetInt32(1) });
                            }
                        }
                    }
                }
            }
            catch { }

            if (sizesList.Count > 0)
            {
                lstSizes.ItemsSource = sizesList;
                _product.SelectedSize = sizesList[0].SizeValue;
            }
            else
            {
                lstSizes.ItemsSource = new List<ProductSizeInfo> { new ProductSizeInfo { SizeValue = "Немає в наявності", StockQuantity = 0 } };
                _product.SelectedSize = null;
            }
        }

        private int GetAvailableQuantity()
        {
            if (_product.ProductType == "Взуття")
            {
                if (!string.IsNullOrEmpty(_product.SelectedSize) && lstSizes.ItemsSource is List<ProductSizeInfo> sizes)
                {
                    var selectedSizeInfo = sizes.FirstOrDefault(s => s.SizeValue == _product.SelectedSize);
                    if (selectedSizeInfo != null)
                    {
                        return selectedSizeInfo.StockQuantity;
                    }
                }
                return 0;
            }
            return _product.TotalQuantity;
        }

        private void LstSizes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _product.Quantity = 1;
        }

        private void BtnMinus_Click(object sender, RoutedEventArgs e)
        {
            if (_product.Quantity > 1) _product.Quantity--;
        }

        private void BtnPlus_Click(object sender, RoutedEventArgs e)
        {
            int maxAvailable = GetAvailableQuantity();

            if (_product.Quantity < maxAvailable)
            {
                _product.Quantity++;
            }
            else
            {
                MessageBox.Show($"Досягнуто максимальної кількості на складі! Доступно: {maxAvailable} од.",
                                "Увага", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void AddToCartDetailed_Click(object sender, RoutedEventArgs e)
        {
            if (_product.ProductType == "Взуття" && string.IsNullOrEmpty(_product.SelectedSize))
            {
                MessageBox.Show("Будь ласка, оберіть розмір перед додаванням до кошика!", "Увага", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int maxAvailable = GetAvailableQuantity();

            if (maxAvailable <= 0)
            {
                MessageBox.Show("Цього товару (або обраного розміру) немає в наявності!", "Увага", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var existingItem = StoreState.CartItems.FirstOrDefault(p => p.Id == _product.Id && p.SelectedSize == _product.SelectedSize);

            if (existingItem != null)
            {
                if (existingItem.Quantity + _product.Quantity > maxAvailable)
                {
                    MessageBox.Show($"Ви не можете додати більше! У кошику вже є {existingItem.Quantity} од., а доступно всього {maxAvailable} од.",
                                    "Увага", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                existingItem.Quantity += _product.Quantity;
                MessageBox.Show($"Кількість товару '{_product.Name}' у кошику збільшено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                if (_product.Quantity > maxAvailable)
                {
                    MessageBox.Show($"Доступно лише {maxAvailable} од.", "Увага", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Product cartItem = new Product
                {
                    Id = _product.Id,
                    Article = _product.Article,
                    Name = _product.Name,
                    Brand = _product.Brand,
                    Price = _product.Price,
                    ImageName = _product.ImageName,
                    SelectedSize = _product.SelectedSize,
                    Quantity = _product.Quantity,
                    TotalQuantity = maxAvailable
                };

                StoreState.CartItems.Add(cartItem);
                MessageBox.Show($"Товар '{_product.Name}' додано до кошика ({_product.Quantity} од.)!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            BackToCatalog_Click(sender, e);
        }

        private void BackToCatalog_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow ?? Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
            if (mainWindow != null)
            {
                if (_previousView != null)
                {
                    string tag = "Catalog";
                    if (_sourceTab == "txtMenuShoes") tag = "Shoes";
                    else if (_sourceTab == "txtMenuAccessories") tag = "Accessories";

                    mainWindow.RestoreView(_previousView, tag);
                }
                else
                {
                    mainWindow.MainContent.Content = new CatalogView(_sourceTab);
                    if (_sourceTab == "txtMenuShoes" && mainWindow.NavShoes != null) mainWindow.NavShoes.IsChecked = true;
                    else if (_sourceTab == "txtMenuAccessories" && mainWindow.NavAccessories != null) mainWindow.NavAccessories.IsChecked = true;
                    else if (mainWindow.NavCatalog != null) mainWindow.NavCatalog.IsChecked = true;
                }
            }
        }
    }
}