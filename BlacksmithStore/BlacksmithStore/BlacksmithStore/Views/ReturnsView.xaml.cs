using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.Sqlite;
using BlacksmithStore.Data;
using BlacksmithStore.Models;

namespace BlacksmithStore.Views
{
    public class OrderReturnItem : INotifyPropertyChanged
    {
        private string GetString(string key) => Application.Current?.TryFindResource(key) as string ?? key;

        public int OrderId { get; set; }
        public int StockId { get; set; }

        public string Article { get; set; }
        public string ProductName { get; set; }
        public string Brand { get; set; }
        public string Size { get; set; }
        public int Quantity { get; set; }
        public decimal SalePrice { get; set; }
        public string OrderDate { get; set; }

        public string OrderStatus { get; set; }

        public bool IsReturnable
        {
            get
            {
                if (OrderStatus == "Скасовано" || OrderStatus == "Cancelled") return false;

                if (DateTime.TryParse(OrderDate, out DateTime date))
                {
                    return (DateTime.Now - date).TotalDays <= 14;
                }
                return false;
            }
        }

        public string ReturnStatus
        {
            get
            {
                if (OrderStatus == "Скасовано" || OrderStatus == "Cancelled")
                {
                    return GetString("txtOrderCanceledReturn") ?? "Скасовано (вже на складі)";
                }

                return IsReturnable ? (GetString("txtReturnAllowed") ?? "Доступно до повернення") : (GetString("txtReturnExpired") ?? "Термін повернення минув");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class ReturnsView : UserControl, INotifyPropertyChanged
    {
        public ObservableCollection<OrderReturnItem> OrderItems { get; set; } = new ObservableCollection<OrderReturnItem>();
        private OrderReturnItem _selectedReturnItem;
        public OrderReturnItem SelectedReturnItem
        {
            get => _selectedReturnItem;
            set { _selectedReturnItem = value; OnPropertyChanged(); }
        }

        public ReturnsView()
        {
            InitializeComponent();
            this.DataContext = this;
            EnsureReturnsTableExists();
        }

        private string GetString(string key)
        {
            return Application.Current.TryFindResource(key) as string ?? key;
        }

        private void EnsureReturnsTableExists()
        {
            try
            {
                using (var conn = new SqliteConnection(StoreState.ConnectionString))
                {
                    conn.Open();
                    string sql = @"CREATE TABLE IF NOT EXISTS returns (
                                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                                    order_id INTEGER NOT NULL,
                                    stock_id INTEGER NOT NULL,
                                    quantity INTEGER NOT NULL,
                                    refund_amount DECIMAL NOT NULL,
                                    reason TEXT,
                                    return_date DATETIME DEFAULT CURRENT_TIMESTAMP
                                  );";
                    new SqliteCommand(sql, conn).ExecuteNonQuery();
                }
            }
            catch { }
        }

        private void SearchOrder_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(tbSearchOrder.Text) || !int.TryParse(tbSearchOrder.Text, out int orderId))
            {
                MessageBox.Show(GetString("msgInvalidReceipt"), GetString("txtWarning"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            LoadOrderDetails(orderId);
        }

        private void LoadOrderDetails(int orderId)
        {
            OrderItems.Clear();
            string orderDate = "";
            try
            {
                using (var conn = new SqliteConnection(StoreState.ConnectionString))
                {
                    conn.Open();

                    string sql = @"SELECT oi.order_id, oi.stock_id, p.name, b.name, 
                                          CASE WHEN p.product_type = 'Взуття' THEN IFNULL(sz.value, '-') ELSE '-' END, 
                                          oi.quantity, oi.price_at_time_of_sale, o.order_date, IFNULL(p.article, '-'),
                                          IFNULL(o.status, 'Виконано')
                                   FROM order_items oi
                                   JOIN orders o ON oi.order_id = o.order_id
                                   JOIN stock st ON oi.stock_id = st.stock_id
                                   JOIN products p ON st.product_id = p.product_id
                                   JOIN brands b ON p.brand_id = b.brand_id
                                   LEFT JOIN sizes sz ON st.size_id = sz.size_id
                                   WHERE oi.order_id = @oid AND oi.quantity > 0";

                    using (var cmd = new SqliteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@oid", orderId);
                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                orderDate = r.GetString(7);
                                OrderItems.Add(new OrderReturnItem
                                {
                                    OrderId = r.GetInt32(0),
                                    StockId = r.GetInt32(1),
                                    ProductName = r.GetString(2),
                                    Brand = r.GetString(3),
                                    Size = r.GetString(4),
                                    Quantity = r.GetInt32(5),
                                    SalePrice = r.GetDecimal(6),
                                    OrderDate = orderDate,
                                    Article = r.GetString(8),
                                    OrderStatus = r.GetString(9)
                                });
                            }
                        }
                    }
                }
                if (OrderItems.Count > 0)
                {
                    txtOrderInfoLabel.Visibility = Visibility.Visible;
                    txtOrderDate.Visibility = Visibility.Visible;
                    txtOrderDate.Text = string.Format(GetString("txtFromDate2"), orderDate);
                }
                else
                {
                    MessageBox.Show(GetString("msgReceiptNotFound"), GetString("txtSearchResult"), MessageBoxButton.OK, MessageBoxImage.Information);
                    txtOrderInfoLabel.Visibility = Visibility.Collapsed;
                    txtOrderDate.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{GetString("msgSearchError")} {ex.Message}", GetString("txtError"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenReturnDialog_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is OrderReturnItem item)
            {
                SelectedReturnItem = item;
                cbReturnQuantity.Items.Clear();
                for (int i = 1; i <= item.Quantity; i++)
                {
                    cbReturnQuantity.Items.Add(i);
                }
                cbReturnQuantity.SelectedIndex = 0;
                ReturnDialogHost.IsOpen = true;
            }
        }

        private void CancelReturn_Click(object sender, RoutedEventArgs e)
        {
            ReturnDialogHost.IsOpen = false;
        }

        private void cbReturnQuantity_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbReturnQuantity.SelectedItem != null && SelectedReturnItem != null)
            {
                int qty = (int)cbReturnQuantity.SelectedItem;
                decimal totalRefund = qty * SelectedReturnItem.SalePrice;
                txtRefundTotal.Text = $"{totalRefund:N0} ₴";
            }
        }

        private void ConfirmReturn_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedReturnItem == null || cbReturnQuantity.SelectedItem == null || cbReturnReason.SelectedItem == null) return;

            if (!SelectedReturnItem.IsReturnable) return;

            int qtyToReturn = (int)cbReturnQuantity.SelectedItem;
            decimal refundAmount = qtyToReturn * SelectedReturnItem.SalePrice;
            var reasonItem = cbReturnReason.SelectedItem as ComboBoxItem;

            string reasonText = reasonItem.Tag != null ? reasonItem.Tag.ToString() : GetString("txtNotSpecified");
            bool isBonusRefunded = false;

            try
            {
                using (var conn = new SqliteConnection(StoreState.ConnectionString))
                {
                    conn.Open();
                    using (var transaction = conn.BeginTransaction())
                    {
                        var stockCmd = new SqliteCommand("UPDATE stock SET quantity = quantity + @q WHERE stock_id = @sid", conn, transaction);
                        stockCmd.Parameters.AddWithValue("@q", qtyToReturn);
                        stockCmd.Parameters.AddWithValue("@sid", SelectedReturnItem.StockId);
                        stockCmd.ExecuteNonQuery();

                        var updateOrderCmd = new SqliteCommand("UPDATE order_items SET quantity = quantity - @q WHERE order_id = @oid AND stock_id = @sid", conn, transaction);
                        updateOrderCmd.Parameters.AddWithValue("@q", qtyToReturn);
                        updateOrderCmd.Parameters.AddWithValue("@oid", SelectedReturnItem.OrderId);
                        updateOrderCmd.Parameters.AddWithValue("@sid", SelectedReturnItem.StockId);
                        updateOrderCmd.ExecuteNonQuery();

                        var auditCmd = new SqliteCommand("INSERT INTO returns (order_id, stock_id, quantity, refund_amount, reason) VALUES (@oid, @sid, @q, @amt, @r)", conn, transaction);
                        auditCmd.Parameters.AddWithValue("@oid", SelectedReturnItem.OrderId);
                        auditCmd.Parameters.AddWithValue("@sid", SelectedReturnItem.StockId);
                        auditCmd.Parameters.AddWithValue("@q", qtyToReturn);
                        auditCmd.Parameters.AddWithValue("@amt", refundAmount);
                        auditCmd.Parameters.AddWithValue("@r", reasonText);
                        auditCmd.ExecuteNonQuery();

                        var updateTotalCmd = new SqliteCommand("UPDATE orders SET total_amount = MAX(0, total_amount - @ref) WHERE order_id = @oid", conn, transaction);
                        updateTotalCmd.Parameters.AddWithValue("@ref", refundAmount);
                        updateTotalCmd.Parameters.AddWithValue("@oid", SelectedReturnItem.OrderId);
                        updateTotalCmd.ExecuteNonQuery();

                        var checkRemCmd = new SqliteCommand("SELECT SUM(quantity) FROM order_items WHERE order_id = @oid", conn, transaction);
                        checkRemCmd.Parameters.AddWithValue("@oid", SelectedReturnItem.OrderId);
                        var remRes = checkRemCmd.ExecuteScalar();
                        int remainingQty = remRes != DBNull.Value ? Convert.ToInt32(remRes) : 0;

                        if (remainingQty <= 0)
                        {
                            var updateOrderStatus = new SqliteCommand("UPDATE orders SET status = 'Скасовано' WHERE order_id = @oid", conn, transaction);
                            updateOrderStatus.Parameters.AddWithValue("@oid", SelectedReturnItem.OrderId);
                            updateOrderStatus.ExecuteNonQuery();
                        }

                        var checkOrderCmd = new SqliteCommand("SELECT user_id, payment_method, comment FROM orders WHERE order_id = @oid", conn, transaction);
                        checkOrderCmd.Parameters.AddWithValue("@oid", SelectedReturnItem.OrderId);
                        int uId = 0;
                        bool usedBonus = false;

                        using (var r = checkOrderCmd.ExecuteReader())
                        {
                            if (r.Read())
                            {
                                uId = r.IsDBNull(0) ? 0 : r.GetInt32(0);
                                string pm = r.IsDBNull(1) ? "" : r.GetString(1);
                                string comm = r.IsDBNull(2) ? "" : r.GetString(2);

                                if (pm.Contains("Використано бонус") || comm.Contains("BONUS_APPLIED"))
                                {
                                    usedBonus = true;
                                }
                            }
                        }

                        if (uId > 0)
                        {
                            var custCmd = new SqliteCommand("SELECT purchased_items_count, available_bonuses FROM customers WHERE user_id = @uid", conn, transaction);
                            custCmd.Parameters.AddWithValue("@uid", uId);

                            int oldItems = 0;
                            int oldBonuses = 0;

                            using (var reader = custCmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    oldItems = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                                    oldBonuses = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                                }
                            }

                            int newItems = Math.Max(0, oldItems - qtyToReturn);
                            int bonusesToDeduct = (oldItems / 10) - (newItems / 10);
                            int newBonuses = Math.Max(0, oldBonuses - bonusesToDeduct);

                            if (remainingQty <= 0 && usedBonus)
                            {
                                newBonuses += 1;
                                isBonusRefunded = true;
                            }

                            var updateCustCmd = new SqliteCommand("UPDATE customers SET purchased_items_count = @ni, available_bonuses = @nb WHERE user_id = @uid", conn, transaction);
                            updateCustCmd.Parameters.AddWithValue("@ni", newItems);
                            updateCustCmd.Parameters.AddWithValue("@nb", newBonuses);
                            updateCustCmd.Parameters.AddWithValue("@uid", uId);
                            updateCustCmd.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                }

                ReturnDialogHost.IsOpen = false;

                string successMessage = GetString("msgReturnSuccess") ?? "Повернення успішно оформлено.";
                if (isBonusRefunded)
                {
                    successMessage += "\n\nУВАГА: Оскільки всі товари з цього чеку було повернуто, використаний бонус (знижку -15%) було відновлено на клубній картці клієнта!";
                }

                MessageBox.Show(successMessage, GetString("txtSuccess") ?? "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadOrderDetails(SelectedReturnItem.OrderId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{GetString("msgReturnError")} {ex.Message}", GetString("txtError"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}