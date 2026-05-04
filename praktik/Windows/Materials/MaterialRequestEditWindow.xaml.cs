using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using praktik.Models;
using praktik.Models.Patterns;

namespace praktik
{
    public partial class MaterialRequestEditWindow : Window
    {
        private readonly WorkPlannerFacade facade = new WorkPlannerFacade();
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
            cbMaterial.ItemsSource = facade.GetMaterialCatalog();
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

        private void BtnAddItem_Click(object sender, RoutedEventArgs e)
        {
            if (cbMaterial.SelectedItem == null)
            {
                MessageBox.Show("Выберите материал из списка.", "Заявка на материалы", MessageBoxButton.OK, MessageBoxImage.Warning);
                cbMaterial.Focus();
                return;
            }

            if (!decimal.TryParse(txtQty.Text, out decimal qty) || qty <= 0)
            {
                MessageBox.Show("Укажите количество материала. Значение должно быть больше нуля.", "Заявка на материалы", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtQty.Focus();
                txtQty.SelectAll();
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
            cbMaterial.Focus();
        }

        private void BtnDeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if (dgItems.SelectedItem is MaterialRequestItem item)
            {
                var result = MessageBox.Show(
                    $"Удалить позицию \"{item.Material?.Name ?? "Материал"}\" из заявки?",
                    "Удаление позиции",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    items.Remove(item);
                }
            }
        }

        private void DgItems_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
        }

        private void CbMaterial_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (txtMaterialUnitHint == null)
            {
                return;
            }

            var unit = (cbMaterial.SelectedItem as MaterialCatalog)?.Unit;
            txtMaterialUnitHint.Text = string.IsNullOrWhiteSpace(unit)
                ? "Введите количество"
                : $"Введите количество, ед. изм.: {unit}";
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (items.Count == 0)
            {
                MessageBox.Show("Добавьте хотя бы одну позицию в заявку.", "Заявка на материалы", MessageBoxButton.OK, MessageBoxImage.Warning);
                cbMaterial.Focus();
                return;
            }

            if (dpRequiredDate.SelectedDate.HasValue && dpRequiredDate.SelectedDate.Value < DateTime.Today)
            {
                MessageBox.Show("Дата \"Требуется к\" не может быть раньше сегодняшнего дня.", "Заявка на материалы", MessageBoxButton.OK, MessageBoxImage.Warning);
                dpRequiredDate.Focus();
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
                    facade.CreateMaterialRequest(request);
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
                    facade.UpdateMaterialRequest(request);
                }

                MessageBox.Show("Заявка сохранена.", "Заявка на материалы", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось сохранить заявку. Проверьте заполненные данные.\n\n{ex.Message}", "Заявка на материалы", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSubmit_Click(object sender, RoutedEventArgs e)
        {
            if (items.Count == 0)
            {
                MessageBox.Show("Добавьте хотя бы одну позицию в заявку.", "Заявка на материалы", MessageBoxButton.OK, MessageBoxImage.Warning);
                cbMaterial.Focus();
                return;
            }

            if (dpRequiredDate.SelectedDate.HasValue && dpRequiredDate.SelectedDate.Value < DateTime.Today)
            {
                MessageBox.Show("Дата \"Требуется к\" не может быть раньше сегодняшнего дня.", "Заявка на материалы", MessageBoxButton.OK, MessageBoxImage.Warning);
                dpRequiredDate.Focus();
                return;
            }

            var result = MessageBox.Show("Отправить заявку на согласование?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
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
                    var requestId = facade.CreateMaterialRequest(request);
                    facade.ChangeMaterialRequestStatus(requestId, "Submitted", LoginWindow.CurrentUser.UserId);
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
                    facade.UpdateMaterialRequest(request);
                    facade.ChangeMaterialRequestStatus(request.RequestId, "Submitted", LoginWindow.CurrentUser.UserId);
                }

                MessageBox.Show("Заявка отправлена на согласование.", "Заявка на материалы", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось отправить заявку. Проверьте заполненные данные.\n\n{ex.Message}", "Заявка на материалы", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

