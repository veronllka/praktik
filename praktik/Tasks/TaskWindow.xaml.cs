using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using praktik.Models;
using praktik.Models.Patterns;

namespace praktik
{
    public partial class TaskWindow : Window
    {
        private readonly WorkPlannerFacade facade = new WorkPlannerFacade();
        private Task task;
        private readonly Task duplicatedFromTask;
        private int? copiedLabelId;
        private ObservableCollection<MaterialRequestDisplay> materialRequests;
        private bool hasUnsavedNote = false;
        public int? SavedTaskId { get; private set; }

        public TaskWindow(Task task = null, bool isDuplicateMode = false)
        {
            InitializeComponent();
            duplicatedFromTask = isDuplicateMode ? task : null;
            this.task = isDuplicateMode ? null : task;
            copiedLabelId = task?.LabelId;

            materialRequests = new ObservableCollection<MaterialRequestDisplay>();
            dgMaterialRequests.ItemsSource = materialRequests;
            
            LoadData();

            if (this.task != null)
            {
                LoadTaskData();
                LoadMaterialRequests();
                CheckAwaitMTSLabel();
                btnPrint.Visibility = Visibility.Visible;
                LoadEvents();
                CheckQuickNotePermissions();
            }
            else if (isDuplicateMode && duplicatedFromTask != null)
            {
                LoadTaskData(duplicatedFromTask);
                ApplyDuplicateDates(duplicatedFromTask);
                Title = "Дублирование задачи";
            }
            else
            {
                dpStartDate.SelectedDate = DateTime.Now;
                dpEndDate.SelectedDate = DateTime.Now.AddDays(7);
            }

            this.Closing += TaskWindow_Closing;
        }

        private void LoadData()
        {
            cbSites.ItemsSource = facade.GetSites();
            cbCrews.ItemsSource = facade.GetCrews();
            cbPriorities.ItemsSource = facade.GetPriorities();
        }

        private void LoadTaskData()
        {
            if (task == null) return;

            LoadTaskData(task);
        }

        private void LoadTaskData(Task sourceTask)
        {
            if (sourceTask == null) return;

            txtTitle.Text = sourceTask.Title;
            txtDescription.Text = sourceTask.Description;
            dpStartDate.SelectedDate = sourceTask.StartDate;
            dpEndDate.SelectedDate = sourceTask.EndDate;

            cbSites.SelectedItem = (cbSites.ItemsSource as IEnumerable<Site>)?
                .FirstOrDefault(s => s.SiteId == sourceTask.SiteId);

            cbCrews.SelectedItem = sourceTask.CrewId.HasValue
                ? (cbCrews.ItemsSource as IEnumerable<Crew>)?.FirstOrDefault(c => c.CrewId == sourceTask.CrewId.Value)
                : null;

            cbPriorities.SelectedItem = (cbPriorities.ItemsSource as IEnumerable<Priority>)?
                .FirstOrDefault(p => p.PriorityId == sourceTask.PriorityId);
        }

        private void LoadMaterialRequests()
        {
            materialRequests.Clear();
            var requests = facade.GetMaterialRequests(task?.TaskId);
            foreach (var req in requests)
            {
                materialRequests.Add(new MaterialRequestDisplay(req));
            }
        }

        private void CheckAwaitMTSLabel()
        {
            if (task == null) return;

            var hasAwaitingRequest = materialRequests.Any(r =>
                r.Status == "Submitted" || r.Status == "Approved" || r.Status == "Issued");

            borderAwaitMTS.Visibility = hasAwaitingRequest ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ApplyDuplicateDates(Task sourceTask)
        {
            if (sourceTask == null) return;

            var duration = (sourceTask.EndDate.Date - sourceTask.StartDate.Date).Days;
            if (duration < 1)
            {
                duration = 7;
            }

            dpStartDate.SelectedDate = DateTime.Today;
            dpEndDate.SelectedDate = DateTime.Today.AddDays(duration);
        }

        private int ResolveInitialStatusId()
        {
            var statuses = facade.GetTaskStatuses();
            var newStatus = statuses.FirstOrDefault(s => string.Equals(s.TaskStatusName, "Новая", StringComparison.OrdinalIgnoreCase))
                         ?? statuses.FirstOrDefault(s => string.Equals(s.TaskStatusName, "New", StringComparison.OrdinalIgnoreCase));

            if (newStatus != null)
            {
                return newStatus.TaskStatusId;
            }

            return facade.GetNewTaskStatusId("Новая");
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtTitle.Text) || cbSites.SelectedItem == null ||
                cbCrews.SelectedItem == null ||
                cbPriorities.SelectedItem == null || dpStartDate.SelectedDate == null ||
                dpEndDate.SelectedDate == null)
            {
                MessageBox.Show("Заполните все обязательные поля");
                return;
            }

            if (dpStartDate.SelectedDate > dpEndDate.SelectedDate)
            {
                MessageBox.Show("Дата окончания должна быть позже даты начала");
                return;
            }

            try
            {
                if (task == null)
                {
                    task = new Task
                    {
                        Title = txtTitle.Text,
                        Description = txtDescription.Text,
                        StartDate = dpStartDate.SelectedDate.Value,
                        EndDate = dpEndDate.SelectedDate.Value,
                        SiteId = (cbSites.SelectedItem is Site site) ? site.SiteId : 0,
                        CrewId = cbCrews.SelectedItem is Crew crew ? (int?)crew.CrewId : null,
                        PriorityId = (cbPriorities.SelectedItem is Priority priority) ? priority.PriorityId : 0,
                        TaskStatusId = ResolveInitialStatusId(),
                        LabelId = copiedLabelId,
                        CreatedBy = LoginWindow.CurrentUser.UserId,
                        CreatedAt = DateTime.Now
                    };

                    facade.AddTask(task);
                    SavedTaskId = task.TaskId > 0 ? (int?)task.TaskId : null;
                }
                else
                {
                    task.Title = txtTitle.Text;
                    task.Description = txtDescription.Text;
                    task.StartDate = dpStartDate.SelectedDate.Value;
                    task.EndDate = dpEndDate.SelectedDate.Value;
                    task.SiteId = (cbSites.SelectedItem is Site site) ? site.SiteId : 0;
                    task.CrewId = cbCrews.SelectedItem is Crew crew ? (int?)crew.CrewId : null;
                    task.PriorityId = (cbPriorities.SelectedItem is Priority priority) ? priority.PriorityId : 0;
                    task.UpdatedAt = DateTime.Now;

                    facade.UpdateTask(task);
                    SavedTaskId = task.TaskId;
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}");
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnNewRequest_Click(object sender, RoutedEventArgs e)
        {
            if (task == null)
            {
                MessageBox.Show("Сначала сохраните задачу");
                return;
            }

            var window = new MaterialRequestEditWindow(task.TaskId);
            if (window.ShowDialog() == true)
            {
                LoadMaterialRequests();
                CheckAwaitMTSLabel();
            }
        }

        private void BtnEditRequest_Click(object sender, RoutedEventArgs e)
        {
            if (dgMaterialRequests.SelectedItem is MaterialRequestDisplay display)
            {
                var request = facade.GetMaterialRequests(task.TaskId, null).FirstOrDefault(r => r.RequestId == display.RequestId);
                if (request == null)
                {
                    MessageBox.Show("Заявка не найдена");
                    return;
                }

                if (request.Status != "Draft" && LoginWindow.CurrentUser.Role != "Диспетчер")
                {
                    MessageBox.Show("Редактирование возможно только для черновиков");
                    return;
                }

                var window = new MaterialRequestEditWindow(task.TaskId, request);
                if (window.ShowDialog() == true)
                {
                    LoadMaterialRequests();
                    CheckAwaitMTSLabel();
                }
            }
            else
            {
                MessageBox.Show("Выберите заявку для редактирования");
            }
        }

        private void BtnViewRequestHistory_Click(object sender, RoutedEventArgs e)
        {
            if (task == null)
            {
                MessageBox.Show("Сначала сохраните задачу");
                return;
            }

            var reports = facade.GetTaskReports(task.TaskId);
            var materialReports = reports.Where(r => 
                r.ReportText != null && (
                    r.ReportText.Contains("заявк") || 
                    r.ReportText.Contains("материал") ||
                    r.ReportText.Contains("МТС")
                )).ToList();

            if (materialReports.Count == 0)
            {
                MessageBox.Show("История по заявкам пуста");
                return;
            }

            var history = string.Join("\n", materialReports.Select(r => 
                $"{r.ReportedAt:dd.MM.yyyy HH:mm} - {r.ReportText} ({r.ReporterName})"));
            
            MessageBox.Show(history, "История по заявкам", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void DgMaterialRequests_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            btnEditRequest.IsEnabled = dgMaterialRequests.SelectedItem != null;
        }

        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            if (task == null)
            {
                MessageBox.Show("Сначала сохраните задачу");
                return;
            }

            var printWindow = new TaskPrintPreviewWindow(task.TaskId);
            printWindow.ShowDialog();
        }

        private void CheckQuickNotePermissions()
        {
            if (task == null) return;

            var user = LoginWindow.CurrentUser;
            if (user != null && (user.Role == "Бригадир" || user.Role == "Диспетчер" || user.Role == "Админ" || user.Role == "Администратор"))
            {
                btnAddQuickNote.Visibility = Visibility.Visible;
            }
        }

        private void LoadEvents()
        {
            if (task == null) return;

            var reports = facade.GetTaskReports(task.TaskId);
            var events = new List<EventDisplay>();

            foreach (var report in reports)
            {
                events.Add(new EventDisplay
                {
                    DisplayText = GetEventDisplayText(report),
                    TimeInfo = $"{report.ReporterName ?? "Система"} • {report.ReportedAt:dd.MM.yyyy HH:mm}"
                });
            }

            lbEvents.ItemsSource = events;
        }

        private void BtnAddQuickNote_Click(object sender, RoutedEventArgs e)
        {
            if (task == null)
            {
                MessageBox.Show("Сначала сохраните задачу");
                return;
            }

            pnlQuickNote.Visibility = Visibility.Visible;
            btnAddQuickNote.Visibility = Visibility.Collapsed;
            txtQuickNote.Focus();
        }

        private void TxtQuickNote_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            var text = txtQuickNote.Text?.Trim() ?? "";
            btnSaveNote.IsEnabled = text.Length >= 3 && text.Length <= 200;
            hasUnsavedNote = !string.IsNullOrWhiteSpace(text);
        }

        private void TxtQuickNote_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter && btnSaveNote.IsEnabled)
            {
                BtnSaveNote_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.Escape)
            {
                BtnCancelNote_Click(sender, e);
                e.Handled = true;
            }
        }

        private void BtnSaveNote_Click(object sender, RoutedEventArgs e)
        {
            TrySaveQuickNote();
        }

        private void BtnCancelNote_Click(object sender, RoutedEventArgs e)
        {
            if (hasUnsavedNote)
            {
                var result = MessageBox.Show("Сохранить черновик?", "Несохраненная заметка", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    if (TrySaveQuickNote())
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

            ResetQuickNoteEditor();
        }

        private void BtnAddEventComment_Click(object sender, RoutedEventArgs e)
        {
            if (task == null)
            {
                MessageBox.Show("Сначала сохраните задачу");
                return;
            }

            var commentText = txtEventComment.Text?.Trim() ?? "";
            
            if (string.IsNullOrWhiteSpace(commentText))
            {
                MessageBox.Show("Введите текст комментария", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (commentText.Length < 3 || commentText.Length > 500)
            {
                MessageBox.Show("Комментарий должен содержать от 3 до 500 символов", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!TryGetProgressPercent(txtEventProgressPercent.Text, out var progressPercent))
            {
                txtEventProgressPercent.Focus();
                txtEventProgressPercent.SelectAll();
                return;
            }

            try
            {
                var userId = LoginWindow.CurrentUser?.UserId ?? 0;
                if (!facade.AddTaskReport(task.TaskId, userId, commentText, progressPercent))
                {
                    throw new InvalidOperationException("Не удалось сохранить отчет по задаче");
                }

                ShowToast("Комментарий добавлен");
                
                txtEventComment.Text = "";
                txtEventProgressPercent.Clear();
                SuggestCompletionIfNeeded(progressPercent);
                LoadEvents();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении комментария: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TaskWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (hasUnsavedNote)
            {
                var result = MessageBox.Show("Сохранить черновик заметки?", "Несохраненная заметка", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    if (task != null && btnSaveNote.IsEnabled)
                    {
                        if (!TrySaveQuickNote())
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

        private bool TrySaveQuickNote()
        {
            if (task == null)
            {
                return false;
            }

            var noteText = txtQuickNote.Text?.Trim() ?? "";
            if (noteText.Length < 3 || noteText.Length > 200)
            {
                MessageBox.Show("Заметка должна содержать от 3 до 200 символов", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!TryGetProgressPercent(txtQuickNoteProgressPercent.Text, out var progressPercent))
            {
                txtQuickNoteProgressPercent.Focus();
                txtQuickNoteProgressPercent.SelectAll();
                return false;
            }

            try
            {
                var userId = LoginWindow.CurrentUser?.UserId ?? 0;
                if (!facade.AddTaskReport(task.TaskId, userId, noteText, progressPercent))
                {
                    throw new InvalidOperationException("Не удалось сохранить отчет по задаче");
                }

                ShowToast("Заметка добавлена");

                ResetQuickNoteEditor();
                SuggestCompletionIfNeeded(progressPercent);
                LoadEvents();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении заметки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void ResetQuickNoteEditor()
        {
            txtQuickNote.Clear();
            txtQuickNoteProgressPercent.Clear();
            pnlQuickNote.Visibility = Visibility.Collapsed;
            btnAddQuickNote.Visibility = Visibility.Visible;
            hasUnsavedNote = false;
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
        }

        private bool IsTaskCompleted()
        {
            return string.Equals(task?.TaskStatus?.TaskStatusName, "Завершено", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(task?.TaskStatus?.TaskStatusName, "Completed", StringComparison.OrdinalIgnoreCase);
        }

        private string GetEventDisplayText(TaskReport report)
        {
            var eventText = string.IsNullOrWhiteSpace(report?.ReportText) ? "Событие" : report.ReportText.Trim();
            return report?.ProgressPercent.HasValue == true
                ? $"{report.ProgressPercent.Value}% • {eventText}"
                : eventText;
        }

        private void ShowToast(string message)
        {
            var toast = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ShowInTaskbar = false,
                Topmost = true,
                SizeToContent = SizeToContent.WidthAndHeight,
                ResizeMode = ResizeMode.NoResize
            };

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(240, 50, 50, 50)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20, 15, 20, 15),
                Margin = new Thickness(10)
            };

            var textBlock = new TextBlock
            {
                Text = message,
                Foreground = Brushes.White,
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

    public class EventDisplay
    {
        public string DisplayText { get; set; }
        public string TimeInfo { get; set; }
    }

    public class MaterialRequestDisplay
    {
        public int RequestId { get; set; }
        public string Status { get; set; }
        public string StatusDisplay { get; set; }
        public DateTime? RequiredDate { get; set; }
        public string RequiredDateDisplay { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedAtDisplay { get; set; }
        public int ItemsCount { get; set; }

        public MaterialRequestDisplay(MaterialRequest request)
        {
            RequestId = request.RequestId;
            Status = request.Status;
            StatusDisplay = GetStatusName(request.Status);
            RequiredDate = request.RequiredDate;
            RequiredDateDisplay = request.RequiredDate?.ToString("dd.MM.yyyy") ?? "-";
            CreatedAt = request.CreatedAt;
            CreatedAtDisplay = request.CreatedAt.ToString("dd.MM.yyyy HH:mm");
            ItemsCount = request.Items?.Count ?? 0;
        }

        private string GetStatusName(string status)
        {
            switch (status)
            {
                case "Draft": return "Черновик";
                case "Submitted": return "Отправлена";
                case "Approved": return "Согласована";
                case "Rejected": return "Отклонена";
                case "Issued": return "Выдана";
                case "Delivered": return "Доставлена";
                case "Closed": return "Закрыта";
                default: return status;
            }
        }
    }
}
