using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using praktik.Models;
using praktik.Models.Patterns;
using ZXing;
using ZXing.Common;

namespace praktik
{
    public partial class TaskQRCodeWindow : Window
    {
        private readonly WorkPlannerFacade facade = new WorkPlannerFacade();
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
                task = facade.GetTaskById(taskId);

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
                string qrData = task.GenerateQRData();
                txtQRData.Text = qrData;

                var writer = new BarcodeWriterPixelData
                {
                    Format = BarcodeFormat.QR_CODE,
                    Options = new EncodingOptions
                    {
                        Width = 280,
                        Height = 280,
                        Margin = 1
                    }
                };

                var pixelData = writer.Write(qrData);
                qrCodeBitmap = BitmapSource.Create(
                    pixelData.Width,
                    pixelData.Height,
                    96,
                    96,
                    System.Windows.Media.PixelFormats.Bgra32,
                    null,
                    pixelData.Pixels,
                    pixelData.Width * 4);

                imgQRCode.Source = qrCodeBitmap;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при генерации QR-кода: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSaveImage_Click(object sender, RoutedEventArgs e)
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

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
