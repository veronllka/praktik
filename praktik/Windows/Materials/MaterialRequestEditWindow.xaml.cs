using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
        private ObservableCollection<MaterialPickerItem> materialPickerItems;

        public MaterialRequestEditWindow(int taskId, MaterialRequest existingRequest = null)
        {
            InitializeComponent();
            this.taskId = taskId;
            this.request = existingRequest;
            items = new ObservableCollection<MaterialRequestItem>();
            materialPickerItems = new ObservableCollection<MaterialPickerItem>();
            dgItems.ItemsSource = items;
            cbMaterial.ItemsSource = materialPickerItems;

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
            materialPickerItems.Clear();

            foreach (var material in facade.GetMaterialCatalog())
            {
                materialPickerItems.Add(MaterialPickerItem.FromMaterial(material));
            }

            materialPickerItems.Add(MaterialPickerItem.CreateAddOption());
        }

        private void SelectMaterial(int materialId)
        {
            cbMaterial.SelectedItem = materialPickerItems
                .FirstOrDefault(item => item.Material != null && item.Material.MaterialId == materialId);
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

            var material = GetSelectedMaterial();
            if (material == null)
            {
                MessageBox.Show("Выберите материал из списка.", "Заявка на материалы", MessageBoxButton.OK, MessageBoxImage.Warning);
                cbMaterial.Focus();
                return;
            }

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

            if (cbMaterial.SelectedItem is MaterialPickerItem selectedItem && selectedItem.IsAddOption)
            {
                cbMaterial.SelectedItem = null;
                txtMaterialUnitHint.Text = "Введите количество";
                Dispatcher.BeginInvoke(new Action(OpenCreateMaterialDialog));
                return;
            }

            var unit = GetSelectedMaterial()?.Unit;
            txtMaterialUnitHint.Text = string.IsNullOrWhiteSpace(unit)
                ? "Введите количество"
                : $"Введите количество, ед. изм.: {unit}";
        }

        private void BtnCreateMaterial_Click(object sender, RoutedEventArgs e)
        {
            OpenCreateMaterialDialog();
        }

        private MaterialCatalog GetSelectedMaterial()
        {
            return (cbMaterial.SelectedItem as MaterialPickerItem)?.Material;
        }

        private void OpenCreateMaterialDialog()
        {
            var dialog = new MaterialCatalogDialog
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true || dialog.Material == null)
            {
                return;
            }

            try
            {
                var materialId = facade.CreateMaterialCatalogItem(dialog.Material);
                LoadMaterials();
                SelectMaterial(materialId);
                txtQty.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось добавить материал.\n\n{ex.Message}", "Материалы", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

        private sealed class MaterialPickerItem
        {
            public MaterialCatalog Material { get; private set; }
            public bool IsAddOption { get; private set; }
            public string Name { get; private set; }
            public string UnitHint { get; private set; }

            public Visibility UnitHintVisibility
            {
                get { return string.IsNullOrWhiteSpace(UnitHint) ? Visibility.Collapsed : Visibility.Visible; }
            }

            public FontWeight FontWeight
            {
                get { return IsAddOption ? FontWeights.SemiBold : FontWeights.Normal; }
            }

            public Brush Foreground
            {
                get { return IsAddOption ? Brushes.White : Brushes.Black; }
            }

            public Brush Background
            {
                get
                {
                    return IsAddOption
                        ? new SolidColorBrush(Color.FromRgb(124, 93, 250))
                        : Brushes.Transparent;
                }
            }

            public static MaterialPickerItem FromMaterial(MaterialCatalog material)
            {
                return new MaterialPickerItem
                {
                    Material = material,
                    Name = material?.Name ?? string.Empty,
                    UnitHint = string.IsNullOrWhiteSpace(material?.Unit) ? string.Empty : $"Ед. изм.: {material.Unit}"
                };
            }

            public static MaterialPickerItem CreateAddOption()
            {
                return new MaterialPickerItem
                {
                    IsAddOption = true,
                    Name = "Добавить +",
                    UnitHint = "Создать новый материал"
                };
            }
        }

        private sealed class MaterialCatalogDialog : Window
        {
            private readonly TextBox txtName;
            private readonly TextBox txtUnit;
            private readonly TextBlock txtNamePlaceholder;
            private readonly TextBlock txtUnitPlaceholder;

            public MaterialCatalog Material { get; private set; }

            public MaterialCatalogDialog()
            {
                Title = "Новый материал";
                Width = 500;
                Height = 350;
                MinWidth = 500;
                MinHeight = 350;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
                ResizeMode = ResizeMode.NoResize;

                var root = new Grid
                {
                    Background = TryFindResource("AppWindowBackgroundBrush") as Brush ?? Brushes.White
                };
                Content = root;

                var card = new Border
                {
                    Padding = new Thickness(26),
                    Margin = new Thickness(22),
                    CornerRadius = new CornerRadius(20),
                    Background = TryFindResource("GlassCardBrush") as Brush ?? Brushes.White,
                    BorderBrush = TryFindResource("GlassShellBorderBrush") as Brush ?? Brushes.LightGray,
                    BorderThickness = new Thickness(1)
                };
                root.Children.Add(card);

                var layout = new Grid();
                layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) });
                layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
                layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });
                layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                card.Child = layout;

                var header = new StackPanel();
                Grid.SetRow(header, 0);
                layout.Children.Add(header);
                header.Children.Add(new TextBlock
                {
                    Text = "Добавить материал",
                    FontSize = 24,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = TryFindResource("AppTextBrush") as Brush ?? Brushes.Black,
                    Margin = new Thickness(0, 0, 0, 4)
                });
                header.Children.Add(new TextBlock
                {
                    Text = "Заполните название и единицу измерения.",
                    FontSize = 13,
                    Foreground = TryFindResource("AppMutedTextBrush") as Brush ?? Brushes.DimGray
                });

                txtName = CreateInput(layout, 2, "Название материала *", "Введите название материала", out txtNamePlaceholder);
                txtUnit = CreateInput(layout, 4, "Единица измерения *", "Например: шт., кг, м2, м3, л, упак.", out txtUnitPlaceholder);

                var buttons = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                Grid.SetRow(buttons, 6);
                layout.Children.Add(buttons);

                buttons.Children.Add(new Button
                {
                    Content = "Отмена",
                    Width = 128,
                    Margin = new Thickness(0, 0, 10, 0),
                    IsCancel = true,
                    Style = TryFindResource("AppOutlinedButton") as Style
                });

                var addButton = new Button
                {
                    Content = "Добавить",
                    Width = 128,
                    IsDefault = true,
                    Style = TryFindResource("AppRaisedButton") as Style
                };
                addButton.Click += BtnSave_Click;
                buttons.Children.Add(addButton);

                Loaded += (sender, args) => txtName.Focus();
                UpdatePlaceholders();
            }

            private TextBox CreateInput(Grid layout, int row, string label, string placeholder, out TextBlock placeholderBlock)
            {
                var panel = new StackPanel();
                Grid.SetRow(panel, row);
                layout.Children.Add(panel);

                panel.Children.Add(new TextBlock
                {
                    Text = label,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = TryFindResource("AppMutedTextBrush") as Brush ?? Brushes.DimGray,
                    Margin = new Thickness(2, 0, 0, 6)
                });

                var inputHost = new Grid();
                panel.Children.Add(inputHost);

                var textBox = new TextBox
                {
                    Height = 48,
                    Padding = new Thickness(14, 0, 14, 0),
                    VerticalContentAlignment = VerticalAlignment.Center,
                    ToolTip = placeholder
                };
                textBox.TextChanged += (sender, args) => UpdatePlaceholders();
                inputHost.Children.Add(textBox);

                placeholderBlock = new TextBlock
                {
                    Text = placeholder,
                    Margin = new Thickness(14, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = TryFindResource("AppSubtleTextBrush") as Brush ?? Brushes.Gray,
                    IsHitTestVisible = false
                };
                inputHost.Children.Add(placeholderBlock);

                return textBox;
            }

            private void UpdatePlaceholders()
            {
                if (txtNamePlaceholder != null)
                {
                    txtNamePlaceholder.Visibility = string.IsNullOrWhiteSpace(txtName.Text)
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }

                if (txtUnitPlaceholder != null)
                {
                    txtUnitPlaceholder.Visibility = string.IsNullOrWhiteSpace(txtUnit.Text)
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }
            }

            private void BtnSave_Click(object sender, RoutedEventArgs e)
            {
                string name = (txtName.Text ?? string.Empty).Trim();
                string unit = (txtUnit.Text ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(name))
                {
                    MessageBox.Show("Введите название материала.", "Материалы", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtName.Focus();
                    return;
                }

                if (string.IsNullOrWhiteSpace(unit))
                {
                    MessageBox.Show("Введите единицу измерения.", "Материалы", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtUnit.Focus();
                    return;
                }

                Material = new MaterialCatalog
                {
                    Name = name,
                    Unit = unit,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };

                DialogResult = true;
                Close();
            }
        }
    }
}

