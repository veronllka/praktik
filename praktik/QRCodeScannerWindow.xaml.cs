using System;
using System.Windows;
using System.Windows.Input;
using praktik.Models;

namespace praktik
{
    public partial class QRCodeScannerWindow : Window
    {
        public int? TaskId { get; private set; }

        public QRCodeScannerWindow()
        {
            InitializeComponent();
            txtQRCode.Focus();
        }

        private void txtQRCode_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            ValidateQRCode();
        }

        private void txtQRCode_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && btnOpenTask.IsEnabled)
            {
                btnOpenTask_Click(sender, e);
            }
        }

        private void ValidateQRCode()
        {
            string qrText = txtQRCode.Text.Trim();
            txtError.Visibility = Visibility.Collapsed;
            btnOpenTask.IsEnabled = false;

            if (string.IsNullOrEmpty(qrText))
            {
                return;
            }

            if (qrText.StartsWith("TASK:", StringComparison.OrdinalIgnoreCase))
            {
                string taskIdStr = qrText.Substring(5).Trim();
                if (int.TryParse(taskIdStr, out int taskId))
                {
                    TaskId = taskId;
                    btnOpenTask.IsEnabled = true;
                    return;
                }
            }

            if (qrText.StartsWith("task:", StringComparison.OrdinalIgnoreCase))
            {
                string taskIdStr = qrText.Substring(5).Trim();
                if (int.TryParse(taskIdStr, out int taskId))
                {
                    TaskId = taskId;
                    btnOpenTask.IsEnabled = true;
                    return;
                }
            }

            if (int.TryParse(qrText, out int directTaskId))
            {
                TaskId = directTaskId;
                btnOpenTask.IsEnabled = true;
                return;
            }

            txtError.Text = "Неверный формат QR-кода. Ожидается: число (ID задачи) или task:123";
            txtError.Visibility = Visibility.Visible;
            TaskId = null;
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
    }
}

