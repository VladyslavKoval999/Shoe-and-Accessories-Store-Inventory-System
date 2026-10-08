using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.Sqlite;
using BlacksmithStore.Data;

namespace BlacksmithStore.Views
{
    public class RoleOption
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class StaffMember : INotifyPropertyChanged
    {
        public int Id { get; set; }

        private string _username;
        public string Username { get => _username; set { _username = value; OnPropertyChanged(); } }

        private string _fullName;
        public string FullName { get => _fullName; set { _fullName = value; OnPropertyChanged(); } }

        private int _roleId;
        public int RoleId
        {
            get => _roleId;
            set
            {
                _roleId = value;
                OnPropertyChanged();
                if (AddSellerView.GlobalRoles != null)
                {
                    var role = AddSellerView.GlobalRoles.FirstOrDefault(r => r.Id == value);
                    if (role != null) RoleName = role.Name;
                }
            }
        }

        private string _roleName;
        public string RoleName { get => _roleName; set { _roleName = value; OnPropertyChanged(); } }

        private string _newPassword;
        public string NewPassword
        {
            get => _newPassword;
            set { _newPassword = value; OnPropertyChanged(); }
        }

        private string _originalDbHash;

        public void SetHashFromDatabase(string hash)
        {
            _originalDbHash = hash;
            NewPassword = "";
        }

        public string GetHashForSave()
        {
            if (string.IsNullOrWhiteSpace(NewPassword))
                return _originalDbHash;

            return AddSellerView.ComputeSha256Hash(NewPassword);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class AddSellerView : UserControl
    {
        public ObservableCollection<StaffMember> StaffList { get; set; } = new ObservableCollection<StaffMember>();
        private List<StaffMember> _allStaff = new List<StaffMember>();

        public static List<RoleOption> GlobalRoles { get; set; } = new List<RoleOption>();
        public List<RoleOption> AvailableRoles { get; set; } = new List<RoleOption>();

        public AddSellerView()
        {
            InitializeComponent();
            this.DataContext = this;
            LoadRoles();
            dgStaff.ItemsSource = StaffList;
        }

        private string GetString(string key)
        {
            return Application.Current.TryFindResource(key) as string ?? key;
        }

        private string GetLocalizedRoleName(string dbRoleName)
        {
            string lower = dbRoleName.ToLower();
            if (lower.Contains("продавець") || lower.Contains("seller")) return GetString("txtRoleSeller");
            if (lower.Contains("менеджер") || lower.Contains("manager")) return GetString("txtRoleManager");
            if (lower.Contains("адмін") || lower.Contains("admin")) return GetString("txtRoleAdmin");
            return dbRoleName;
        }

        public static string ComputeSha256Hash(string rawData)
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

        private void LoadRoles()
        {
            AvailableRoles.Clear();
            try
            {
                using (var conn = new SqliteConnection(StoreState.ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new SqliteCommand("SELECT role_id, role_name FROM Roles WHERE role_id != 4", conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            AvailableRoles.Add(new RoleOption
                            {
                                Id = r.GetInt32(0),
                                Name = GetLocalizedRoleName(r.GetString(1))
                            });
                        }
                    }
                }
                GlobalRoles = AvailableRoles.ToList();

                cbStaffRole.ItemsSource = AvailableRoles;
                cbStaffRole.DisplayMemberPath = "Name";
                cbStaffRole.SelectedValuePath = "Id";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{GetString("msgDbError")} {ex.Message}", GetString("txtError"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CreateStaff_Click(object sender, RoutedEventArgs e)
        {
            string login = edtStaffLogin.Text.Trim();
            string fullName = edtStaffFullName.Text.Trim();
            string password = edtStaffPassword.Password.Trim();

            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(fullName) ||
                string.IsNullOrEmpty(password) || cbStaffRole.SelectedValue == null)
            {
                MessageBox.Show(GetString("msgFillAllFields"), GetString("txtWarning"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int roleId = (int)cbStaffRole.SelectedValue;
            string hashedPass = ComputeSha256Hash(password);

            try
            {
                using (var conn = new SqliteConnection(StoreState.ConnectionString))
                {
                    conn.Open();

                    using (var checkCmd = new SqliteCommand("SELECT COUNT(*) FROM Users WHERE username = @login", conn))
                    {
                        checkCmd.Parameters.AddWithValue("@login", login);
                        if (Convert.ToInt32(checkCmd.ExecuteScalar()) > 0)
                        {
                            MessageBox.Show(GetString("msgLoginExists"), GetString("txtError"), MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }

                    string sql = "INSERT INTO Users (role_id, full_name, username, password_hash) VALUES (@role, @fname, @login, @pass)";
                    using (var cmd = new SqliteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@role", roleId);
                        cmd.Parameters.AddWithValue("@fname", fullName);
                        cmd.Parameters.AddWithValue("@login", login);
                        cmd.Parameters.AddWithValue("@pass", hashedPass);
                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show(string.Format(GetString("msgStaffAdded"), fullName), GetString("txtSuccess"), MessageBoxButton.OK, MessageBoxImage.Information);

                edtStaffLogin.Clear();
                edtStaffFullName.Clear();
                edtStaffPassword.Clear();
                cbStaffRole.SelectedIndex = -1;

                LoadStaff();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{GetString("msgDbError")} {ex.Message}", GetString("txtError"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl tc && tc.SelectedIndex == 1)
            {
                LoadStaff();
            }
        }

        private void LoadStaff()
        {
            _allStaff.Clear();
            try
            {
                using (var conn = new SqliteConnection(StoreState.ConnectionString))
                {
                    conn.Open();
                    string sql = @"
                        SELECT u.user_id, u.username, u.full_name, u.role_id, r.role_name, u.password_hash 
                        FROM Users u
                        JOIN Roles r ON u.role_id = r.role_id
                        WHERE u.role_id != 4";

                    using (var cmd = new SqliteCommand(sql, conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            var staff = new StaffMember
                            {
                                Id = r.GetInt32(0),
                                Username = r.GetString(1),
                                FullName = r.GetString(2),
                                RoleId = r.GetInt32(3),
                                RoleName = GetLocalizedRoleName(r.GetString(4))
                            };

                            staff.SetHashFromDatabase(r.GetString(5));
                            _allStaff.Add(staff);
                        }
                    }
                }
            }
            catch { }

            FilterDatabase(edtSearchDatabase.Text);
        }

        private void SearchDatabase_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterDatabase(edtSearchDatabase.Text);
        }

        private void FilterDatabase(string query)
        {
            StaffList.Clear();

            if (string.IsNullOrWhiteSpace(query))
            {
                foreach (var s in _allStaff) StaffList.Add(s);
            }
            else
            {
                string q = query.ToLower();
                foreach (var s in _allStaff)
                {
                    if ((s.Username != null && s.Username.ToLower().Contains(q)) ||
                        (s.FullName != null && s.FullName.ToLower().Contains(q)) ||
                        (s.RoleName != null && s.RoleName.ToLower().Contains(q)) ||
                        s.Id.ToString().Contains(q))
                    {
                        StaffList.Add(s);
                    }
                }
            }
        }

        private void ToggleEditMode_Changed(object sender, RoutedEventArgs e)
        {
            bool isEditing = ToggleEditMode.IsChecked == true;
            dgStaff.IsReadOnly = !isEditing;

            BtnSaveChanges.Visibility = isEditing ? Visibility.Visible : Visibility.Collapsed;
            BtnDeleteStaff.Visibility = isEditing ? Visibility.Visible : Visibility.Collapsed;

            if (colPassword != null)
            {
                colPassword.Visibility = isEditing ? Visibility.Visible : Visibility.Collapsed;
            }
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
                        foreach (var staff in StaffList)
                        {
                            if (string.IsNullOrWhiteSpace(staff.Username) || string.IsNullOrWhiteSpace(staff.FullName))
                            {
                                MessageBox.Show(string.Format(GetString("msgEmptyLoginName"), staff.Id), GetString("txtWarning"), MessageBoxButton.OK, MessageBoxImage.Warning);
                                return;
                            }
                        }

                        foreach (var staff in StaffList)
                        {
                            string sql = "UPDATE Users SET username = @login, full_name = @fname, role_id = @role";

                            if (!string.IsNullOrWhiteSpace(staff.NewPassword))
                            {
                                sql += ", password_hash = @pass";
                            }

                            sql += " WHERE user_id = @id";

                            using (var cmd = new SqliteCommand(sql, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@login", staff.Username);
                                cmd.Parameters.AddWithValue("@fname", staff.FullName);
                                cmd.Parameters.AddWithValue("@role", staff.RoleId);
                                cmd.Parameters.AddWithValue("@id", staff.Id);

                                if (!string.IsNullOrWhiteSpace(staff.NewPassword))
                                {
                                    cmd.Parameters.AddWithValue("@pass", ComputeSha256Hash(staff.NewPassword));
                                }

                                cmd.ExecuteNonQuery();
                            }
                        }
                        transaction.Commit();
                    }
                }

                ToggleEditMode.IsChecked = false;
                LoadStaff();
                MessageBox.Show(GetString("msgChangesSaved"), GetString("txtSuccess"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("UNIQUE") || ex.Message.Contains("constraint"))
                    MessageBox.Show(GetString("msgLoginTaken"), GetString("txtError"), MessageBoxButton.OK, MessageBoxImage.Error);
                else
                    MessageBox.Show($"{GetString("msgSaveError")} {ex.Message}", GetString("txtError"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteStaff_Click(object sender, RoutedEventArgs e)
        {
            if (dgStaff.SelectedItem is StaffMember selectedStaff)
            {
                if (StoreState.CurrentUser != null && StoreState.CurrentUser.Username == selectedStaff.Username)
                {
                    MessageBox.Show(GetString("msgCannotDeleteSelf"), GetString("txtError"), MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var result = MessageBox.Show(
                    string.Format(GetString("msgConfirmDeleteStaff"), selectedStaff.FullName),
                    GetString("txtConfirm"), MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        using (var conn = new SqliteConnection(StoreState.ConnectionString))
                        {
                            conn.Open();
                            using (var cmd = new SqliteCommand("DELETE FROM Users WHERE user_id = @id", conn))
                            {
                                cmd.Parameters.AddWithValue("@id", selectedStaff.Id);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        MessageBox.Show(GetString("msgStaffDeleted"), GetString("txtSuccess"), MessageBoxButton.OK, MessageBoxImage.Information);
                        LoadStaff();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"{GetString("msgDeleteError")} {ex.Message}", GetString("txtError"), MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show(GetString("msgSelectStaffToDelete"), GetString("txtWarning"), MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}