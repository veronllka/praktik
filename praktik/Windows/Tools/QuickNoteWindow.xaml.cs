using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using praktik.Models.Patterns;

namespace praktik
{
    public partial class QuickNoteWindow : Window
    {
        private readonly WorkPlannerFacade facade = new WorkPlannerFacade();
        private int taskId;
        private bool hasUnsavedNote = false;

        public QuickNoteWindow(int taskId)
        {
            InitializeComponent();
            this.taskId = taskId;
            txtNote.Focus();
            this.Closing += QuickNoteWindow_Closing;
        }

        private void TxtNote_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            var text = txtNote.Text?.Trim() ?? "";
            btnSave.IsEnabled = text.Length >= 3 && text.Length <= 200;
            hasUnsavedNote = !string.IsNullOrWhiteSpace(text);
        }

        private void TxtNote_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && btnSave.IsEnabled)
            {
                BtnSave_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                BtnCancel_Click(sender, e);
                e.Handled = true;
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            TrySaveNote();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            if (hasUnsavedNote)
            {
                var result = MessageBox.Show("Сохранить черновик?", "Несохраненная заметка", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    if (TrySaveNote())
                    {
                        return;
                    }

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
                        if (!TrySaveNote(closeWindow: false))
                        {
                            e.Cancel = true;
                            return;
                        }
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

        private bool TrySaveNote(bool closeWindow = true)
        {
            var noteText = txtNote.Text?.Trim() ?? "";
            if (noteText.Length < 3 || noteText.Length > 200)
            {
                MessageBox.Show("Заметка должна содержать от 3 до 200 символов", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!TryGetProgressPercent(txtProgressPercent.Text, out var progressPercent))
            {
                txtProgressPercent.Focus();
                txtProgressPercent.SelectAll();
                return false;
            }

            try
            {
                var userId = LoginWindow.CurrentUser?.UserId ?? 0;
                if (!facade.AddTaskReport(taskId, userId, noteText, progressPercent))
                {
                    throw new InvalidOperationException("Не удалось сохранить отчет по задаче");
                }

                ShowToast("Заметка добавлена");
                hasUnsavedNote = false;
                SuggestCompletionIfNeeded(progressPercent);

                if (closeWindow)
                {
                    DialogResult = true;
                    Close();
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении заметки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void ProgressTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                e.Handled = !IsProgressInputAllowed(textBox, e.Text);
            }
        }

        private void ProgressTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!(sender is TextBox textBox))
            {
                return;
            }

            var pastedText = e.DataObject.GetData(typeof(string)) as string;
            if (!IsProgressInputAllowed(textBox, pastedText))
            {
                e.CancelCommand();
            }
        }

        private bool TryGetProgressPercent(string text, out int? progressPercent)
        {
            progressPercent = null;
            var trimmedText = text?.Trim();

            if (string.IsNullOrEmpty(trimmedText))
            {
                return true;
            }

            if (int.TryParse(trimmedText, out var value) && value >= 0 && value <= 100)
            {
                progressPercent = value;
                return true;
            }

            MessageBox.Show("Процент выполнения должен быть числом от 0 до 100", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        private bool IsProgressInputAllowed(TextBox textBox, string newText)
        {
            var candidateText = BuildCandidateText(textBox, newText);
            if (string.IsNullOrWhiteSpace(candidateText))
            {
                return true;
            }

            return int.TryParse(candidateText, out var value) && value >= 0 && value <= 100;
        }

        private string BuildCandidateText(TextBox textBox, string newText)
        {
            var currentText = textBox.Text ?? string.Empty;

            if (textBox.SelectionLength > 0)
            {
                currentText = currentText.Remove(textBox.SelectionStart, textBox.SelectionLength);
            }

            return currentText.Insert(textBox.SelectionStart, newText ?? string.Empty).Trim();
        }

        private void SuggestCompletionIfNeeded(int? progressPercent)
        {
            if (progressPercent != 100)
            {
                return;
            }

            var currentTask = facade.GetTaskById(taskId);
            if (currentTask == null ||
                string.Equals(currentTask.TaskStatus?.TaskStatusName, "Завершено", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(currentTask.TaskStatus?.TaskStatusName, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var completedStatus = facade.GetTaskStatuses().FirstOrDefault(status =>
                string.Equals(status.TaskStatusName, "Завершено", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status.TaskStatusName, "Completed", StringComparison.OrdinalIgnoreCase));

            if (completedStatus == null)
            {
                return;
            }

            var result = MessageBox.Show(
                "Указан прогресс 100%. Перевести задачу в статус «Завершено»?",
                "Завершение задачи",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            var userId = LoginWindow.CurrentUser?.UserId ?? 0;
            if (!facade.UpdateTaskStatus(taskId, completedStatus.TaskStatusId, userId, out var errorMessage))
            {
                MessageBox.Show(errorMessage ?? "Не удалось изменить статус задачи", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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






