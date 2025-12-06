using System;
using System.Windows;
using System.Windows.Input;
using praktik.Models;

namespace praktik
{
    public partial class QuickNoteWindow : Window
    {
        private WorkPlannerContext db = new WorkPlannerContext();
        private int taskId;
        private bool hasUnsavedNote = false;

        public QuickNoteWindow(int taskId)
        {
            InitializeComponent();
            this.taskId = taskId;
            txtNote.Focus();
            this.Closing += QuickNoteWindow_Closing;
        }

        private void txtNote_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            var text = txtNote.Text?.Trim() ?? "";
            btnSave.IsEnabled = text.Length >= 3 && text.Length <= 200;
            hasUnsavedNote = !string.IsNullOrWhiteSpace(text);
        }

        private void txtNote_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && btnSave.IsEnabled)
            {
                btnSave_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                btnCancel_Click(sender, e);
                e.Handled = true;
            }
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            var noteText = txtNote.Text?.Trim() ?? "";
            
            if (noteText.Length < 3 || noteText.Length > 200)
            {
                MessageBox.Show("Заметка должна содержать от 3 до 200 символов", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var userId = LoginWindow.CurrentUser?.UserId ?? 0;
                db.AddTaskReport(taskId, userId, noteText);

                ShowToast("Заметка добавлена");
                
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении заметки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            if (hasUnsavedNote)
            {
                var result = MessageBox.Show("Сохранить черновик?", "Несохраненная заметка", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    btnSave_Click(sender, e);
                    return;
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    return;
                }
            }

            DialogResult = false;
            Close();
        }

        private void QuickNoteWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (hasUnsavedNote)
            {
                var result = MessageBox.Show("Сохранить черновик заметки?", "Несохраненная заметка", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    if (btnSave.IsEnabled)
                    {
                        btnSave_Click(null, null);
                    }
                    else
                    {
                        e.Cancel = true;
                        return;
                    }
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
            }
        }

        private void ShowToast(string message)
        {
            var toast = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent,
                ShowInTaskbar = false,
                Topmost = true,
                SizeToContent = SizeToContent.WidthAndHeight,
                ResizeMode = ResizeMode.NoResize
            };

            var border = new System.Windows.Controls.Border
            {
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(240, 50, 50, 50)),
                CornerRadius = new System.Windows.CornerRadius(8),
                Padding = new Thickness(20, 15, 20, 15),
                Margin = new Thickness(10)
            };

            var textBlock = new System.Windows.Controls.TextBlock
            {
                Text = message,
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 14
            };

            border.Child = textBlock;
            toast.Content = border;

            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;
            toast.Left = screenWidth - 350;
            toast.Top = 100;

            toast.Show();

            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(2);
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                toast.Close();
            };
            timer.Start();
        }
    }
}


