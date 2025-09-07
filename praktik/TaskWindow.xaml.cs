using System;
using System.Linq;
using System.Windows;
using praktik.Models;

namespace praktik
{
    public partial class TaskWindow : Window
    {
        private WorkPlannerContext db = new WorkPlannerContext();
        private Task task;

        public TaskWindow(Task task = null)
        {
            InitializeComponent();
            this.task = task;
            LoadData();

            if (task != null)
            {
                LoadTaskData();
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

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            try
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

                // Получаем выбранные объекты
                var selectedSite = cbSites.SelectedItem as Site;
                var selectedCrew = cbCrews.SelectedItem as Crew;
                var selectedPriority = cbPriorities.SelectedItem as Priority;

                if (selectedSite == null)
                {
                    MessageBox.Show("Выберите объект");
                    return;
                }

                if (selectedPriority == null)
                {
                    MessageBox.Show("Выберите приоритет");
                    return;
                }

                if (task == null)
                {
                    // Создание новой задачи
                    task = new Task
                    {
                        Title = txtTitle.Text,
                        Description = txtDescription.Text,
                        StartDate = dpStartDate.SelectedDate.Value,
                        EndDate = dpEndDate.SelectedDate.Value,
                        SiteId = selectedSite.SiteId,
                        CrewId = selectedCrew?.CrewId, // Может быть null
                        PriorityId = selectedPriority.PriorityId,
                        TaskStatusId = db.GetNewTaskStatusId("Новая"),
                        CreatedBy = LoginWindow.CurrentUser?.UserId ?? 1,
                        CreatedAt = DateTime.Now
                    };

                    db.AddTask(task);
                    MessageBox.Show("Задача успешно создана");
                }
                else
                {
                    // Обновление существующей задачи
                    task.Title = txtTitle.Text;
                    task.Description = txtDescription.Text;
                    task.StartDate = dpStartDate.SelectedDate.Value;
                    task.EndDate = dpEndDate.SelectedDate.Value;
                    task.SiteId = selectedSite.SiteId;
                    task.CrewId = selectedCrew?.CrewId; // Может быть null
                    task.PriorityId = selectedPriority.PriorityId;
                    task.UpdatedAt = DateTime.Now;

                    db.UpdateTask(task);
                    MessageBox.Show("Задача успешно обновлена");
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
    }
}
