using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using praktik.Models;

namespace praktik
{
    public partial class MaterialRequestEditWindow : Window
    {
        private WorkPlannerContext db = new WorkPlannerContext();
        private MaterialRequest request;
        private int taskId;
        private ObservableCollection<MaterialRequestItem> items;

        public MaterialRequestEditWindow(int taskId, MaterialRequest existingRequest = null)
        {
            InitializeComponent();
            this.taskId = taskId;
            this.request = existingRequest;
            items = new ObservableCollection<MaterialRequestItem>();
            dgItems.ItemsSource = items;

            LoadMaterials();
            
            if (request != null)
            {
                LoadRequestData();
            }
            else
            {
                txtStatus.Text = "Черновик";
                dpRequiredDate.SelectedDate = DateTime.Now.AddDays(2);
            }
        }

        private void LoadMaterials()
        {
            cbMaterial.ItemsSource = db.GetMaterialCatalog();
        }

        private void LoadRequestData()
        {
            txtStatus.Text = GetStatusName(request.Status);
            dpRequiredDate.SelectedDate = request.RequiredDate;
            txtComment.Text = request.Comment;
            
            foreach (var item in request.Items)
            {
                items.Add(new MaterialRequestItem
                {
                    RequestItemId = item.RequestItemId,
                    MaterialId = item.MaterialId,
                    Qty = item.Qty,
                    Comment = item.Comment,
                    Material = item.Material
                });
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

        private void btnAddItem_Click(object sender, RoutedEventArgs e)
        {
            if (cbMaterial.SelectedItem == null)
            {
                MessageBox.Show("Выберите материал");
                return;
            }

            if (!decimal.TryParse(txtQty.Text, out decimal qty) || qty <= 0)
            {
                MessageBox.Show("Введите корректное количество (больше 0)");
                return;
            }

            var material = cbMaterial.SelectedItem as MaterialCatalog;
            items.Add(new MaterialRequestItem
            {
                MaterialId = material.MaterialId,
                Qty = qty,
                Comment = txtItemComment.Text,
                Material = material
            });

            txtQty.Text = "";
            txtItemComment.Text = "";
            cbMaterial.SelectedItem = null;
        }

        private void btnDeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if (dgItems.SelectedItem is MaterialRequestItem item)
            {
                items.Remove(item);
            }
        }

        private void dgItems_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            if (items.Count == 0)
            {
                MessageBox.Show("Добавьте хотя бы одну позицию в заявку");
                return;
            }

            if (dpRequiredDate.SelectedDate.HasValue && dpRequiredDate.SelectedDate.Value < DateTime.Today)
            {
                MessageBox.Show("Дата 'требуется к' не может быть в прошлом");
                return;
            }

            try
            {
                if (request == null)
                {
                    request = new MaterialRequest
                    {
                        TaskId = taskId,
                        CreatedByUserId = LoginWindow.CurrentUser.UserId,
                        RequiredDate = dpRequiredDate.SelectedDate,
                        Status = "Draft",
                        Comment = txtComment.Text,
                        Items = items.ToList()
                    };
                    db.CreateMaterialRequest(request);
                }
                else
                {
                    if (request.Status != "Draft")
                    {
                        MessageBox.Show("Редактирование возможно только для черновиков");
                        return;
                    }

                    request.RequiredDate = dpRequiredDate.SelectedDate;
                    request.Comment = txtComment.Text;
                    request.Items = items.ToList();
                    db.UpdateMaterialRequest(request);
                }

                MessageBox.Show("Заявка сохранена");
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}");
            }
        }

        private void btnSubmit_Click(object sender, RoutedEventArgs e)
        {
            if (items.Count == 0)
            {
                MessageBox.Show("Добавьте хотя бы одну позицию в заявку");
                return;
            }

            if (dpRequiredDate.SelectedDate.HasValue && dpRequiredDate.SelectedDate.Value < DateTime.Today)
            {
                MessageBox.Show("Дата 'требуется к' не может быть в прошлом");
                return;
            }

            var result = MessageBox.Show("Отправить заявку на согласование?", "Подтверждение", MessageBoxButton.YesNo);
            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                if (request == null)
                {
                    request = new MaterialRequest
                    {
                        TaskId = taskId,
                        CreatedByUserId = LoginWindow.CurrentUser.UserId,
                        RequiredDate = dpRequiredDate.SelectedDate,
                        Status = "Draft",
                        Comment = txtComment.Text,
                        Items = items.ToList()
                    };
                    var requestId = db.CreateMaterialRequest(request);
                    db.ChangeMaterialRequestStatus(requestId, "Submitted", LoginWindow.CurrentUser.UserId);
                }
                else
                {
                    if (request.Status != "Draft")
                    {
                        MessageBox.Show("Отправка возможна только для черновиков");
                        return;
                    }

                    request.RequiredDate = dpRequiredDate.SelectedDate;
                    request.Comment = txtComment.Text;
                    request.Items = items.ToList();
                    db.UpdateMaterialRequest(request);
                    db.ChangeMaterialRequestStatus(request.RequestId, "Submitted", LoginWindow.CurrentUser.UserId);
                }

                MessageBox.Show("Заявка отправлена на согласование");
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при отправке: {ex.Message}");
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

