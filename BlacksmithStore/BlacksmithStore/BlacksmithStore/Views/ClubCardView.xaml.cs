using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using BlacksmithStore.Data;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BlacksmithStore.Views
{
    public class ClubCustomer : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string CardNumber { get; set; }

        private string _login;
        public string Login { get => _login; set { _login = value; OnPropertyChanged(); } }

        private string _fullName;
        public string FullName { get => _fullName; set { _fullName = value; OnPropertyChanged(); } }

        private string _phone;
        public string Phone { get => _phone; set { _phone = value; OnPropertyChanged(); } }

        private string _email;
        public string Email { get => _email; set { _email = value; OnPropertyChanged(); } }

        private int _itemsCount;
        public int ItemsCount { get => _itemsCount; set { _itemsCount = value; OnPropertyChanged(); } }

        private int _bonuses;
        public int Bonuses { get => _bonuses; set { _bonuses = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class ClubCardView : UserControl
    {
        public ObservableCollection<ClubCustomer> CustomersList { get; set; } = new ObservableCollection<ClubCustomer>();
        private List<ClubCustomer> _allCustomers = new List<ClubCustomer>();
        private bool _isFormattingPhone = false;

        private int _activeCustomerId = 0;
        private int _activeBonuses = 0;

        private bool _isCardFlipped = false;

        public ClubCardView()
        {
            InitializeComponent();
            ClearPreview();
            dgCustomers.ItemsSource = CustomersList;
        }

        private string GetString(string key)
        {
            return Application.Current.TryFindResource(key) as string ?? key;
        }

        private string ComputeSha256Hash(string rawData)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        private void Card_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var animOut = new DoubleAnimation { To = 0, Duration = TimeSpan.FromMilliseconds(150) };
            animOut.Completed += (s, ev) =>
            {
                _isCardFlipped = !_isCardFlipped;

                CardFront.Visibility = _isCardFlipped ? Visibility.Collapsed : Visibility.Visible;
                CardBack.Visibility = _isCardFlipped ? Visibility.Visible : Visibility.Collapsed;

                var animIn = new DoubleAnimation { To = 1, Duration = TimeSpan.FromMilliseconds(150) };
                CardScaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, animIn);
            };
            CardScaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, animOut);
        }

        private void edtCustPhone_GotFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(edtCustPhone.Text) || edtCustPhone.Text == "+380")
            {
                edtCustPhone.Text = "+380 ";
                edtCustPhone.SelectionStart = edtCustPhone.Text.Length;
            }
        }

        private void edtCustPhone_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Back || e.Key == Key.Delete)
            {
                if (edtCustPhone.SelectionStart <= 5 && edtCustPhone.SelectionLength == 0)
                {
                    e.Handled = true;
                }
            }
        }

        private void PhoneInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isFormattingPhone) return;

            var tb = sender as TextBox;
            if (tb == null) return;

            string text = tb.Text;

            if (text.StartsWith("B", StringComparison.OrdinalIgnoreCase) || text.StartsWith("S", StringComparison.OrdinalIgnoreCase))
                return;

            _isFormattingPhone = true;

            string digits = new string(text.Where(char.IsDigit).ToArray());

            if (digits.StartsWith("380")) digits = digits.Substring(3);
            else if (digits.StartsWith("0")) digits = digits.Substring(1);

            string formatted = "+380";
            if (digits.Length > 0)
            {
                formatted += " " + digits.Substring(0, Math.Min(2, digits.Length));
                if (digits.Length > 2)
                    formatted += " " + digits.Substring(2, Math.Min(3, digits.Length - 2));
                if (digits.Length > 5)
                    formatted += " " + digits.Substring(5, Math.Min(2, digits.Length - 5));
                if (digits.Length > 7)
                    formatted += " " + digits.Substring(7, Math.Min(2, digits.Length - 7));
            }

            if (string.IsNullOrEmpty(digits) && !text.Contains("+380"))
                tb.Text = "";
            else
                tb.Text = formatted;

            tb.SelectionStart = tb.Text.Length;
            _isFormattingPhone = false;
        }

        private void Input_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtClientNameDisplay == null || txtAvatarInitials == null) return;

            string login = edtCustLogin.Text.Trim();

            if (string.IsNullOrWhiteSpace(login))
            {
                txtClientNameDisplay.Text = GetString("txtLoginCardHint");
                txtAvatarInitials.Text = "?";
            }
            else
            {
                txtClientNameDisplay.Text = login;
                txtAvatarInitials.Text = login.Substring(0, 1).ToUpper();
            }
        }

        private void UpdateQrCode(string cardNumber)
        {
            if (string.IsNullOrEmpty(cardNumber)) return;
            try
            {
                string qrUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=100x100&data={cardNumber}";
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(qrUrl, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                imgQrCodeBack.Source = bitmap;
            }
            catch { }
        }

        private void CreateCard_Click(object sender, RoutedEventArgs e)
        {
            string login = edtCustLogin.Text.Trim();
            string fullName = edtCustFullName.Text.Trim();
            string email = edtCustEmail.Text.Trim();
            string password = edtCustPassword.Password.Trim();
            string phone = new string(edtCustPhone.Text.Where(char.IsDigit).ToArray());

            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(phone) ||
                string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show(GetString("msgRequiredFieldsCard"), GetString("txtWarning"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string hashedPassword = ComputeSha256Hash(password);
            string cardNumber = GenerateCardNumber();

            SqliteTransaction transaction = null;
            try
            {
                using (var conn = new SqliteConnection(StoreState.ConnectionString))
                {
                    conn.Open();
                    transaction = conn.BeginTransaction();

                    using (var checkLoginCmd = new SqliteCommand("SELECT COUNT(*) FROM users WHERE username = @login", conn, transaction))
                    {
                        checkLoginCmd.Parameters.AddWithValue("@login", login);
                        if (Convert.ToInt32(checkLoginCmd.ExecuteScalar()) > 0)
                        {
                            MessageBox.Show(GetString("msgLoginExists"), GetString("txtError"), MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }

                    using (var checkPhoneCmd = new SqliteCommand("SELECT COUNT(*) FROM customers WHERE phone_number = @phone", conn, transaction))
                    {
                        checkPhoneCmd.Parameters.AddWithValue("@phone", phone);
                        if (Convert.ToInt32(checkPhoneCmd.ExecuteScalar()) > 0)
                        {
                            MessageBox.Show(GetString("msgPhoneExists"), GetString("txtError"), MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }

                    string sqlUser = "INSERT INTO users (role_id, full_name, username, password_hash) VALUES (4, @fname, @login, @pass)";
                    long newUserId = -1;
                    using (var cmdUser = new SqliteCommand(sqlUser, conn, transaction))
                    {
                        cmdUser.Parameters.AddWithValue("@fname", fullName);
                        cmdUser.Parameters.AddWithValue("@login", login);
                        cmdUser.Parameters.AddWithValue("@pass", hashedPassword);
                        cmdUser.ExecuteNonQuery();

                        using (var lastRowIdCmd = new SqliteCommand("SELECT last_insert_rowid()", conn, transaction))
                        {
                            newUserId = (long)lastRowIdCmd.ExecuteScalar();
                        }
                    }

                    string sqlCust = @"
                        INSERT INTO customers (loyalty_card_number, phone_number, email, user_id, purchased_items_count, available_bonuses) 
                        VALUES (@card, @phone, @email, @userId, 0, 0)";
                    using (var cmdCust = new SqliteCommand(sqlCust, conn, transaction))
                    {
                        cmdCust.Parameters.AddWithValue("@card", cardNumber);
                        cmdCust.Parameters.AddWithValue("@phone", phone);
                        cmdCust.Parameters.AddWithValue("@email", email);
                        cmdCust.Parameters.AddWithValue("@userId", newUserId);
                        cmdCust.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }

                lblCardNumber.Text = cardNumber;
                lblCardNumberBack.Text = cardNumber;
                lblCardFullName.Text = fullName.ToUpper();
                UpdateStatsUI(0, 0);
                UpdateQrCode(cardNumber);

                MessageBox.Show(string.Format(GetString("msgCardActivated"), cardNumber), GetString("txtSuccess"), MessageBoxButton.OK, MessageBoxImage.Information);

                edtCustLogin.Clear();
                edtCustFullName.Clear();
                edtCustPhone.Clear();
                edtCustEmail.Clear();
                edtCustPassword.Clear();

                LoadCustomers();
            }
            catch (Exception ex)
            {
                transaction?.Rollback();
                MessageBox.Show($"{GetString("msgDbError")} {ex.Message}", GetString("txtError"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void edtSearchClient_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = edtSearchClient.Text.Trim();

            if (query.Length >= 12 && query.StartsWith("BS-", StringComparison.OrdinalIgnoreCase))
            {
                PerformSearch(query);
            }
        }

        private void SearchCustomer_Click(object sender, RoutedEventArgs e)
        {
            string query = edtSearchClient.Text.Trim();
            if (string.IsNullOrEmpty(query))
            {
                MessageBox.Show(GetString("msgEnterDataOrScan"), GetString("txtWarning"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            PerformSearch(query);
        }

        private void PerformSearch(string query)
        {
            string q = query.ToLower();
            string cleanQueryPhone = new string(query.Where(char.IsDigit).ToArray());

            try
            {
                using (var conn = new SqliteConnection(StoreState.ConnectionString))
                {
                    conn.Open();

                    string sql = @"
                        SELECT c.loyalty_card_number, c.phone_number, c.purchased_items_count, c.available_bonuses, u.username, c.customer_id, u.full_name, c.email 
                        FROM customers c
                        LEFT JOIN users u ON c.user_id = u.user_id";

                    using (var cmd = new SqliteCommand(sql, conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        bool found = false;
                        while (r.Read())
                        {
                            string cardNum = r.IsDBNull(0) ? "BS-XXXX-XXXX" : r.GetString(0);
                            string phone = r.IsDBNull(1) ? "" : r.GetString(1);
                            string email = r.IsDBNull(7) ? "" : r.GetString(7);
                            string login = r.IsDBNull(4) ? GetString("txtNoLogin") : r.GetString(4);
                            string fullName = r.IsDBNull(6) ? "" : r.GetString(6);

                            string cleanCustPhone = new string(phone.Where(char.IsDigit).ToArray());

                            if (cardNum.ToLower().Contains(q) ||
                                login.ToLower().Contains(q) ||
                                fullName.ToLower().Contains(q) ||
                                email.ToLower().Contains(q) ||
                                (!string.IsNullOrEmpty(cleanQueryPhone) && cleanCustPhone.Contains(cleanQueryPhone)))
                            {
                                int purchasedCount = r.IsDBNull(2) ? 0 : r.GetInt32(2);
                                int bonuses = r.IsDBNull(3) ? 0 : r.GetInt32(3);
                                _activeCustomerId = r.IsDBNull(5) ? 0 : r.GetInt32(5);
                                _activeBonuses = bonuses;

                                lblCardNumber.Text = cardNum;
                                lblCardNumberBack.Text = cardNum;
                                txtClientNameDisplay.Text = login;
                                txtAvatarInitials.Text = login != GetString("txtNoLogin") ? login.Substring(0, 1).ToUpper() : "?";
                                lblCardFullName.Text = string.IsNullOrWhiteSpace(fullName) ? GetString("txtDefaultClient") : fullName.ToUpper();

                                UpdateStatsUI(purchasedCount, bonuses);
                                UpdateQrCode(cardNum);

                                pnlActionBonus.Visibility = Visibility.Visible;
                                found = true;
                                break;
                            }
                        }

                        if (!found)
                        {
                            if (!query.StartsWith("BS-", StringComparison.OrdinalIgnoreCase))
                            {
                                MessageBox.Show(GetString("msgClientNotFound"), GetString("txtSearchResult"), MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                            ClearPreview();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{GetString("msgSearchError")}{ex.Message}");
            }
        }

        private void ConnectBonusToSale_Click(object sender, RoutedEventArgs e)
        {
            if (_activeCustomerId <= 0) return;

            string cardNum = lblCardNumber.Text;

            SalesView.PerformGlobalCustomerSearch(cardNum, false);

            var mainWindow = Window.GetWindow(this) as MainWindow;
            if (mainWindow != null)
            {
                mainWindow.NavigateFromDashboard("Sales");
            }
        }

        private void UpdateStatsUI(int purchased, int bonuses)
        {
            lblItemsCount.Text = purchased.ToString();
            lblBonuses.Text = bonuses.ToString();

            int currentProgress = purchased % 10;
            int itemsLeft = 10 - currentProgress;

            pbBonusProgress.Value = currentProgress;
            lblProgressText.Text = $"{currentProgress * 10}%";
            lblItemsLeft.Text = $"{GetString("txtItemsLeftToBuy")} {itemsLeft}";
        }

        private void ClearPreview()
        {
            lblCardNumber.Text = "BS-XXXX-XXXX";
            lblCardNumberBack.Text = "BS-XXXX-XXXX";

            if (Application.Current != null)
            {
                lblCardFullName.Text = GetString("txtDefaultClient");
                txtClientNameDisplay.Text = GetString("txtLoginCardHint");
            }
            else
            {
                lblCardFullName.Text = "КЛІЄНТ";
                txtClientNameDisplay.Text = "Логін в клубній карті";
            }

            txtAvatarInitials.Text = "?";
            _activeCustomerId = 0;
            _activeBonuses = 0;
            pnlActionBonus.Visibility = Visibility.Collapsed;
            imgQrCodeBack.Source = null;
            UpdateStatsUI(0, 0);
        }

        private string GenerateCardNumber()
        {
            Random rnd = new Random();
            return $"BS-{rnd.Next(1000, 9999)}-{rnd.Next(1000, 9999)}";
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl tc && tc.SelectedIndex == 2)
            {
                LoadCustomers();
            }
        }

        private void LoadCustomers()
        {
            _allCustomers.Clear();
            try
            {
                using (var conn = new SqliteConnection(StoreState.ConnectionString))
                {
                    conn.Open();
                    string sql = @"
                        SELECT c.customer_id, c.loyalty_card_number, c.phone_number, c.email, 
                               c.purchased_items_count, c.available_bonuses, u.username, c.user_id, u.full_name 
                        FROM customers c
                        LEFT JOIN users u ON c.user_id = u.user_id";

                    using (var cmd = new SqliteCommand(sql, conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            string rawPhone = r.IsDBNull(2) ? "" : r.GetString(2);
                            string formattedPhone = FormatPhoneStringForGrid(rawPhone);

                            _allCustomers.Add(new ClubCustomer
                            {
                                Id = r.GetInt32(0),
                                CardNumber = r.IsDBNull(1) ? "" : r.GetString(1),
                                Phone = formattedPhone,
                                Email = r.IsDBNull(3) ? "" : r.GetString(3),
                                ItemsCount = r.IsDBNull(4) ? 0 : r.GetInt32(4),
                                Bonuses = r.IsDBNull(5) ? 0 : r.GetInt32(5),
                                Login = r.IsDBNull(6) ? "" : r.GetString(6),
                                UserId = r.IsDBNull(7) ? 0 : r.GetInt32(7),
                                FullName = r.IsDBNull(8) ? "" : r.GetString(8)
                            });
                        }
                    }
                }
            }
            catch { }

            FilterDatabase(edtSearchDatabase.Text);
        }

        private void SearchDatabase_TextChanged(object sender, TextChangedEventArgs e)
        {
            string text = edtSearchDatabase.Text;

            if (!_isFormattingPhone && text.Length > 0 && text.All(c => char.IsDigit(c) || c == '+' || c == ' '))
            {
                _isFormattingPhone = true;
                string digits = new string(text.Where(char.IsDigit).ToArray());

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

                if (!string.IsNullOrEmpty(digits) || text.Contains("+380"))
                {
                    edtSearchDatabase.Text = formatted;
                    edtSearchDatabase.SelectionStart = edtSearchDatabase.Text.Length;
                }
                _isFormattingPhone = false;
            }

            FilterDatabase(edtSearchDatabase.Text);
        }

        private void FilterDatabase(string query)
        {
            CustomersList.Clear();

            if (string.IsNullOrWhiteSpace(query))
            {
                foreach (var c in _allCustomers) CustomersList.Add(c);
            }
            else
            {
                string q = query.ToLower();
                string cleanQueryPhone = new string(query.Where(char.IsDigit).ToArray());

                foreach (var c in _allCustomers)
                {
                    string cleanCustPhone = new string((c.Phone ?? "").Where(char.IsDigit).ToArray());

                    if ((c.Login != null && c.Login.IndexOf(query, StringComparison.InvariantCultureIgnoreCase) >= 0) ||
                        (c.FullName != null && c.FullName.IndexOf(query, StringComparison.InvariantCultureIgnoreCase) >= 0) ||
                        (c.Email != null && c.Email.IndexOf(query, StringComparison.InvariantCultureIgnoreCase) >= 0) ||
                        (c.CardNumber != null && c.CardNumber.IndexOf(query, StringComparison.InvariantCultureIgnoreCase) >= 0) ||
                        (!string.IsNullOrEmpty(cleanQueryPhone) && cleanCustPhone.Contains(cleanQueryPhone)))
                    {
                        CustomersList.Add(c);
                    }
                }
            }
        }

        private string FormatPhoneStringForGrid(string raw)
        {
            string digits = new string(raw.Where(char.IsDigit).ToArray());
            if (digits.StartsWith("380")) digits = digits.Substring(3);

            string formatted = "+380";
            if (digits.Length >= 2) formatted += " " + digits.Substring(0, 2);
            if (digits.Length >= 5) formatted += " " + digits.Substring(2, 3);
            if (digits.Length >= 7) formatted += " " + digits.Substring(5, 2);
            if (digits.Length >= 9) formatted += " " + digits.Substring(7, 2);

            return formatted == "+380" ? raw : formatted;
        }

        private void ToggleEditMode_Changed(object sender, RoutedEventArgs e)
        {
            bool isEditing = ToggleEditMode.IsChecked == true;
            dgCustomers.IsReadOnly = !isEditing;

            BtnSaveChanges.Visibility = isEditing ? Visibility.Visible : Visibility.Collapsed;
            BtnDeleteCustomer.Visibility = isEditing ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SaveChanges_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var conn = new SqliteConnection(StoreState.ConnectionString))
                {
                    conn.Open();
                    using (var transaction = conn.BeginTransaction())
                    {
                        foreach (var cust in CustomersList)
                        {
                            if (string.IsNullOrWhiteSpace(cust.Email))
                            {
                                MessageBox.Show(string.Format(GetString("msgEmailRequired"), cust.CardNumber), GetString("txtWarning"), MessageBoxButton.OK, MessageBoxImage.Warning);
                                return;
                            }
                            if (string.IsNullOrWhiteSpace(cust.Login))
                            {
                                MessageBox.Show(string.Format(GetString("msgLoginRequired"), cust.CardNumber), GetString("txtWarning"), MessageBoxButton.OK, MessageBoxImage.Warning);
                                return;
                            }
                        }

                        string sqlCust = "UPDATE customers SET phone_number = @phone, email = @email, purchased_items_count = @items, available_bonuses = @bonuses WHERE customer_id = @id";
                        string sqlUser = "UPDATE users SET full_name = @fname, username = @login WHERE user_id = @uid";

                        using (var cmdCust = new SqliteCommand(sqlCust, conn, transaction))
                        using (var cmdUser = new SqliteCommand(sqlUser, conn, transaction))
                        {
                            foreach (var cust in CustomersList)
                            {
                                string cleanPhone = new string((cust.Phone ?? "").Where(char.IsDigit).ToArray());

                                cmdCust.Parameters.Clear();
                                cmdCust.Parameters.AddWithValue("@phone", cleanPhone);
                                cmdCust.Parameters.AddWithValue("@email", cust.Email ?? "");
                                cmdCust.Parameters.AddWithValue("@items", cust.ItemsCount);
                                cmdCust.Parameters.AddWithValue("@bonuses", cust.Bonuses);
                                cmdCust.Parameters.AddWithValue("@id", cust.Id);
                                cmdCust.ExecuteNonQuery();

                                if (cust.UserId > 0)
                                {
                                    cmdUser.Parameters.Clear();
                                    cmdUser.Parameters.AddWithValue("@fname", cust.FullName ?? "");
                                    cmdUser.Parameters.AddWithValue("@login", cust.Login ?? "");
                                    cmdUser.Parameters.AddWithValue("@uid", cust.UserId);
                                    cmdUser.ExecuteNonQuery();
                                }
                            }
                        }
                        transaction.Commit();
                    }
                }

                ToggleEditMode.IsChecked = false;
                LoadCustomers();
                MessageBox.Show(GetString("msgClientChangesSaved"), GetString("txtSuccess"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("UNIQUE") || ex.Message.Contains("constraint"))
                {
                    MessageBox.Show(GetString("msgLoginTakenCard"), GetString("txtError"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    MessageBox.Show($"{GetString("msgSaveDbError")}{ex.Message}", GetString("txtError"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void DeleteCustomer_Click(object sender, RoutedEventArgs e)
        {
            if (dgCustomers.SelectedItem is ClubCustomer selectedCust)
            {
                var result = MessageBox.Show(
                    string.Format(GetString("msgConfirmDeleteClient"), selectedCust.Login ?? selectedCust.CardNumber),
                    GetString("txtConfirmDelete"), MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        using (var conn = new SqliteConnection(StoreState.ConnectionString))
                        {
                            conn.Open();
                            using (var tx = conn.BeginTransaction())
                            {
                                using (var cmd = new SqliteCommand("DELETE FROM customers WHERE customer_id = @id", conn, tx))
                                {
                                    cmd.Parameters.AddWithValue("@id", selectedCust.Id);
                                    cmd.ExecuteNonQuery();
                                }

                                if (selectedCust.UserId > 0)
                                {
                                    using (var cmdUser = new SqliteCommand("DELETE FROM users WHERE user_id = @uid", conn, tx))
                                    {
                                        cmdUser.Parameters.AddWithValue("@uid", selectedCust.UserId);
                                        cmdUser.ExecuteNonQuery();
                                    }
                                }

                                tx.Commit();
                            }
                        }

                        MessageBox.Show(GetString("msgClientDeleted"), GetString("txtSuccess"), MessageBoxButton.OK, MessageBoxImage.Information);
                        LoadCustomers();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"{GetString("msgDeleteError")}{ex.Message}", GetString("txtError"), MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show(GetString("msgSelectClientToDelete"), GetString("txtWarning"), MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}