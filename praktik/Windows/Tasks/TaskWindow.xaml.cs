using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
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
        private readonly ObservableCollection<AttachmentDisplay> taskAttachments = new ObservableCollection<AttachmentDisplay>();
        private bool hasUnsavedNote = false;
        private bool skipPendingDraftCheck;
        private string selectedEventAttachmentPath;
        private string selectedQuickNoteAttachmentPath;
        public int? SavedTaskId { get; private set; }
        private const string GenerateDescriptionButtonText = "Сгенерировать описание";
        private const string GeneratingDescriptionButtonText = "Генерация...";
        private const string GenerateDescriptionHintText = "Описание строится по названию задачи и стилю строительных работ. Объект добавляется только если выбран.";

        public TaskWindow(Task task = null, bool isDuplicateMode = false)
        {
            InitializeComponent();
            duplicatedFromTask = isDuplicateMode ? task : null;
            this.task = isDuplicateMode ? null : task;
            copiedLabelId = task?.LabelId;

            materialRequests = new ObservableCollection<MaterialRequestDisplay>();
            dgMaterialRequests.ItemsSource = materialRequests;
            icTaskAttachments.ItemsSource = taskAttachments;
            UpdateEventAttachmentState();
            UpdateQuickNoteAttachmentState();
            
            LoadData();
            ConfigureAiDescriptionUi();

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

        private void ConfigureAiDescriptionUi()
        {
            var canGenerate = facade.CanGenerateTaskDescription;
            btnGenerateDescription.Visibility = canGenerate ? Visibility.Visible : Visibility.Collapsed;
            btnGenerateDescription.Content = GenerateDescriptionButtonText;
            if (btnGenerateDescription.Parent is Grid headerGrid)
            {
                var duplicatedLabel = headerGrid.Children.OfType<TextBlock>().FirstOrDefault();
                if (duplicatedLabel != null)
                {
                    duplicatedLabel.Visibility = Visibility.Collapsed;
                }
            }
            txtAiHint.Visibility = canGenerate ? Visibility.Visible : Visibility.Collapsed;
            txtAiHint.Text = GenerateDescriptionHintText;
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
            if (!ConfirmPendingDraftsBeforeClose())
            {
                return;
            }

            if (!TryBuildTaskSaveInput(out var taskInput))
            {
                return;
            }

            try
            {
                SaveTask(taskInput);
                skipPendingDraftCheck = true;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось сохранить задачу. Проверьте заполненные данные.\n\n{ex.Message}", "Задача", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return;
        }

        private bool TryBuildTaskSaveInput(out TaskSaveInput taskInput)
        {
            taskInput = null;

            var title = txtTitle.Text?.Trim();
            var description = string.IsNullOrWhiteSpace(txtDescription.Text) ? null : txtDescription.Text.Trim();

            if (string.IsNullOrWhiteSpace(title))
            {
                MessageBox.Show("Введите название задачи.", "Задача", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtTitle.Focus();
                txtTitle.SelectAll();
                return false;
            }

            if (cbSites.SelectedItem == null)
            {
                MessageBox.Show("Выберите объект.", "Задача", MessageBoxButton.OK, MessageBoxImage.Warning);
                cbSites.Focus();
                return false;
            }

            if (cbCrews.SelectedItem == null)
            {
                MessageBox.Show("Выберите бригаду.", "Задача", MessageBoxButton.OK, MessageBoxImage.Warning);
                cbCrews.Focus();
                return false;
            }

            if (cbPriorities.SelectedItem == null)
            {
                MessageBox.Show("Выберите приоритет задачи.", "Задача", MessageBoxButton.OK, MessageBoxImage.Warning);
                cbPriorities.Focus();
                return false;
            }

            if (dpStartDate.SelectedDate == null)
            {
                MessageBox.Show("Укажите дату начала работ.", "Задача", MessageBoxButton.OK, MessageBoxImage.Warning);
                dpStartDate.Focus();
                return false;
            }

            if (dpEndDate.SelectedDate == null)
            {
                MessageBox.Show("Укажите срок выполнения задачи.", "Задача", MessageBoxButton.OK, MessageBoxImage.Warning);
                dpEndDate.Focus();
                return false;
            }

            if (dpStartDate.SelectedDate > dpEndDate.SelectedDate)
            {
                MessageBox.Show("Дата окончания не должна быть раньше даты начала.", "Задача", MessageBoxButton.OK, MessageBoxImage.Warning);
                dpEndDate.Focus();
                return false;
            }

            if (!TaskReportAttachmentService.TryValidateSourceFile(selectedEventAttachmentPath, out var eventAttachmentError))
            {
                MessageBox.Show(eventAttachmentError, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            taskInput = new TaskSaveInput
            {
                Title = title,
                Description = description,
                StartDate = dpStartDate.SelectedDate.Value,
                EndDate = dpEndDate.SelectedDate.Value,
                SiteId = ((Site)cbSites.SelectedItem).SiteId,
                CrewId = ((Crew)cbCrews.SelectedItem).CrewId,
                PriorityId = ((Priority)cbPriorities.SelectedItem).PriorityId
            };

            return true;
        }

        private void SaveTask(TaskSaveInput taskInput)
        {
            if (task == null)
            {
                task = CreateTask(taskInput);
                facade.AddTask(task);
                SavedTaskId = task.TaskId > 0 ? (int?)task.TaskId : null;
                return;
            }

            ApplyTaskSaveInput(task, taskInput);
            task.UpdatedAt = DateTime.Now;
            facade.UpdateTask(task);
            SavedTaskId = task.TaskId;
        }

        private Task CreateTask(TaskSaveInput taskInput)
        {
            var newTask = new Task
            {
                TaskStatusId = ResolveInitialStatusId(),
                LabelId = copiedLabelId,
                CreatedBy = LoginWindow.CurrentUser.UserId,
                CreatedAt = DateTime.Now
            };

            ApplyTaskSaveInput(newTask, taskInput);
            return newTask;
        }

        private static void ApplyTaskSaveInput(Task targetTask, TaskSaveInput taskInput)
        {
            targetTask.Title = taskInput.Title;
            targetTask.Description = taskInput.Description;
            targetTask.StartDate = taskInput.StartDate;
            targetTask.EndDate = taskInput.EndDate;
            targetTask.SiteId = taskInput.SiteId;
            targetTask.CrewId = taskInput.CrewId;
            targetTask.PriorityId = taskInput.PriorityId;
        }

        private async void BtnGenerateDescription_Click(object sender, RoutedEventArgs e)
        {
            await HandleGenerateDescriptionAsync();
            return;

        }

        private void SetDescriptionGenerationState(bool isGenerating)
        {
            btnGenerateDescription.IsEnabled = !isGenerating;
            btnGenerateDescription.Content = isGenerating ? GeneratingDescriptionButtonText : GenerateDescriptionButtonText;
            Mouse.OverrideCursor = isGenerating ? Cursors.Wait : null;
            return;

        }

        private async System.Threading.Tasks.Task HandleGenerateDescriptionAsync()
        {
            var title = txtTitle.Text?.Trim();
            if (!IsTitleReadyForDescriptionGeneration(title))
            {
                MessageBox.Show("Для генерации нужно осмысленное название задачи: минимум 4 символа и хотя бы одно осмысленное слово.", "LM Studio", MessageBoxButton.OK, MessageBoxImage.Information);
                txtTitle.Focus();
                txtTitle.SelectAll();
                return;
            }

            var hasExistingDescription = !string.IsNullOrWhiteSpace(txtDescription.Text);
            var appendToExistingDescription = false;

            if (hasExistingDescription)
            {
                var decision = MessageBox.Show(
                    "В поле описания уже есть текст.\n\nНажмите \"Да\", чтобы заменить его.\nНажмите \"Нет\", чтобы добавить сгенерированное описание в конец.",
                    "LM Studio",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (decision == MessageBoxResult.Cancel)
                {
                    return;
                }

                appendToExistingDescription = decision == MessageBoxResult.No;
            }

            try
            {
                SetDescriptionGenerationState(true);

                var request = BuildTaskDescriptionRequest(title);
                var result = await facade.GenerateTaskDescriptionAsync(request);
                if (!result.IsSuccess)
                {
                    MessageBox.Show(result.ErrorMessage, "LM Studio", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var generatedDescription = result.Description?.Trim();
                if (string.IsNullOrWhiteSpace(generatedDescription))
                {
                    MessageBox.Show("LM Studio вернул пустой текст описания.", "LM Studio", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                txtDescription.Text = appendToExistingDescription
                    ? string.Concat(txtDescription.Text.TrimEnd(), Environment.NewLine, Environment.NewLine, generatedDescription)
                    : generatedDescription;

                txtDescription.Focus();
                txtDescription.CaretIndex = txtDescription.Text.Length;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при генерации описания: {ex.Message}", "LM Studio", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetDescriptionGenerationState(false);
            }
        }

        private bool IsTitleReadyForDescriptionGeneration(string title)
        {
            return !string.IsNullOrWhiteSpace(title) &&
                   title.Trim().Length >= 4 &&
                   title.Any(char.IsLetter);
        }

        private TaskDescriptionGenerationRequest BuildTaskDescriptionRequest(string title)
        {
            return new TaskDescriptionGenerationRequest
            {
                Title = title,
                SiteName = (cbSites.SelectedItem as Site)?.SiteName,
                Examples = GetTaskDescriptionExamples(title)
            };
        }

        private List<TaskDescriptionExample> GetTaskDescriptionExamples(string title)
        {
            var currentTaskId = task?.TaskId ?? 0;

            var candidates = facade.GetTasks()
                .Where(existingTask =>
                    existingTask.TaskId != currentTaskId &&
                    !string.IsNullOrWhiteSpace(existingTask.Title) &&
                    !string.IsNullOrWhiteSpace(existingTask.Description))
                .Select(existingTask => new
                {
                    Task = existingTask,
                    Score = CalculateTaskDescriptionExampleScore(title, existingTask)
                })
                .OrderByDescending(item => item.Score)
                .ThenByDescending(item => item.Task.TaskId)
                .ToList();

            var relevantExamples = candidates
                .Where(item => item.Score > 0)
                .Take(3)
                .Select(item => new TaskDescriptionExample
                {
                    Title = item.Task.Title.Trim(),
                    Description = item.Task.Description.Trim()
                })
                .ToList();

            return relevantExamples;
        }

        private int CalculateTaskDescriptionExampleScore(string title, Task existingTask)
        {
            var inputTokens = ExtractTitleTokens(title);
            var existingTokens = ExtractTitleTokens(existingTask.Title);
            var overlap = inputTokens.Intersect(existingTokens).Count();
            return overlap * 5;
        }

        private HashSet<string> ExtractTitleTokens(string value)
        {
            var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var buffer = new System.Text.StringBuilder();

            foreach (var character in value ?? string.Empty)
            {
                if (char.IsLetterOrDigit(character))
                {
                    buffer.Append(char.ToLowerInvariant(character));
                    continue;
                }

                FlushTitleToken(buffer, tokens);
            }

            FlushTitleToken(buffer, tokens);
            return tokens;
        }

        private void FlushTitleToken(System.Text.StringBuilder buffer, HashSet<string> tokens)
        {
            if (buffer.Length < 3)
            {
                buffer.Clear();
                return;
            }

            var normalizedToken = NormalizeTitleToken(buffer.ToString());
            if (!string.IsNullOrWhiteSpace(normalizedToken))
            {
                tokens.Add(normalizedToken);
            }

            buffer.Clear();
        }

        private string NormalizeTitleToken(string token)
        {
            var normalized = token?.Trim().ToLowerInvariant() ?? string.Empty;
            if (normalized.Length < 3)
            {
                return string.Empty;
            }

            var suffixes = new[]
            {
                "иями", "ями", "ами", "ого", "ему", "ому", "ыми", "ими",
                "иях", "ией", "ция", "ции", "ение", "ений", "ание", "аний",
                "ость", "ости", "ный", "ний", "овая", "овый", "овка", "евка",
                "ах", "ях", "ов", "ев", "ом", "ем", "ой", "ей", "ый", "ий",
                "ая", "яя", "ое", "ее", "ам", "ям", "ию", "ью", "ия", "ие",
                "а", "я", "ы", "и", "о", "е", "ь"
            };

            foreach (var suffix in suffixes.OrderByDescending(item => item.Length))
            {
                if (normalized.Length - suffix.Length < 3)
                {
                    continue;
                }

                if (normalized.EndsWith(suffix, StringComparison.Ordinal))
                {
                    normalized = normalized.Substring(0, normalized.Length - suffix.Length);
                    break;
                }
            }

            return normalized;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            if (!ConfirmPendingDraftsBeforeClose())
            {
                return;
            }

            skipPendingDraftCheck = true;
            DialogResult = false;
            Close();
        }

        private void BtnNewRequest_Click(object sender, RoutedEventArgs e)
        {
            if (task == null)
            {
                MessageBox.Show("Сначала сохраните задачу, затем создайте заявку на материалы.", "Заявка на материалы", MessageBoxButton.OK, MessageBoxImage.Information);
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
                    MessageBox.Show("Заявка не найдена.", "Заявка на материалы", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (request.Status != "Draft" && LoginWindow.CurrentUser.Role != "Диспетчер")
                {
                    MessageBox.Show("Редактирование возможно только для черновиков.", "Заявка на материалы", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                MessageBox.Show("Выберите заявку для редактирования.", "Заявка на материалы", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnViewRequestHistory_Click(object sender, RoutedEventArgs e)
        {
            if (task == null)
            {
                MessageBox.Show("Сначала сохраните задачу.", "История заявки", MessageBoxButton.OK, MessageBoxImage.Information);
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
                MessageBox.Show("История по заявкам пока пуста.", "История заявки", MessageBoxButton.OK, MessageBoxImage.Information);
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
                MessageBox.Show("Сначала сохраните задачу.", "Печать", MessageBoxButton.OK, MessageBoxImage.Information);
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
                    AttachmentUrl = report.AttachmentUrl,
                    DisplayText = GetEventDisplayText(report),
                    TimeInfo = $"{report.ReporterName ?? "Система"} • {report.ReportedAt:dd.MM.yyyy HH:mm}"
                });
            }

            LoadTaskAttachments(reports);
            lbEvents.ItemsSource = events;
        }

        private void LoadTaskAttachments(IEnumerable<TaskReport> reports)
        {
            taskAttachments.Clear();

            if (reports != null)
            {
                foreach (var report in reports.Where(r => !string.IsNullOrWhiteSpace(r.AttachmentUrl)))
                {
                    taskAttachments.Add(new AttachmentDisplay(report));
                }
            }

            if (txtNoTaskAttachments != null)
            {
                txtNoTaskAttachments.Visibility = taskAttachments.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void BtnAddQuickNote_Click(object sender, RoutedEventArgs e)
        {
            if (task == null)
            {
                MessageBox.Show("Сначала сохраните задачу.", "Отчет по задаче", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            pnlQuickNote.Visibility = Visibility.Visible;
            btnAddQuickNote.Visibility = Visibility.Collapsed;
            txtQuickNote.Focus();
        }

        private void BtnAttachEventFile_Click(object sender, RoutedEventArgs e)
        {
            SelectAttachmentForContext(
                path => selectedEventAttachmentPath = path,
                UpdateEventAttachmentState);
        }

        private void BtnRemoveEventAttachment_Click(object sender, RoutedEventArgs e)
        {
            selectedEventAttachmentPath = null;
            UpdateEventAttachmentState();
        }

        private void BtnAttachQuickNoteFile_Click(object sender, RoutedEventArgs e)
        {
            SelectAttachmentForContext(
                path => selectedQuickNoteAttachmentPath = path,
                UpdateQuickNoteAttachmentState);
        }

        private void BtnRemoveQuickNoteAttachment_Click(object sender, RoutedEventArgs e)
        {
            selectedQuickNoteAttachmentPath = null;
            UpdateQuickNoteAttachmentState();
        }

        private void SelectAttachmentForContext(Action<string> setPath, Action refreshUi)
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

            setPath(dialog.FileName);
            refreshUi();
        }

        private void UpdateEventAttachmentState()
        {
            UpdateAttachmentState(txtEventAttachment, btnRemoveEventAttachment, selectedEventAttachmentPath);
            return;
        }

        private void UpdateQuickNoteAttachmentState()
        {
            UpdateAttachmentState(txtQuickNoteAttachment, btnRemoveQuickNoteAttachment, selectedQuickNoteAttachmentPath);
            UpdateQuickNoteDraftState();
            return;
        }

        private static void UpdateAttachmentState(TextBlock attachmentTextBlock, Button removeButton, string attachmentPath)
        {
            if (attachmentTextBlock == null || removeButton == null)
            {
                return;
            }

            var hasAttachment = !string.IsNullOrWhiteSpace(attachmentPath);
            attachmentTextBlock.Text = hasAttachment
                ? $"Выбрано: {TaskReportAttachmentService.GetAttachmentFileName(attachmentPath)}"
                : "Вложение не выбрано";
            attachmentTextBlock.ToolTip = hasAttachment ? attachmentPath : null;
            removeButton.Visibility = hasAttachment ? Visibility.Visible : Visibility.Collapsed;
        }

        private void TxtQuickNote_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateQuickNoteDraftState();
        }

        private void TxtQuickNoteProgressPercent_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateQuickNoteDraftState();
        }

        private void UpdateQuickNoteDraftState()
        {
            var text = txtQuickNote?.Text?.Trim() ?? string.Empty;
            var hasTypedText = !string.IsNullOrWhiteSpace(text);
            var hasValidText = text.Length >= 3 && text.Length <= 200;
            var hasOtherInput = !string.IsNullOrWhiteSpace(txtQuickNoteProgressPercent?.Text)
                || !string.IsNullOrWhiteSpace(selectedQuickNoteAttachmentPath);

            if (btnSaveNote != null)
            {
                btnSaveNote.IsEnabled = hasValidText || (!hasTypedText && hasOtherInput);
            }

            hasUnsavedNote = hasTypedText || hasOtherInput;
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
            TrySaveEventDraft();
            return;
        }

        private void TaskWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (skipPendingDraftCheck)
            {
                return;
            }

            if (!ConfirmPendingDraftsBeforeClose())
            {
                e.Cancel = true;
                return;
            }

            skipPendingDraftCheck = true;
            return;
        }

        private bool ConfirmPendingDraftsBeforeClose()
        {
            return ConfirmPendingEventDraft() && ConfirmPendingQuickNoteDraft();
        }

        private bool ConfirmPendingEventDraft()
        {
            return ConfirmPendingDraft(
                HasPendingEventDraft(),
                TrySaveEventDraft,
                ClearEventDraft,
                "В блоке добавления отчета есть несохраненные данные. Сохранить отчет сейчас?",
                "Несохраненный отчет");
        }

        private bool ConfirmPendingQuickNoteDraft()
        {
            return ConfirmPendingDraft(
                HasPendingQuickNoteDraft(),
                TrySaveQuickNoteCore,
                ResetQuickNoteEditor,
                "Есть несохраненная заметка. Сохранить ее сейчас?",
                "Несохраненная заметка");
        }

        private bool HasPendingEventDraft()
        {
            return HasPendingDraft(txtEventComment?.Text, txtEventProgressPercent?.Text, selectedEventAttachmentPath);
        }

        private bool HasPendingQuickNoteDraft()
        {
            return HasPendingDraft(txtQuickNote?.Text, txtQuickNoteProgressPercent?.Text, selectedQuickNoteAttachmentPath);
        }

        private bool ConfirmPendingDraft(bool hasPendingDraft, Func<bool> saveDraft, Action discardDraft, string message, string caption)
        {
            if (!hasPendingDraft)
            {
                return true;
            }

            var result = MessageBox.Show(message, caption, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                return saveDraft();
            }

            if (result == MessageBoxResult.No)
            {
                discardDraft();
                return true;
            }

            return false;
        }

        private static bool HasPendingDraft(string text, string progressText, string attachmentPath)
        {
            return !string.IsNullOrWhiteSpace(text)
                || !string.IsNullOrWhiteSpace(progressText)
                || !string.IsNullOrWhiteSpace(attachmentPath);
        }

        private void ClearEventDraft()
        {
            txtEventComment.Clear();
            txtEventProgressPercent.Clear();
            selectedEventAttachmentPath = null;
            UpdateEventAttachmentState();
        }

        private bool TryGetDraftProgressPercent(TextBox progressTextBox, out int? progressPercent)
        {
            if (!TryGetProgressPercent(progressTextBox.Text, out progressPercent))
            {
                progressTextBox.Focus();
                progressTextBox.SelectAll();
                return false;
            }

            return true;
        }

        private bool EnsureDraftHasContent(string text, string attachmentPath, int? progressPercent, string emptyMessage)
        {
            if (HasDraftContent(text, attachmentPath, progressPercent))
            {
                return true;
            }

            MessageBox.Show(emptyMessage, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        private static bool HasDraftContent(string text, string attachmentPath, int? progressPercent)
        {
            return !string.IsNullOrWhiteSpace(text)
                || !string.IsNullOrWhiteSpace(attachmentPath)
                || progressPercent.HasValue;
        }

        private bool ValidateDraftTextLength(string text, int minLength, int maxLength, string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(text) || (text.Length >= minLength && text.Length <= maxLength))
            {
                return true;
            }

            MessageBox.Show(errorMessage, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        private bool TryValidateDraftAttachment(string attachmentPath)
        {
            if (!TaskReportAttachmentService.TryValidateSourceFile(attachmentPath, out var attachmentError))
            {
                MessageBox.Show(attachmentError, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private bool TrySaveTaskDraft(string reportText, int? progressPercent, string attachmentPath, Action resetDraft, string successMessage, string errorPrefix)
        {
            try
            {
                var userId = LoginWindow.CurrentUser?.UserId ?? 0;
                if (!facade.AddTaskReport(task.TaskId, userId, reportText, progressPercent, attachmentPath))
                {
                    throw new InvalidOperationException("Не удалось сохранить отчет по задаче");
                }

                ShowToast(successMessage);
                resetDraft();
                SuggestCompletionIfNeeded(progressPercent);
                LoadEvents();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{errorPrefix}: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private static string NormalizeDraftText(string text)
        {
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }

        private bool TrySaveEventDraft()
        {
            if (task == null)
            {
                MessageBox.Show("Сначала сохраните задачу.", "Отчет по задаче", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }

            var commentText = txtEventComment.Text?.Trim() ?? string.Empty;
            if (!TryGetDraftProgressPercent(txtEventProgressPercent, out var progressPercent))
            {
                return false;
            }

            if (!EnsureDraftHasContent(
                commentText,
                selectedEventAttachmentPath,
                progressPercent,
                "Добавьте комментарий, укажите прогресс или прикрепите файл."))
            {
                return false;
            }

            if (!ValidateDraftTextLength(
                commentText,
                3,
                500,
                "Комментарий должен содержать от 3 до 500 символов"))
            {
                return false;
            }

            if (!TryValidateDraftAttachment(selectedEventAttachmentPath))
            {
                return false;
            }

            return TrySaveTaskDraft(
                NormalizeDraftText(commentText),
                progressPercent,
                selectedEventAttachmentPath,
                ClearEventDraft,
                "Отчет добавлен",
                "Ошибка при сохранении отчета");
        }

        private bool TrySaveQuickNoteCore()
        {
            if (task == null)
            {
                return false;
            }

            var noteText = txtQuickNote.Text?.Trim() ?? string.Empty;
            if (!TryGetDraftProgressPercent(txtQuickNoteProgressPercent, out var progressPercent))
            {
                return false;
            }

            if (!EnsureDraftHasContent(
                noteText,
                selectedQuickNoteAttachmentPath,
                progressPercent,
                "Добавьте текст заметки, укажите прогресс или прикрепите файл."))
            {
                return false;
            }

            if (!ValidateDraftTextLength(
                noteText,
                3,
                200,
                "Заметка должна содержать от 3 до 200 символов"))
            {
                return false;
            }

            if (!TryValidateDraftAttachment(selectedQuickNoteAttachmentPath))
            {
                return false;
            }

            return TrySaveTaskDraft(
                NormalizeDraftText(noteText),
                progressPercent,
                selectedQuickNoteAttachmentPath,
                ResetQuickNoteEditor,
                "Отчет добавлен",
                "Ошибка при сохранении заметки");
        }

        private bool TrySaveQuickNote()
        {
            return TrySaveQuickNoteCore();
        }

        private void ResetQuickNoteEditor()
        {
            txtQuickNote.Clear();
            txtQuickNoteProgressPercent.Clear();
            selectedQuickNoteAttachmentPath = null;
            UpdateQuickNoteAttachmentState();
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
            var eventText = string.IsNullOrWhiteSpace(report?.ReportText)
                ? (string.IsNullOrWhiteSpace(report?.AttachmentUrl) ? "Обновление по задаче" : "Добавлено вложение")
                : report.ReportText.Trim();
            return report?.ProgressPercent.HasValue == true
                ? $"{report.ProgressPercent.Value}% • {eventText}"
                : eventText;
        }

        private void BtnOpenTaskAttachmentFromList_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is FrameworkElement element) || !(element.DataContext is AttachmentDisplay attachment))
            {
                return;
            }

            if (!TaskReportAttachmentService.TryOpenAttachment(attachment.AttachmentUrl, out var errorMessage))
            {
                MessageBox.Show(errorMessage, "Вложение", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnOpenEventAttachment_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is FrameworkElement element) || !(element.DataContext is EventDisplay eventDisplay))
            {
                return;
            }

            if (!TaskReportAttachmentService.TryOpenAttachment(eventDisplay.AttachmentUrl, out var errorMessage))
            {
                MessageBox.Show(errorMessage, "Вложение", MessageBoxButton.OK, MessageBoxImage.Information);
            }
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

        private sealed class TaskSaveInput
        {
            public string Title { get; set; }
            public string Description { get; set; }
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public int SiteId { get; set; }
            public int? CrewId { get; set; }
            public int PriorityId { get; set; }
        }
    }

    public class EventDisplay
    {
        public string DisplayText { get; set; }
        public string TimeInfo { get; set; }
        public string AttachmentUrl { get; set; }
        public bool HasAttachment => !string.IsNullOrWhiteSpace(AttachmentUrl);
        public string AttachmentFileName => TaskReportAttachmentService.GetAttachmentFileName(AttachmentUrl);
    }

    public class AttachmentDisplay
    {
        public AttachmentDisplay(TaskReport report)
        {
            AttachmentUrl = report?.AttachmentUrl;
            FileName = TaskReportAttachmentService.GetAttachmentFileName(report?.AttachmentUrl);
            MetaText = $"{report?.ReporterName ?? "Система"} • {report?.ReportedAt:dd.MM.yyyy HH:mm}";
        }

        public string AttachmentUrl { get; set; }
        public string FileName { get; set; }
        public string MetaText { get; set; }
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
