using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using System.IO;
using Microsoft.Data.Sqlite;
using BlacksmithStore.Data;
using BlacksmithStore.Models;
using LiveCharts;
using LiveCharts.Wpf;

namespace BlacksmithStore.Views
{
    public class ReportDetailDisplayItem
    {
        public string Article { get; set; }
        public string ProductName { get; set; }
        public string Brand { get; set; }
        public string ProductType { get; set; }
        public string Size { get; set; }
        public int Quantity { get; set; }
        public decimal SalePrice { get; set; }
        public string SaleDate { get; set; }
    }

    public partial class ReportsView : UserControl, INotifyPropertyChanged
    {
        public ObservableCollection<ReportItem> ReportResults { get; set; } = new ObservableCollection<ReportItem>();
        public ObservableCollection<ReportDetailDisplayItem> ReportDetailItems { get; set; } = new ObservableCollection<ReportDetailDisplayItem>();

        private SeriesCollection _trendSeries;
        public SeriesCollection TrendSeries { get => _trendSeries; set { _trendSeries = value; OnPropertyChanged(); } }

        private string[] _trendLabels;
        public string[] TrendLabels { get => _trendLabels; set { _trendLabels = value; OnPropertyChanged(); } }

        private SeriesCollection _pieSeries;
        public SeriesCollection PieSeries { get => _pieSeries; set { _pieSeries = value; OnPropertyChanged(); } }

        private double _chartMinWidth = 400;
        public double ChartMinWidth
        {
            get => _chartMinWidth;
            set { _chartMinWidth = value; OnPropertyChanged(); }
        }

        public Func<double, string> YFormatter { get; set; }

        private Visibility _trendVisibility = Visibility.Collapsed;
        public Visibility TrendVisibility { get => _trendVisibility; set { _trendVisibility = value; OnPropertyChanged(); } }

        private Visibility _pieVisibility = Visibility.Collapsed;
        public Visibility PieVisibility { get => _pieVisibility; set { _pieVisibility = value; OnPropertyChanged(); } }

        private Visibility _emptyChartVisibility = Visibility.Visible;
        public Visibility EmptyChartVisibility { get => _emptyChartVisibility; set { _emptyChartVisibility = value; OnPropertyChanged(); } }

        public ReportsView()
        {
            InitializeComponent();
            this.DataContext = this;
            YFormatter = value => value.ToString("N0");
        }

        private void GenerateReport_Click(object sender, RoutedEventArgs e)
        {
            if (cbReportType.SelectedIndex == -1) { MessageBox.Show("Оберіть тип звіту"); return; }

            string sDate = dpStart.SelectedDate?.ToString("yyyy-MM-dd") ?? "2000-01-01";
            string eDate = dpEnd.SelectedDate?.ToString("yyyy-MM-dd") ?? "2099-12-31";
            string endDateWithTime = $"{eDate} 23:59:59";

            ReportResults.Clear();
            string type = (cbReportType.SelectedItem as ComboBoxItem).Content.ToString();

            try
            {
                using (var conn = new SqliteConnection(StoreState.ConnectionString))
                {
                    conn.Open();

                    string sqlGlobal = $@"
                        SELECT 
                            (SELECT COUNT(order_id) FROM orders WHERE status != 'Скасовано' AND order_date BETWEEN '{sDate}' AND '{endDateWithTime}'),
                            (SELECT IFNULL(SUM(oi.quantity), 0) FROM order_items oi JOIN orders o ON oi.order_id = o.order_id WHERE o.status != 'Скасовано' AND o.order_date BETWEEN '{sDate}' AND '{endDateWithTime}'),
                            (SELECT IFNULL(SUM(total_amount), 0) FROM orders WHERE status != 'Скасовано' AND order_date BETWEEN '{sDate}' AND '{endDateWithTime}')";

                    using (var gCmd = new SqliteCommand(sqlGlobal, conn))
                    using (var gReader = gCmd.ExecuteReader())
                    {
                        if (gReader.Read())
                        {
                            txtOrderCount.Text = gReader.GetInt32(0).ToString();
                            txtTotalUnits.Text = gReader.GetInt32(1).ToString();
                            txtTotalRevenue.Text = $"{gReader.GetDecimal(2):N0} грн";
                        }
                    }

                    string sql = "";

                    if (type == "Топ продажів (Найпопулярніші товари)")
                    {
                        sql = $@"SELECT p.name, b.name, SUM(oi.quantity), SUM(oi.quantity * oi.price_at_time_of_sale) 
                                 FROM order_items oi 
                                 JOIN orders o ON oi.order_id = o.order_id 
                                 JOIN stock st ON oi.stock_id = st.stock_id 
                                 JOIN products p ON st.product_id = p.product_id
                                 JOIN brands b ON p.brand_id = b.brand_id 
                                 WHERE o.status != 'Скасовано' AND o.order_date BETWEEN '{sDate}' AND '{endDateWithTime}' 
                                 GROUP BY p.product_id 
                                 ORDER BY SUM(oi.quantity) DESC LIMIT 50";
                    }
                    else if (type == "Аналітика по категоріях")
                    {
                        sql = $@"SELECT IFNULL(c.name, 'Без категорії'), 'Категорія', SUM(oi.quantity), SUM(oi.quantity * oi.price_at_time_of_sale)
                                 FROM order_items oi 
                                 JOIN orders o ON oi.order_id = o.order_id
                                 JOIN stock st ON oi.stock_id = st.stock_id 
                                 JOIN products p ON st.product_id = p.product_id
                                 LEFT JOIN categories c ON p.category_id = c.category_id
                                 WHERE o.status != 'Скасовано' AND o.order_date BETWEEN '{sDate}' AND '{endDateWithTime}' 
                                 GROUP BY c.category_id
                                 ORDER BY SUM(oi.quantity * oi.price_at_time_of_sale) DESC";
                    }
                    else if (type == "Аналітика по брендах")
                    {
                        sql = $@"SELECT IFNULL(b.name, 'Без бренду'), 'Бренд', SUM(oi.quantity), SUM(oi.quantity * oi.price_at_time_of_sale)
                                 FROM order_items oi 
                                 JOIN orders o ON oi.order_id = o.order_id
                                 JOIN stock st ON oi.stock_id = st.stock_id 
                                 JOIN products p ON st.product_id = p.product_id
                                 LEFT JOIN brands b ON p.brand_id = b.brand_id
                                 WHERE o.status != 'Скасовано' AND o.order_date BETWEEN '{sDate}' AND '{endDateWithTime}' 
                                 GROUP BY b.brand_id
                                 ORDER BY SUM(oi.quantity * oi.price_at_time_of_sale) DESC";
                    }
                    else if (type == "Аналітика по підтипах")
                    {
                        sql = $@"SELECT IFNULL(sub.name, 'Без підтипу'), 'Підтип', SUM(oi.quantity), SUM(oi.quantity * oi.price_at_time_of_sale)
                                 FROM order_items oi 
                                 JOIN orders o ON oi.order_id = o.order_id
                                 JOIN stock st ON oi.stock_id = st.stock_id 
                                 JOIN products p ON st.product_id = p.product_id
                                 LEFT JOIN product_subtypes sub ON p.subtype_id = sub.subtype_id
                                 WHERE o.status != 'Скасовано' AND o.order_date BETWEEN '{sDate}' AND '{endDateWithTime}' 
                                 GROUP BY IFNULL(sub.name, 'Без підтипу')
                                 ORDER BY SUM(oi.quantity * oi.price_at_time_of_sale) DESC";
                    }
                    else if (type == "Статистика проданих розмірів")
                    {
                        sql = $@"SELECT IFNULL(sz.value, 'Універсальний'), 'Розмір', SUM(oi.quantity), SUM(oi.quantity * oi.price_at_time_of_sale)
                                 FROM order_items oi 
                                 JOIN orders o ON oi.order_id = o.order_id
                                 JOIN stock st ON oi.stock_id = st.stock_id 
                                 LEFT JOIN sizes sz ON st.size_id = sz.size_id
                                 WHERE o.status != 'Скасовано' AND o.order_date BETWEEN '{sDate}' AND '{endDateWithTime}' 
                                 GROUP BY sz.value
                                 ORDER BY SUM(oi.quantity) DESC";
                    }
                    else if (type == "Динаміка продажів по днях")
                    {
                        sql = $@"SELECT substr(o.order_date, 1, 10), 'Дата', SUM(oi.quantity), SUM(o.total_amount)
                                 FROM orders o 
                                 LEFT JOIN order_items oi ON o.order_id = oi.order_id
                                 WHERE o.status != 'Скасовано' AND o.order_date BETWEEN '{sDate}' AND '{endDateWithTime}' 
                                 GROUP BY substr(o.order_date, 1, 10)
                                 ORDER BY substr(o.order_date, 1, 10) ASC";
                    }
                    else if (type == "Критичний залишок (< 5 од.)")
                    {
                        sql = @"SELECT p.name, IFNULL(sz.value, 'Універсальний'), SUM(st.quantity), p.base_price 
                                FROM products p 
                                JOIN stock st ON p.product_id = st.product_id 
                                LEFT JOIN sizes sz ON st.size_id = sz.size_id
                                GROUP BY p.product_id, st.size_id 
                                HAVING SUM(st.quantity) < 5 
                                ORDER BY SUM(st.quantity) ASC";
                    }
                    else if (type == "Аналітика джерел (Магазин vs Веб-сайт)")
                    {
                        sql = $@"SELECT IFNULL(order_source, 'Магазин'), 'Джерело', COUNT(order_id), IFNULL(SUM(total_amount), 0)
                                 FROM orders
                                 WHERE status != 'Скасовано' AND order_date BETWEEN '{sDate}' AND '{endDateWithTime}'
                                 GROUP BY IFNULL(order_source, 'Магазин')";
                    }
                    else if (type == "Використання клубних карт")
                    {
                        sql = $@"SELECT 'Активні користувачі', 'Мають карту і робили покупки', COUNT(customer_id), 
                                        IFNULL((SELECT SUM(o.total_amount) FROM orders o JOIN customers c2 ON o.user_id = c2.user_id WHERE c2.loyalty_card_number IS NOT NULL AND c2.purchased_items_count > 0 AND o.status != 'Скасовано' AND o.order_date BETWEEN '{sDate}' AND '{endDateWithTime}'), 0)
                                 FROM customers WHERE loyalty_card_number IS NOT NULL AND loyalty_card_number != '' AND purchased_items_count > 0
                                 UNION ALL
                                 SELECT 'Нові користувачі', 'Мають карту, але 0 покупок', COUNT(customer_id), 0
                                 FROM customers WHERE loyalty_card_number IS NOT NULL AND loyalty_card_number != '' AND purchased_items_count = 0";
                    }

                    using (var cmd = new SqliteCommand(sql, conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            var item = new ReportItem
                            {
                                Name = r.GetString(0),
                                Detail = r.GetString(1),
                                Count = r.GetInt32(2),
                                Total = r.GetDecimal(3)
                            };
                            ReportResults.Add(item);
                        }
                    }
                }

                BuildCharts(type);
            }
            catch (Exception ex) { MessageBox.Show("Помилка генерації звіту: " + ex.Message); }
        }

        private void BuildCharts(string type)
        {
            if (ReportResults.Count == 0)
            {
                EmptyChartVisibility = Visibility.Visible;
                TrendVisibility = Visibility.Collapsed;
                PieVisibility = Visibility.Collapsed;
                return;
            }

            EmptyChartVisibility = Visibility.Collapsed;

            var labels = new List<string>();
            var values = new ChartValues<double>();
            PieSeries = new SeriesCollection();

            foreach (var item in ReportResults)
            {
                labels.Add(item.Name);

                double val = type.Contains("залишок") || type.Contains("розмірів") || type.Contains("Використання") ? item.Count : (double)item.Total;
                values.Add(val);

                var pieSeries = new PieSeries
                {
                    Title = item.Name,
                    Values = new ChartValues<double> { val },
                    DataLabels = true
                };

                pieSeries.SetResourceReference(LiveCharts.Wpf.Series.ForegroundProperty, "TextMainBrush");

                PieSeries.Add(pieSeries);
            }

            double calculatedWidth = labels.Count * 60;
            ChartMinWidth = calculatedWidth < 400 ? 400 : calculatedWidth;

            if (type.Contains("категоріях") || type.Contains("брендах") || type.Contains("підтипах") || type.Contains("джерел") || type.Contains("Використання"))
            {
                PieVisibility = Visibility.Visible;
                TrendVisibility = Visibility.Collapsed;
            }
            else if (type.Contains("по днях"))
            {
                var lineSeries = new LineSeries { Title = "Виторг", Values = values };
                lineSeries.SetResourceReference(LiveCharts.Wpf.Series.ForegroundProperty, "TextMainBrush");
                TrendSeries = new SeriesCollection { lineSeries };

                TrendLabels = labels.ToArray();
                TrendVisibility = Visibility.Visible;
                PieVisibility = Visibility.Collapsed;
            }
            else
            {
                var columnSeries = new ColumnSeries { Title = type.Contains("залишок") ? "Залишок" : "Показник", Values = values };
                columnSeries.SetResourceReference(LiveCharts.Wpf.Series.ForegroundProperty, "TextMainBrush");
                TrendSeries = new SeriesCollection { columnSeries };

                TrendLabels = labels.ToArray();
                TrendVisibility = Visibility.Visible;
                PieVisibility = Visibility.Collapsed;
            }
        }

        private void ClearReportFilters_Click(object sender, RoutedEventArgs e)
        {
            dpStart.SelectedDate = null; dpEnd.SelectedDate = null; cbReportType.SelectedIndex = -1;
            ReportResults.Clear(); ReportDetailItems.Clear();
            txtTotalRevenue.Text = "0 грн"; txtTotalUnits.Text = "0"; txtOrderCount.Text = "0";
            ReportDetailsOverlay.Visibility = Visibility.Collapsed;

            EmptyChartVisibility = Visibility.Visible; TrendVisibility = Visibility.Collapsed; PieVisibility = Visibility.Collapsed;
        }

        private void dgReportResults_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgReportResults.SelectedItem is ReportItem selectedReportItem)
            {
                string reportType = (cbReportType.SelectedItem as ComboBoxItem)?.Content.ToString();

                if (reportType == "Аналітика по категоріях" ||
                    reportType == "Аналітика по брендах" ||
                    reportType == "Аналітика по підтипах" ||
                    reportType == "Статистика проданих розмірів" ||
                    reportType == "Динаміка продажів по днях")
                {
                    LoadReportDetails(selectedReportItem, reportType);
                    ReportDetailsOverlay.Visibility = Visibility.Visible;
                }
            }
        }

        private void CloseDetails_Click(object sender, RoutedEventArgs e)
        {
            ReportDetailsOverlay.Visibility = Visibility.Collapsed;
            ReportDetailItems.Clear();
        }

        private void LoadReportDetails(ReportItem item, string reportType)
        {
            ReportDetailItems.Clear();
            string sDate = dpStart.SelectedDate?.ToString("yyyy-MM-dd") ?? "2000-01-01";
            string eDate = dpEnd.SelectedDate?.ToString("yyyy-MM-dd") ?? "2099-12-31";
            string endDateWithTime = $"{eDate} 23:59:59";
            string sql = "";

            try
            {
                using (var conn = new SqliteConnection(StoreState.ConnectionString))
                {
                    conn.Open();

                    string baseSelect = @"SELECT IFNULL(p.article, '-'), p.name, b.name, p.product_type, 
                                          IFNULL(sz.value, '-'), oi.quantity, oi.price_at_time_of_sale, substr(o.order_date, 1, 10)
                                          FROM order_items oi
                                          JOIN orders o ON oi.order_id = o.order_id
                                          JOIN stock st ON oi.stock_id = st.stock_id
                                          JOIN products p ON st.product_id = p.product_id
                                          LEFT JOIN brands b ON p.brand_id = b.brand_id
                                          LEFT JOIN sizes sz ON st.size_id = sz.size_id
                                          LEFT JOIN categories c ON p.category_id = c.category_id 
                                          LEFT JOIN product_subtypes sub ON p.subtype_id = sub.subtype_id 
                                          LEFT JOIN users u ON o.user_id = u.user_id 
                                          LEFT JOIN customers cust ON o.user_id = cust.user_id ";

                    var cmd = new SqliteCommand("", conn);

                    if (reportType == "Аналітика по категоріях")
                    {
                        sql = baseSelect + "WHERE o.status != 'Скасовано' AND o.order_date BETWEEN @sd AND @ed AND IFNULL(c.name, 'Без категорії') = @target ";
                    }
                    else if (reportType == "Аналітика по брендах")
                    {
                        sql = baseSelect + "WHERE o.status != 'Скасовано' AND o.order_date BETWEEN @sd AND @ed AND IFNULL(b.name, 'Без бренду') = @target ";
                    }
                    else if (reportType == "Аналітика по підтипах")
                    {
                        sql = baseSelect + "WHERE o.status != 'Скасовано' AND o.order_date BETWEEN @sd AND @ed AND IFNULL(sub.name, 'Без підтипу') = @target ";
                    }
                    else if (reportType == "Статистика проданих розмірів")
                    {
                        sql = baseSelect + "WHERE o.status != 'Скасовано' AND o.order_date BETWEEN @sd AND @ed AND IFNULL(sz.value, 'Універсальний') = @target ";
                    }
                    else if (reportType == "Динаміка продажів по днях")
                    {
                        sql = baseSelect + "WHERE o.status != 'Скасовано' AND substr(o.order_date, 1, 10) = @target ";
                    }

                    cmd.Parameters.AddWithValue("@target", item.Name);
                    cmd.Parameters.AddWithValue("@sd", sDate);
                    cmd.Parameters.AddWithValue("@ed", endDateWithTime);

                    sql += "ORDER BY o.order_id DESC";
                    cmd.CommandText = sql;

                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            var detail = new ReportDetailDisplayItem
                            {
                                Article = r.GetString(0),
                                ProductName = r.GetString(1),
                                Brand = r.GetString(2),
                                ProductType = r.GetString(3),
                                Size = r.GetString(4),
                                Quantity = r.GetInt32(5),
                                SalePrice = r.GetDecimal(6),
                                SaleDate = r.GetString(7)
                            };
                            ReportDetailItems.Add(detail);
                        }
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show("Помилка завантаження деталей: " + ex.Message); }
        }

        private FlowDocument GenerateExportDocument()
        {
            FlowDocument doc = new FlowDocument
            {
                PagePadding = new Thickness(50, 50, 50, 50),
                PageWidth = 793.92,
                PageHeight = 1122.24,
                FontFamily = new FontFamily("Arial"),
                Background = Brushes.White,
                Foreground = Brushes.Black,
                ColumnWidth = 793.92
            };

            string reportType = cbReportType.SelectedItem is ComboBoxItem cbi ? cbi.Content.ToString() : "Аналітичний звіт";
            string period = $"{dpStart.SelectedDate?.ToString("dd.MM.yyyy") ?? "..."} - {dpEnd.SelectedDate?.ToString("dd.MM.yyyy") ?? "..."}";

            Table headerTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 20) };
            headerTable.Columns.Add(new TableColumn { Width = new GridLength(400) });
            headerTable.Columns.Add(new TableColumn { Width = new GridLength(290) });

            TableRowGroup hrg = new TableRowGroup();
            headerTable.RowGroups.Add(hrg);
            TableRow hr = new TableRow();
            hrg.Rows.Add(hr);

            var pLeft1 = new Paragraph(new Run("BLACKSMITH STORE")) { FontSize = 28, FontWeight = FontWeights.Black, Foreground = Brushes.Black, Margin = new Thickness(0) };
            var cellLeft = new TableCell(pLeft1) { BorderThickness = new Thickness(0) };
            hr.Cells.Add(cellLeft);

            var pRight1 = new Paragraph(new Run("ЗВІТ")) { FontSize = 16, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Right, Margin = new Thickness(0) };
            var pRight2 = new Paragraph(new Run($"Створено: {DateTime.Now:dd.MM.yyyy HH:mm}")) { FontSize = 12, Foreground = Brushes.Gray, TextAlignment = TextAlignment.Right, Margin = new Thickness(0) };
            var cellRight = new TableCell() { BorderThickness = new Thickness(0) };
            cellRight.Blocks.Add(pRight1);
            cellRight.Blocks.Add(pRight2);
            hr.Cells.Add(cellRight);

            doc.Blocks.Add(headerTable);

            doc.Blocks.Add(new Paragraph() { BorderBrush = Brushes.Black, BorderThickness = new Thickness(0, 2, 0, 0), Margin = new Thickness(0, 0, 0, 20) });

            doc.Blocks.Add(new Paragraph(new Run(reportType.ToUpper())) { FontSize = 18, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 5) });
            doc.Blocks.Add(new Paragraph(new Run($"Аналітичний період звітності: {period}")) { FontSize = 13, Foreground = Brushes.DimGray, Margin = new Thickness(0, 0, 0, 25) });

            var pKpi = new Paragraph();
            pKpi.Inlines.Add(new Run($"• Загальний фінансовий виторг: {txtTotalRevenue.Text}\n") { FontWeight = FontWeights.Bold, FontSize = 14 });
            pKpi.Inlines.Add(new Run($"• Кількість реалізованих одиниць: {txtTotalUnits.Text} шт.\n") { FontSize = 14 });
            pKpi.Inlines.Add(new Run($"• Загальна кількість транзакцій (чеків): {txtOrderCount.Text} од.") { FontSize = 14 });
            pKpi.Margin = new Thickness(0, 0, 0, 25);
            pKpi.LineHeight = 22;
            doc.Blocks.Add(pKpi);

            int currentIndex = 0;
            int firstPageLimit = 18;
            int normalPageLimit = 25;
            bool isFirstPage = true;
            var resultsList = ReportResults.ToList();

            while (currentIndex < resultsList.Count)
            {
                int currentLimit = isFirstPage ? firstPageLimit : normalPageLimit;
                var chunk = resultsList.Skip(currentIndex).Take(currentLimit).ToList();

                if (!isFirstPage)
                {
                    doc.Blocks.Add(new Paragraph(new Run("")) { BreakPageBefore = true, Margin = new Thickness(0) });
                }

                Table table = new Table { CellSpacing = 0, BorderBrush = Brushes.Black, BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 0, 10) };
                table.Columns.Add(new TableColumn { Width = new GridLength(260) });
                table.Columns.Add(new TableColumn { Width = new GridLength(180) });
                table.Columns.Add(new TableColumn { Width = new GridLength(100) });
                table.Columns.Add(new TableColumn { Width = new GridLength(150) });

                TableRowGroup mainRg = new TableRowGroup();
                table.RowGroups.Add(mainRg);

                TableRow hRowTable = new TableRow { Background = Brushes.Black };
                mainRg.Rows.Add(hRowTable);

                Action<TableRow, string, bool, TextAlignment> addCellToTable = (r, text, isH, align) => {
                    var p = new Paragraph(new Run(text)) { Margin = new Thickness(6), FontSize = 12, TextAlignment = align };
                    if (isH) { p.Foreground = Brushes.White; p.FontWeight = FontWeights.Bold; }
                    r.Cells.Add(new TableCell(p) { BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(1), Padding = new Thickness(4) });
                };

                addCellToTable(hRowTable, "Об'єкт дослідження / Назва", true, TextAlignment.Left);
                addCellToTable(hRowTable, "Параметр", true, TextAlignment.Left);
                addCellToTable(hRowTable, "Кількість", true, TextAlignment.Center);
                addCellToTable(hRowTable, "Загальний обсяг", true, TextAlignment.Right);

                int rowIndex = 0;
                foreach (var res in chunk)
                {
                    TableRow row = new TableRow();
                    if (rowIndex % 2 != 0) row.Background = Brushes.WhiteSmoke;

                    mainRg.Rows.Add(row);
                    addCellToTable(row, res.Name, false, TextAlignment.Left);
                    addCellToTable(row, res.Detail, false, TextAlignment.Left);
                    addCellToTable(row, res.Count.ToString(), false, TextAlignment.Center);

                    addCellToTable(row, $"{res.Total:N0} грн.", false, TextAlignment.Right);
                    rowIndex++;
                }

                doc.Blocks.Add(table);

                currentIndex += currentLimit;
                isFirstPage = false;
            }

            doc.Blocks.Add(new Paragraph() { BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0, 1, 0, 0), Margin = new Thickness(0, 20, 0, 5) });
            var pFooter = new Paragraph(new Run("Генерація документу виконана автоматично. Електронний звіт не потребує фізичних підписів.")) { FontSize = 10, Foreground = Brushes.DarkGray, FontStyle = FontStyles.Italic, TextAlignment = TextAlignment.Center };
            doc.Blocks.Add(pFooter);

            return doc;
        }

        private void ExportPdf_Click(object sender, RoutedEventArgs e)
        {
            if (ReportResults.Count == 0)
            {
                MessageBox.Show("Немає даних для експорту! Згенеруйте звіт.", "Увага", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            PrintDialog printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                FlowDocument doc = GenerateExportDocument();
                doc.PageHeight = printDialog.PrintableAreaHeight;
                doc.PageWidth = printDialog.PrintableAreaWidth;
                doc.ColumnWidth = printDialog.PrintableAreaWidth;

                IDocumentPaginatorSource idpSource = doc;
                printDialog.PrintDocument(idpSource.DocumentPaginator, "Аналітичний звіт");
            }
        }

        private void ExportWord_Click(object sender, RoutedEventArgs e)
        {
            if (ReportResults.Count == 0)
            {
                MessageBox.Show("Немає даних для експорту! Згенеруйте звіт.", "Увага", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Зберегти як документ Word (RTF)",
                Filter = "Word Document (*.rtf)|*.rtf",
                DefaultExt = ".rtf",
                FileName = $"Аналітика_Blacksmith_{DateTime.Now:yyyyMMdd}.rtf"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    FlowDocument doc = GenerateExportDocument();
                    TextRange range = new TextRange(doc.ContentStart, doc.ContentEnd);
                    using (FileStream fStream = new FileStream(dialog.FileName, FileMode.Create))
                    {
                        range.Save(fStream, DataFormats.Rtf);
                    }
                    MessageBox.Show("Звіт успішно збережено у форматі Word (RTF)!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка при збереженні файлу: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}