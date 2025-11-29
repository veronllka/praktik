using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using praktik.Models;

namespace praktik
{
    public partial class TaskWindow : Window
    {
        private WorkPlannerContext db = new WorkPlannerContext();
        private Task task;
        private ObservableCollection<MaterialRequestDisplay> materialRequests;
        private bool hasUnsavedNote = false;

        public TaskWindow(Task task = null)
        {
            InitializeComponent();
            this.task = task;
            materialRequests = new ObservableCollection<MaterialRequestDisplay>();
            dgMaterialRequests.ItemsSource = materialRequests;
            
            LoadData();

            if (task != null)
            {
                LoadTaskData();
                LoadMaterialRequests();
                CheckAwaitMTSLabel();
                btnPrint.Visibility = Visibility.Visible;
                LoadEvents();
                CheckQuickNotePermissions();
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
            cbSites.ItemsSource = db.GetSites();
            cbCrews.ItemsSource = db.GetCrews();
            cbPriorities.ItemsSource = db.GetPriorities();
        }

        private void LoadTaskData()
        {
            txtTitle.Text = task.Title;
            txtDescription.Text = task.Description;
            dpStartDate.SelectedDate = task.StartDate;
            dpEndDate.SelectedDate = task.EndDate;
            cbSites.SelectedItem = task.Site;
            cbCrews.SelectedItem = task.Crew;
            cbPriorities.SelectedItem = task.Priority;
        }

        private void LoadMaterialRequests()
        {
            materialRequests.Clear();
            var requests = db.GetMaterialRequests(task?.TaskId);
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

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtTitle.Text) || cbSites.SelectedItem == null ||
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
                        SiteId = (cbSites.SelectedItem as Site).SiteId,
                        CrewId = cbCrews.SelectedItem != null ? (cbCrews.SelectedItem as Crew).CrewId : (int?)null,
                        PriorityId = (cbPriorities.SelectedItem as Priority).PriorityId,
                        TaskStatusId = db.GetNewTaskStatusId("New"),
                        CreatedBy = LoginWindow.CurrentUser.UserId,
                        CreatedAt = DateTime.Now
                    };

                    db.AddTask(task);
                }
                else
                {
                    task.Title = txtTitle.Text;
                    task.Description = txtDescription.Text;
                    task.StartDate = dpStartDate.SelectedDate.Value;
                    task.EndDate = dpEndDate.SelectedDate.Value;
                    task.SiteId = (cbSites.SelectedItem as Site).SiteId;
                    task.CrewId = cbCrews.SelectedItem != null ? (cbCrews.SelectedItem as Crew).CrewId : (int?)null;
                    task.PriorityId = (cbPriorities.SelectedItem as Priority).PriorityId;
                    task.UpdatedAt = DateTime.Now;

                    db.UpdateTask(task);
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}");
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void btnNewRequest_Click(object sender, RoutedEventArgs e)
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

        private void btnEditRequest_Click(object sender, RoutedEventArgs e)
        {
            if (dgMaterialRequests.SelectedItem is MaterialRequestDisplay display)
            {
                var request = db.GetMaterialRequests(task.TaskId, null).FirstOrDefault(r => r.RequestId == display.RequestId);
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

        private void btnViewRequestHistory_Click(object sender, RoutedEventArgs e)
        {
            if (task == null)
            {
                MessageBox.Show("Сначала сохраните задачу");
                return;
            }

            var reports = db.GetTaskReports(task.TaskId);
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

        private void dgMaterialRequests_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            btnEditRequest.IsEnabled = dgMaterialRequests.SelectedItem != null;
        }

        private void btnPrint_Click(object sender, RoutedEventArgs e)
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

            var reports = db.GetTaskReports(task.TaskId);
            var events = new List<EventDisplay>();

            foreach (var report in reports)
            {
                events.Add(new EventDisplay
                {
                    DisplayText = report.ReportText ?? "Событие",
                    TimeInfo = $"{report.ReporterName} • {report.ReportedAt:dd.MM.yyyy HH:mm}"
                });
            }

            lbEvents.ItemsSource = events;
        }

        private void btnAddQuickNote_Click(object sender, RoutedEventArgs e)
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

        private void txtQuickNote_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            var text = txtQuickNote.Text?.Trim() ?? "";
            btnSaveNote.IsEnabled = text.Length >= 3 && text.Length <= 200;
            hasUnsavedNote = !string.IsNullOrWhiteSpace(text);
        }

        private void txtQuickNote_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter && btnSaveNote.IsEnabled)
            {
                btnSaveNote_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.Escape)
            {
                btnCancelNote_Click(sender, e);
                e.Handled = true;
            }
        }

        private void btnSaveNote_Click(object sender, RoutedEventArgs e)
        {
            if (task == null) return;

            var noteText = txtQuickNote.Text?.Trim() ?? "";
            
            if (noteText.Length < 3 || noteText.Length > 200)
            {
                MessageBox.Show("Заметка должна содержать от 3 до 200 символов", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var userId = LoginWindow.CurrentUser?.UserId ?? 0;
                db.AddTaskReport(task.TaskId, userId, noteText);

                ShowToast("Заметка добавлена");
                
                txtQuickNote.Text = "";
                pnlQuickNote.Visibility = Visibility.Collapsed;
                btnAddQuickNote.Visibility = Visibility.Visible;
                hasUnsavedNote = false;

                LoadEvents();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении заметки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnCancelNote_Click(object sender, RoutedEventArgs e)
        {
            if (hasUnsavedNote)
            {
                var result = MessageBox.Show("Сохранить черновик?", "Несохраненная заметка", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    btnSaveNote_Click(sender, e);
                    return;
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    return;
                }
            }

            txtQuickNote.Text = "";
            pnlQuickNote.Visibility = Visibility.Collapsed;
            btnAddQuickNote.Visibility = Visibility.Visible;
            hasUnsavedNote = false;
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
                        btnSaveNote_Click(null, null);
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
