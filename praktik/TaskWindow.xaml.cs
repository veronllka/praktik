using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using praktik.Models;

namespace praktik
{
    public partial class TaskWindow : Window
    {
        private WorkPlannerContext db = new WorkPlannerContext();
        private Task task;
        private ObservableCollection<MaterialRequestDisplay> materialRequests;

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
            }
            else
            {
                dpStartDate.SelectedDate = DateTime.Now;
                dpEndDate.SelectedDate = DateTime.Now.AddDays(7);
            }
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
