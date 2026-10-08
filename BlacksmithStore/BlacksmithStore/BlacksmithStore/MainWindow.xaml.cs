using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using BlacksmithStore.Data;
using BlacksmithStore.Views;
using MaterialDesignThemes.Wpf;

namespace BlacksmithStore
{
    public partial class MainWindow : Window
    {
        public BitmapImage LogoImage { get; private set; }
        private bool isEnglish = false;
        private DateTime _lastKeystroke = new DateTime(0);
        private string _barcodeBuffer = "";
        private DispatcherTimer _scannerTimer;
        private DispatcherTimer _webOrdersTimer;
        private bool _isRestoringView = false;

        public MainWindow()
        {
            try { SQLitePCL.Batteries_V2.Init(); } catch { }
            try
            {
                string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PNG", "Main", "logo.png");
                if (File.Exists(logoPath)) LogoImage = new BitmapImage(new Uri(logoPath));
            }
            catch { }

            InitializeComponent();
            this.DataContext = this;
            DatabaseService.LoadProducts();

            ApplyUserRole();

            _scannerTimer = new DispatcherTimer();
            _scannerTimer.Interval = TimeSpan.FromMilliseconds(200);
            _scannerTimer.Tick += ScannerTimer_Tick;

            _webOrdersTimer = new DispatcherTimer();
            _webOrdersTimer.Interval = TimeSpan.FromSeconds(5);
            _webOrdersTimer.Tick += WebOrdersTimer_Tick;
            _webOrdersTimer.Start();

            CheckNewWebOrders();

            this.PreviewTextInput += MainWindow_PreviewTextInput;
            this.PreviewKeyDown += MainWindow_PreviewKeyDown;

            MainContent.Content = new HomeView();
        }

        private string GetString(string key)
        {
            return Application.Current.TryFindResource(key) as string ?? key;
        }

        private void ApplyUserRole()
        {
            if (StoreState.CurrentUser != null)
            {
                string dbRole = StoreState.CurrentUser.Role;
                string localizedRole = dbRole;

                string lowerRole = dbRole.ToLower();
                if (lowerRole.Contains("продавець") || lowerRole.Contains("seller")) localizedRole = GetString("txtRoleSeller");
                else if (lowerRole.Contains("менеджер") || lowerRole.Contains("manager")) localizedRole = GetString("txtRoleManager");
                else if (lowerRole.Contains("адмін") || lowerRole.Contains("admin")) localizedRole = GetString("txtRoleAdmin");

                txtUserNameTop.Text = $"{StoreState.CurrentUser.Username} ({localizedRole})";

                SectionAdmin.Visibility = Visibility.Collapsed;
                SectionManager.Visibility = Visibility.Collapsed;
                SectionSeller.Visibility = Visibility.Visible;

                if (lowerRole.Contains("admin") || lowerRole.Contains("адмін"))
                {
                    SectionAdmin.Visibility = Visibility.Visible;
                    SectionManager.Visibility = Visibility.Visible;
                }
                else if (lowerRole.Contains("manager") || lowerRole.Contains("менеджер"))
                {
                    SectionManager.Visibility = Visibility.Visible;
                }
            }
            else
            {
                txtUserNameTop.Text = GetString("txtGuest");
            }
        }

        public void NavigateFromDashboard(string tag)
        {
            RadioButton targetButton = null;
            switch (tag)
            {
                case "Home": targetButton = NavHome; break;
                case "Catalog": targetButton = NavCatalog; break;
                case "Shoes": targetButton = NavShoes; break;
                case "Accessories": targetButton = NavAccessories; break;
                case "Sales": targetButton = NavSales; break;
                case "Returns": targetButton = NavReturns; break;
                case "ClubCard": targetButton = NavClubCard; break;
                case "AddProduct": targetButton = NavAddProduct; break;
                case "Reports": targetButton = NavReports; break;
                case "AddSeller": targetButton = NavAddSeller; break;
                case "WebOrders": targetButton = NavWebOrders; break;
            }
            if (targetButton != null && targetButton.Visibility == Visibility.Visible)
            {
                targetButton.IsChecked = true;
            }
        }

        public void RestoreView(UserControl view, string tag)
        {
            _isRestoringView = true;
            MainContent.Content = view;
            NavigateFromDashboard(tag);
            _isRestoringView = false;
        }

        private void MenuButton_Checked(object sender, RoutedEventArgs e)
        {
            if (_isRestoringView) return;
            var button = sender as RadioButton;
            if (button == null || MainContent == null) return;
            string command = button.Tag?.ToString();

            switch (command)
            {
                case "Home": MainContent.Content = new HomeView(); break;
                case "Catalog": MainContent.Content = new CatalogView("txtMenuCatalog"); break;
                case "Shoes": MainContent.Content = new CatalogView("txtMenuShoes"); break;
                case "Accessories": MainContent.Content = new CatalogView("txtMenuAccessories"); break;
                case "Sales": MainContent.Content = new SalesView(); break;
                case "Returns": MainContent.Content = new ReturnsView(); break;
                case "ClubCard": MainContent.Content = new ClubCardView(); break;
                case "AddProduct": MainContent.Content = new ProductEditorView(); break;
                case "Reports": MainContent.Content = new ReportsView(); break;
                case "AddSeller": MainContent.Content = new AddSellerView(); break;
                case "WebOrders":
                    MainContent.Content = new WebOrdersView();
                    CheckNewWebOrders();
                    break;
            }
        }

        private void WebOrdersTimer_Tick(object sender, EventArgs e)
        {
            CheckNewWebOrders();
        }

        public void CheckNewWebOrders()
        {
            int count = DatabaseService.GetNewWebOrdersCount();
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    var badge = this.FindName("BadgeWebOrders") as Border;
                    var txtCount = this.FindName("TxtWebOrdersCount") as TextBlock;
                    if (badge != null && txtCount != null)
                    {
                        if (count > 0)
                        {
                            txtCount.Text = count.ToString();
                            badge.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            badge.Visibility = Visibility.Collapsed;
                        }
                    }
                }
                catch { }
            });
        }

        private void ThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            PaletteHelper paletteHelper = new PaletteHelper();
            var theme = paletteHelper.GetTheme();
            bool isDark = ThemeToggle.IsChecked == true;
            theme.SetBaseTheme(isDark ? BaseTheme.Dark : BaseTheme.Light);
            paletteHelper.SetTheme(theme);

            if (isDark)
            {
                Application.Current.Resources["BgMainBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A1A1A"));
                Application.Current.Resources["BgTopBarBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#111111"));
                Application.Current.Resources["BgSidebarBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#252525"));
                Application.Current.Resources["BgCardBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A2A2A"));
                Application.Current.Resources["BgDataGridBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#222222"));
                Application.Current.Resources["TextMainBrush"] = new SolidColorBrush(Colors.White);
                Application.Current.Resources["TextMutedBrush"] = new SolidColorBrush(Colors.Gray);
                Application.Current.Resources["BorderLineBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#444444"));
            }
            else
            {
                Application.Current.Resources["BgMainBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F2F5"));
                Application.Current.Resources["BgTopBarBrush"] = new SolidColorBrush(Colors.White);
                Application.Current.Resources["BgSidebarBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8EAED"));
                Application.Current.Resources["BgCardBrush"] = new SolidColorBrush(Colors.White);
                Application.Current.Resources["BgDataGridBrush"] = new SolidColorBrush(Colors.White);
                Application.Current.Resources["TextMainBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#111111"));
                Application.Current.Resources["TextMutedBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#555555"));
                Application.Current.Resources["BorderLineBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DDDDDD"));
            }
        }

        private void BtnLanguage_Click(object sender, RoutedEventArgs e)
        {
            isEnglish = !isEnglish;
            BtnLanguage.Content = isEnglish ? "ENG" : "УКР";
            string lang = isEnglish ? "en" : "uk";
            var dictionary = new ResourceDictionary();
            dictionary.Source = new Uri($"Langs/Lang.{lang}.xaml", UriKind.Relative);

            var dictToReplace = Application.Current.Resources.MergedDictionaries.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Langs/Lang."));
            if (dictToReplace != null)
                Application.Current.Resources.MergedDictionaries.Remove(dictToReplace);

            Application.Current.Resources.MergedDictionaries.Add(dictionary);

            ApplyUserRole();

            RadioButton activeNav = new[] { NavHome, NavCatalog, NavShoes, NavAccessories, NavSales, NavReturns, NavClubCard, NavWebOrders, NavAddProduct, NavReports, NavAddSeller }.FirstOrDefault(r => r != null && r.IsChecked == true);

            if (activeNav != null)
            {
                activeNav.IsChecked = false;
                activeNav.IsChecked = true;
            }
        }

        private void ScannerTimer_Tick(object sender, EventArgs e)
        {
            _scannerTimer.Stop();
            string finalBarcode = _barcodeBuffer.Trim();
            _barcodeBuffer = "";
            if (finalBarcode.Length >= 3)
            {
                ProcessBarcode(finalBarcode);
            }
        }

        private void MainWindow_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            TimeSpan elapsed = DateTime.Now - _lastKeystroke;
            if (elapsed.TotalMilliseconds > 70)
            {
                _barcodeBuffer = "";
            }
            _barcodeBuffer += e.Text;
            _lastKeystroke = DateTime.Now;

            if (_barcodeBuffer.Length > 1)
            {
                e.Handled = true;
            }
            _scannerTimer.Stop();
            _scannerTimer.Start();
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Return)
            {
                if (_barcodeBuffer.Length > 0)
                {
                    _scannerTimer.Stop();
                    string finalBarcode = _barcodeBuffer.Trim();
                    _barcodeBuffer = "";
                    e.Handled = true;

                    if (finalBarcode.Length >= 3)
                    {
                        ProcessBarcode(finalBarcode);
                    }
                }
            }
        }

        private void ProcessBarcode(string barcode)
        {
            if (barcode.StartsWith("BS-", StringComparison.OrdinalIgnoreCase))
            {
                if (MainContent.Content is SalesView salesView)
                {
                    salesView.edtPhoneSearch.Text = barcode;
                }
                else
                {
                    ResetSidebarSelection();
                    if (NavClubCard != null) NavClubCard.IsChecked = true;
                    var clubView = new ClubCardView();
                    clubView.edtSearchClient.Text = barcode;
                    MainContent.Content = clubView;
                }
                return;
            }

            var product = StoreState.AllProducts.FirstOrDefault(p =>
                p.Article != null && p.Article.Equals(barcode, StringComparison.OrdinalIgnoreCase));

            if (product != null)
            {
                product.SelectedSize = (product.ProductType == "Взуття" && product.AvailableSizes.Any()) ? product.AvailableSizes[0] : "-";
                ResetSidebarSelection();
                MainContent.Content = new ProductDetailsView(product);
            }
            else
            {
                MessageBox.Show(string.Format(GetString("txtProductNotFound"), barcode), string.Format(GetString("txtSearchedFor"), barcode), MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ResetSidebarSelection()
        {
            NavHome.IsChecked = false;
            NavCatalog.IsChecked = false;
            NavShoes.IsChecked = false;
            NavAccessories.IsChecked = false;
            NavSales.IsChecked = false;
            NavReturns.IsChecked = false;
            NavClubCard.IsChecked = false;
            if (NavWebOrders != null) NavWebOrders.IsChecked = false;
            if (NavAddProduct != null) NavAddProduct.IsChecked = false;
            if (NavReports != null) NavReports.IsChecked = false;
            if (NavAddSeller != null) NavAddSeller.IsChecked = false;
        }
    }
}