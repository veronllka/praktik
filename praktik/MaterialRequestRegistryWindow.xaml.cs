using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.IO;
using Microsoft.Win32;
using praktik.Models;

namespace praktik
{
    public partial class MaterialRequestRegistryWindow : Window
    {
        private WorkPlannerContext db = new WorkPlannerContext();
        private ObservableCollection<MaterialRequestRegistryDisplay> requests;
        private MaterialRequest selectedRequest;

        public MaterialRequestRegistryWindow()
        {
            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации окна: {ex.Message}\n\n{ex.StackTrace}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            if (cbSiteFilter == null || cbCrewFilter == null || cbStatusFilter == null || dgRequests == null)
            {
                MessageBox.Show("Ошибка инициализации элементов управления. Проверьте XAML файл.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            if (requests == null)
            {
                requests = new ObservableCollection<MaterialRequestRegistryDisplay>();
            }
            
            dgRequests.ItemsSource = requests;
            
            try
            {
                LoadFilters();
                LoadRequests();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadFilters()
        {
            if (cbSiteFilter != null)
            {
                cbSiteFilter.ItemsSource = db.GetSites();
            }
            if (cbCrewFilter != null)
            {
                cbCrewFilter.ItemsSource = db.GetCrews();
            }
        }

        private void LoadRequests()
        {
            if (requests == null)
            {
                requests = new ObservableCollection<MaterialRequestRegistryDisplay>();
                if (dgRequests != null)
                {
                    dgRequests.ItemsSource = requests;
                }
            }
            
            requests.Clear();
            
            var allRequests = db.GetMaterialRequests();
            
            var filtered = allRequests.AsQueryable();
            
            if (cbStatusFilter != null && cbStatusFilter.SelectedItem is System.Windows.Controls.ComboBoxItem statusItem && 
                statusItem.Tag != null)
            {
                filtered = filtered.Where(r => r.Status == statusItem.Tag.ToString());
            }
            
            if (cbSiteFilter != null && cbSiteFilter.SelectedItem is Site site)
            {
                filtered = filtered.Where(r => r.Task != null && r.Task.SiteId == site.SiteId);
            }
            
            if (cbCrewFilter != null && cbCrewFilter.SelectedItem is Crew crew)
            {
                filtered = filtered.Where(r => r.Task != null && r.Task.CrewId.HasValue && r.Task.CrewId.Value == crew.CrewId);
            }
            
            if (dpRequiredDateFilter != null && dpRequiredDateFilter.SelectedDate.HasValue)
            {
                var date = dpRequiredDateFilter.SelectedDate.Value;
                filtered = filtered.Where(r => r.RequiredDate.HasValue && r.RequiredDate.Value >= date);
            }
            
            var tasks = db.GetTasks();
            foreach (var req in filtered.ToList())
            {
                var task = tasks.FirstOrDefault(t => t.TaskId == req.TaskId);
                requests.Add(new MaterialRequestRegistryDisplay(req, task));
            }
        }

        private void Filter_Changed(object sender, EventArgs e)
        {
            LoadRequests();
        }

        private void btnResetFilter_Click(object sender, RoutedEventArgs e)
        {
            if (cbStatusFilter != null)
                cbStatusFilter.SelectedIndex = 0;
            if (cbSiteFilter != null)
                cbSiteFilter.SelectedItem = null;
            if (cbCrewFilter != null)
                cbCrewFilter.SelectedItem = null;
            if (dpRequiredDateFilter != null)
                dpRequiredDateFilter.SelectedDate = null;
            LoadRequests();
        }

        private void dgRequests_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (dgRequests.SelectedItem is MaterialRequestRegistryDisplay display)
            {
                var allRequests = db.GetMaterialRequests();
                selectedRequest = allRequests.FirstOrDefault(r => r.RequestId == display.RequestId);
                if (selectedRequest != null)
                {
                    ShowRequestDetails(selectedRequest);
                    UpdateActionButtons();
                }
            }
            else
            {
                selectedRequest = null;
                txtSelectedRequestInfo.Text = "Выберите заявку для просмотра деталей";
                dgRequestItems.Visibility = Visibility.Collapsed;
                UpdateActionButtons();
            }
        }

        private void ShowRequestDetails(MaterialRequest request)
        {
            var task = db.GetTasks().FirstOrDefault(t => t.TaskId == request.TaskId);
            txtSelectedRequestInfo.Text = $"Заявка #{request.RequestId}\n" +
                                         $"Задача: {task?.Title ?? "Не найдена"}\n" +
                                         $"Статус: {GetStatusName(request.Status)}\n" +
                                         $"Создана: {request.CreatedAt:dd.MM.yyyy HH:mm}\n" +
                                         $"Требуется к: {request.RequiredDate?.ToString("dd.MM.yyyy") ?? "не указано"}";
            
            dgRequestItems.ItemsSource = request.Items;
            dgRequestItems.Visibility = Visibility.Visible;
        }

        private void UpdateActionButtons()
        {
            if (selectedRequest == null)
            {
                btnApprove.IsEnabled = false;
                btnReject.IsEnabled = false;
                btnIssue.IsEnabled = false;
                btnDeliver.IsEnabled = false;
                btnClose.IsEnabled = false;
                btnEdit.IsEnabled = false;
                return;
            }

            btnApprove.IsEnabled = selectedRequest.Status == "Submitted";
            btnReject.IsEnabled = selectedRequest.Status == "Submitted";
            btnIssue.IsEnabled = selectedRequest.Status == "Approved";
            btnDeliver.IsEnabled = selectedRequest.Status == "Issued";
            btnClose.IsEnabled = selectedRequest.Status == "Delivered";
            btnEdit.IsEnabled = selectedRequest.Status == "Draft" || selectedRequest.Status == "Submitted" || selectedRequest.Status == "Approved";
        }

        private void btnApprove_Click(object sender, RoutedEventArgs e)
        {
            if (selectedRequest == null) return;
            
            try
            {
                db.ChangeMaterialRequestStatus(selectedRequest.RequestId, "Approved", LoginWindow.CurrentUser.UserId);
                MessageBox.Show("Заявка согласована");
                LoadRequests();
                selectedRequest = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
            }
        }

        private void btnReject_Click(object sender, RoutedEventArgs e)
        {
            if (selectedRequest == null) return;
            
            var dialog = new MaterialRequestActionWindow("Отклонение заявки", "Причина отклонения", "Комментарий");
            if (dialog.ShowDialog() == true)
            {
                if (string.IsNullOrWhiteSpace(dialog.Note))
                {
                    MessageBox.Show("Укажите причину отклонения");
                    return;
                }
                
                try
                {
                    db.ChangeMaterialRequestStatus(selectedRequest.RequestId, "Rejected", LoginWindow.CurrentUser.UserId, dialog.Note);
                    MessageBox.Show("Заявка отклонена");
                    LoadRequests();
                    selectedRequest = null;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}");
                }
            }
        }

        private void btnIssue_Click(object sender, RoutedEventArgs e)
        {
            if (selectedRequest == null) return;
            
            var dialog = new MaterialRequestActionWindow("Выдача материалов", "Номер документа", "Примечание");
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    db.ChangeMaterialRequestStatus(selectedRequest.RequestId, "Issued", LoginWindow.CurrentUser.UserId, dialog.Note);
                    if (!string.IsNullOrEmpty(dialog.DocNumber))
                    {
                        db.AddMaterialDeliveryDoc(selectedRequest.RequestId, "Issued", dialog.DocNumber, dialog.Note);
                    }
                    MessageBox.Show("Выдача отмечена");
                    LoadRequests();
                    selectedRequest = null;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}");
                }
            }
        }

        private void btnDeliver_Click(object sender, RoutedEventArgs e)
        {
            if (selectedRequest == null) return;
            
            var dialog = new MaterialRequestActionWindow("Доставка материалов", "Номер документа", "Примечание");
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    db.ChangeMaterialRequestStatus(selectedRequest.RequestId, "Delivered", LoginWindow.CurrentUser.UserId, dialog.Note);
                    if (!string.IsNullOrEmpty(dialog.DocNumber))
                    {
                        db.AddMaterialDeliveryDoc(selectedRequest.RequestId, "Delivered", dialog.DocNumber, dialog.Note);
                    }
                    MessageBox.Show("Доставка отмечена");
                    LoadRequests();
                    selectedRequest = null;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}");
                }
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            if (selectedRequest == null) return;
            
            var result = MessageBox.Show("Закрыть заявку?", "Подтверждение", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    db.ChangeMaterialRequestStatus(selectedRequest.RequestId, "Closed", LoginWindow.CurrentUser.UserId);
                    MessageBox.Show("Заявка закрыта");
                    LoadRequests();
                    selectedRequest = null;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}");
                }
            }
        }

        private void btnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (selectedRequest == null) return;
            
            var window = new MaterialRequestEditWindow(selectedRequest.TaskId, selectedRequest);
            if (window.ShowDialog() == true)
            {
                LoadRequests();
            }
        }

        private void btnExport_Click(object sender, RoutedEventArgs e)
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "CSV файлы (*.csv)|*.csv|Excel файлы (*.xlsx)|*.xlsx",
                FileName = $"Заявки_{DateTime.Now:yyyyMMdd_HHmmss}"
            };
            
            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    ExportToFile(saveDialog.FileName, saveDialog.FilterIndex == 1);
                    MessageBox.Show("Экспорт выполнен успешно");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при экспорте: {ex.Message}");
                }
            }
        }

        private void ExportToFile(string fileName, bool isCsv)
        {
            var tasks = db.GetTasks();
            var sites = db.GetSites();
            var crews = db.GetCrews();
            
            using (var writer = new StreamWriter(fileName, false, System.Text.Encoding.UTF8))
            {
                if (isCsv)
                {
                    writer.WriteLine("ID;Задача;Объект;Бригада;Статус;Требуется к;Создана;Позиции");
                    
                    foreach (var req in requests)
                    {
                        var task = tasks.FirstOrDefault(t => t.TaskId == req.TaskId);
                        var site = task != null ? sites.FirstOrDefault(s => s.SiteId == task.SiteId) : null;
                        var crew = task != null && task.CrewId.HasValue ? crews.FirstOrDefault(c => c.CrewId == task.CrewId.Value) : null;
                        
                        var items = selectedRequest?.Items != null && selectedRequest.RequestId == req.RequestId
                            ? string.Join("; ", selectedRequest.Items.Select(i => $"{i.Material.Name} {i.Qty} {i.Material.Unit}"))
                            : $"{req.ItemsCount} позиций";
                        
                        writer.WriteLine($"{req.RequestId};{req.TaskTitle};{site?.SiteName ?? ""};{crew?.CrewName ?? ""};{req.StatusDisplay};{req.RequiredDateDisplay};{req.CreatedAtDisplay};{items}");
                    }
                }
                else
                {
                    ExportToFile(fileName.Replace(".xlsx", ".csv"), true);
                }
            }
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

    public class MaterialRequestRegistryDisplay
    {
        public int RequestId { get; set; }
        public int TaskId { get; set; }
        public string TaskTitle { get; set; }
        public string SiteName { get; set; }
        public string CrewName { get; set; }
        public string Status { get; set; }
        public string StatusDisplay { get; set; }
        public DateTime? RequiredDate { get; set; }
        public string RequiredDateDisplay { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedAtDisplay { get; set; }
        public int ItemsCount { get; set; }
        public string IndicatorText { get; set; }
        public string IndicatorColor { get; set; }

        public MaterialRequestRegistryDisplay(MaterialRequest request, Task task = null)
        {
            RequestId = request.RequestId;
            TaskId = request.TaskId;
            TaskTitle = task?.Title ?? request.Task?.Title ?? "Не найдена";
            SiteName = task?.Site?.SiteName ?? request.Task?.Site?.SiteName ?? "";
            CrewName = task?.Crew?.CrewName ?? request.Task?.Crew?.CrewName ?? "";
            Status = request.Status;
            StatusDisplay = GetStatusName(request.Status);
            RequiredDate = request.RequiredDate;
            RequiredDateDisplay = request.RequiredDate?.ToString("dd.MM.yyyy") ?? "-";
            CreatedAt = request.CreatedAt;
            CreatedAtDisplay = request.CreatedAt.ToString("dd.MM.yyyy HH:mm");
            ItemsCount = request.Items?.Count ?? 0;
            
            if (request.RequiredDate.HasValue)
            {
                var daysUntil = (request.RequiredDate.Value - DateTime.Today).Days;
                if (daysUntil < 0)
                {
                    IndicatorText = "ПРОСРОЧЕНО";
                    IndicatorColor = "#E53935";
                }
                else if (daysUntil == 0)
                {
                    IndicatorText = "СЕГОДНЯ";
                    IndicatorColor = "#FF9800";
                }
                else if (daysUntil == 1)
                {
                    IndicatorText = "ЗАВТРА";
                    IndicatorColor = "#FDD835";
                }
                else
                {
                    IndicatorText = "";
                    IndicatorColor = "Transparent";
                }
            }
            else
            {
                IndicatorText = "";
                IndicatorColor = "Transparent";
            }
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

