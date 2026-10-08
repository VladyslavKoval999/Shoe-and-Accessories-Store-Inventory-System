using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.Sqlite;
using BlacksmithStore.Data;

namespace BlacksmithStore.Views
{
    public partial class HomeView : UserControl
    {
        public HomeView()
        {
            InitializeComponent();
            SetupDashboard();
            LoadStatisticsData();
        }

        private string GetString(string key)
        {
            return Application.Current.TryFindResource(key) as string ?? key;
        }

        private string GetLocalizedRoleName(string dbRoleName)
        {
            if (string.IsNullOrEmpty(dbRoleName)) return "";
            string lower = dbRoleName.ToLower();
            if (lower.Contains("продавець") || lower.Contains("seller")) return GetString("txtRoleSeller");
            if (lower.Contains("менеджер") || lower.Contains("manager")) return GetString("txtRoleManager");
            if (lower.Contains("адмін") || lower.Contains("admin")) return GetString("txtRoleAdmin");
            return dbRoleName;
        }

        private void SetupDashboard()
        {
            if (StoreState.CurrentUser == null) return;

            int hour = DateTime.Now.Hour;
            string greeting = hour >= 5 && hour < 12 ? GetString("txtGoodMorning") :
                              hour >= 12 && hour < 18 ? GetString("txtGoodAfternoon") :
                              GetString("txtGoodEvening");

            string name = !string.IsNullOrEmpty(StoreState.CurrentUser.FullName)
                          ? StoreState.CurrentUser.FullName.Split(' ')[0]
                          : StoreState.CurrentUser.Username;

            txtWelcomeUser.Text = $"{greeting}, {name}!";

            string roleDisplay = GetLocalizedRoleName(StoreState.CurrentUser.Role);
            txtRoleDescription.Text = $"{GetString("txtYourWorkspace")} {roleDisplay}";

            string role = StoreState.CurrentUser.Role.ToLower();

            TileStaff.Visibility = Visibility.Collapsed;
            TileAdminReports.Visibility = Visibility.Collapsed;
            TileAddProduct.Visibility = Visibility.Collapsed;
            TileOnlineOrders.Visibility = Visibility.Collapsed;
            TileStock.Visibility = Visibility.Collapsed;

            if (role.Contains("admin") || role.Contains("адмін"))
            {
                TileStaff.Visibility = Visibility.Visible;
                TileAdminReports.Visibility = Visibility.Visible;
                TileAddProduct.Visibility = Visibility.Visible;
                TileOnlineOrders.Visibility = Visibility.Visible;
                TileStock.Visibility = Visibility.Visible;
            }
            else if (role.Contains("manager") || role.Contains("менеджер"))
            {
                TileAdminReports.Visibility = Visibility.Visible;
                TileAddProduct.Visibility = Visibility.Visible;
                TileOnlineOrders.Visibility = Visibility.Visible;
                TileStock.Visibility = Visibility.Visible;
            }
        }

        private void LoadStatisticsData()
        {
            try
            {
                using (var conn = new SqliteConnection(StoreState.ConnectionString))
                {
                    conn.Open();

                    string sqlSales = "SELECT SUM(total_amount) FROM Orders WHERE date(order_date) = date('now', 'localtime')";
                    using (var cmd = new SqliteCommand(sqlSales, conn))
                    {
                        var result = cmd.ExecuteScalar();
                        decimal todaySales = result != DBNull.Value ? Convert.ToDecimal(result) : 0m;
                        statSalesToday.Text = $"{todaySales:N0} ₴";
                    }

                    string sqlSalesCount = "SELECT COUNT(*) FROM Orders WHERE date(order_date) = date('now', 'localtime')";
                    using (var cmd = new SqliteCommand(sqlSalesCount, conn))
                    {
                        var result = cmd.ExecuteScalar();
                        int salesCount = result != DBNull.Value ? Convert.ToInt32(result) : 0;
                        statSalesCount.Text = salesCount.ToString();
                    }

                    string sqlCustomers = "SELECT COUNT(*) FROM Customers";
                    using (var cmd = new SqliteCommand(sqlCustomers, conn))
                    {
                        var result = cmd.ExecuteScalar();
                        int customersCount = result != DBNull.Value ? Convert.ToInt32(result) : 0;
                        statClubMembers.Text = customersCount.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Помилка завантаження статистики: " + ex.Message);
            }
        }

        private void Tile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                string tag = btn.Tag.ToString();

                var mainWindow = Window.GetWindow(this) as MainWindow;
                if (mainWindow != null)
                {
                    mainWindow.NavigateFromDashboard(tag);
                }
            }
        }
    }
}