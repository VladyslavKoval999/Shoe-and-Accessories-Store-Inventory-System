using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Data.Sqlite;
using BlacksmithStore.Data;
using BlacksmithStore.Models;

namespace BlacksmithStore.Views
{
    public partial class WebOrdersView : UserControl, INotifyPropertyChanged
    {
        private ObservableCollection<WebOrder> _allOrders = new ObservableCollection<WebOrder>();
        public ObservableCollection<WebOrder> FilteredOrders { get; set; } = new ObservableCollection<WebOrder>();

        private DispatcherTimer _refreshTimer;

        private WebOrder _selectedOrder;
        public WebOrder SelectedOrder
        {
            get => _selectedOrder;
            set
            {
                _selectedOrder = value;
                NotifyPropertyChanged();
                if (DetailsPanel != null && txtEmptySelection != null)
                {
                    DetailsPanel.Visibility = value != null ? Visibility.Visible : Visibility.Collapsed;
                    txtEmptySelection.Visibility = value != null ? Visibility.Collapsed : Visibility.Visible;
                }
            }
        }

        public WebOrdersView()
        {
            InitializeComponent();
            this.DataContext = this;

            this.Loaded += (s, e) =>
            {
                RefreshOrdersSilently();

                if (_refreshTimer == null)
                {
                    _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                    _refreshTimer.Tick += (sender, args) => RefreshOrdersSilently();
                }
                _refreshTimer.Start();
            };

            this.Unloaded += (s, e) =>
            {
                _refreshTimer?.Stop();
            };
        }

        private string GetString(string key)
        {
            return Application.Current.TryFindResource(key) as string ?? key;
        }

        private void RefreshOrdersSilently()
        {
            try
            {
                var dbOrders = new List<WebOrder>();

                using (var conn = new SqliteConnection(StoreState.ConnectionString))
                {
                    conn.Open();

                    try { new SqliteCommand("ALTER TABLE Orders ADD COLUMN delivery_address TEXT DEFAULT 'Не вказано'", conn).ExecuteNonQuery(); } catch { }
                    try { new SqliteCommand("ALTER TABLE Orders ADD COLUMN comment TEXT DEFAULT ''", conn).ExecuteNonQuery(); } catch { }
                    try { new SqliteCommand("ALTER TABLE Orders ADD COLUMN order_source TEXT DEFAULT 'Магазин'", conn).ExecuteNonQuery(); } catch { }
                    try { new SqliteCommand("ALTER TABLE Orders ADD COLUMN status TEXT DEFAULT 'Очікує збору'", conn).ExecuteNonQuery(); } catch { }

                    string sqlOrders = @"
                        SELECT o.order_id, 
                               o.order_date, 
                               o.total_amount,
                               IFNULL(u.full_name, 'Гість'), 
                               IFNULL(c.phone_number, '-'),
                               IFNULL(o.delivery_address, 'Не вказано'), 
                               IFNULL(o.comment, ''), 
                               IFNULL(o.status, 'Очікує збору'),
                               IFNULL(o.user_id, 0),
                               IFNULL(o.payment_method, '')
                        FROM Orders o
                        LEFT JOIN Users u ON o.user_id = u.user_id
                        LEFT JOIN Customers c ON o.user_id = c.user_id
                        WHERE o.order_source = 'Веб-сайт' OR o.order_source = 'Web' OR o.order_source = 'Сайт'
                        ORDER BY o.order_id DESC";

                    using (var cmd = new SqliteCommand(sqlOrders, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var order = new WebOrder
                            {
                                Id = reader.GetInt32(0),
                                OrderDate = reader.GetString(1),
                                TotalAmount = reader.GetDecimal(2),
                                CustomerName = reader.GetString(3),
                                CustomerPhone = reader.GetString(4),
                                DeliveryAddress = reader.GetString(5),
                                Comment = reader.GetString(6),
                                Status = reader.GetString(7),
                                UserId = reader.GetInt32(8),
                                PaymentMethod = reader.GetString(9)
                            };

                            if (order.Status == "Виконано") order.Status = "Доставлено";
                            if (order.CustomerName == "Гість") order.CustomerName = GetString("txtGuest");
                            if (order.DeliveryAddress.Contains("Адреса доставки:"))
                            {
                                var parts = order.DeliveryAddress.Split(new[] { "| Ком:" }, StringSplitOptions.None);
                                order.DeliveryAddress = parts[0].Replace("Готівка/Картка кур'єру.", "").Replace("Адреса доставки:", "").Trim();
                                if (parts.Length > 1 && string.IsNullOrEmpty(order.Comment))
                                {
                                    order.Comment = parts[1].Trim();
                                }
                            }
                            dbOrders.Add(order);
                        }
                    }

                    foreach (var order in dbOrders)
                    {
                        string sqlItems = @"
                            SELECT IFNULL(p.article, '-'), p.name, IFNULL(s.value, '-'), oi.quantity, oi.price_at_time_of_sale
                            FROM Order_Items oi
                            JOIN Stock st ON oi.stock_id = st.stock_id
                            JOIN Products p ON st.product_id = p.product_id
                            LEFT JOIN Sizes s ON st.size_id = s.size_id
                            WHERE oi.order_id = @oid";

                        using (var cmdItems = new SqliteCommand(sqlItems, conn))
                        {
                            cmdItems.Parameters.AddWithValue("@oid", order.Id);
                            using (var ri = cmdItems.ExecuteReader())
                            {
                                while (ri.Read())
                                {
                                    string rawSize = ri.GetString(2);
                                    if (double.TryParse(rawSize.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
                                        rawSize = val.ToString();

                                    order.Items.Add(new WebOrderItem
                                    {
                                        Article = ri.GetString(0),
                                        ProductName = ri.GetString(1),
                                        Size = rawSize,
                                        Quantity = ri.GetInt32(3),
                                        Price = ri.GetDecimal(4)
                                    });
                                }
                            }
                        }
                    }
                }

                var toRemove = _allOrders.Where(o => !dbOrders.Any(db => db.Id == o.Id)).ToList();
                foreach (var r in toRemove) { _allOrders.Remove(r); }

                for (int i = 0; i < dbOrders.Count; i++)
                {
                    var dbO = dbOrders[i];
                    var existing = _allOrders.FirstOrDefault(o => o.Id == dbO.Id);

                    if (existing == null)
                    {
                        _allOrders.Insert(i, dbO);
                    }
                    else
                    {
                        bool needsItemUpdate = false;

                        if (existing.Status != dbO.Status) existing.Status = dbO.Status;
                        if (existing.TotalAmount != dbO.TotalAmount) existing.TotalAmount = dbO.TotalAmount;
                        if (existing.DeliveryAddress != dbO.DeliveryAddress) existing.DeliveryAddress = dbO.DeliveryAddress;
                        if (existing.Comment != dbO.Comment) existing.Comment = dbO.Comment;
                        if (existing.PaymentMethod != dbO.PaymentMethod) existing.PaymentMethod = dbO.PaymentMethod;

                        if (existing.Items.Count != dbO.Items.Count)
                        {
                            needsItemUpdate = true;
                        }
                        else
                        {
                            for (int j = 0; j < existing.Items.Count; j++)
                            {
                                if (existing.Items[j].Article != dbO.Items[j].Article ||
                                    existing.Items[j].Quantity != dbO.Items[j].Quantity)
                                {
                                    needsItemUpdate = true;
                                    break;
                                }
                            }
                        }

                        if (needsItemUpdate)
                        {
                            existing.Items.Clear();
                            foreach (var item in dbO.Items) existing.Items.Add(item);
                        }
                    }
                }

                ApplyFilterSilent();
            }
            catch { }
        }

        private void cbStatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyFilterSilent();

        private void ApplyFilterSilent()
        {
            if (cbStatusFilter == null || cbStatusFilter.SelectedItem == null) return;
            string selectedFilter = (cbStatusFilter.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

            var expectedOrders = _allOrders.Where(o => selectedFilter == "Всі замовлення" || o.Status == selectedFilter).ToList();
            var expectedIds = expectedOrders.Select(o => o.Id).ToList();

            var toRemove = FilteredOrders.Where(o => !expectedIds.Contains(o.Id)).ToList();
            foreach (var r in toRemove) FilteredOrders.Remove(r);

            for (int i = 0; i < expectedOrders.Count; i++)
            {
                if (i >= FilteredOrders.Count || FilteredOrders[i].Id != expectedOrders[i].Id)
                {
                    var existing = FilteredOrders.FirstOrDefault(o => o.Id == expectedOrders[i].Id);
                    if (existing != null) FilteredOrders.Remove(existing);

                    FilteredOrders.Insert(i, expectedOrders[i]);
                }
            }
        }

        private void dgWebOrders_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgWebOrders.SelectedItem is WebOrder order)
            {
                SelectedOrder = order;
                if (cbUpdateStatus != null)
                {
                    var matchingStatus = cbUpdateStatus.Items.Cast<ComboBoxItem>().FirstOrDefault(i => i.Content.ToString() == order.Status);
                    if (matchingStatus != null) cbUpdateStatus.SelectedItem = matchingStatus;
                }
            }
            else
            {
                SelectedOrder = null;
            }
        }

        private void UpdateStatus_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedOrder == null || cbUpdateStatus.SelectedItem == null) return;

            string newStatus = (cbUpdateStatus.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            string dbStatusToSave = newStatus == "Доставлено" ? "Виконано" : newStatus;

            int currentOrderId = SelectedOrder.Id;
            int userId = SelectedOrder.UserId;
            string currentAddress = SelectedOrder.DeliveryAddress ?? "";
            string currentComment = SelectedOrder.Comment ?? "";

            string phoneStr = SelectedOrder.CustomerPhone ?? "";
            string cleanPhone = new string(phoneStr.Where(c => char.IsDigit(c) || c == '+').ToArray());
            int totalItemsBought = SelectedOrder.Items.Sum(i => i.Quantity);

            string bonusDebugMessage = GetString("msgBonusNotAwarded");

            try
            {
                using (var conn = new SqliteConnection(StoreState.ConnectionString))
                {
                    conn.Open();
                    using (var tx = conn.BeginTransaction())
                    {
                        string oldDbStatus = "Очікує збору";
                        using (var cmdOld = new SqliteCommand("SELECT IFNULL(status, 'Очікує збору') FROM Orders WHERE order_id = @id", conn, tx))
                        {
                            cmdOld.Parameters.AddWithValue("@id", currentOrderId);
                            var res = cmdOld.ExecuteScalar();
                            if (res != null && res != DBNull.Value) oldDbStatus = res.ToString();
                        }

                        bool usedBonus = false;
                        using (var pCmd = new SqliteCommand("SELECT IFNULL(payment_method, '') FROM Orders WHERE order_id = @id", conn, tx))
                        {
                            pCmd.Parameters.AddWithValue("@id", currentOrderId);
                            var pm = pCmd.ExecuteScalar()?.ToString();
                            if (pm != null && pm.Contains("(Використано бонус)"))
                            {
                                usedBonus = true;
                            }
                        }

                        string sqlOrder = "UPDATE Orders SET status = @status, delivery_address = @address, comment = @comment WHERE order_id = @id";
                        using (var cmd = new SqliteCommand(sqlOrder, conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@status", dbStatusToSave);
                            cmd.Parameters.AddWithValue("@address", currentAddress);
                            cmd.Parameters.AddWithValue("@comment", currentComment);
                            cmd.Parameters.AddWithValue("@id", currentOrderId);
                            cmd.ExecuteNonQuery();
                        }

                        if (totalItemsBought > 0)
                        {
                            int custId = 0, oldTotal = 0, currentBonuses = 0;
                            string sqlGetCust = @"
                                SELECT customer_id, IFNULL(purchased_items_count, 0), IFNULL(available_bonuses, 0) 
                                FROM Customers 
                                WHERE (user_id = @uid AND @uid > 0) 
                                   OR (phone_number LIKE '%' || @phone || '%' AND length(@phone) > 7) 
                                LIMIT 1";

                            using (var cmdC = new SqliteCommand(sqlGetCust, conn, tx))
                            {
                                cmdC.Parameters.AddWithValue("@uid", userId);
                                cmdC.Parameters.AddWithValue("@phone", cleanPhone.Length > 7 ? cleanPhone : "NO_MATCH_123");
                                using (var rC = cmdC.ExecuteReader())
                                {
                                    if (rC.Read())
                                    {
                                        custId = rC.GetInt32(0);
                                        oldTotal = rC.GetInt32(1);
                                        currentBonuses = rC.GetInt32(2);
                                    }
                                }
                            }

                            if (custId > 0)
                            {
                                if (dbStatusToSave == "Виконано" && oldDbStatus != "Виконано")
                                {
                                    int newTotal = oldTotal + totalItemsBought;
                                    int newBonusesEarned = (newTotal / 10) - (oldTotal / 10);
                                    using (var cmdU = new SqliteCommand("UPDATE Customers SET purchased_items_count = @nt, available_bonuses = @nb WHERE customer_id = @cid", conn, tx))
                                    {
                                        cmdU.Parameters.AddWithValue("@nt", newTotal);
                                        cmdU.Parameters.AddWithValue("@nb", currentBonuses + newBonusesEarned);
                                        cmdU.Parameters.AddWithValue("@cid", custId);
                                        cmdU.ExecuteNonQuery();
                                    }
                                    bonusDebugMessage = string.Format(GetString("msgBonusEarned"), totalItemsBought, newTotal % 10, newBonusesEarned);
                                }
                                else if (dbStatusToSave == "Скасовано" && oldDbStatus != "Скасовано")
                                {
                                    string sqlRestore = @"
                                        UPDATE Stock 
                                        SET quantity = quantity + (
                                            SELECT quantity FROM Order_Items 
                                            WHERE Order_Items.stock_id = Stock.stock_id AND Order_Items.order_id = @id
                                        )
                                        WHERE stock_id IN (SELECT stock_id FROM Order_Items WHERE order_id = @id)";
                                    using (var rCmd = new SqliteCommand(sqlRestore, conn, tx))
                                    {
                                        rCmd.Parameters.AddWithValue("@id", currentOrderId);
                                        rCmd.ExecuteNonQuery();
                                    }

                                    if (oldDbStatus == "Виконано" || oldDbStatus == "Доставлено")
                                    {
                                        int newTotal = Math.Max(0, oldTotal - totalItemsBought);
                                        int bonusesToRevert = (oldTotal / 10) - (newTotal / 10);
                                        int newBonuses = Math.Max(0, currentBonuses - bonusesToRevert);

                                        if (usedBonus) newBonuses += 1;

                                        using (var cmdU = new SqliteCommand("UPDATE Customers SET purchased_items_count = @nt, available_bonuses = @nb WHERE customer_id = @cid", conn, tx))
                                        {
                                            cmdU.Parameters.AddWithValue("@nt", newTotal);
                                            cmdU.Parameters.AddWithValue("@nb", newBonuses);
                                            cmdU.Parameters.AddWithValue("@cid", custId);
                                            cmdU.ExecuteNonQuery();
                                        }
                                        bonusDebugMessage = string.Format(GetString("msgOrderCancelled"), totalItemsBought);
                                        if (usedBonus) bonusDebugMessage += "\nВикористаний бонус успішно повернуто!";
                                    }
                                    else
                                    {
                                        if (usedBonus)
                                        {
                                            using (var cmdU = new SqliteCommand("UPDATE Customers SET available_bonuses = available_bonuses + 1 WHERE customer_id = @cid", conn, tx))
                                            {
                                                cmdU.Parameters.AddWithValue("@cid", custId);
                                                cmdU.ExecuteNonQuery();
                                            }
                                            bonusDebugMessage = "Замовлення скасовано. Товари та використаний бонус повернуто!";
                                        }
                                        else
                                        {
                                            bonusDebugMessage = GetString("msgOrderCancelledNoBonus");
                                        }
                                    }
                                }
                                else
                                {
                                    bonusDebugMessage = GetString("msgStatusUpdated");
                                }
                            }
                        }
                        tx.Commit();
                    }
                }

                var orderInList = _allOrders.FirstOrDefault(o => o.Id == currentOrderId);
                if (orderInList != null) orderInList.Status = newStatus;

                ApplyFilterSilent();
                (Application.Current.MainWindow as MainWindow)?.CheckNewWebOrders();

                MessageBox.Show(string.Format(GetString("msgStatusChanged"), currentOrderId, newStatus, bonusDebugMessage), GetString("txtSaved"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{GetString("msgSaveError")} {ex.Message}", GetString("txtError"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PrintInvoice_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedOrder == null) return;
            WebOrder currentOrder = SelectedOrder;

            FlowDocument doc = GeneratePremiumInvoice(currentOrder);

            Window previewWindow = new Window
            {
                Title = "Друк документа",
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
                Content = GetString("txtPrint") ?? "Друк",
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
                Content = GetString("txtSavePdf") ?? "Зберегти PDF",
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

            RoutedEventHandler printAction = (s, ev) =>
            {
                PrintDialog printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    viewer.Document = null;
                    printDialog.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, $"Invoice_WEB_{currentOrder.Id:D10}");
                    previewWindow.Close();
                }
            };

            btnPrint.Click += printAction;
            btnSavePdf.Click += (s, ev) =>
            {
                MessageBox.Show("Оберіть Microsoft Print to PDF у списку принтерів", "Зберегти PDF", MessageBoxButton.OK, MessageBoxImage.Information);
                printAction(s, ev);
            };

            previewWindow.Content = grid;
            previewWindow.ShowDialog();
        }

        private FlowDocument GeneratePremiumInvoice(WebOrder order)
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
            rightStack.Children.Add(new TextBlock { Text = "ВИДАТКОВА НАКЛАДНА", FontSize = 20, FontWeight = FontWeights.Bold });
            rightStack.Children.Add(new TextBlock { Text = $"Замовлення №: {order.Id:D10}", FontSize = 14, TextAlignment = TextAlignment.Right, Margin = new Thickness(0, 2, 0, 0) });

            DateTime parsedDate;
            string dateStr = DateTime.TryParse(order.OrderDate, out parsedDate) ? parsedDate.ToString("dd.MM.yyyy HH:mm") : order.OrderDate;
            rightStack.Children.Add(new TextBlock { Text = $"Дата: {dateStr}", FontSize = 13, Foreground = Brushes.Gray, TextAlignment = TextAlignment.Right, Margin = new Thickness(0, 2, 0, 0) });

            Grid.SetColumn(rightStack, 1);
            headerGrid.Children.Add(rightStack);

            doc.Blocks.Add(new BlockUIContainer(headerGrid));
            doc.Blocks.Add(new BlockUIContainer(new Rectangle { Height = 2, Fill = Brushes.Black, Margin = new Thickness(0, 0, 0, 20) }));

            var infoGrid = new Grid { Margin = new Thickness(0, 0, 0, 20) };
            infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var supplierPanel = new StackPanel { Margin = new Thickness(0, 0, 20, 0) };
            supplierPanel.Children.Add(new TextBlock { Text = "ВІДПРАВНИК / ПОСТАЧАЛЬНИК:", FontSize = 11, Foreground = Brushes.Gray, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 8) });
            supplierPanel.Children.Add(new TextBlock { Text = "Blacksmith Store", FontSize = 16, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 5) });
            supplierPanel.Children.Add(new TextBlock { Text = "Офіційний онлайн-магазин", FontSize = 14, Margin = new Thickness(0, 0, 0, 2) });
            supplierPanel.Children.Add(new TextBlock { Text = "Email: blacksmithstoreinfo@gmail.com", FontSize = 14 });
            Grid.SetColumn(supplierPanel, 0);
            infoGrid.Children.Add(supplierPanel);

            var customerPanel = new StackPanel();
            customerPanel.Children.Add(new TextBlock { Text = "ОДЕРЖУВАЧ / КЛІЄНТ:", FontSize = 11, Foreground = Brushes.Gray, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 8) });
            customerPanel.Children.Add(new TextBlock { Text = order.CustomerName, FontSize = 16, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 5) });

            string displayPhone = order.CustomerPhone;
            if (!string.IsNullOrWhiteSpace(displayPhone) && displayPhone != "-" && !displayPhone.StartsWith("+"))
            {
                displayPhone = "+" + displayPhone;
            }

            customerPanel.Children.Add(new TextBlock { Text = $"Телефон: {displayPhone}", FontSize = 14, Margin = new Thickness(0, 0, 0, 2) });
            customerPanel.Children.Add(new TextBlock { Text = $"Доставка: {order.DeliveryAddress}", FontSize = 14, Margin = new Thickness(0, 0, 0, 2), TextWrapping = TextWrapping.Wrap });

            if (!string.IsNullOrWhiteSpace(order.Comment))
            {
                customerPanel.Children.Add(new TextBlock { Text = $"Коментар: {order.Comment}", FontSize = 14, TextWrapping = TextWrapping.Wrap });
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
            table.Columns.Add(new TableColumn { Width = new GridLength(270) });
            table.Columns.Add(new TableColumn { Width = new GridLength(60) });
            table.Columns.Add(new TableColumn { Width = new GridLength(60) });
            table.Columns.Add(new TableColumn { Width = new GridLength(90) });
            table.Columns.Add(new TableColumn { Width = new GridLength(93) });

            TableRowGroup rg = new TableRowGroup(); table.RowGroups.Add(rg);
            TableRow hRow = new TableRow { Background = Brushes.Black }; rg.Rows.Add(hRow);

            Action<TableRow, string, bool, TextAlignment> addCell = (r, text, isH, align) =>
            {
                var p = new Paragraph(new Run(text ?? "-")) { Margin = new Thickness(5, 8, 5, 8), FontSize = 12, TextAlignment = align };
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
            foreach (var item in order.Items)
            {
                TableRow row = new TableRow(); rg.Rows.Add(row);
                addCell(row, index.ToString(), false, TextAlignment.Center);
                addCell(row, item.Article, false, TextAlignment.Left);
                addCell(row, item.ProductName, false, TextAlignment.Left);
                addCell(row, string.IsNullOrWhiteSpace(item.Size) ? "-" : item.Size, false, TextAlignment.Center);
                addCell(row, item.Quantity.ToString(), false, TextAlignment.Center);
                addCell(row, $"{item.Price:N0}", false, TextAlignment.Right);
                addCell(row, $"{(item.Price * item.Quantity):N0}", false, TextAlignment.Right);

                itemsSum += item.Price * item.Quantity;
                index++;
            }
            doc.Blocks.Add(table);

            var totalGrid = new Grid { Margin = new Thickness(0, 5, 0, 40) };
            totalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var totalStackContainer = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right };

            decimal expectedTotalWithoutDiscount = itemsSum + 50m;
            decimal actualDiscount = expectedTotalWithoutDiscount - order.TotalAmount;

            bool hasDiscountMark = !string.IsNullOrEmpty(order.PaymentMethod) && order.PaymentMethod.Contains("Використано бонус");
            bool hasDiscount = hasDiscountMark || actualDiscount > 0;

            var subTotalStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 0, 5) };
            subTotalStack.Children.Add(new TextBlock { Text = "Сума товарів: ", FontSize = 14, Foreground = Brushes.Gray, Margin = new Thickness(0, 0, 15, 0) });
            subTotalStack.Children.Add(new TextBlock { Text = $"{itemsSum:N0} ₴", FontSize = 16, FontWeight = FontWeights.Bold });
            totalStackContainer.Children.Add(subTotalStack);

            if (hasDiscount)
            {
                decimal discountToDisplay = actualDiscount > 0 ? actualDiscount : (itemsSum * 0.15m);
                var discountRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 0, 5) };
                discountRow.Children.Add(new TextBlock { Text = "Застосовано бонуси (знижка 15%): ", FontSize = 14, Foreground = Brushes.Gray, Margin = new Thickness(0, 0, 15, 0) });
                discountRow.Children.Add(new TextBlock { Text = $"-{discountToDisplay:N0} ₴", FontSize = 16, FontWeight = FontWeights.Bold, Foreground = Brushes.DarkRed });
                totalStackContainer.Children.Add(discountRow);
            }

            var deliveryStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 0, 15) };
            deliveryStack.Children.Add(new TextBlock { Text = "Доставка (кур'єр м. Харків): ", FontSize = 14, Foreground = Brushes.Gray, Margin = new Thickness(0, 0, 15, 0) });
            deliveryStack.Children.Add(new TextBlock { Text = "50 ₴", FontSize = 16, FontWeight = FontWeights.Bold });
            totalStackContainer.Children.Add(deliveryStack);

            var totalStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, Orientation = Orientation.Horizontal };
            totalStack.Children.Add(new TextBlock { Text = "Всього до сплати: ", FontSize = 20, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 15, 0) });
            totalStack.Children.Add(new TextBlock { Text = $"{order.TotalAmount:N0} ₴", FontSize = 26, FontWeight = FontWeights.Black, VerticalAlignment = VerticalAlignment.Center });

            totalStackContainer.Children.Add(totalStack);
            Grid.SetColumn(totalStackContainer, 0);
            totalGrid.Children.Add(totalStackContainer);
            doc.Blocks.Add(new BlockUIContainer(totalGrid));

            var sigGrid = new Grid { Margin = new Thickness(0, 20, 0, 0) };
            sigGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            sigGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var fromPanel = new StackPanel { Orientation = Orientation.Horizontal };
            fromPanel.Children.Add(new TextBlock { Text = "Кур'єр: ", FontSize = 14, FontWeight = FontWeights.Bold });
            fromPanel.Children.Add(new Border { BorderBrush = Brushes.Black, BorderThickness = new Thickness(0, 0, 0, 1), Width = 180, Margin = new Thickness(10, 0, 0, 0) });
            Grid.SetColumn(fromPanel, 0);
            sigGrid.Children.Add(fromPanel);

            var toPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            toPanel.Children.Add(new TextBlock { Text = "Товар отримав: ", FontSize = 14, FontWeight = FontWeights.Bold });
            toPanel.Children.Add(new Border { BorderBrush = Brushes.Black, BorderThickness = new Thickness(0, 0, 0, 1), Width = 180, Margin = new Thickness(10, 0, 0, 0) });
            Grid.SetColumn(toPanel, 1);
            sigGrid.Children.Add(toPanel);

            doc.Blocks.Add(new BlockUIContainer(sigGrid));

            foreach (var item in order.Items)
            {
                for (int i = 0; i < item.Quantity; i++)
                {
                    var breakParagraph = new Paragraph() { BreakPageBefore = true };
                    doc.Blocks.Add(breakParagraph);

                    var labelBorder = new Border
                    {
                        BorderBrush = Brushes.Black,
                        BorderThickness = new Thickness(4),
                        CornerRadius = new CornerRadius(10),
                        Padding = new Thickness(35),
                        Margin = new Thickness(60, 40, 60, 40),
                        Background = Brushes.White,
                        SnapsToDevicePixels = true
                    };

                    var labelContent = new StackPanel();
                    var receiptGrid = new Grid { Margin = new Thickness(0, 0, 0, 20) };
                    receiptGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    receiptGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var receiptLeftStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                    receiptLeftStack.Children.Add(new TextBlock { Text = "ТОВАРНИЙ ЧЕК", FontSize = 28, FontWeight = FontWeights.Black, Margin = new Thickness(0, 0, 0, 10) });
                    receiptLeftStack.Children.Add(new TextBlock { Text = $"Замовлення №: {order.Id:D10}", FontSize = 18, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 5) });
                    receiptLeftStack.Children.Add(new TextBlock { Text = $"{dateStr}", FontSize = 16, Foreground = Brushes.DarkGray });
                    Grid.SetColumn(receiptLeftStack, 0);
                    receiptGrid.Children.Add(receiptLeftStack);

                    var qrStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(20, 0, 0, 0) };
                    try
                    {
                        string qrUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=150x150&data={order.Id:D10}";
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(qrUrl, UriKind.Absolute);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        var img = new Image { Source = bitmap, Width = 90, Height = 90, HorizontalAlignment = HorizontalAlignment.Right };
                        qrStack.Children.Add(img);
                    }
                    catch { }
                    Grid.SetColumn(qrStack, 1);
                    receiptGrid.Children.Add(qrStack);

                    labelContent.Children.Add(receiptGrid);
                    labelContent.Children.Add(new Rectangle { Height = 2, Fill = Brushes.LightGray, StrokeDashArray = new DoubleCollection { 4, 4 }, Margin = new Thickness(0, 0, 0, 20) });

                    labelContent.Children.Add(new TextBlock { Text = $"Арт. {item.Article ?? "-"}", FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 5) });
                    labelContent.Children.Add(new TextBlock { Text = item.ProductName, FontSize = 26, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 30) });

                    var spGrid = new Grid();
                    spGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    spGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    var sizeBlock = new StackPanel();
                    sizeBlock.Children.Add(new TextBlock { Text = "РОЗМІР", FontSize = 14, Foreground = Brushes.Gray, FontWeight = FontWeights.Bold });
                    sizeBlock.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(item.Size) ? "-" : item.Size, FontSize = 32, FontWeight = FontWeights.Black });
                    Grid.SetColumn(sizeBlock, 0);
                    spGrid.Children.Add(sizeBlock);

                    var priceBlock = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right };
                    priceBlock.Children.Add(new TextBlock { Text = "ЦІНА", FontSize = 14, Foreground = Brushes.Gray, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Right });
                    priceBlock.Children.Add(new TextBlock { Text = $"{item.Price:N0} ₴", FontSize = 32, FontWeight = FontWeights.Black, TextAlignment = TextAlignment.Right });
                    Grid.SetColumn(priceBlock, 1);
                    spGrid.Children.Add(priceBlock);

                    labelContent.Children.Add(spGrid);

                    if (hasDiscount)
                    {
                        labelContent.Children.Add(new TextBlock
                        {
                            Text = "До замовлення застосовано клубні бонуси (-15%)",
                            FontSize = 14,
                            Foreground = Brushes.DarkRed,
                            FontWeight = FontWeights.Bold,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0, 15, 0, 0)
                        });
                    }

                    labelContent.Children.Add(new Rectangle
                    {
                        Height = 2,
                        Fill = Brushes.LightGray,
                        StrokeDashArray = new DoubleCollection { 4, 4 },
                        Margin = new Thickness(0, 30, 0, 20)
                    });

                    labelContent.Children.Add(new TextBlock
                    {
                        Text = "Дякуємо за покупку!",
                        FontSize = 24,
                        FontWeight = FontWeights.Bold,
                        HorizontalAlignment = HorizontalAlignment.Center
                    });

                    labelContent.Children.Add(new TextBlock
                    {
                        Text = "www.blacksmith.ua",
                        FontSize = 18,
                        Foreground = Brushes.Gray,
                        FontWeight = FontWeights.SemiBold,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 5, 0, 0)
                    });

                    labelContent.Children.Add(new TextBlock
                    {
                        Text = "Обмін та повернення протягом 14 днів за наявності чека",
                        FontSize = 14,
                        Foreground = Brushes.DarkGray,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 15, 0, 0)
                    });

                    labelBorder.Child = labelContent;
                    doc.Blocks.Add(new BlockUIContainer(labelBorder));
                }
            }

            return doc;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}