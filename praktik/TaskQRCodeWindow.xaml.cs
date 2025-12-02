using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using QRCoder;
using praktik.Models;

namespace praktik
{
    public partial class TaskQRCodeWindow : Window
    {
        private WorkPlannerContext db = new WorkPlannerContext();
        private Models.Task task;
        private BitmapSource qrCodeBitmap;

        public TaskQRCodeWindow(int taskId)
        {
            InitializeComponent();
            LoadTask(taskId);
        }

        private void LoadTask(int taskId)
        {
            try
            {
                task = db.GetTaskById(taskId);

                if (task == null)
                {
                    MessageBox.Show("Задача не найдена", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    Close();
                    return;
                }

                LoadTaskData();
                GenerateQRCode();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке задачи: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private void LoadTaskData()
        {
            if (task == null) return;

            txtTitle.Text = task.Title;
            txtTaskId.Text = $"#{task.TaskId}";
            txtSite.Text = task.Site?.SiteName ?? "—";
            txtCrew.Text = task.Crew?.CrewName ?? "Не назначена";
            txtPriority.Text = task.Priority?.PriorityName ?? "—";
            txtPeriod.Text = $"{task.StartDate:dd.MM.yyyy} — {task.EndDate:dd.MM.yyyy}";
        }

        private void GenerateQRCode()
        {
            try
            {
                // Получаем JSON данные для QR-кода
                string qrData = task.GenerateQRData();
                txtQRData.Text = qrData;

                // Генерируем QR-код
                QRCodeGenerator qrGenerator = new QRCodeGenerator();
                QRCodeData qrCodeData = qrGenerator.CreateQrCode(qrData, QRCodeGenerator.ECCLevel.Q);
                
                // Создаем bitmap с QR-кодом
                var qrCode = new QRCode(qrCodeData);
                var qrCodeImage = qrCode.GetGraphic(20, "#000000", "#FFFFFF", true);

                // Конвертируем в BitmapSource для WPF
                qrCodeBitmap = ConvertBitmapToBitmapSource(qrCodeImage);
                imgQRCode.Source = qrCodeBitmap;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при генерации QR-кода: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private BitmapSource ConvertBitmapToBitmapSource(System.Drawing.Bitmap bitmap)
        {
            var bitmapData = bitmap.LockBits(
                new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                System.Drawing.Imaging.ImageLockMode.ReadOnly,
                bitmap.PixelFormat);

            var bitmapSource = BitmapSource.Create(
                bitmapData.Width,
                bitmapData.Height,
                96,
                96,
                PixelFormats.Bgra32,
                null,
                bitmapData.Scan0,
                bitmapData.Stride * bitmapData.Height,
                bitmapData.Stride);

            bitmap.UnlockBits(bitmapData);

            return bitmapSource;
        }

        private void btnSaveImage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveFileDialog saveDialog = new SaveFileDialog
                {
                    Filter = "PNG Image|*.png",
                    FileName = $"QRCode_Task_{task.TaskId}_{DateTime.Now:yyyyMMdd_HHmmss}.png",
                    Title = "Сохранить QR-код"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    // Сохраняем QR-код
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(qrCodeBitmap));

                    using (var fileStream = new FileStream(saveDialog.FileName, FileMode.Create))
                    {
                        encoder.Save(fileStream);
                    }

                    MessageBox.Show($"QR-код успешно сохранён:\n{saveDialog.FileName}", 
                                    "Успешно", 
                                    MessageBoxButton.OK, 
                                    MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении QR-кода: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
