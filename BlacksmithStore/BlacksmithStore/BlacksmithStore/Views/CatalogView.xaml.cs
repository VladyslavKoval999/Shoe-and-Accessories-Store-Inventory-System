using BlacksmithStore.Data;
using BlacksmithStore.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace BlacksmithStore.Views
{
    public partial class CatalogView : UserControl
    {
        public ObservableCollection<Product> FilteredProducts { get; set; } = new ObservableCollection<Product>();
        private string _currentTab;
        private CancellationTokenSource _debounceCts;

        public CatalogView(string titleKey)
        {
            InitializeComponent();
            this.DataContext = this;
            _currentTab = titleKey;

            CategoryTitle.SetResourceReference(TextBlock.TextProperty, titleKey);

            ApplyUserRoleAccess();
            LoadFilterData();
            ApplyFilters();
        }

        public void ReloadDataFromDB()
        {
            LoadFilterData();
            ApplyFilters();
        }

        private void ApplyUserRoleAccess()
        {
            if (StoreState.CurrentUser != null)
            {
                string role = StoreState.CurrentUser.Role.ToLower();
                if (role.Contains("admin") || role.Contains("адмін") || role.Contains("manager") || role.Contains("менеджер"))
                {
                    pnlEditMode.Visibility = Visibility.Visible;
                }
                else
                {
                    pnlEditMode.Visibility = Visibility.Collapsed;
                    ToggleEditMode.IsChecked = false;
                }
            }
            else
            {
                pnlEditMode.Visibility = Visibility.Collapsed;
            }
        }

        private void LoadFilterData()
        {
            DatabaseService.LoadProducts();
            if (StoreState.AllProducts == null) return;

            IEnumerable<Product> relevantProducts = StoreState.AllProducts;

            if (_currentTab == "txtMenuShoes")
            {
                relevantProducts = StoreState.AllProducts.Where(p => p.ProductType == "Взуття");
                cbSizeFilter.Visibility = Visibility.Visible;
            }
            else if (_currentTab == "txtMenuAccessories")
            {
                relevantProducts = StoreState.AllProducts.Where(p => p.ProductType == "Аксесуар");
                cbSizeFilter.Visibility = Visibility.Collapsed;
            }

            string selBrand = cbBrandFilter.Text;
            cbBrandFilter.ItemsSource = relevantProducts.Select(p => p.Brand).Where(b => !string.IsNullOrEmpty(b)).Distinct().OrderBy(x => x).ToList();
            if (!string.IsNullOrEmpty(selBrand)) cbBrandFilter.Text = selBrand;

            string selSubtype = cbSubtypeFilter.Text;
            cbSubtypeFilter.ItemsSource = relevantProducts.Select(p => p.Subtype).Where(s => !string.IsNullOrEmpty(s)).Distinct().OrderBy(x => x).ToList();
            if (!string.IsNullOrEmpty(selSubtype)) cbSubtypeFilter.Text = selSubtype;

            string selSize = cbSizeFilter.Text;
            cbSizeFilter.ItemsSource = relevantProducts
                .Where(p => p.AvailableSizes != null)
                .SelectMany(p => p.AvailableSizes)
                .Distinct()
                .OrderBy(s => double.TryParse(s, out double d) ? d : 9999)
                .ToList();
            if (!string.IsNullOrEmpty(selSize)) cbSizeFilter.Text = selSize;

            string selColor = cbColorFilter.Text;
            cbColorFilter.ItemsSource = relevantProducts
                .Where(p => p.AvailableColors != null)
                .SelectMany(p => p.AvailableColors)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c)
                .ToList();
            if (!string.IsNullOrEmpty(selColor)) cbColorFilter.Text = selColor;
        }

        private async void FilterChanged(object sender, RoutedEventArgs e)
        {
            _debounceCts?.Cancel();
            _debounceCts = new CancellationTokenSource();
            var token = _debounceCts.Token;

            try
            {
                await Task.Delay(150, token);
                ApplyFilters();
            }
            catch (TaskCanceledException) { }
        }

        private void ApplyFilters()
        {
            if (StoreState.AllProducts == null) return;

            var res = StoreState.AllProducts.AsEnumerable();

            if (_currentTab == "txtMenuShoes") res = res.Where(p => p.ProductType == "Взуття");
            else if (_currentTab == "txtMenuAccessories") res = res.Where(p => p.ProductType == "Аксесуар");

            if (cbCategoryFilter?.SelectedItem is ComboBoxItem catItem)
            {
                string catVal = catItem.Tag?.ToString() ?? catItem.Content?.ToString();
                res = res.Where(p => p.Category == catVal);
            }

            if (!string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                string search = SearchBox.Text;
                res = res.Where(p =>
                    (p.Name != null && p.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (p.Article != null && p.Article.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (p.Brand != null && p.Brand.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                );
            }

            if (cbBrandFilter.SelectedItem is string b) res = res.Where(p => p.Brand == b);
            if (cbSubtypeFilter.SelectedItem is string sub) res = res.Where(p => p.Subtype == sub);

            if (cbSeasonFilter?.SelectedItem is ComboBoxItem seasonItem)
            {
                string seasonVal = seasonItem.Tag?.ToString() ?? seasonItem.Content?.ToString();
                if (seasonVal != "Усі")
                    res = res.Where(p => p.Season == seasonVal);
            }

            if (cbColorFilter.SelectedItem is string color)
            {
                res = res.Where(p => p.AvailableColors != null &&
                                     p.AvailableColors.Any(c => c.Equals(color, StringComparison.OrdinalIgnoreCase)));
            }

            if (cbStockFilter?.SelectedItem is ComboBoxItem stockItem)
            {
                string stockVal = stockItem.Tag?.ToString() ?? stockItem.Content?.ToString();
                if (stockVal == "В наявності") res = res.Where(p => p.TotalQuantity > 0);
                else res = res.Where(p => p.TotalQuantity == 0);
            }

            if (cbSizeFilter.SelectedItem is string sz)
                res = res.Where(p => p.AvailableSizes != null && p.AvailableSizes.Contains(sz));

            if (decimal.TryParse(tbMinPrice.Text, out decimal minPrice))
                res = res.Where(p => p.Price >= minPrice);

            if (decimal.TryParse(tbMaxPrice.Text, out decimal maxPrice))
                res = res.Where(p => p.Price <= maxPrice);

            FilteredProducts.Clear();
            foreach (var p in res.ToList()) FilteredProducts.Add(p);
        }

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = "";
            tbMinPrice.Text = "";
            tbMaxPrice.Text = "";
            cbCategoryFilter.SelectedIndex = -1;
            cbSubtypeFilter.SelectedIndex = -1;
            cbBrandFilter.SelectedIndex = -1;
            cbSeasonFilter.SelectedIndex = -1;
            cbStockFilter.SelectedIndex = -1;
            cbSizeFilter.SelectedIndex = -1;
            cbColorFilter.SelectedIndex = -1;

            ApplyFilters();
        }

        private void ViewDetails_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var product = btn?.DataContext as Product;
            if (product != null)
            {
                var mainWindow = Window.GetWindow(this) as MainWindow ?? Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                if (mainWindow != null)
                {
                    mainWindow.MainContent.Content = new ProductDetailsView(product, _currentTab, this);

                    if (mainWindow.NavHome != null) mainWindow.NavHome.IsChecked = false;
                    if (mainWindow.NavCatalog != null) mainWindow.NavCatalog.IsChecked = false;
                    if (mainWindow.NavShoes != null) mainWindow.NavShoes.IsChecked = false;
                    if (mainWindow.NavAccessories != null) mainWindow.NavAccessories.IsChecked = false;
                }
            }
        }

        private void EditProduct_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var product = btn?.DataContext as Product;
            if (product != null)
            {
                var mainWindow = Window.GetWindow(this) as MainWindow ?? Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                if (mainWindow != null)
                {
                    mainWindow.MainContent.Content = new ProductEditorView(product, this);
                }
            }
        }
    }
}