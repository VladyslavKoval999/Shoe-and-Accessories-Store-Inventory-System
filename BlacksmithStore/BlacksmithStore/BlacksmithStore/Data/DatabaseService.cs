using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Microsoft.Data.Sqlite;
using BlacksmithStore.Models;

namespace BlacksmithStore.Data
{
    public static class DatabaseService
    {
        private static string GetString(string key)
        {
            var res = Application.Current?.TryFindResource(key);
            return res as string ?? key;
        }

        public static void LoadProducts()
        {
            StoreState.AllProducts.Clear();
            if (!System.IO.File.Exists(StoreState.DbPath)) return;

            try
            {
                using (var conn = new SqliteConnection(StoreState.ConnectionString))
                {
                    conn.Open();
                    string sql = @"
                        SELECT p.product_id, p.article, p.name, p.base_price, p.images, p.season, p.product_type,
                               c.name, b.name, sub.name, TOTAL(st.quantity),
                               GROUP_CONCAT(DISTINCT CASE WHEN st.quantity > 0 THEN s.value ELSE NULL END), p.description,
                               GROUP_CONCAT(DISTINCT CASE WHEN st.quantity > 0 THEN col.name ELSE NULL END)
                        FROM Products p
                        JOIN Categories c ON p.category_id = c.category_id
                        JOIN Brands b ON p.brand_id = b.brand_id
                        LEFT JOIN Product_Subtypes sub ON p.subtype_id = sub.subtype_id
                        LEFT JOIN Stock st ON p.product_id = st.product_id
                        LEFT JOIN Sizes s ON st.size_id = s.size_id
                        LEFT JOIN Colors col ON st.color_id = col.color_id
                        GROUP BY p.product_id";

                    using (var cmd = new SqliteCommand(sql, conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            var prod = new Product
                            {
                                Id = r.GetInt32(0),
                                Article = r.IsDBNull(1) ? "" : r.GetString(1),
                                Name = r.GetString(2),
                                Price = r.GetDecimal(3),
                                ImageName = r.IsDBNull(4) ? "" : r.GetString(4),
                                Season = r.IsDBNull(5) ? "Усі" : r.GetString(5),
                                ProductType = r.GetString(6),
                                Category = r.GetString(7),
                                Brand = r.GetString(8),
                                Subtype = r.IsDBNull(9) ? "Інше" : r.GetString(9),
                                TotalQuantity = (int)r.GetDouble(10),
                                Description = r.IsDBNull(12) ? "" : r.GetString(12)
                            };

                            if (!r.IsDBNull(11))
                            {
                                var rawSizes = r.GetString(11).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                                prod.AvailableSizes = rawSizes.Select(s => FormatSize(s.Trim())).Where(s => !string.IsNullOrEmpty(s)).ToList();
                            }

                            if (!r.IsDBNull(13))
                            {
                                var rawColors = r.GetString(13).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                                prod.AvailableColors = rawColors.Select(c => c.Trim()).Where(c => !string.IsNullOrEmpty(c)).ToList();
                            }

                            StoreState.AllProducts.Add(prod);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{GetString("msgLoadProductsError")}{ex.Message}");
            }
        }

        private static string FormatSize(string rawSize)
        {
            if (string.IsNullOrWhiteSpace(rawSize)) return "";
            if (double.TryParse(rawSize.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
            {
                return val.ToString();
            }
            return rawSize;
        }

        public static ObservableCollection<WebOrder> GetWebOrdersFromDb()
        {
            var orders = new ObservableCollection<WebOrder>();
            if (!System.IO.File.Exists(StoreState.DbPath)) return orders;

            try
            {
                using (var conn = new SqliteConnection(StoreState.ConnectionString))
                {
                    conn.Open();

                    try { new SqliteCommand("ALTER TABLE Orders ADD COLUMN status TEXT DEFAULT 'Очікує збору'", conn).ExecuteNonQuery(); } catch { }
                    try { new SqliteCommand("ALTER TABLE Orders ADD COLUMN order_source TEXT DEFAULT 'Магазин'", conn).ExecuteNonQuery(); } catch { }

                    string sql = @"
                        SELECT o.order_id, o.order_date, o.total_amount,
                               IFNULL(u.full_name, 'Гість'), IFNULL(c.phone_number, '-'),
                               'м. Харків, Кур''єрська доставка' as address,
                               IFNULL(o.status, 'Очікує збору') as status
                        FROM Orders o
                        LEFT JOIN Users u ON o.user_id = u.user_id
                        LEFT JOIN Customers c ON u.user_id = c.user_id
                        WHERE o.order_source = 'Веб-сайт'
                        ORDER BY o.order_id DESC";

                    using (var cmd = new SqliteCommand(sql, conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            var order = new WebOrder
                            {
                                Id = r.GetInt32(0),
                                OrderDate = r.GetString(1),
                                TotalAmount = r.GetDecimal(2),
                                CustomerName = r.GetString(3),
                                CustomerPhone = r.GetString(4),
                                DeliveryAddress = r.GetString(5),
                                Status = r.GetString(6),
                                Items = GetOrderItemsFromDb(r.GetInt32(0), conn)
                            };
                            orders.Add(order);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{GetString("msgLoadOrdersError")}{ex.Message}");
            }

            return orders;
        }

        private static ObservableCollection<WebOrderItem> GetOrderItemsFromDb(int orderId, SqliteConnection conn)
        {
            var items = new ObservableCollection<WebOrderItem>();
            string sql = @"
                SELECT p.name, IFNULL(b.name, 'Без бренду'), IFNULL(s.value, '-'), oi.quantity, oi.price_at_time_of_sale, IFNULL(p.article, '-')
                FROM Order_Items oi
                JOIN Stock st ON oi.stock_id = st.stock_id
                JOIN Products p ON st.product_id = p.product_id
                LEFT JOIN Brands b ON p.brand_id = b.brand_id
                LEFT JOIN Sizes s ON st.size_id = s.size_id
                WHERE oi.order_id = @oid";

            using (var cmd = new SqliteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@oid", orderId);
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        items.Add(new WebOrderItem
                        {
                            ProductName = r.GetString(0),
                            Brand = r.GetString(1),
                            Size = FormatSize(r.GetString(2)),
                            Quantity = r.GetInt32(3),
                            Price = r.GetDecimal(4),
                            Article = r.GetString(5)
                        });
                    }
                }
            }
            return items;
        }

        public static void UpdateOrderStatus(int orderId, string newStatus)
        {
            try
            {
                using (var conn = new SqliteConnection(StoreState.ConnectionString))
                {
                    conn.Open();
                    string sql = "UPDATE Orders SET status = @status WHERE order_id = @id";
                    using (var cmd = new SqliteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@status", newStatus);
                        cmd.Parameters.AddWithValue("@id", orderId);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch { }
        }

        public static bool ProcessWebOrder(int orderId, string newStatus)
        {
            try
            {
                using (var conn = new SqliteConnection(StoreState.ConnectionString))
                {
                    conn.Open();
                    using (var transaction = conn.BeginTransaction())
                    {
                        string checkSql = "SELECT status, IFNULL(user_id, 0) FROM Orders WHERE order_id = @id";
                        string currentStatus = "";
                        int userId = 0;

                        using (var cmdCheck = new SqliteCommand(checkSql, conn, transaction))
                        {
                            cmdCheck.Parameters.AddWithValue("@id", orderId);
                            using (var reader = cmdCheck.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    currentStatus = reader.GetString(0);
                                    userId = reader.GetInt32(1);
                                }
                            }
                        }

                        string updateSql = "UPDATE Orders SET status = @status WHERE order_id = @id";
                        using (var cmdUpdate = new SqliteCommand(updateSql, conn, transaction))
                        {
                            cmdUpdate.Parameters.AddWithValue("@status", newStatus);
                            cmdUpdate.Parameters.AddWithValue("@id", orderId);
                            cmdUpdate.ExecuteNonQuery();
                        }

                        var itemsCountCmd = new SqliteCommand("SELECT SUM(quantity) FROM Order_Items WHERE order_id = @id", conn, transaction);
                        itemsCountCmd.Parameters.AddWithValue("@id", orderId);
                        var countRes = itemsCountCmd.ExecuteScalar();
                        int totalItemsBought = countRes != DBNull.Value ? Convert.ToInt32(countRes) : 0;

                        if (newStatus == "Доставлено" && currentStatus != "Доставлено")
                        {
                            if (userId > 0 && totalItemsBought > 0)
                            {
                                var custCmd = new SqliteCommand("SELECT purchased_items_count FROM Customers WHERE user_id = @uid", conn, transaction);
                                custCmd.Parameters.AddWithValue("@uid", userId);
                                var custRes = custCmd.ExecuteScalar();

                                if (custRes != null && custRes != DBNull.Value)
                                {
                                    int oldTotal = Convert.ToInt32(custRes);
                                    int newTotal = oldTotal + totalItemsBought;
                                    int newBonusesEarned = (newTotal / 10) - (oldTotal / 10);

                                    var updateCustCmd = new SqliteCommand(@"
                                        UPDATE Customers 
                                        SET purchased_items_count = @newTotal, 
                                            available_bonuses = available_bonuses + @newBonuses 
                                        WHERE user_id = @uid", conn, transaction);

                                    updateCustCmd.Parameters.AddWithValue("@newTotal", newTotal);
                                    updateCustCmd.Parameters.AddWithValue("@newBonuses", newBonusesEarned);
                                    updateCustCmd.Parameters.AddWithValue("@uid", userId);
                                    updateCustCmd.ExecuteNonQuery();
                                }
                            }
                        }
                        else if (newStatus == "Скасовано" && currentStatus != "Скасовано")
                        {
                            var restoreCmd = new SqliteCommand(@"
                                UPDATE Stock 
                                SET quantity = quantity + (
                                    SELECT quantity FROM Order_Items 
                                    WHERE Order_Items.stock_id = Stock.stock_id AND Order_Items.order_id = @id
                                ) 
                                WHERE stock_id IN (SELECT stock_id FROM Order_Items WHERE order_id = @id)", conn, transaction);

                            restoreCmd.Parameters.AddWithValue("@id", orderId);
                            restoreCmd.ExecuteNonQuery();

                            if (currentStatus == "Доставлено" && userId > 0 && totalItemsBought > 0)
                            {
                                var custCmd = new SqliteCommand("SELECT purchased_items_count, available_bonuses FROM Customers WHERE user_id = @uid", conn, transaction);
                                custCmd.Parameters.AddWithValue("@uid", userId);

                                using (var reader = custCmd.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        int oldTotal = reader.GetInt32(0);
                                        int currentBonuses = reader.GetInt32(1);

                                        int newTotal = Math.Max(0, oldTotal - totalItemsBought);
                                        int bonusesToRevert = (oldTotal / 10) - (newTotal / 10);
                                        int newBonuses = Math.Max(0, currentBonuses - bonusesToRevert);

                                        var revertCustCmd = new SqliteCommand(@"
                                            UPDATE Customers 
                                            SET purchased_items_count = @newTotal, 
                                                available_bonuses = @newBonuses 
                                            WHERE user_id = @uid", conn, transaction);

                                        revertCustCmd.Parameters.AddWithValue("@newTotal", newTotal);
                                        revertCustCmd.Parameters.AddWithValue("@newBonuses", newBonuses);
                                        revertCustCmd.Parameters.AddWithValue("@uid", userId);
                                        revertCustCmd.ExecuteNonQuery();
                                    }
                                }
                            }
                        }

                        transaction.Commit();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{GetString("msgProcessOrderError")}{ex.Message}", GetString("txtError"), MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public static int GetNewWebOrdersCount()
        {
            if (!System.IO.File.Exists(StoreState.DbPath)) return 0;

            try
            {
                using (var conn = new SqliteConnection(StoreState.ConnectionString))
                {
                    conn.Open();
                    string sql = "SELECT COUNT(*) FROM Orders WHERE order_source = 'Веб-сайт' AND status = 'Очікує збору'";
                    using (var cmd = new SqliteCommand(sql, conn))
                    {
                        var res = cmd.ExecuteScalar();
                        return res != DBNull.Value ? Convert.ToInt32(res) : 0;
                    }
                }
            }
            catch { return 0; }
        }
    }
}