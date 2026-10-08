using System;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using Microsoft.Data.Sqlite;
using BlacksmithStore.Data;
using BlacksmithStore.Models;

namespace BlacksmithStore
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            try { SQLitePCL.Batteries_V2.Init(); } catch { }

            InitializeComponent();
        }

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            string username = txtUsername.Text.Trim();
            string password = txtPassword.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ShowError("Будь ласка, введіть логін та пароль.");
                return;
            }

            if (AuthenticateUser(username, password))
            {
                MainWindow mainWindow = new MainWindow();
                mainWindow.Show();
                this.Close();
            }
            else
            {

            }
        }

        private bool AuthenticateUser(string username, string password)
        {
            if (!System.IO.File.Exists(StoreState.DbPath))
            {
                ShowError("Базу даних не знайдено за шляхом:\n" + StoreState.DbPath);
                return false;
            }

            try
            {
                string hashedInputPassword = ComputeSha256Hash(password);

                using (var conn = new SqliteConnection(StoreState.ConnectionString))
                {
                    conn.Open();

                    string sql = @"
                        SELECT u.user_id, u.username, u.full_name, r.role_name 
                        FROM users u
                        JOIN roles r ON u.role_id = r.role_id
                        WHERE u.username = @user AND u.password_hash = @pass";

                    using (var cmd = new SqliteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@user", username);
                        cmd.Parameters.AddWithValue("@pass", hashedInputPassword);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                StoreState.CurrentUser = new UserModel
                                {
                                    Id = reader.GetInt32(0),
                                    Username = reader.GetString(1),
                                    FullName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                    Role = reader.GetString(3)
                                };
                                return true;
                            }
                            else
                            {
                                ShowError("Невірний логін або пароль!");
                                return false;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError($"Помилка БД: {ex.Message}");
                return false;
            }
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

        private void ShowError(string message)
        {
            txtError.Text = message;
            txtError.Visibility = Visibility.Visible;
        }
    }
}