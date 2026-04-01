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
        private const string GenerateDescriptionButtonText = "\u0421\u0433\u0435\u043d\u0435\u0440\u0438\u0440\u043e\u0432\u0430\u0442\u044c \u043e\u043f\u0438\u0441\u0430\u043d\u0438\u0435";
        private const string GeneratingDescriptionButtonText = "\u0413\u0435\u043d\u0435\u0440\u0430\u0446\u0438\u044f...";
        private const string GenerateDescriptionHintText = "\u041e\u043f\u0438\u0441\u0430\u043d\u0438\u0435 \u0441\u0442\u0440\u043e\u0438\u0442\u0441\u044f \u043f\u043e \u043d\u0430\u0437\u0432\u0430\u043d\u0438\u044e \u0437\u0430\u0434\u0430\u0447\u0438 \u0438 \u0441\u0442\u0438\u043b\u044e \u0441\u0442\u0440\u043e\u0438\u0442\u0435\u043b\u044c\u043d\u044b\u0445 \u0440\u0430\u0431\u043e\u0442. \u041e\u0431\u044a\u0435\u043a\u0442 \u0434\u043e\u0431\u0430\u0432\u043b\u044f\u0435\u0442\u0441\u044f \u0442\u043e\u043b\u044c\u043a\u043e \u0435\u0441\u043b\u0438 \u0432\u044b\u0431\u0440\u0430\u043d.";

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

            var title = txtTitle.Text?.Trim();
            var description = string.IsNullOrWhiteSpace(txtDescription.Text) ? null : txtDescription.Text.Trim();

            if (string.IsNullOrWhiteSpace(title) || cbSites.SelectedItem == null ||
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

            if (!TaskReportAttachmentService.TryValidateSourceFile(selectedEventAttachmentPath, out var eventAttachmentError))
            {
                MessageBox.Show(eventAttachmentError, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (task == null)
                {
                    task = new Task
                    {
                        Title = title,
                        Description = description,
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
                    task.Title = title;
                    task.Description = description;
                    task.StartDate = dpStartDate.SelectedDate.Value;
                    task.EndDate = dpEndDate.SelectedDate.Value;
                    task.SiteId = (cbSites.SelectedItem is Site site) ? site.SiteId : 0;
                    task.CrewId = cbCrews.SelectedItem is Crew crew ? (int?)crew.CrewId : null;
                    task.PriorityId = (cbPriorities.SelectedItem is Priority priority) ? priority.PriorityId : 0;
                    task.UpdatedAt = DateTime.Now;

                    facade.UpdateTask(task);
                    SavedTaskId = task.TaskId;
                }

                skipPendingDraftCheck = true;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}");
            }
        }

        private async void BtnGenerateDescription_Click(object sender, RoutedEventArgs e)
        {
            await HandleGenerateDescriptionAsync();
            return;

#if false
            var title = txtTitle.Text?.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                MessageBox.Show("Р’РІРµРґРёС‚Рµ РЅР°Р·РІР°РЅРёРµ Р·Р°РґР°С‡Рё, С‡С‚РѕР±С‹ СЃРіРµРЅРµСЂРёСЂРѕРІР°С‚СЊ РѕРїРёСЃР°РЅРёРµ.", "LM Studio", MessageBoxButton.OK, MessageBoxImage.Information);
                txtTitle.Focus();
                return;
            }

            var hasExistingDescription = !string.IsNullOrWhiteSpace(txtDescription.Text);
            var appendToExistingDescription = false;

            if (hasExistingDescription)
            {
                var decision = MessageBox.Show(
                    "Р’ РїРѕР»Рµ РѕРїРёСЃР°РЅРёСЏ СѓР¶Рµ РµСЃС‚СЊ С‚РµРєСЃС‚.\n\nРќР°Р¶РјРёС‚Рµ \"Р”Р°\", С‡С‚РѕР±С‹ Р·Р°РјРµРЅРёС‚СЊ РµРіРѕ.\nРќР°Р¶РјРёС‚Рµ \"РќРµС‚\", С‡С‚РѕР±С‹ РґРѕР±Р°РІРёС‚СЊ СЃРіРµРЅРµСЂРёСЂРѕРІР°РЅРЅС‹Р№ С‚РµРєСЃС‚ РІ РєРѕРЅРµС†.",
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

                var request = new TaskDescriptionGenerationRequest
                {
                    Title = title,
                    SiteName = (cbSites.SelectedItem as Site)?.SiteName,
                    CrewName = (cbCrews.SelectedItem as Crew)?.CrewName,
                    PriorityName = (cbPriorities.SelectedItem as Priority)?.PriorityName,
                    StartDate = dpStartDate.SelectedDate,
                    EndDate = dpEndDate.SelectedDate
                };

                var result = await facade.GenerateTaskDescriptionAsync(request);
                if (!result.IsSuccess)
                {
                    MessageBox.Show(result.ErrorMessage, "LM Studio", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var generatedDescription = result.Description?.Trim();
                if (string.IsNullOrWhiteSpace(generatedDescription))
                {
                    MessageBox.Show("LM Studio РІРµСЂРЅСѓР» РїСѓСЃС‚РѕР№ С‚РµРєСЃС‚ РѕРїРёСЃР°РЅРёСЏ.", "LM Studio", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                MessageBox.Show($"РћС€РёР±РєР° РїСЂРё РіРµРЅРµСЂР°С†РёРё РѕРїРёСЃР°РЅРёСЏ: {ex.Message}", "LM Studio", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetDescriptionGenerationState(false);
            }
#endif
        }

        private void SetDescriptionGenerationState(bool isGenerating)
        {
            btnGenerateDescription.IsEnabled = !isGenerating;
            btnGenerateDescription.Content = isGenerating ? GeneratingDescriptionButtonText : GenerateDescriptionButtonText;
            Mouse.OverrideCursor = isGenerating ? Cursors.Wait : null;
            return;

#if false
            btnGenerateDescription.IsEnabled = !isGenerating;
            btnGenerateDescription.Content = isGenerating ? "Р“РµРЅРµСЂР°С†РёСЏ..." : "AI-РѕРїРёСЃР°РЅРёРµ";
            Mouse.OverrideCursor = isGenerating ? Cursors.Wait : null;
        }

        #endif
        }

        private async System.Threading.Tasks.Task HandleGenerateDescriptionAsync()
        {
            var title = txtTitle.Text?.Trim();
            if (!IsTitleReadyForDescriptionGeneration(title))
            {
                MessageBox.Show("\u0414\u043b\u044f \u0433\u0435\u043d\u0435\u0440\u0430\u0446\u0438\u0438 \u043d\u0443\u0436\u043d\u043e \u043e\u0441\u043c\u044b\u0441\u043b\u0435\u043d\u043d\u043e\u0435 \u043d\u0430\u0437\u0432\u0430\u043d\u0438\u0435 \u0437\u0430\u0434\u0430\u0447\u0438: \u043c\u0438\u043d\u0438\u043c\u0443\u043c 4 \u0441\u0438\u043c\u0432\u043e\u043b\u0430 \u0438 \u0445\u043e\u0442\u044f \u0431\u044b \u043e\u0434\u043d\u043e \u043e\u0441\u043c\u044b\u0441\u043b\u0435\u043d\u043d\u043e\u0435 \u0441\u043b\u043e\u0432\u043e.", "LM Studio", MessageBoxButton.OK, MessageBoxImage.Information);
                txtTitle.Focus();
                txtTitle.SelectAll();
                return;
            }

            var hasExistingDescription = !string.IsNullOrWhiteSpace(txtDescription.Text);
            var appendToExistingDescription = false;

            if (hasExistingDescription)
            {
                var decision = MessageBox.Show(
                    "\u0412 \u043f\u043e\u043b\u0435 \u043e\u043f\u0438\u0441\u0430\u043d\u0438\u044f \u0443\u0436\u0435 \u0435\u0441\u0442\u044c \u0442\u0435\u043a\u0441\u0442.\n\n\u041d\u0430\u0436\u043c\u0438\u0442\u0435 \"\u0414\u0430\", \u0447\u0442\u043e\u0431\u044b \u0437\u0430\u043c\u0435\u043d\u0438\u0442\u044c \u0435\u0433\u043e.\n\u041d\u0430\u0436\u043c\u0438\u0442\u0435 \"\u041d\u0435\u0442\", \u0447\u0442\u043e\u0431\u044b \u0434\u043e\u0431\u0430\u0432\u0438\u0442\u044c \u0441\u0433\u0435\u043d\u0435\u0440\u0438\u0440\u043e\u0432\u0430\u043d\u043d\u043e\u0435 \u043e\u043f\u0438\u0441\u0430\u043d\u0438\u0435 \u0432 \u043a\u043e\u043d\u0435\u0446.",
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
                    MessageBox.Show("LM Studio \u0432\u0435\u0440\u043d\u0443\u043b \u043f\u0443\u0441\u0442\u043e\u0439 \u0442\u0435\u043a\u0441\u0442 \u043e\u043f\u0438\u0441\u0430\u043d\u0438\u044f.", "LM Studio", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                MessageBox.Show($"\u041e\u0448\u0438\u0431\u043a\u0430 \u043f\u0440\u0438 \u0433\u0435\u043d\u0435\u0440\u0430\u0446\u0438\u0438 \u043e\u043f\u0438\u0441\u0430\u043d\u0438\u044f: {ex.Message}", "LM Studio", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show("Сначала сохраните задачу");
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
            if (txtEventAttachment == null || btnRemoveEventAttachment == null)
            {
                return;
            }

            var hasAttachment = !string.IsNullOrWhiteSpace(selectedEventAttachmentPath);
            txtEventAttachment.Text = hasAttachment
                ? $"Выбрано: {TaskReportAttachmentService.GetAttachmentFileName(selectedEventAttachmentPath)}"
                : "Вложение не выбрано";
            txtEventAttachment.ToolTip = hasAttachment ? selectedEventAttachmentPath : null;
            btnRemoveEventAttachment.Visibility = hasAttachment ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateQuickNoteAttachmentState()
        {
            if (txtQuickNoteAttachment == null || btnRemoveQuickNoteAttachment == null)
            {
                return;
            }

            var hasAttachment = !string.IsNullOrWhiteSpace(selectedQuickNoteAttachmentPath);
            txtQuickNoteAttachment.Text = hasAttachment
                ? $"Выбрано: {TaskReportAttachmentService.GetAttachmentFileName(selectedQuickNoteAttachmentPath)}"
                : "Вложение не выбрано";
            txtQuickNoteAttachment.ToolTip = hasAttachment ? selectedQuickNoteAttachmentPath : null;
            btnRemoveQuickNoteAttachment.Visibility = hasAttachment ? Visibility.Visible : Visibility.Collapsed;
            UpdateQuickNoteDraftState();
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
#if false

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

            if (!TaskReportAttachmentService.TryValidateSourceFile(selectedEventAttachmentPath, out var eventAttachmentError))
            {
                MessageBox.Show(eventAttachmentError, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var userId = LoginWindow.CurrentUser?.UserId ?? 0;
                if (!facade.AddTaskReport(task.TaskId, userId, commentText, progressPercent, selectedEventAttachmentPath))
                {
                    throw new InvalidOperationException("Не удалось сохранить отчет по задаче");
                }

                ShowToast("Комментарий добавлен");
                
                txtEventComment.Text = "";
                txtEventProgressPercent.Clear();
                selectedEventAttachmentPath = null;
                UpdateEventAttachmentState();
                SuggestCompletionIfNeeded(progressPercent);
                LoadEvents();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении комментария: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
#endif
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
#if false

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
#endif
        }

        private bool ConfirmPendingDraftsBeforeClose()
        {
            return ConfirmPendingEventDraft() && ConfirmPendingQuickNoteDraft();
        }

        private bool ConfirmPendingEventDraft()
        {
            if (!HasPendingEventDraft())
            {
                return true;
            }

            var result = MessageBox.Show(
                "В блоке добавления отчета есть несохраненные данные. Сохранить отчет сейчас?",
                "Несохраненный отчет",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                return TrySaveEventDraft();
            }

            if (result == MessageBoxResult.No)
            {
                ClearEventDraft();
                return true;
            }

            return false;
        }

        private bool ConfirmPendingQuickNoteDraft()
        {
            if (!HasPendingQuickNoteDraft())
            {
                return true;
            }

            var result = MessageBox.Show(
                "Есть несохраненная заметка. Сохранить ее сейчас?",
                "Несохраненная заметка",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                return TrySaveQuickNoteCore();
            }

            if (result == MessageBoxResult.No)
            {
                ResetQuickNoteEditor();
                return true;
            }

            return false;
        }

        private bool HasPendingEventDraft()
        {
            return !string.IsNullOrWhiteSpace(txtEventComment?.Text)
                || !string.IsNullOrWhiteSpace(txtEventProgressPercent?.Text)
                || !string.IsNullOrWhiteSpace(selectedEventAttachmentPath);
        }

        private bool HasPendingQuickNoteDraft()
        {
            return !string.IsNullOrWhiteSpace(txtQuickNote?.Text)
                || !string.IsNullOrWhiteSpace(txtQuickNoteProgressPercent?.Text)
                || !string.IsNullOrWhiteSpace(selectedQuickNoteAttachmentPath);
        }

        private void ClearEventDraft()
        {
            txtEventComment.Clear();
            txtEventProgressPercent.Clear();
            selectedEventAttachmentPath = null;
            UpdateEventAttachmentState();
        }

        private bool TrySaveEventDraft()
        {
            if (task == null)
            {
                MessageBox.Show("РЎРЅР°С‡Р°Р»Р° СЃРѕС…СЂР°РЅРёС‚Рµ Р·Р°РґР°С‡Сѓ");
                return false;
            }

            var commentText = txtEventComment.Text?.Trim() ?? string.Empty;
            if (!TryGetProgressPercent(txtEventProgressPercent.Text, out var progressPercent))
            {
                txtEventProgressPercent.Focus();
                txtEventProgressPercent.SelectAll();
                return false;
            }

            var hasComment = !string.IsNullOrWhiteSpace(commentText);
            var hasAttachment = !string.IsNullOrWhiteSpace(selectedEventAttachmentPath);
            var hasProgress = progressPercent.HasValue;

            if (!hasComment && !hasAttachment && !hasProgress)
            {
                MessageBox.Show("Добавьте комментарий, укажите прогресс или прикрепите файл.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (hasComment && (commentText.Length < 3 || commentText.Length > 500))
            {
                MessageBox.Show("РљРѕРјРјРµРЅС‚Р°СЂРёР№ РґРѕР»Р¶РµРЅ СЃРѕРґРµСЂР¶Р°С‚СЊ РѕС‚ 3 РґРѕ 500 СЃРёРјРІРѕР»РѕРІ", "РћС€РёР±РєР°", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!TaskReportAttachmentService.TryValidateSourceFile(selectedEventAttachmentPath, out var eventAttachmentError))
            {
                MessageBox.Show(eventAttachmentError, "РћС€РёР±РєР°", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            try
            {
                var userId = LoginWindow.CurrentUser?.UserId ?? 0;
                var reportText = hasComment ? commentText : null;
                if (!facade.AddTaskReport(task.TaskId, userId, reportText, progressPercent, selectedEventAttachmentPath))
                {
                    throw new InvalidOperationException("РќРµ СѓРґР°Р»РѕСЃСЊ СЃРѕС…СЂР°РЅРёС‚СЊ РѕС‚С‡РµС‚ РїРѕ Р·Р°РґР°С‡Рµ");
                }

                ShowToast("Отчет добавлен");
                ClearEventDraft();
                SuggestCompletionIfNeeded(progressPercent);
                LoadEvents();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"РћС€РёР±РєР° РїСЂРё СЃРѕС…СЂР°РЅРµРЅРёРё РѕС‚С‡РµС‚Р°: {ex.Message}", "РћС€РёР±РєР°", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private bool TrySaveQuickNoteCore()
        {
            if (task == null)
            {
                return false;
            }

            var noteText = txtQuickNote.Text?.Trim() ?? string.Empty;
            if (!TryGetProgressPercent(txtQuickNoteProgressPercent.Text, out var progressPercent))
            {
                txtQuickNoteProgressPercent.Focus();
                txtQuickNoteProgressPercent.SelectAll();
                return false;
            }

            var hasText = !string.IsNullOrWhiteSpace(noteText);
            var hasAttachment = !string.IsNullOrWhiteSpace(selectedQuickNoteAttachmentPath);
            var hasProgress = progressPercent.HasValue;

            if (!hasText && !hasAttachment && !hasProgress)
            {
                MessageBox.Show("Добавьте текст заметки, укажите прогресс или прикрепите файл.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (hasText && (noteText.Length < 3 || noteText.Length > 200))
            {
                MessageBox.Show("Р—Р°РјРµС‚РєР° РґРѕР»Р¶РЅР° СЃРѕРґРµСЂР¶Р°С‚СЊ РѕС‚ 3 РґРѕ 200 СЃРёРјРІРѕР»РѕРІ", "РћС€РёР±РєР°", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!TaskReportAttachmentService.TryValidateSourceFile(selectedQuickNoteAttachmentPath, out var quickAttachmentError))
            {
                MessageBox.Show(quickAttachmentError, "РћС€РёР±РєР°", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            try
            {
                var userId = LoginWindow.CurrentUser?.UserId ?? 0;
                var reportText = hasText ? noteText : null;
                if (!facade.AddTaskReport(task.TaskId, userId, reportText, progressPercent, selectedQuickNoteAttachmentPath))
                {
                    throw new InvalidOperationException("РќРµ СѓРґР°Р»РѕСЃСЊ СЃРѕС…СЂР°РЅРёС‚СЊ РѕС‚С‡РµС‚ РїРѕ Р·Р°РґР°С‡Рµ");
                }

                ShowToast("Отчет добавлен");
                ResetQuickNoteEditor();
                SuggestCompletionIfNeeded(progressPercent);
                LoadEvents();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"РћС€РёР±РєР° РїСЂРё СЃРѕС…СЂР°РЅРµРЅРёРё Р·Р°РјРµС‚РєРё: {ex.Message}", "РћС€РёР±РєР°", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private bool TrySaveQuickNote()
        {
            return TrySaveQuickNoteCore();
#if false

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

            if (!TaskReportAttachmentService.TryValidateSourceFile(selectedQuickNoteAttachmentPath, out var quickAttachmentError))
            {
                MessageBox.Show(quickAttachmentError, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            try
            {
                var userId = LoginWindow.CurrentUser?.UserId ?? 0;
                if (!facade.AddTaskReport(task.TaskId, userId, noteText, progressPercent, selectedQuickNoteAttachmentPath))
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
#endif
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
