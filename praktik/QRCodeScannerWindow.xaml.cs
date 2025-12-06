using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using praktik.Models;
using ZXing;

namespace praktik
{
    public partial class QRCodeScannerWindow : Window
    {
        public int? TaskId { get; private set; }
        public bool ShouldHighlightTask { get; private set; }
        private readonly WorkPlannerContext db;

        public QRCodeScannerWindow()
        {
            InitializeComponent();
            db = new WorkPlannerContext();
        }

        private void ShowTaskPreview(int taskId)
        {
            try
            {
                var task = db.GetTaskById(taskId);

                if (task != null)
                {
                    txtError.Text = $"✓ Задача найдена: {task.Title} (ID: {taskId})";
                    txtError.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(46, 125, 50));
                    statusBorder.Visibility = Visibility.Visible;
                }
                else
                {
                    txtError.Text = $"⚠ Задача с ID {taskId} не найдена в базе данных";
                    txtError.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(237, 108, 2));
                    statusBorder.Visibility = Visibility.Visible;
                }
            }
            catch
            {
            }
        }

        private void btnOpenTask_Click(object sender, RoutedEventArgs e)
        {
            if (!TaskId.HasValue)
            {
                MessageBox.Show("Введите корректный QR-код задачи", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var db = new WorkPlannerContext();
                var task = db.GetTaskById(TaskId.Value);

                if (task == null)
                {
                    MessageBox.Show("Задача не найдена", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии задачи: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void btnSelectImage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog openDialog = new OpenFileDialog
                {
                    Filter = "Изображения|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Все файлы|*.*",
                    Title = "Выберите изображение с QR-кодом"
                };

                if (openDialog.ShowDialog() == true)
                {
                    DecodeQRCodeFromImage(openDialog.FileName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при выборе изображения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DecodeQRCodeFromImage(string imagePath)
        {
            try
            {
                // загружаем изображение
                BitmapImage bitmapImage = new BitmapImage(new Uri(imagePath));
                
                // конвертируем в WritableBitmap для ZXing
                WriteableBitmap writeableBitmap = new WriteableBitmap(bitmapImage);
                
                var reader = new BarcodeReader
                {
                    AutoRotate = true,
                    Options = new ZXing.Common.DecodingOptions
                    {
                        TryHarder = true,
                        TryInverted = true,
                        PossibleFormats = new[] { BarcodeFormat.QR_CODE }
                    }
                };

                // декодируем QR код
                var result = reader.Decode(writeableBitmap);

                if (result != null)
                {
                    // парсим результат напрямую
                    string qrText = result.Text.Trim();
                    statusBorder.Visibility = Visibility.Collapsed;
                    btnOpenTask.IsEnabled = false;

                    if (qrText.StartsWith("{") && qrText.Contains("\"type\":\"TASK\""))
                    {
                        var idMatch = System.Text.RegularExpressions.Regex.Match(qrText, @"""id"":(\d+)");
                        if (idMatch.Success && int.TryParse(idMatch.Groups[1].Value, out int jsonTaskId))
                        {
                            TaskId = jsonTaskId;
                            btnOpenTask.IsEnabled = true;
                            ShowTaskPreview(jsonTaskId);
                            return;
                        }
                    }

                    if (qrText.StartsWith("TASK:", StringComparison.OrdinalIgnoreCase))
                    {
                        string taskIdStr = qrText.Substring(5).Trim();
                        if (int.TryParse(taskIdStr, out int taskId))
                        {
                            TaskId = taskId;
                            btnOpenTask.IsEnabled = true;
                            ShowTaskPreview(taskId);
                            return;
                        }
                    }

                    if (int.TryParse(qrText, out int directTaskId))
                    {
                        TaskId = directTaskId;
                        btnOpenTask.IsEnabled = true;
                        ShowTaskPreview(directTaskId);
                        return;
                    }

                    txtError.Text = "QR-код не содержит корректных данных задачи";
                    txtError.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(211, 47, 47));
                    statusBorder.Visibility = Visibility.Visible;
                }
                else
                {
                    txtError.Text = "QR-код не найден на изображении. Попробуйте другое изображение.";
                    txtError.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(211, 47, 47));
                    statusBorder.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                txtError.Text = $"Ошибка при распознавании QR-кода: {ex.Message}";
                txtError.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(211, 47, 47));
                statusBorder.Visibility = Visibility.Visible;
            }
        }
    }
}

