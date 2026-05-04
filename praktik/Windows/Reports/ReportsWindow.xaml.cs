using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using praktik.Models;
using praktik.Models.Patterns;

namespace praktik
{
    public partial class ReportsWindow : Window
    {
        private readonly WorkPlannerFacade facade = new WorkPlannerFacade();
        private readonly List<ChartPoint> currentData = new List<ChartPoint>();

        public ReportsWindow()
        {
            InitializeComponent();
            Loaded += (_, __) => GenerateReport();
            SizeChanged += (_, __) => RedrawChart();
        }

        private void BtnGenerate_Click(object sender, RoutedEventArgs e) => GenerateReport();

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (currentData == null || currentData.Count == 0)
            {
                MessageBox.Show("Нет данных для экспорта");
                return;
            }

            var format = (cbExportFormat.SelectedItem as ComboBoxItem)?.Content?.ToString();
            string filter;
            string defaultExt;
            switch (format)
            {
                case "JPEG":
                    filter = "JPEG Image|*.jpg";
                    defaultExt = "jpg";
                    break;
                case "CSV":
                    filter = "CSV файл|*.csv";
                    defaultExt = "csv";
                    break;
                default:
                    filter = "PNG Image|*.png";
                    defaultExt = "png";
                    break;
            }

            var dialog = new SaveFileDialog
            {
                Filter = filter,
                FileName = $"report.{defaultExt}"
            };

            if (dialog.ShowDialog() == true)
            {
                if (format == "CSV")
                {
                    ExportToCsv(dialog.FileName);
                }
                else
                {
                    ExportToImage(dialog.FileName, format == "JPEG");
                }
                MessageBox.Show("Отчёт сохранён");
            }
        }

        private void GenerateReport()
        {
            currentData.Clear();

            var reportType = (cbReportType.SelectedItem as ComboBoxItem)?.Content?.ToString();
            switch (reportType)
            {
                case "Пользователи по ролям":
                    currentData.AddRange(GetUsersByRole());
                    txtTitle.Text = "Пользователи по ролям";
                    break;
                case "Задачи по объектам":
                    currentData.AddRange(GetTasksBySite());
                    txtTitle.Text = "Задачи по объектам";
                    break;
                case "Задачи по бригадам":
                    currentData.AddRange(GetTasksByCrew());
                    txtTitle.Text = "Задачи по бригадам";
                    break;
                case "Просроченные задачи":
                    currentData.AddRange(GetOverdueTasksBySite());
                    txtTitle.Text = "Просроченные задачи по объектам";
                    break;
            }

            UpdateEmptyState();
            RedrawChart();
        }

        private List<ChartPoint> GetUsersByRole()
        {
            var users = facade.GetUsers() ?? new List<User>();
            return users
                .GroupBy(u => string.IsNullOrWhiteSpace(u.Role) ? "Не указана" : u.Role)
                .Select(g => new ChartPoint { Label = g.Key, Value = g.Count() })
                .OrderByDescending(p => p.Value)
                .ToList();
        }

        private List<ChartPoint> GetTasksBySite()
        {
            var tasks = facade.GetTasks() ?? new List<Models.Task>();
            return tasks.GroupBy(t => t.Site?.SiteName ?? "Не указан")
                        .Select(g => new ChartPoint { Label = g.Key, Value = g.Count() })
                        .OrderByDescending(p => p.Value)
                        .ToList();
        }

        private List<ChartPoint> GetTasksByCrew()
        {
            var tasks = facade.GetTasks() ?? new List<Models.Task>();
            return tasks.GroupBy(t => t.Crew?.CrewName ?? "Не назначена")
                        .Select(g => new ChartPoint { Label = g.Key, Value = g.Count() })
                        .OrderByDescending(p => p.Value)
                        .ToList();
        }

        private List<ChartPoint> GetOverdueTasksBySite()
        {
            var tasks = facade.GetTasks() ?? new List<Models.Task>();
            var now = DateTime.Now;
            return tasks.Where(t => t.EndDate < now && t.TaskStatus?.TaskStatusName != "Завершено")
                        .GroupBy(t => t.Site?.SiteName ?? "Не указан")
                        .Select(g => new ChartPoint { Label = g.Key, Value = g.Count() })
                        .OrderByDescending(p => p.Value)
                        .ToList();
        }

        private void RedrawChart()
        {
            if (ChartCanvas == null) return;

            ChartCanvas.Children.Clear();

            if (currentData == null || currentData.Count == 0)
            {
                UpdateEmptyState();
                return;
            }

            var chartType = (cbChartType.SelectedItem as ComboBoxItem)?.Content?.ToString();
            switch (chartType)
            {
                case "Круговая":
                    DrawPieChart();
                    break;
                case "Гистограмма (вертикальная)":
                    DrawColumnChart();
                    break;
                default:
                    DrawBarChart();
                    break;
            }
        }

        private void DrawBarChart()
        {
            double width = Math.Max(ChartBorder.ActualWidth - 48, 480);
            double padding = 40;
            double barHeight = 32;
            double gap = 14;
            var palette = GetChartPalette();

            double maxVal = Math.Max(1, currentData.Max(p => p.Value));
            double chartHeight = currentData.Count * (barHeight + gap);
            double startY = padding;

            ChartCanvas.Width = width;
            ChartCanvas.Height = chartHeight + padding * 2;

            int index = 0;
            foreach (var point in currentData)
            {
                double barWidth = ((width - padding * 2) * point.Value) / maxVal;
                double y = startY + index * (barHeight + gap);

                var rect = new Rectangle
                {
                    Width = barWidth,
                    Height = barHeight,
                    RadiusX = 4,
                    RadiusY = 4,
                    Fill = palette[index % palette.Length]
                };
                Canvas.SetLeft(rect, padding);
                Canvas.SetTop(rect, y);
                ChartCanvas.Children.Add(rect);

                var label = new TextBlock
                {
                    Text = point.Label,
                    Foreground = GetThemeBrush("AppTextBrush", "#F5F7FB"),
                    FontSize = 13,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Canvas.SetLeft(label, 0);
                Canvas.SetTop(label, y + (barHeight - 16) / 2);
                ChartCanvas.Children.Add(label);

                var valueText = new TextBlock
                {
                    Text = point.Value.ToString(),
                    Foreground = GetThemeBrush("AppAccentTextBrush", "#FFFFFF"),
                    FontWeight = FontWeights.Bold,
                    FontSize = 12
                };
                Canvas.SetLeft(valueText, padding + barWidth - 24);
                Canvas.SetTop(valueText, y + (barHeight - 16) / 2);
                ChartCanvas.Children.Add(valueText);

                index++;
            }
        }

        private void DrawPieChart()
        {
            double size = Math.Min(Math.Max(ChartBorder.ActualWidth - 48, 340), Math.Max(ChartBorder.ActualHeight - 48, 340));
            double radius = (size - 80) / 2;
            Point center = new Point(size / 2, size / 2);
            var palette = GetChartPalette();

            ChartCanvas.Width = size;
            ChartCanvas.Height = size;

            double total = Math.Max(1, currentData.Sum(p => p.Value));
            double startAngle = 0;

            for (int colorIndex = 0; colorIndex < currentData.Count; colorIndex++)
            {
                var point = currentData[colorIndex];
                double sweep = (point.Value / total) * 360;
                var path = CreatePieSlice(center, radius, startAngle, sweep, palette[colorIndex % palette.Length]);
                ChartCanvas.Children.Add(path);

                var label = new TextBlock
                {
                    Text = $"{point.Label} ({point.Value})",
                    Foreground = GetThemeBrush("AppTextBrush", "#F5F7FB"),
                    FontSize = 12
                };
                double midAngle = (startAngle + sweep / 2) * Math.PI / 180;
                double labelRadius = radius + 20;
                Canvas.SetLeft(label, center.X + labelRadius * Math.Cos(midAngle) - 20);
                Canvas.SetTop(label, center.Y + labelRadius * Math.Sin(midAngle) - 10);
                ChartCanvas.Children.Add(label);

                startAngle += sweep;
            }
        }

        private void DrawColumnChart()
        {
            double width = Math.Max(ChartBorder.ActualWidth - 48, 420);
            double height = Math.Max(ChartBorder.ActualHeight - 120, 280);
            double padding = 50;
            double barWidth = Math.Max(24, (width - padding * 2) / Math.Max(1, currentData.Count) - 16);
            double maxVal = Math.Max(1, currentData.Max(p => p.Value));
            var palette = GetChartPalette();

            ChartCanvas.Width = width;
            ChartCanvas.Height = height;

            for (int i = 0; i < currentData.Count; i++)
            {
                var point = currentData[i];
                double normalizedHeight = ((height - padding * 2) * point.Value) / maxVal;
                double x = padding + i * (barWidth + 16);
                double y = height - padding - normalizedHeight;

                var rect = new Rectangle
                {
                    Width = barWidth,
                    Height = normalizedHeight,
                    RadiusX = 4,
                    RadiusY = 4,
                    Fill = palette[i % palette.Length]
                };
                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, y);
                ChartCanvas.Children.Add(rect);

                var valueText = new TextBlock
                {
                    Text = point.Value.ToString(),
                    Foreground = GetThemeBrush("AppAccentTextBrush", "#FFFFFF"),
                    FontWeight = FontWeights.Bold,
                    FontSize = 12
                };
                Canvas.SetLeft(valueText, x + (barWidth - 16) / 2);
                Canvas.SetTop(valueText, y + 4);
                ChartCanvas.Children.Add(valueText);

                var label = new TextBlock
                {
                    Text = point.Label,
                    Foreground = GetThemeBrush("AppTextBrush", "#F5F7FB"),
                    FontSize = 12,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Width = barWidth + 12
                };
                Canvas.SetLeft(label, x - 6);
                Canvas.SetTop(label, height - padding + 4);
                ChartCanvas.Children.Add(label);
            }
        }

        private Path CreatePieSlice(Point center, double radius, double startAngle, double sweepAngle, Brush fill)
        {
            double startRad = startAngle * Math.PI / 180;
            double endRad = (startAngle + sweepAngle) * Math.PI / 180;

            Point startPoint = new Point(center.X + radius * Math.Cos(startRad), center.Y + radius * Math.Sin(startRad));
            Point endPoint = new Point(center.X + radius * Math.Cos(endRad), center.Y + radius * Math.Sin(endRad));

            bool isLarge = sweepAngle > 180;

            var segment = new PathFigure
            {
                StartPoint = center,
                Segments = new PathSegmentCollection
                {
                    new LineSegment(startPoint, true),
                    new ArcSegment(endPoint, new Size(radius, radius), 0, isLarge, SweepDirection.Clockwise, true),
                    new LineSegment(center, true)
                }
            };

            var geometry = new PathGeometry();
            geometry.Figures.Add(segment);

            return new Path
            {
                Fill = fill,
                Stroke = GetThemeBrush("AppSurfaceBrush", "#1C2027"),
                StrokeThickness = 2,
                Data = geometry
            };
        }

        private SolidColorBrush[] GetChartPalette()
        {
            return new[]
            {
                GetThemeBrush("AppChartBrush1", "#8D6E63"),
                GetThemeBrush("AppChartBrush2", "#B88B5B"),
                GetThemeBrush("AppChartBrush3", "#A9877D"),
                GetThemeBrush("AppChartBrush4", "#6A5148"),
                GetThemeBrush("AppChartBrush5", "#8E765E"),
                GetThemeBrush("AppChartBrush6", "#C7A88D")
            };
        }

        private SolidColorBrush GetThemeBrush(string resourceKey, string fallbackHex)
        {
            return AppThemeManager.ResolveBrush(resourceKey, fallbackHex);
        }

        private void ExportToImage(string path, bool jpeg)
        {
            var target = ChartBorder;
            double width = target.ActualWidth;
            double height = target.ActualHeight;
            if (width < 1 || height < 1) return;

            var rtb = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
            var dv = new DrawingVisual();
            using (var ctx = dv.RenderOpen())
            {
                var vb = new VisualBrush(target);
                ctx.DrawRectangle(vb, null, new Rect(new Point(), new Size(width, height)));
            }
            rtb.Render(dv);

            BitmapEncoder encoder = jpeg ? (BitmapEncoder)new JpegBitmapEncoder() : new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using (var stream = System.IO.File.Create(path))
            {
                encoder.Save(stream);
            }
        }

        private void ExportToCsv(string path)
        {
            var lines = new List<string> { "Label;Value" };
            lines.AddRange(currentData.Select(p => $"{p.Label};{p.Value}"));
            System.IO.File.WriteAllLines(path, lines);
        }

        private void UpdateEmptyState()
        {
            if (EmptyState == null || ChartCanvas == null) return;
            bool hasData = currentData != null && currentData.Count > 0;
            EmptyState.Visibility = hasData ? Visibility.Collapsed : Visibility.Visible;
            ChartCanvas.Visibility = hasData ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public class ChartPoint
    {
        public string Label { get; set; }
        public int Value { get; set; }
    }
}
