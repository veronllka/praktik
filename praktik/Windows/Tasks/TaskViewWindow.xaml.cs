using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using praktik.Models;
using praktik.Models.Patterns;

namespace praktik
{
    public partial class TaskViewWindow : Window
    {
        private readonly WorkPlannerFacade facade = new WorkPlannerFacade();
        private Task task;
        private TaskReport lastReport;
        private string selectedAttachmentPath;

        public TaskViewWindow(int taskId)
        {
            InitializeComponent();
            UpdateSelectedAttachmentState();
            LoadTask(taskId);
        }

        private void LoadTask(int taskId)
        {
            if (!TaskReportAttachmentService.TryValidateSourceFile(selectedAttachmentPath, out var attachmentValidationError))
            {
                MessageBox.Show(attachmentValidationError, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

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
                LoadLastReport();
                LoadPrintJournal();
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
            txtTaskId.Text = $"ID задачи: #{task.TaskId}";
            
            txtSite.Text = $"{task.Site.SiteName}";
            if (!string.IsNullOrEmpty(task.Site.Address))
            {
                txtSite.Text += $"\n{task.Site.Address}";
            }

            if (task.Crew != null)
            {
                txtCrew.Text = task.Crew.CrewName;
                if (task.Crew.Brigadier != null)
                {
                    txtBrigadier.Text = $"Бригадир: {task.Crew.Brigadier.Username}";
                    txtBrigadier.Visibility = Visibility.Visible;
                }
                else
                {
                    txtBrigadier.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                txtCrew.Text = "Не назначена";
                txtBrigadier.Visibility = Visibility.Collapsed;
            }

            txtPeriod.Text = $"{task.StartDate:dd.MM.yyyy} — {task.EndDate:dd.MM.yyyy}";

            txtPriority.Text = task.Priority?.PriorityName ?? "—";
            txtStatus.Text = task.TaskStatus?.TaskStatusName ?? "—";
            txtLastPrintedSummary.Text = FormatLastPrintedAt(task.LastPrintedAt);

            txtDescription.Text = !string.IsNullOrEmpty(task.Description) ? task.Description : "Описание отсутствует";
            
            LoadNotes();
        }

        private void LoadLastReport()
        {
            if (task == null) return;

            var reports = facade.GetTaskReports(task.TaskId);
            lastReport = reports.FirstOrDefault(r => r.ProgressPercent.HasValue) ?? reports.FirstOrDefault();

            if (lastReport != null && (!string.IsNullOrWhiteSpace(lastReport.ReportText) || lastReport.ProgressPercent.HasValue))
            {
                pnlProgress.Visibility = Visibility.Visible;
                
                if (lastReport.ProgressPercent.HasValue)
                {
                    txtProgress.Text = $"{lastReport.ProgressPercent.Value}%";
                }
                else
                {
                    txtProgress.Text = "—";
                }

                txtComment.Text = !string.IsNullOrEmpty(lastReport.ReportText) ? lastReport.ReportText : "—";
            }
            else
            {
                pnlProgress.Visibility = Visibility.Collapsed;
            }
        }

        private void LoadNotes()
        {
            if (task == null) return;

            var reports = facade.GetTaskReports(task.TaskId);
            icNotes.ItemsSource = reports;
        }

        private void LoadPrintJournal()
        {
            if (task == null || !CanViewPrintJournal())
            {
                pnlPrintJournal.Visibility = Visibility.Collapsed;
                icPrintHistory.ItemsSource = null;
                return;
            }

            var printLogs = facade.GetTaskPrintLogs(task.TaskId);
            var lastPrintedAt = task.LastPrintedAt ?? printLogs.FirstOrDefault()?.PrintedAt;

            pnlPrintJournal.Visibility = Visibility.Visible;
            txtLastPrintedAt.Text = FormatLastPrintedAt(lastPrintedAt);
            txtLastPrintedSummary.Text = FormatLastPrintedAt(lastPrintedAt);

            icPrintHistory.ItemsSource = printLogs;
            txtPrintHistoryEmpty.Visibility = printLogs.Count > 0
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private string FormatLastPrintedAt(DateTime? printedAt)
        {
            return printedAt.HasValue
                ? printedAt.Value.ToString("dd.MM.yyyy HH:mm")
                : "Не печаталась";
        }

        private bool CanViewPrintJournal()
        {
            var role = (LoginWindow.CurrentUser?.Role ?? string.Empty).Trim().ToLowerInvariant();

            return role == "администратор"
                || role == "админ"
                || role == "admin"
                || role == "administrator"
                || role == "диспетчер"
                || role == "dispatcher";
        }

        private void TxtNewNote_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            var text = txtNewNote.Text?.Trim() ?? "";
            btnSaveNote.IsEnabled = text.Length >= 3 && text.Length <= 500;
        }

        private void BtnSaveNote_Click(object sender, RoutedEventArgs e)
        {
            if (task == null) return;

            var noteText = txtNewNote.Text?.Trim() ?? "";
            
            if (noteText.Length < 3 || noteText.Length > 500)
            {
                MessageBox.Show("Заметка должна содержать от 3 до 500 символов", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!TryGetProgressPercent(txtProgressPercent.Text, out var progressPercent))
            {
                txtProgressPercent.Focus();
                txtProgressPercent.SelectAll();
                return;
            }

            if (!TaskReportAttachmentService.TryValidateSourceFile(selectedAttachmentPath, out var attachmentValidationError))
            {
                MessageBox.Show(attachmentValidationError, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var userId = LoginWindow.CurrentUser?.UserId ?? 0;
                if (!facade.AddTaskReport(task.TaskId, userId, noteText, progressPercent, selectedAttachmentPath))
                {
                    throw new InvalidOperationException("Не удалось сохранить отчет по задаче");
                }

                ClearNoteInputs();
                SuggestCompletionIfNeeded(progressPercent);
                LoadNotes();
                LoadLastReport();
                
                MessageBox.Show("Заметка добавлена", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении заметки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancelNote_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtNewNote.Text))
            {
                var result = MessageBox.Show("Отменить ввод заметки?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    ClearNoteInputs();
                }

                return;
            }

            if (!string.IsNullOrWhiteSpace(selectedAttachmentPath))
            {
                ClearNoteInputs();
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

        private void ClearNoteInputs()
        {
            txtNewNote.Clear();
            txtProgressPercent.Clear();
            selectedAttachmentPath = null;
            UpdateSelectedAttachmentState();
        }

        private void BtnAttachFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Выберите вложение",
                Filter = TaskReportAttachmentService.BuildFileDialogFilter(),
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            if (!TaskReportAttachmentService.TryValidateSourceFile(dialog.FileName, out var errorMessage))
            {
                MessageBox.Show(errorMessage, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            selectedAttachmentPath = dialog.FileName;
            UpdateSelectedAttachmentState();
        }

        private void BtnRemoveAttachment_Click(object sender, RoutedEventArgs e)
        {
            selectedAttachmentPath = null;
            UpdateSelectedAttachmentState();
        }

        private void UpdateSelectedAttachmentState()
        {
            if (txtSelectedAttachment == null || btnRemoveAttachment == null)
            {
                return;
            }

            var hasAttachment = !string.IsNullOrWhiteSpace(selectedAttachmentPath);
            txtSelectedAttachment.Text = hasAttachment
                ? TaskReportAttachmentService.GetAttachmentFileName(selectedAttachmentPath)
                : "Вложение не выбрано";
            txtSelectedAttachment.ToolTip = hasAttachment ? selectedAttachmentPath : null;
            btnRemoveAttachment.Visibility = hasAttachment ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnOpenAttachment_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is FrameworkElement element) || !(element.DataContext is TaskReport report))
            {
                return;
            }

            if (!TaskReportAttachmentService.TryOpenAttachment(report.AttachmentUrl, out var errorMessage))
            {
                MessageBox.Show(errorMessage, "Вложение", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void SuggestCompletionIfNeeded(int? progressPercent)
        {
            if (task == null || progressPercent != 100 || IsTaskCompleted())
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
            if (!facade.UpdateTaskStatus(task.TaskId, completedStatus.TaskStatusId, userId, out var errorMessage))
            {
                MessageBox.Show(errorMessage ?? "Не удалось изменить статус задачи", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            task.TaskStatusId = completedStatus.TaskStatusId;
            task.TaskStatus = completedStatus;
            txtStatus.Text = completedStatus.TaskStatusName;
        }

        private bool IsTaskCompleted()
        {
            return string.Equals(task?.TaskStatus?.TaskStatusName, "Завершено", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(task?.TaskStatus?.TaskStatusName, "Completed", StringComparison.OrdinalIgnoreCase);
        }

        private void BtnShowQR_Click(object sender, RoutedEventArgs e)
        {
            if (task != null)
            {
                var qrWindow = new TaskQRCodeWindow(task.TaskId);
                qrWindow.ShowDialog();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
