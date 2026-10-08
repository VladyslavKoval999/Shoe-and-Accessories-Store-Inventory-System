using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using System.Net.Mail;
using BlacksmithStore.Data;
using BlacksmithStore.Models;

namespace BlacksmithStore.Views
{
    public class EditorSizeItem : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string Value { get; set; }
        private string _quantity = "";
        public string Quantity
        {
            get => _quantity;
            set
            {
                if (string.IsNullOrEmpty(value) || int.TryParse(value, out _))
                {
                    _quantity = value;
                    OnPropertyChanged();
                }
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class ProductEditorView : UserControl
    {
        private int _editingId = -1;
        private string _selectedFullImagePath = "";
        private Product _product;
        private UserControl _previousView;

        private List<EditorSizeItem> _allDbSizes = new List<EditorSizeItem>();
        public ObservableCollection<EditorSizeItem> AllSizesOptions { get; set; } = new ObservableCollection<EditorSizeItem>();
        private Dictionary<string, int> categoriesDict = new Dictionary<string, int>();
        private Dictionary<string, int> brandsDict = new Dictionary<string, int>();
        private Dictionary<string, int> subtypesDict = new Dictionary<string, int>();
        private Dictionary<string, int> colorsDict = new Dictionary<string, int>();

        public ProductEditorView(UserControl previousView = null)
        {
            InitializeComponent();
            _product = new Product();
            _previousView = previousView;
            this.DataContext = this;
            LoadEditorDictionaries();
            ResetEditorFields();
        }

        public ProductEditorView(Product p, UserControl previousView = null)
        {
            InitializeComponent();
            _product = p;
            _previousView = previousView;
            this.DataContext = this;
            LoadEditorDictionaries();
            LoadProductData(p);
        }

        private string GetString(string key)
        {
            return Application.Current.TryFindResource(key) as string ?? key;
        }

        private void CheckExistingProduct(object sender, RoutedEventArgs e)
        {
            string name = edtName.Text.Trim();
            string art = edtArticle.Text.Trim();

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(art)) return;

            try
            {
                using (var conn = new SqliteConnection(StoreState.ConnectionString))
                {
                    conn.Open();
                    string sql = @"SELECT p.product_id, p.article, p.name, p.description, p.base_price, 
                                          p.product_type, p.season, p.images,
                                          c.name as category_name, b.name as brand_name, ps.name as subtype_name
                                   FROM products p
                                   LEFT JOIN categories c ON p.category_id = c.category_id
                                   LEFT JOIN brands b ON p.brand_id = b.brand_id
                                   LEFT JOIN product_subtypes ps ON p.subtype_id = ps.subtype_id
                                   WHERE p.article = @art AND p.name = @name COLLATE NOCASE LIMIT 1";

                    using (var cmd = new SqliteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@art", art);
                        cmd.Parameters.AddWithValue("@name", name);

                        using (var r = cmd.ExecuteReader())
                        {
                            if (r.Read())
                            {
                                int id = r.GetInt32(0);

                                if (id == _editingId) return;

                                var p = new Product
                                {
                                    Id = id,
                                    Article = r.IsDBNull(1) ? "" : r.GetString(1),
                                    Name = r.IsDBNull(2) ? "" : r.GetString(2),
                                    Description = r.IsDBNull(3) ? "" : r.GetString(3),
                                    Price = r.IsDBNull(4) ? 0 : r.GetDecimal(4),
                                    ProductType = r.IsDBNull(5) ? "" : r.GetString(5),
                                    Season = r.IsDBNull(6) ? "" : r.GetString(6),
                                    ImageName = r.IsDBNull(7) ? "" : r.GetString(7),
                                    Category = r.IsDBNull(8) ? "" : r.GetString(8),
                                    Brand = r.IsDBNull(9) ? "" : r.GetString(9),
                                    Subtype = r.IsDBNull(10) ? "" : r.GetString(10)
                                };

                                LoadProductData(p);

                                string successMsg = Application.Current.TryFindResource("msgProductAutoLoaded") as string
                                                    ?? "Товар з такою назвою та артикулом вже існує в базі.\nУсі дані (включно із залишками) автоматично завантажено для швидкого поповнення!";
                                MessageBox.Show(successMsg, GetString("txtSuccess"), MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private void LoadEditorDictionaries()
        {
            categoriesDict.Clear(); brandsDict.Clear(); subtypesDict.Clear(); colorsDict.Clear();
            _allDbSizes.Clear(); AllSizesOptions.Clear();

            if (!File.Exists(StoreState.DbPath)) return;

            try
            {
                using (var conn = new SqliteConnection(StoreState.ConnectionString))
                {
                    conn.Open();
                    var cmd = new SqliteCommand("SELECT category_id, name FROM categories", conn);
                    using (var r = cmd.ExecuteReader()) while (r.Read()) categoriesDict.Add(r.GetString(1), r.GetInt32(0));

                    cmd.CommandText = "SELECT brand_id, name FROM brands";
                    using (var r = cmd.ExecuteReader()) while (r.Read()) brandsDict.Add(r.GetString(1), r.GetInt32(0));

                    cmd.CommandText = "SELECT subtype_id, name FROM product_subtypes";
                    using (var r = cmd.ExecuteReader()) while (r.Read()) subtypesDict.Add(r.GetString(1), r.GetInt32(0));

                    cmd.CommandText = "SELECT color_id, name FROM colors";
                    using (var r = cmd.ExecuteReader()) while (r.Read()) colorsDict.Add(r.GetString(1), r.GetInt32(0));

                    cmd.CommandText = "SELECT size_id, value FROM sizes ORDER BY CAST(value AS REAL)";
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            _allDbSizes.Add(new EditorSizeItem { Id = r.GetInt32(0), Value = r.GetString(1), Quantity = "" });
                        }
                    }
                }

                edtCategory.ItemsSource = categoriesDict.Keys.ToList();
                edtBrand.ItemsSource = brandsDict.Keys.ToList();
                edtSubtype.ItemsSource = subtypesDict.Keys.ToList();
                edtColor.ItemsSource = colorsDict.Keys.ToList();
                icSizesEditor.ItemsSource = AllSizesOptions;

                FilterSizesByCategory();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{GetString("msgDictLoadError")} {ex.Message}");
            }
        }

        private void edtCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() => FilterSizesByCategory()), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void edtCategory_KeyUp(object sender, KeyEventArgs e)
        {
            FilterSizesByCategory();
        }

        private void FilterSizesByCategory()
        {
            if (edtCategory == null) return;

            string cat = "";
            if (edtCategory.SelectedItem != null)
                cat = edtCategory.SelectedItem.ToString().ToLower();
            else if (!string.IsNullOrWhiteSpace(edtCategory.Text))
                cat = edtCategory.Text.ToLower();

            AllSizesOptions.Clear();

            foreach (var size in _allDbSizes)
            {
                bool keep = true;
                if (int.TryParse(size.Value, out int s))
                {
                    if (cat.Contains("жінк") || cat.Contains("women")) keep = (s >= 36 && s <= 45);
                    else if (cat.Contains("чоловік") || cat.Contains("men")) keep = (s >= 41 && s <= 50);
                    else if (cat.Contains("діт") || cat.Contains("хлоп") || cat.Contains("дівч") || cat.Contains("kid")) keep = (s >= 21 && s <= 41);
                }
                if (keep) AllSizesOptions.Add(size);
            }
        }

        private void ResetEditorFields()
        {
            _editingId = -1;
            EditorTitle.Text = GetString("txtAddNewProduct");
            edtName.Text = "";
            edtArticle.Text = "";
            edtDescription.Text = "";
            edtPrice.Text = "";
            edtBrand.Text = "";
            edtSubtype.Text = "";
            edtColor.Text = "";
            txtSelectedFileName.Text = GetString("txtNoFileChosen");
            imgPreview.Source = null;
            edtCategory.SelectedIndex = -1;
            edtProductType.SelectedIndex = -1;
            edtSeason.SelectedIndex = -1;
            txtAccessoryQuantity.Text = "";

            foreach (var s in _allDbSizes) s.Quantity = "";
            _selectedFullImagePath = "";
            FilterSizesByCategory();
        }

        private void LoadProductData(Product p)
        {
            _editingId = p.Id;
            EditorTitle.Text = string.Format(GetString("txtEditingProduct"), p.Name);

            edtName.Text = p.Name;
            edtArticle.Text = p.Article;
            edtDescription.Text = p.Description;
            edtPrice.Text = p.Price.ToString();
            txtSelectedFileName.Text = string.IsNullOrEmpty(p.ImageName) ? GetString("txtNoFileChosen") : p.ImageName;

            try
            {
                imgPreview.Source = p.ImagePath;

                if (!string.IsNullOrEmpty(p.ImageName))
                {
                    _selectedFullImagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PNG", "Product", p.ImageName);
                }
            }
            catch { }

            edtCategory.SelectedItem = p.Category;
            edtCategory.Text = p.Category;
            edtBrand.Text = p.Brand;
            edtSubtype.Text = p.Subtype;

            foreach (ComboBoxItem item in edtProductType.Items)
                if (item.Content.ToString() == p.ProductType) { edtProductType.SelectedItem = item; break; }

            foreach (ComboBoxItem item in edtSeason.Items)
                if (item.Content.ToString() == p.Season) { edtSeason.SelectedItem = item; break; }

            try
            {
                using (var conn = new SqliteConnection(StoreState.ConnectionString))
                {
                    conn.Open();
                    var cmdColor = new SqliteCommand("SELECT c.name FROM stock st JOIN colors c ON st.color_id = c.color_id WHERE st.product_id = @pid LIMIT 1", conn);
                    cmdColor.Parameters.AddWithValue("@pid", p.Id);
                    var colRes = cmdColor.ExecuteScalar();
                    if (colRes != null && colRes != DBNull.Value) edtColor.Text = colRes.ToString();
                    else edtColor.Text = "";

                    if (p.ProductType == "Взуття")
                    {
                        var cmd = new SqliteCommand("SELECT size_id, quantity FROM stock WHERE product_id=@pid", conn);
                        cmd.Parameters.AddWithValue("@pid", p.Id);
                        var stockDict = new Dictionary<int, int>();

                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read()) stockDict[r.GetInt32(0)] = r.GetInt32(1);
                        }

                        foreach (var opt in _allDbSizes)
                        {
                            if (stockDict.ContainsKey(opt.Id) && stockDict[opt.Id] > 0)
                                opt.Quantity = stockDict[opt.Id].ToString();
                            else
                                opt.Quantity = "";
                        }
                        FilterSizesByCategory();
                    }
                    else
                    {
                        var cmd = new SqliteCommand("SELECT quantity FROM stock WHERE product_id=@pid AND size_id IS NULL LIMIT 1", conn);
                        cmd.Parameters.AddWithValue("@pid", p.Id);
                        var res = cmd.ExecuteScalar();
                        if (res != null && res != DBNull.Value) txtAccessoryQuantity.Text = res.ToString();
                        else txtAccessoryQuantity.Text = "";
                    }
                }
            }
            catch { }
        }

        private void edtProductType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (stkSizes == null || pnlAccessoryStock == null) return;
            string type = (edtProductType.SelectedItem as ComboBoxItem)?.Content.ToString();

            if (type == "Взуття")
            {
                stkSizes.Visibility = Visibility.Visible;
                pnlAccessoryStock.Visibility = Visibility.Collapsed;
            }
            else if (type == "Аксесуар")
            {
                stkSizes.Visibility = Visibility.Collapsed;
                pnlAccessoryStock.Visibility = Visibility.Visible;
            }
            else
            {
                stkSizes.Visibility = Visibility.Collapsed;
                pnlAccessoryStock.Visibility = Visibility.Collapsed;
            }
        }

        private void ChooseImage_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog op = new OpenFileDialog { Filter = "Images (*.png;*.jpg)|*.png;*.jpg" };
            if (op.ShowDialog() == true)
            {
                _selectedFullImagePath = op.FileName;
                txtSelectedFileName.Text = Path.GetFileName(op.FileName);
                imgPreview.Source = new BitmapImage(new Uri(_selectedFullImagePath));
            }
        }

        private void SaveProduct_Click(object sender, RoutedEventArgs e)
        {
            string type = (edtProductType.SelectedItem as ComboBoxItem)?.Content.ToString();

            if (string.IsNullOrWhiteSpace(edtName.Text) || string.IsNullOrWhiteSpace(edtArticle.Text) ||
                string.IsNullOrWhiteSpace(edtCategory.Text) || string.IsNullOrWhiteSpace(edtBrand.Text) ||
                string.IsNullOrWhiteSpace(edtColor.Text))
            {
                MessageBox.Show(GetString("msgFillRequiredFields"), GetString("txtWarning"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int currentProductId = _editingId;

            try
            {
                using (var conn = new SqliteConnection(StoreState.ConnectionString))
                {
                    conn.Open();
                    using (var tx = conn.BeginTransaction())
                    {
                        long bIdRaw = GetOrCreateId(conn, tx, "brands", "brand_id", edtBrand.Text.Trim());
                        object bId = bIdRaw == 0 ? DBNull.Value : (object)bIdRaw;

                        long cIdRaw = GetOrCreateId(conn, tx, "categories", "category_id", edtCategory.Text.Trim());
                        object cId = cIdRaw == 0 ? DBNull.Value : (object)cIdRaw;

                        long sIdRaw = string.IsNullOrWhiteSpace(edtSubtype.Text) ? 0 : GetOrCreateId(conn, tx, "product_subtypes", "subtype_id", edtSubtype.Text.Trim());
                        object sId = sIdRaw == 0 ? DBNull.Value : (object)sIdRaw;

                        long colIdRaw = GetOrCreateId(conn, tx, "colors", "color_id", edtColor.Text.Trim());
                        object colId = colIdRaw == 0 ? DBNull.Value : (object)colIdRaw;

                        string imageName = txtSelectedFileName.Text == GetString("txtNoFileChosen") ? "" : txtSelectedFileName.Text;

                        if (!string.IsNullOrEmpty(_selectedFullImagePath) && File.Exists(_selectedFullImagePath))
                        {
                            string destPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PNG", "Product");
                            Directory.CreateDirectory(destPath);
                            imageName = Path.GetFileName(_selectedFullImagePath);
                            string destFile = Path.Combine(destPath, imageName);

                            if (!File.Exists(destFile) || _selectedFullImagePath != destFile)
                            {
                                try { File.Copy(_selectedFullImagePath, destFile, true); } catch { }
                            }
                        }

                        string sql = _editingId == -1
                            ? "INSERT INTO products (article, name, description, base_price, product_type, season, category_id, brand_id, subtype_id, images) VALUES (@art, @n, @d, @p, @pt, @s, @ci, @bi, @si, @img); SELECT last_insert_rowid();"
                            : "UPDATE products SET article=@art, name=@n, description=@d, base_price=@p, product_type=@pt, season=@s, category_id=@ci, brand_id=@bi, subtype_id=@si, images=@img WHERE product_id=@id; SELECT @id;";

                        var cmd = new SqliteCommand(sql, conn, tx);
                        cmd.Parameters.AddWithValue("@art", edtArticle.Text);
                        cmd.Parameters.AddWithValue("@n", edtName.Text);
                        cmd.Parameters.AddWithValue("@d", edtDescription.Text ?? "");
                        cmd.Parameters.AddWithValue("@p", decimal.TryParse(edtPrice.Text, out decimal pr) ? pr : 0);
                        cmd.Parameters.AddWithValue("@pt", type ?? "Взуття");
                        cmd.Parameters.AddWithValue("@s", (edtSeason.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Усі");
                        cmd.Parameters.AddWithValue("@ci", cId);
                        cmd.Parameters.AddWithValue("@bi", bId);
                        cmd.Parameters.AddWithValue("@si", sId);
                        cmd.Parameters.AddWithValue("@img", imageName);

                        if (_editingId != -1) cmd.Parameters.AddWithValue("@id", _editingId);

                        currentProductId = Convert.ToInt32(cmd.ExecuteScalar());

                        if (_editingId != -1)
                        {
                            using (var resetCmd = new SqliteCommand("UPDATE stock SET quantity = 0 WHERE product_id=@pid", conn, tx))
                            {
                                resetCmd.Parameters.AddWithValue("@pid", currentProductId);
                                resetCmd.ExecuteNonQuery();
                            }
                        }

                        if (type == "Взуття")
                        {
                            foreach (var size in AllSizesOptions)
                            {
                                if (!string.IsNullOrEmpty(size.Quantity) && int.TryParse(size.Quantity, out int qty) && qty > 0)
                                {
                                    long existingStockId = 0;
                                    using (var checkCmd = new SqliteCommand("SELECT stock_id FROM stock WHERE product_id=@pid AND size_id=@sid LIMIT 1", conn, tx))
                                    {
                                        checkCmd.Parameters.AddWithValue("@pid", currentProductId);
                                        checkCmd.Parameters.AddWithValue("@sid", size.Id);
                                        var res = checkCmd.ExecuteScalar();
                                        if (res != null && res != DBNull.Value) existingStockId = Convert.ToInt64(res);
                                    }

                                    if (existingStockId > 0)
                                    {
                                        using (var updCmd = new SqliteCommand("UPDATE stock SET quantity=@qty, color_id=@colid WHERE stock_id=@sid", conn, tx))
                                        {
                                            updCmd.Parameters.AddWithValue("@qty", qty);
                                            updCmd.Parameters.AddWithValue("@colid", colId);
                                            updCmd.Parameters.AddWithValue("@sid", existingStockId);
                                            updCmd.ExecuteNonQuery();
                                        }
                                    }
                                    else
                                    {
                                        using (var sCmd = new SqliteCommand("INSERT INTO stock (product_id, color_id, size_id, quantity, availability_status) VALUES (@pid, @colid, @sid, @qty, 'В наявності')", conn, tx))
                                        {
                                            sCmd.Parameters.AddWithValue("@pid", currentProductId);
                                            sCmd.Parameters.AddWithValue("@colid", colId);
                                            sCmd.Parameters.AddWithValue("@sid", size.Id);
                                            sCmd.Parameters.AddWithValue("@qty", qty);
                                            sCmd.ExecuteNonQuery();
                                        }
                                    }
                                }
                            }
                        }
                        else if (type == "Аксесуар")
                        {
                            if (int.TryParse(txtAccessoryQuantity.Text.Trim(), out int accQty) && accQty > 0)
                            {
                                long existingStockId = 0;
                                using (var checkCmd = new SqliteCommand("SELECT stock_id FROM stock WHERE product_id=@pid AND size_id IS NULL LIMIT 1", conn, tx))
                                {
                                    checkCmd.Parameters.AddWithValue("@pid", currentProductId);
                                    var res = checkCmd.ExecuteScalar();
                                    if (res != null && res != DBNull.Value) existingStockId = Convert.ToInt64(res);
                                }

                                if (existingStockId > 0)
                                {
                                    using (var updCmd = new SqliteCommand("UPDATE stock SET quantity=@qty, color_id=@colid WHERE stock_id=@sid", conn, tx))
                                    {
                                        updCmd.Parameters.AddWithValue("@qty", accQty);
                                        updCmd.Parameters.AddWithValue("@colid", colId);
                                        updCmd.Parameters.AddWithValue("@sid", existingStockId);
                                        updCmd.ExecuteNonQuery();
                                    }
                                }
                                else
                                {
                                    using (var sCmd = new SqliteCommand("INSERT INTO stock (product_id, color_id, size_id, quantity, availability_status) VALUES (@pid, @colid, NULL, @qty, 'В наявності')", conn, tx))
                                    {
                                        sCmd.Parameters.AddWithValue("@pid", currentProductId);
                                        sCmd.Parameters.AddWithValue("@colid", colId);
                                        sCmd.Parameters.AddWithValue("@qty", accQty);
                                        sCmd.ExecuteNonQuery();
                                    }
                                }
                            }
                        }
                        tx.Commit();
                    }
                }

                string emailReport = CheckAndSendRestockNotifications(currentProductId);
                string successMsg = string.Format(GetString("msgProductSavedWithReport"), emailReport);
                MessageBox.Show(successMsg, GetString("txtSaved"), MessageBoxButton.OK, MessageBoxImage.Information);

                var mainWindow = Window.GetWindow(this) as MainWindow ?? Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                if (mainWindow != null)
                {
                    if (_previousView is CatalogView catalog)
                    {
                        catalog.ReloadDataFromDB();
                        mainWindow.RestoreView(catalog, "Catalog");
                    }
                    else
                    {
                        mainWindow.MainContent.Content = new CatalogView("txtMenuCatalog");
                        if (mainWindow.NavCatalog != null) mainWindow.NavCatalog.IsChecked = true;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{GetString("msgSaveError")} {ex.Message}", GetString("txtError"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string CheckAndSendRestockNotifications(int productId)
        {
            int requestsFound = 0;
            int emailsSentCount = 0;
            List<string> errorLog = new List<string>();

            try
            {
                using (var conn = new SqliteConnection(StoreState.ConnectionString))
                {
                    conn.Open();
                    try { new SqliteCommand("SELECT 1 FROM Restock_Requests LIMIT 1", conn).ExecuteScalar(); }
                    catch { return GetString("msgNoRestockTable"); }

                    string sql = @"SELECT r.request_id, r.size, u.full_name, c.email, p.name 
                                   FROM Restock_Requests r
                                   JOIN Users u ON r.user_id = u.user_id
                                   JOIN Customers c ON r.user_id = c.user_id
                                   JOIN Products p ON r.product_id = p.product_id
                                   WHERE r.product_id = @pid AND r.is_fulfilled = 0";

                    var requestsToFulfill = new List<Tuple<int, string, string, string, string>>();

                    using (var cmd = new SqliteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@pid", productId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                requestsFound++;
                                int reqId = reader.GetInt32(0);
                                string size = reader.IsDBNull(1) ? null : reader.GetString(1);
                                string userName = reader.GetString(2);
                                string email = reader.IsDBNull(3) ? null : reader.GetString(3);
                                string prodName = reader.GetString(4);

                                bool inStock = false;
                                if (string.IsNullOrEmpty(size))
                                {
                                    var stCmd = new SqliteCommand("SELECT quantity FROM Stock WHERE product_id = @pid LIMIT 1", conn);
                                    stCmd.Parameters.AddWithValue("@pid", productId);
                                    var res = stCmd.ExecuteScalar();
                                    inStock = (res != null && Convert.ToInt32(res) > 0);
                                }
                                else
                                {
                                    var stCmd = new SqliteCommand("SELECT st.quantity FROM Stock st JOIN Sizes s ON st.size_id = s.size_id WHERE st.product_id = @pid AND s.value = @size LIMIT 1", conn);
                                    stCmd.Parameters.AddWithValue("@pid", productId);
                                    stCmd.Parameters.AddWithValue("@size", size);
                                    var res = stCmd.ExecuteScalar();
                                    inStock = (res != null && Convert.ToInt32(res) > 0);
                                }

                                if (inStock && !string.IsNullOrEmpty(email))
                                {
                                    requestsToFulfill.Add(new Tuple<int, string, string, string, string>(reqId, size, userName, email, prodName));
                                }
                            }
                        }
                    }

                    if (requestsFound == 0) return GetString("msgNoClientsWaiting");

                    foreach (var req in requestsToFulfill)
                    {
                        string smtpError;
                        bool sent = SendRestockEmail(req.Item4, req.Item3, req.Item5, req.Item2, out smtpError);
                        if (sent)
                        {
                            using (var updateCmd = new SqliteCommand("UPDATE Restock_Requests SET is_fulfilled = 1 WHERE request_id = @id", conn))
                            {
                                updateCmd.Parameters.AddWithValue("@id", req.Item1);
                                updateCmd.ExecuteNonQuery();
                            }
                            emailsSentCount++;
                        }
                        else
                        {
                            errorLog.Add($"На {req.Item4}: {smtpError}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return $"{GetString("msgReadRequestsError")} {ex.Message}";
            }

            if (errorLog.Any())
            {
                return string.Format(GetString("msgRestockReportWithErrors"), requestsFound, emailsSentCount, string.Join("\n", errorLog));
            }

            return string.Format(GetString("msgRestockReportSuccess"), requestsFound, emailsSentCount);
        }

        private bool SendRestockEmail(string toEmail, string userName, string productName, string size, out string errorMessage)
        {
            errorMessage = "";
            try
            {
                using (var client = new SmtpClient("smtp.gmail.com", 587))
                {
                    client.UseDefaultCredentials = false;
                    //client.Credentials = new System.Net.NetworkCredential("********@gmail.com", "*************");
                    client.EnableSsl = true;

                    string sizeText = string.IsNullOrEmpty(size) ? "" : $" (розмір {size})";
                    var mail = new MailMessage
                    {
                        From = new MailAddress("blacksmithstoreinfo@gmail.com", "Blacksmith Store"),
                        Subject = "🔥 Товар знову в наявності! - Blacksmith Store",
                        Body = $"<h3 style='color:#FF8C00;'>Вітаємо, {userName}!</h3><p>Товар <b>{productName}</b>{sizeText}, який ви так очікували, знову з'явився у наявності на нашому сайті та на складі в магазині.</p><p>Встигніть оформити замовлення, поки він є в наявності!</p><br/><p>З повагою,<br/>Команда Blacksmith Store</p>",
                        IsBodyHtml = true
                    };
                    mail.To.Add(toEmail);
                    client.Send(mail);
                    return true;
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        private long GetOrCreateId(SqliteConnection conn, SqliteTransaction tx, string tableName, string idColumn, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0;
            using (var cmdFind = new SqliteCommand($"SELECT {idColumn} FROM {tableName} WHERE name = @val COLLATE NOCASE", conn, tx))
            {
                cmdFind.Parameters.AddWithValue("@val", value);
                var res = cmdFind.ExecuteScalar();
                if (res != null && res != DBNull.Value) return Convert.ToInt64(res);
            }
            using (var cmdIns = new SqliteCommand($"INSERT INTO {tableName} (name) VALUES (@val)", conn, tx))
            {
                cmdIns.Parameters.AddWithValue("@val", value);
                cmdIns.ExecuteNonQuery();
                using (var cmdId = new SqliteCommand("SELECT last_insert_rowid()", conn, tx))
                {
                    return (long)cmdId.ExecuteScalar();
                }
            }
        }

        private void BackToCatalog_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow ?? Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
            if (mainWindow != null)
            {
                if (_previousView != null)
                {
                    mainWindow.RestoreView(_previousView, "Catalog");
                }
                else
                {
                    mainWindow.MainContent.Content = new CatalogView("txtMenuCatalog");
                    if (mainWindow.NavCatalog != null) mainWindow.NavCatalog.IsChecked = true;
                }
            }
        }
    }
}