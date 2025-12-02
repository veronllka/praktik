using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using praktik.Models;
using System.IO;
using Microsoft.Win32;

namespace praktik
{
    // Partial class содержащий методы для работы с заявками на материалы
    public partial class MainWindow
    {
        private ObservableCollection<MaterialRequestRegistryDisplay> materialRequests;
        private MaterialRequest selectedMaterialRequest;

        private void LoadMaterialRequests()
        {
            if (cbMRSiteFilter == null || cbMRCrewFilter == null || cbMRStatusFilter == null || dgMaterialRequests == null)
            {
                return;
            }

            cbMRSiteFilter.ItemsSource = db.GetSites();
            cbMRCrewFilter.ItemsSource = db.GetCrews();

            if (materialRequests == null)
            {
                materialRequests = new ObservableCollection<MaterialRequestRegistryDisplay>();
                dgMaterialRequests.ItemsSource = materialRequests;
            }

            materialRequests.Clear();

            var allRequests = db.GetMaterialRequests();
            var filtered = allRequests.AsQueryable();

            if (cbMRStatusFilter != null && cbMRStatusFilter.SelectedItem is ComboBoxItem statusItem &&
                statusItem.Tag != null)
            {
                filtered = filtered.Where(r => r.Status == statusItem.Tag.ToString());
            }

            if (cbMRSiteFilter != null && cbMRSiteFilter.SelectedItem is Site site)
            {
                filtered = filtered.Where(r => r.Task != null && r.Task.SiteId == site.SiteId);
            }

            if (cbMRCrewFilter != null && cbMRCrewFilter.SelectedItem is Crew crew)
            {
                filtered = filtered.Where(r => r.Task != null && r.Task.CrewId.HasValue && r.Task.CrewId.Value == crew.CrewId);
            }

            if (dpMRRequiredDateFilter != null && dpMRRequiredDateFilter.SelectedDate.HasValue)
            {
                var date = dpMRRequiredDateFilter.SelectedDate.Value;
                filtered = filtered.Where(r => r.RequiredDate.HasValue && r.RequiredDate.Value >= date);
            }

            var tasks = db.GetTasks();
            foreach (var req in filtered.ToList())
            {
                var task = tasks.FirstOrDefault(t => t.TaskId == req.TaskId);
                materialRequests.Add(new MaterialRequestRegistryDisplay(req, task));
            }
        }

        private void MRFilter_Changed(object sender, EventArgs e)
        {
            LoadMaterialRequests();
        }

        private void btnMRResetFilter_Click(object sender, RoutedEventArgs e)
        {
            if (cbMRStatusFilter != null)
                cbMRStatusFilter.SelectedIndex = 0;
            if (cbMRSiteFilter != null)
                cbMRSiteFilter.SelectedItem = null;
            if (cbMRCrewFilter != null)
                cbMRCrewFilter.SelectedItem = null;
            if (dpMRRequiredDateFilter != null)
                dpMRRequiredDateFilter.SelectedDate = null;
            LoadMaterialRequests();
        }

        private void dgMaterialRequests_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgMaterialRequests.SelectedItem is MaterialRequestRegistryDisplay display)
            {
                var allRequests = db.GetMaterialRequests();
                selectedMaterialRequest = allRequests.FirstOrDefault(r => r.RequestId == display.RequestId);
                if (selectedMaterialRequest != null)
                {
                    ShowMaterialRequestDetails(selectedMaterialRequest);
                    UpdateMaterialRequestActionButtons();
                }
            }
            else
            {
                selectedMaterialRequest = null;
                txtMRSelectedInfo.Text = "Выберите заявку для просмотра деталей";
                dgMRItems.Visibility = Visibility.Collapsed;
                UpdateMaterialRequestActionButtons();
            }
        }

        private void ShowMaterialRequestDetails(MaterialRequest request)
        {
            var task = db.GetTasks().FirstOrDefault(t => t.TaskId == request.TaskId);
            txtMRSelectedInfo.Text = $"Заявка #{request.RequestId}\n" +
                                    $"Задача: {task?.Title ?? "Не найдена"}\n" +
                                    $"Статус: {GetMRStatusName(request.Status)}\n" +
                                    $"Создана: {request.CreatedAt:dd.MM.yyyy HH:mm}\n" +
                                    $"Требуется к: {request.RequiredDate?.ToString("dd.MM.yyyy") ?? "не указано"}";

            dgMRItems.ItemsSource = request.Items;
            dgMRItems.Visibility = Visibility.Visible;
        }

        private void UpdateMaterialRequestActionButtons()
        {
            if (selectedMaterialRequest == null)
            {
                btnMRApprove.IsEnabled = false;
                btnMRReject.IsEnabled = false;
                btnMRIssue.IsEnabled = false;
                btnMRDeliver.IsEnabled = false;
                btnMRClose.IsEnabled = false;
                btnMREdit.IsEnabled = false;
                return;
            }

            btnMRApprove.IsEnabled = selectedMaterialRequest.Status == "Submitted";
            btnMRReject.IsEnabled = selectedMaterialRequest.Status == "Submitted";
            btnMRIssue.IsEnabled = selectedMaterialRequest.Status == "Approved";
            btnMRDeliver.IsEnabled = selectedMaterialRequest.Status == "Issued";
            btnMRClose.IsEnabled = selectedMaterialRequest.Status == "Delivered";
            btnMREdit.IsEnabled = selectedMaterialRequest.Status == "Draft" || selectedMaterialRequest.Status == "Submitted" || selectedMaterialRequest.Status == "Approved";
        }

        private void btnMRApprove_Click(object sender, RoutedEventArgs e)
        {
            if (selectedMaterialRequest == null) return;

            try
            {
                db.ChangeMaterialRequestStatus(selectedMaterialRequest.RequestId, "Approved", LoginWindow.CurrentUser.UserId);
                MessageBox.Show("Заявка согласована");
                LoadMaterialRequests();
                selectedMaterialRequest = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
            }
        }

        private void btnMRReject_Click(object sender, RoutedEventArgs e)
        {
            if (selectedMaterialRequest == null) return;

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
                    db.ChangeMaterialRequestStatus(selectedMaterialRequest.RequestId, "Rejected", LoginWindow.CurrentUser.UserId, dialog.Note);
                    MessageBox.Show("Заявка отклонена");
                    LoadMaterialRequests();
                    selectedMaterialRequest = null;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}");
                }
            }
        }

        private void btnMRIssue_Click(object sender, RoutedEventArgs e)
        {
            if (selectedMaterialRequest == null) return;

            var dialog = new MaterialRequestActionWindow("Выдача материалов", "Номер документа", "Примечание");
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    db.ChangeMaterialRequestStatus(selectedMaterialRequest.RequestId, "Issued", LoginWindow.CurrentUser.UserId, dialog.Note);
                    if (!string.IsNullOrEmpty(dialog.DocNumber))
                    {
                        db.AddMaterialDeliveryDoc(selectedMaterialRequest.RequestId, "Issued", dialog.DocNumber, dialog.Note);
                    }
                    MessageBox.Show("Выдача отмечена");
                    LoadMaterialRequests();
                    selectedMaterialRequest = null;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}");
                }
            }
        }

        private void btnMRDeliver_Click(object sender, RoutedEventArgs e)
        {
            if (selectedMaterialRequest == null) return;

            var dialog = new MaterialRequestActionWindow("Доставка материалов", "Номер документа", "Примечание");
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    db.ChangeMaterialRequestStatus(selectedMaterialRequest.RequestId, "Delivered", LoginWindow.CurrentUser.UserId, dialog.Note);
                    if (!string.IsNullOrEmpty(dialog.DocNumber))
                    {
                        db.AddMaterialDeliveryDoc(selectedMaterialRequest.RequestId, "Delivered", dialog.DocNumber, dialog.Note);
                    }
                    MessageBox.Show("Доставка отмечена");
                    LoadMaterialRequests();
                    selectedMaterialRequest = null;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}");
                }
            }
        }

        private void btnMRClose_Click(object sender, RoutedEventArgs e)
        {
            if (selectedMaterialRequest == null) return;

            var result = MessageBox.Show("Закрыть заявку?", "Подтверждение", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    db.ChangeMaterialRequestStatus(selectedMaterialRequest.RequestId, "Closed", LoginWindow.CurrentUser.UserId);
                    MessageBox.Show("Заявка закрыта");
                    LoadMaterialRequests();
                    selectedMaterialRequest = null;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}");
                }
            }
        }

        private void btnMREdit_Click(object sender, RoutedEventArgs e)
        {
            if (selectedMaterialRequest == null) return;

            var window = new MaterialRequestEditWindow(selectedMaterialRequest.TaskId, selectedMaterialRequest);
            if (window.ShowDialog() == true)
            {
                LoadMaterialRequests();
            }
        }

        private void btnMRAddRequest_Click(object sender, RoutedEventArgs e)
        {
            var taskSelectionWindow = new TaskSelectionWindow();
            if (taskSelectionWindow.ShowDialog() == true && taskSelectionWindow.SelectedTask != null)
            {
                var window = new MaterialRequestEditWindow(taskSelectionWindow.SelectedTask.TaskId);
                if (window.ShowDialog() == true)
                {
                    LoadMaterialRequests();
                }
            }
        }

        private void btnMRExport_Click(object sender, RoutedEventArgs e)
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "CSV файлы (*.csv)|*.csv",
                FileName = $"Заявки_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    ExportMaterialRequestsToFile(saveDialog.FileName);
                    MessageBox.Show("Экспорт выполнен успешно");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при экспорте: {ex.Message}");
                }
            }
        }

        private void ExportMaterialRequestsToFile(string fileName)
        {
            var tasks = db.GetTasks();
            var sites = db.GetSites();
            var crews = db.GetCrews();

            using (var writer = new StreamWriter(fileName, false, System.Text.Encoding.UTF8))
            {
                writer.WriteLine("ID;Задача;Объект;Бригада;Статус;Требуется к;Создана;Позиции");

                foreach (var req in materialRequests)
                {
                    var task = tasks.FirstOrDefault(t => t.TaskId == req.TaskId);
                    var site = task != null ? sites.FirstOrDefault(s => s.SiteId == task.SiteId) : null;
                    var crew = task != null && task.CrewId.HasValue ? crews.FirstOrDefault(c => c.CrewId == task.CrewId.Value) : null;

                    var items = selectedMaterialRequest?.Items != null && selectedMaterialRequest.RequestId == req.RequestId
                        ? string.Join("; ", selectedMaterialRequest.Items.Select(i => $"{i.Material.Name} {i.Qty} {i.Material.Unit}"))
                        : $"{req.ItemsCount} позиций";

                    writer.WriteLine($"{req.RequestId};{req.TaskTitle};{site?.SiteName ?? ""};{crew?.CrewName ?? ""};{req.StatusDisplay};{req.RequiredDateDisplay};{req.CreatedAtDisplay};{items}");
                }
            }
        }

        private string GetMRStatusName(string status)
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
