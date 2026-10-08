using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Data.Sqlite;
using BlacksmithStore.Data;
using BlacksmithStore.Models;

namespace BlacksmithStore.Views
{
    public partial class SalesView : UserControl
    {
        private static int _currentCustomerId = 0;
        private static int _currentUserId = 0;
        private static int _availableBonuses = 0;
        private static int _purchasedItemsCount = 0;
        private static bool _isDiscountApplied = false;
        private static string _currentCustomerName = "";
        private static string _currentCardNumber = "";
        private static string _currentCustomerPhone = "";
        private static string _currentSearchQuery = "";

        public static event Action GlobalCustomerUpdated;

        private CancellationTokenSource _searchCts;

        private static bool _isScannerHooked = false;
        private static string _barcodeBuffer = "";
        private static DateTime _lastKeystroke = DateTime.Now;

        static SalesView()
        {
            if (!_isScannerHooked)
            {
                InputManager.Current.PreProcessInput += GlobalInputHandler;
                _isScannerHooked = true;
            }
        }

        public SalesView()
        {
            InitializeComponent();
            icCartItems.ItemsSource = StoreState.CartItems;
            StoreState.CartItems.CollectionChanged += (s, e) => CalculateTotals();

            this.Loaded += (s, e) => GlobalCustomerUpdated += OnGlobalCustomerUpdated;
            this.Unloaded += (s, e) => GlobalCustomerUpdated -= OnGlobalCustomerUpdated;

            OnGlobalCustomerUpdated();
        }

        private static string GetString(string key)
        {
            return Application.Current.TryFindResource(key) as string ?? key;
        }

        private static void GlobalInputHandler(object sender, PreProcessInputEventArgs e)
        {
            if (e.StagingItem.Input is TextCompositionEventArgs textArgs)
            {
                TimeSpan elapsed = DateTime.Now - _lastKeystroke;
                if (elapsed.TotalMilliseconds > 100) _barcodeBuffer = "";
                _barcodeBuffer += textArgs.Text;
                _lastKeystroke = DateTime.Now;
            }
            else if (e.StagingItem.Input is KeyEventArgs keyArgs && keyArgs.RoutedEvent == Keyboard.KeyDownEvent)
            {
                if (keyArgs.Key == Key.Enter && _barcodeBuffer.Length >= 5)
                {
                    TimeSpan elapsed = DateTime.Now - _lastKeystroke;
                    if (elapsed.TotalMilliseconds < 150)
                    {
                        string scannedCode = _barcodeBuffer.Trim();
                        _barcodeBuffer = "";
                        keyArgs.Handled = true;

                        Application.Current.Dispatcher.InvokeAsync(() => {
                            PerformGlobalCustomerSearch(scannedCode, true);
                        });
                    }
                }
            }
        }

        public static void PerformGlobalCustomerSearch(string query, bool showPopup)
        {
            string cleanQueryPhone = new string(query.Where(char.IsDigit).ToArray());

            if (cleanQueryPhone.StartsWith("380")) cleanQueryPhone = cleanQueryPhone.Substring(3);
            else if (cleanQueryPhone.StartsWith("0")) cleanQueryPhone = cleanQueryPhone.Substring(1);

            try
            {
                using (var conn = new SqliteConnection(StoreState.ConnectionString))
                {
                    conn.Open();

                    string sql = @"
                        SELECT c.customer_id, 
                               IFNULL(c.available_bonuses, 0), 
                               IFNULL(u.full_name, c.phone_number), 
                               IFNULL(c.user_id, 0), 
                               IFNULL(c.purchased_items_count, 0), 
                               IFNULL(c.loyalty_card_number, 'Номер не вказано'),
                               IFNULL(c.phone_number, 'Не вказано'),
                               IFNULL(c.email, ''),
                               IFNULL(u.username, '')
                        FROM customers c 
                        LEFT JOIN users u ON c.user_id = u.user_id";

                    using (var cmd = new SqliteCommand(sql, conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        bool found = false;
                        while (r.Read())
                        {
                            string cardNum = r.GetString(5);
                            string phone = r.GetString(6);
                            string email = r.GetString(7);
                            string login = r.GetString(8);
                            string fullName = r.GetString(2);

                            string cleanCustPhone = new string(phone.Where(char.IsDigit).ToArray());

                            if (cardNum.IndexOf(query, StringComparison.InvariantCultureIgnoreCase) >= 0 ||
                                login.IndexOf(query, StringComparison.InvariantCultureIgnoreCase) >= 0 ||
                                fullName.IndexOf(query, StringComparison.InvariantCultureIgnoreCase) >= 0 ||
                                email.IndexOf(query, StringComparison.InvariantCultureIgnoreCase) >= 0 ||
                                (!string.IsNullOrEmpty(cleanQueryPhone) && cleanCustPhone.Contains(cleanQueryPhone)))
                            {
                                _currentCustomerId = r.GetInt32(0);
                                _availableBonuses = r.GetInt32(1);
                                _currentCustomerName = fullName;
                                _currentUserId = r.GetInt32(3);
                                _purchasedItemsCount = r.GetInt32(4);
                                _currentCardNumber = cardNum;
                                _currentCustomerPhone = phone;
                                _currentSearchQuery = query;
                                _isDiscountApplied = false;

                                found = true;
                                break;
                            }
                        }

                        if (found)
                        {
                            GlobalCustomerUpdated?.Invoke();

                            if (showPopup)
                            {
                                string msg = Application.Current.Dispatcher.Invoke(() => GetString("msgScannerSuccess"));
                                string title = Application.Current.Dispatcher.Invoke(() => GetString("txtScannerTitle"));
                                MessageBox.Show(string.Format(msg, _currentCustomerName), title, MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                        }
                        else if (showPopup)
                        {
                            string msg = Application.Current.Dispatcher.Invoke(() => GetString("msgScannerNotFound"));
                            string title = Application.Current.Dispatcher.Invoke(() => GetString("txtScannerTitle"));
                            MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }
            }
            catch { }
        }

        private void OnGlobalCustomerUpdated()
        {
            if (_currentCustomerId > 0)
            {
                edtPhoneSearch.Text = _currentSearchQuery;
                txtCustomerName.Text = _currentCustomerName;
                txtCardNumber.Text = _currentCardNumber;
                txtBonusProgress.Text = $"{_purchasedItemsCount % 10} / 10";
                txtAvailableBonuses.Text = $"{_availableBonuses} од.";
                pnlCustomerInfo.Visibility = Visibility.Visible;
                btnApplyDiscount.IsEnabled = _availableBonuses > 0;

                string cancelBtnTxt = Application.Current.TryFindResource("txtBtnCancel") as string ?? "СКАСУВАТИ";
                string applyBtnTxt = Application.Current.TryFindResource("txtBtnApplyDiscount") as string ?? "ЗАСТОСУВАТИ ЗНИЖКУ -15%";
                btnApplyDiscount.Content = _isDiscountApplied ? cancelBtnTxt + " -15%" : applyBtnTxt;
            }
            else
            {
                _currentUserId = 0;
                _availableBonuses = 0;
                _purchasedItemsCount = 0;
                _isDiscountApplied = false;
                _currentCustomerName = "";
                _currentCardNumber = "";
                _currentCustomerPhone = "";
                _currentSearchQuery = "";

                if (edtPhoneSearch != null) edtPhoneSearch.Text = "";
                if (pnlCustomerInfo != null) pnlCustomerInfo.Visibility = Visibility.Collapsed;
                if (btnApplyDiscount != null)
                {
                    btnApplyDiscount.IsEnabled = false;
                    btnApplyDiscount.SetResourceReference(Button.ContentProperty, "txtBtnApplyDiscount");
                }
            }
            CalculateTotals();
        }

        private void CalculateTotals()
        {
            if (StoreState.CartItems == null || StoreState.CartItems.Count == 0)
            {
                pnlEmptyCart.Visibility = Visibility.Visible;
                icCartItems.Visibility = Visibility.Collapsed;
            }
            else
            {
                pnlEmptyCart.Visibility = Visibility.Collapsed;
                icCartItems.Visibility = Visibility.Visible;
            }

            decimal subtotal = StoreState.CartItems?.Sum(x => x.TotalPrice) ?? 0;
            decimal discount = 0;

            if (_isDiscountApplied && _availableBonuses > 0)
            {
                discount = subtotal * 0.15m;
            }
            else
            {
                _isDiscountApplied = false;
            }

            decimal finalTotal = subtotal - discount;

            txtSubtotal.Text = $"{subtotal:N0} ₴";
            txtDiscount.Text = $"-{discount:N0} ₴";
            txtFinalTotal.Text = $"{finalTotal:N0} ₴";
        }

        private void BtnMinus_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var product = btn?.Tag as Product;
            if (product != null && product.Quantity > 1)
            {
                product.Quantity--;
                CalculateTotals();
            }
        }

        private void BtnPlus_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var product = btn?.Tag as Product;
            if (product != null)
            {
                if (product.Quantity < product.TotalQuantity)
                {
                    product.Quantity++;
                    CalculateTotals();
                }
                else
                {
                    MessageBox.Show(string.Format(GetString("msgItemLimitReached"), product.TotalQuantity), GetString("txtStock"), MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void BtnRemove_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var product = btn?.Tag as Product;
            if (product != null)
            {
                StoreState.CartItems.Remove(product);
                CalculateTotals();
            }
        }

        private void ClearCart_Click(object sender, RoutedEventArgs e)
        {
            if (StoreState.CartItems.Count > 0)
            {
                DialogOverlay.Visibility = Visibility.Visible;
            }
        }

        private void DialogYes_Click(object sender, RoutedEventArgs e)
        {
            DialogOverlay.Visibility = Visibility.Collapsed;
            StoreState.CartItems.Clear();
            _currentCustomerId = 0;
            OnGlobalCustomerUpdated();
        }

        private void DialogNo_Click(object sender, RoutedEventArgs e)
        {
            DialogOverlay.Visibility = Visibility.Collapsed;
        }

        private void ClearCustomer_Click(object sender, RoutedEventArgs e)
        {
            _currentCustomerId = 0;
            OnGlobalCustomerUpdated();
        }

        private async void edtPhoneSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = edtPhoneSearch.Text.Trim();
            if (string.IsNullOrEmpty(query))
            {
                _currentCustomerId = 0;
                OnGlobalCustomerUpdated();
                return;
            }

            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;

            try
            {
                await Task.Delay(400, token);
                PerformGlobalCustomerSearch(query, false);
            }
            catch (TaskCanceledException) { }
        }

        private void FindCustomer_Click(object sender, RoutedEventArgs e)
        {
            _searchCts?.Cancel();
            string query = edtPhoneSearch.Text.Trim();
            if (string.IsNullOrEmpty(query))
            {
                MessageBox.Show(GetString("msgScanCardPrompt"), GetString("txtWarning"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            PerformGlobalCustomerSearch(query, true);
        }

        private void ApplyDiscount_Click(object sender, RoutedEventArgs e)
        {
            if (_availableBonuses > 0 && StoreState.CartItems.Count > 0)
            {
                _isDiscountApplied = !_isDiscountApplied;

                string cancelBtnTxt = Application.Current.TryFindResource("txtBtnCancel") as string ?? "СКАСУВАТИ";
                string applyBtnTxt = Application.Current.TryFindResource("txtBtnApplyDiscount") as string ?? "ЗАСТОСУВАТИ ЗНИЖКУ -15%";

                btnApplyDiscount.Content = _isDiscountApplied ? cancelBtnTxt + " -15%" : applyBtnTxt;

                CalculateTotals();
            }
        }

        private void Checkout_Click(object sender, RoutedEventArgs e)
        {
            if (StoreState.CartItems.Count == 0)
            {
                MessageBox.Show(GetString("txtCartEmptyWarning"), GetString("txtWarning"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            long nextOrderId = 1;
            try
            {
                using (var conn = new SqliteConnection(StoreState.ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new SqliteCommand("SELECT IFNULL(MAX(order_id), 0) + 1 FROM Orders", conn))
                    {
                        nextOrderId = Convert.ToInt64(cmd.ExecuteScalar());
                    }
                }
            }
            catch { }

            FlowDocument doc = GenerateReceiptDocument(nextOrderId);

            string previewTitle = Application.Current.TryFindResource("txtReceiptPreview") as string ?? "Попередній перегляд: Чек № {0}";
            Window previewWindow = new Window
            {
                Title = string.Format(previewTitle, nextOrderId.ToString("D10")),
                Width = 1000,
                Height = 850,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background = Brushes.WhiteSmoke
            };

            Grid grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Border toolbarBorder = new Border { Background = Brushes.White, BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(15) };
            StackPanel toolbar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };

            Brush goldBrush = (Brush)new BrushConverter().ConvertFromString("#A67C27");

            Button btnPrint = new Button
            {
                Content = Application.Current.TryFindResource("txtPrintAndSell") as string ?? "🖨 Друк та Продаж",
                Height = 45,
                Padding = new Thickness(20, 0, 20, 0),
                Margin = new Thickness(0, 0, 20, 0),
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Background = Brushes.Black,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                VerticalContentAlignment = VerticalAlignment.Center
            };

            Button btnSavePdf = new Button
            {
                Content = Application.Current.TryFindResource("txtSavePdfAndSell") as string ?? "💾 Зберегти (PDF) та Продаж",
                Height = 45,
                Padding = new Thickness(20, 0, 20, 0),
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Background = goldBrush,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                VerticalContentAlignment = VerticalAlignment.Center
            };

            toolbar.Children.Add(btnPrint);
            toolbar.Children.Add(btnSavePdf);
            toolbarBorder.Child = toolbar;

            Grid.SetRow(toolbarBorder, 0);
            grid.Children.Add(toolbarBorder);

            FlowDocumentPageViewer viewer = new FlowDocumentPageViewer
            {
                Document = doc,
                Zoom = 100,
                Margin = new Thickness(10)
            };
            Grid.SetRow(viewer, 1);
            grid.Children.Add(viewer);

            bool isSaleCompleted = false;

            RoutedEventHandler printAction = (s, ev) =>
            {
                PrintDialog printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    viewer.Document = null;
                    printDialog.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, $"Чек_Каса_{nextOrderId:D10}");
                    isSaleCompleted = true;
                    previewWindow.Close();
                }
            };

            btnPrint.Click += printAction;

            btnSavePdf.Click += (s, ev) =>
            {
                MessageBox.Show("Оберіть Microsoft Print to PDF у списку принтерів", "Зберегти PDF", MessageBoxButton.OK, MessageBoxImage.Information);
                printAction(s, ev);
            };

            previewWindow.Closed += (s, ev) =>
            {
                if (isSaleCompleted)
                {
                    FinalizeSaleDbTransaction();
                }
                else
                {
                    MessageBox.Show(GetString("txtSaleCancelled"), GetString("txtCancellation"), MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };

            previewWindow.Content = grid;
            previewWindow.ShowDialog();
        }

        private void FinalizeSaleDbTransaction()
        {
            decimal subtotal = StoreState.CartItems.Sum(x => x.TotalPrice);
            decimal discount = _isDiscountApplied ? subtotal * 0.15m : 0;
            decimal finalTotal = subtotal - discount;
            int totalItemsCount = StoreState.CartItems.Sum(x => x.Quantity);

            string paymentMethod = _isDiscountApplied ? "Картка/Готівка (Використано бонус)" : "Картка/Готівка";
            string reportMessage = string.Format(GetString("txtSaleRecorded"), finalTotal);

            try
            {
                using (var conn = new SqliteConnection(StoreState.ConnectionString))
                {
                    conn.Open();
                    using (var transaction = conn.BeginTransaction())
                    {
                        long safeUserId = _currentUserId;
                        if (safeUserId <= 0)
                        {
                            using (var cmdCheck = new SqliteCommand("SELECT user_id FROM Users WHERE username = 'guest' LIMIT 1", conn, transaction))
                            {
                                var res = cmdCheck.ExecuteScalar();
                                if (res != null && res != DBNull.Value)
                                {
                                    safeUserId = Convert.ToInt64(res);
                                }
                                else
                                {
                                    using (var cmdInsGuest = new SqliteCommand(
                                        "INSERT INTO Users (role_id, full_name, username, password_hash) VALUES (4, 'Гість', 'guest', ''); SELECT last_insert_rowid();",
                                        conn, transaction))
                                    {
                                        safeUserId = Convert.ToInt64(cmdInsGuest.ExecuteScalar());
                                    }
                                }
                            }
                        }

                        string sqlOrder = "INSERT INTO Orders (user_id, order_date, total_amount, payment_method, status, order_source) VALUES (@uid, @date, @total, @pay, 'Виконано', 'Магазин')";
                        long newOrderId = 0;

                        using (var cmdOrder = new SqliteCommand(sqlOrder, conn, transaction))
                        {
                            cmdOrder.Parameters.AddWithValue("@uid", safeUserId);
                            cmdOrder.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
                            cmdOrder.Parameters.AddWithValue("@total", finalTotal);
                            cmdOrder.Parameters.AddWithValue("@pay", paymentMethod);
                            cmdOrder.ExecuteNonQuery();

                            using (var idCmd = new SqliteCommand("SELECT last_insert_rowid()", conn, transaction))
                            {
                                newOrderId = (long)idCmd.ExecuteScalar();
                            }
                        }

                        foreach (var item in StoreState.CartItems)
                        {
                            long stockId = 0;
                            string sqlFindStock = @"SELECT st.stock_id FROM Stock st 
                                                    LEFT JOIN Sizes s ON st.size_id = s.size_id 
                                                    WHERE st.product_id = @pid AND IFNULL(s.value, '-') = @size LIMIT 1";

                            using (var cmdFind = new SqliteCommand(sqlFindStock, conn, transaction))
                            {
                                cmdFind.Parameters.AddWithValue("@pid", item.Id);
                                cmdFind.Parameters.AddWithValue("@size", item.SelectedSize ?? "-");
                                var res = cmdFind.ExecuteScalar();
                                if (res != null && res != DBNull.Value) stockId = Convert.ToInt64(res);
                            }

                            if (stockId > 0)
                            {
                                string sqlItem = "INSERT INTO Order_Items (order_id, stock_id, quantity, price_at_time_of_sale) VALUES (@oid, @sid, @qty, @price)";
                                using (var cmdItem = new SqliteCommand(sqlItem, conn, transaction))
                                {
                                    cmdItem.Parameters.AddWithValue("@oid", newOrderId);
                                    cmdItem.Parameters.AddWithValue("@sid", stockId);
                                    cmdItem.Parameters.AddWithValue("@qty", item.Quantity);

                                    decimal discountedPrice = _isDiscountApplied ? item.Price * 0.85m : item.Price;
                                    cmdItem.Parameters.AddWithValue("@price", discountedPrice);
                                    cmdItem.ExecuteNonQuery();
                                }

                                string sqlStock = "UPDATE Stock SET quantity = quantity - @qty WHERE stock_id = @sid";
                                using (var cmdStock = new SqliteCommand(sqlStock, conn, transaction))
                                {
                                    cmdStock.Parameters.AddWithValue("@qty", item.Quantity);
                                    cmdStock.Parameters.AddWithValue("@sid", stockId);
                                    cmdStock.ExecuteNonQuery();
                                }
                            }
                        }

                        if (_currentCustomerId > 0)
                        {
                            int dbTotal = 0, dbBonuses = 0;
                            using (var cmdC = new SqliteCommand("SELECT purchased_items_count, available_bonuses FROM Customers WHERE customer_id = @cid", conn, transaction))
                            {
                                cmdC.Parameters.AddWithValue("@cid", _currentCustomerId);
                                using (var rC = cmdC.ExecuteReader())
                                {
                                    if (rC.Read())
                                    {
                                        dbTotal = rC.GetInt32(0);
                                        dbBonuses = rC.GetInt32(1);
                                    }
                                }
                            }

                            if (_isDiscountApplied) dbBonuses -= 1;

                            int newTotalItems = dbTotal + totalItemsCount;
                            int newlyEarnedBonuses = (newTotalItems / 10) - (dbTotal / 10);
                            int finalBonuses = Math.Max(0, dbBonuses + newlyEarnedBonuses);

                            using (var cmdUpdate = new SqliteCommand("UPDATE Customers SET purchased_items_count = @nt, available_bonuses = @nb WHERE customer_id = @cid", conn, transaction))
                            {
                                cmdUpdate.Parameters.AddWithValue("@nt", newTotalItems);
                                cmdUpdate.Parameters.AddWithValue("@nb", finalBonuses);
                                cmdUpdate.Parameters.AddWithValue("@cid", _currentCustomerId);
                                cmdUpdate.ExecuteNonQuery();
                            }

                            reportMessage += "\n\n" + string.Format(GetString("msgBonusEarned"), totalItemsCount, newTotalItems % 10, newlyEarnedBonuses);
                        }

                        transaction.Commit();
                    }
                }

                MessageBox.Show(reportMessage, GetString("txtSuccess"), MessageBoxButton.OK, MessageBoxImage.Information);

                StoreState.CartItems.Clear();
                _currentCustomerId = 0;
                OnGlobalCustomerUpdated();
                DatabaseService.LoadProducts();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{GetString("msgDbError")} {ex.Message}", GetString("txtError"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private FlowDocument GenerateReceiptDocument(long orderId)
        {
            FlowDocument doc = new FlowDocument
            {
                PagePadding = new Thickness(40),
                PageWidth = 793.92,
                PageHeight = 1122.24,
                ColumnWidth = 713.92,
                FontFamily = new FontFamily("Segoe UI"),
                Foreground = Brushes.Black,
                Background = Brushes.White
            };

            var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 15) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var leftStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            leftStack.Children.Add(new TextBlock { Text = "BLACKSMITH STORE", FontSize = 34, FontWeight = FontWeights.Black });
            Grid.SetColumn(leftStack, 0);
            headerGrid.Children.Add(leftStack);

            var rightStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            rightStack.Children.Add(new TextBlock { Text = "ТОВАРНИЙ ЧЕК", FontSize = 22, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Right });
            rightStack.Children.Add(new TextBlock { Text = $"Чек № {orderId:D10}", FontSize = 16, TextAlignment = TextAlignment.Right, Margin = new Thickness(0, 2, 0, 0) });
            rightStack.Children.Add(new TextBlock { Text = $"Дата: {DateTime.Now.ToString("dd.MM.yyyy HH:mm")}", FontSize = 13, Foreground = Brushes.Gray, TextAlignment = TextAlignment.Right, Margin = new Thickness(0, 2, 0, 0) });
            Grid.SetColumn(rightStack, 1);
            headerGrid.Children.Add(rightStack);

            doc.Blocks.Add(new BlockUIContainer(headerGrid));
            doc.Blocks.Add(new BlockUIContainer(new Rectangle { Height = 3, Fill = Brushes.Black, Margin = new Thickness(0, 0, 0, 25) }));

            var infoGrid = new Grid { Margin = new Thickness(0, 0, 0, 30) };
            infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var storePanel = new StackPanel { Margin = new Thickness(0, 0, 20, 0) };
            storePanel.Children.Add(new TextBlock { Text = "ПРОДАВЕЦЬ / ДЖЕРЕЛО:", FontSize = 11, Foreground = Brushes.Gray, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 8) });
            storePanel.Children.Add(new TextBlock { Text = "Blacksmith Store", FontSize = 16, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 5) });
            storePanel.Children.Add(new TextBlock { Text = "Магазин", FontSize = 14, Margin = new Thickness(0, 0, 0, 2) });
            storePanel.Children.Add(new TextBlock { Text = "Касир: " + (StoreState.CurrentUser?.FullName ?? "Адміністратор"), FontSize = 14 });
            Grid.SetColumn(storePanel, 0);
            infoGrid.Children.Add(storePanel);

            var customerPanel = new StackPanel();
            customerPanel.Children.Add(new TextBlock { Text = "ПОКУПЕЦЬ / КЛІЄНТ:", FontSize = 11, Foreground = Brushes.Gray, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 8) });
            string custName = _currentCustomerId > 0 ? _currentCustomerName : "Гість";
            customerPanel.Children.Add(new TextBlock { Text = custName, FontSize = 16, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 5) });

            if (_currentCustomerId > 0)
            {
                string displayPhone = _currentCustomerPhone;
                if (!string.IsNullOrWhiteSpace(displayPhone) && displayPhone != "Не вказано" && displayPhone != "-")
                {
                    string digits = new string(displayPhone.Where(char.IsDigit).ToArray());
                    if (digits.StartsWith("380")) digits = digits.Substring(3);
                    else if (digits.StartsWith("0")) digits = digits.Substring(1);

                    string formatted = "+380";
                    if (digits.Length > 0)
                    {
                        formatted += " " + digits.Substring(0, Math.Min(2, digits.Length));
                        if (digits.Length > 2) formatted += " " + digits.Substring(2, Math.Min(3, digits.Length - 2));
                        if (digits.Length > 5) formatted += " " + digits.Substring(5, Math.Min(2, digits.Length - 5));
                        if (digits.Length > 7) formatted += " " + digits.Substring(7, Math.Min(2, digits.Length - 7));
                    }
                    displayPhone = formatted;
                }

                customerPanel.Children.Add(new TextBlock { Text = $"Телефон: {displayPhone}", FontSize = 14, Margin = new Thickness(0, 0, 0, 2) });
                customerPanel.Children.Add(new TextBlock { Text = $"Клубна картка: {_currentCardNumber}", FontSize = 14, Margin = new Thickness(0, 0, 0, 2) });
            }

            Grid.SetColumn(customerPanel, 1);
            infoGrid.Children.Add(customerPanel);

            doc.Blocks.Add(new BlockUIContainer(infoGrid));

            Table table = new Table
            {
                CellSpacing = 0,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 10, 0, 20)
            };

            table.Columns.Add(new TableColumn { Width = new GridLength(40) });
            table.Columns.Add(new TableColumn { Width = new GridLength(100) });
            table.Columns.Add(new TableColumn { Width = new GridLength(240) });
            table.Columns.Add(new TableColumn { Width = new GridLength(70) });
            table.Columns.Add(new TableColumn { Width = new GridLength(60) });
            table.Columns.Add(new TableColumn { Width = new GridLength(95) });
            table.Columns.Add(new TableColumn { Width = new GridLength(108) });

            TableRowGroup rg = new TableRowGroup(); table.RowGroups.Add(rg);
            TableRow hRow = new TableRow { Background = Brushes.Black }; rg.Rows.Add(hRow);

            Action<TableRow, string, bool, TextAlignment> addCell = (r, text, isH, align) =>
            {
                var p = new Paragraph(new Run(text ?? "-")) { Margin = new Thickness(5, 8, 5, 8), FontSize = 13, TextAlignment = align };
                if (isH) { p.Foreground = Brushes.White; p.FontWeight = FontWeights.Bold; }
                r.Cells.Add(new TableCell(p) { BorderBrush = Brushes.Black, BorderThickness = new Thickness(0, 0, 0, 1) });
            };

            addCell(hRow, "№", true, TextAlignment.Center);
            addCell(hRow, "Артикул", true, TextAlignment.Left);
            addCell(hRow, "Найменування товару", true, TextAlignment.Left);
            addCell(hRow, "Розмір", true, TextAlignment.Center);
            addCell(hRow, "К-сть", true, TextAlignment.Center);
            addCell(hRow, "Ціна", true, TextAlignment.Right);
            addCell(hRow, "Сума", true, TextAlignment.Right);

            int index = 1;
            decimal itemsSum = 0;
            foreach (var item in StoreState.CartItems)
            {
                TableRow row = new TableRow(); rg.Rows.Add(row);
                addCell(row, index.ToString(), false, TextAlignment.Center);
                addCell(row, item.Article ?? "-", false, TextAlignment.Left);
                addCell(row, item.Name, false, TextAlignment.Left);
                addCell(row, string.IsNullOrWhiteSpace(item.SelectedSize) ? "-" : item.SelectedSize, false, TextAlignment.Center);
                addCell(row, item.Quantity.ToString(), false, TextAlignment.Center);
                addCell(row, $"{item.Price:N0}", false, TextAlignment.Right);
                addCell(row, $"{item.TotalPrice:N0}", false, TextAlignment.Right);
                itemsSum += item.TotalPrice;
                index++;
            }
            doc.Blocks.Add(table);

            var totalGrid = new Grid { Margin = new Thickness(0, 15, 0, 60) };
            totalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var totalStackContainer = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right };

            decimal discountAmt = _isDiscountApplied ? itemsSum * 0.15m : 0;
            decimal finalTotal = itemsSum - discountAmt;

            var subTotalStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 0, 5) };
            subTotalStack.Children.Add(new TextBlock { Text = "Сума товарів: ", FontSize = 14, Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 15, 0) });
            subTotalStack.Children.Add(new TextBlock { Text = $"{itemsSum:N0} ₴", FontSize = 16, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
            totalStackContainer.Children.Add(subTotalStack);

            if (_isDiscountApplied)
            {
                var discountStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 0, 15) };
                discountStack.Children.Add(new TextBlock { Text = "Застосовано бонуси (-15%): ", FontSize = 14, Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 15, 0) });
                discountStack.Children.Add(new TextBlock { Text = $"-{discountAmt:N0} ₴", FontSize = 16, FontWeight = FontWeights.Bold, Foreground = Brushes.DarkRed, VerticalAlignment = VerticalAlignment.Center });
                totalStackContainer.Children.Add(discountStack);
            }

            var totalStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            totalStack.Children.Add(new TextBlock { Text = "Всього до сплати: ", FontSize = 20, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 15, 0) });
            totalStack.Children.Add(new TextBlock { Text = $"{finalTotal:N0} ₴", FontSize = 26, FontWeight = FontWeights.Black, VerticalAlignment = VerticalAlignment.Center });

            totalStackContainer.Children.Add(totalStack);
            Grid.SetColumn(totalStackContainer, 0);
            totalGrid.Children.Add(totalStackContainer);
            doc.Blocks.Add(new BlockUIContainer(totalGrid));

            var sigGrid = new Grid { Margin = new Thickness(0, 20, 0, 0) };
            sigGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            sigGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            

            var footerStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            footerStack.Children.Add(new TextBlock { Text = "Дякуємо за покупку!", FontSize = 20, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center });
            footerStack.Children.Add(new TextBlock { Text = "Обмін та повернення протягом 14 днів за наявності чека", FontSize = 12, Foreground = Brushes.DarkGray, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 0) });
            doc.Blocks.Add(new BlockUIContainer(footerStack));

            return doc;
        }
    }
}