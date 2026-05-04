using System;
using System.Linq;
using System.Windows;
using praktik.Models;
using praktik.Models.Patterns;

namespace praktik
{
    public partial class TaskStatusWindow : Window
    {
        private readonly WorkPlannerFacade facade = new WorkPlannerFacade();
        private Task task;

        public TaskStatusWindow(Task task)
        {
            InitializeComponent();
            this.task = task;
            LoadData();
        }

        private void LoadData()
        {
            txtTaskInfo.Text = $"Задача: {task.Title}\nТекущий статус: {task.TaskStatus.TaskStatusName}";
            cbStatuses.ItemsSource = facade.GetTaskStatuses();
            cbStatuses.SelectedItem = task.TaskStatus;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (cbStatuses.SelectedItem == null)
            {
                MessageBox.Show("Выберите новый статус задачи.", "Статус задачи", MessageBoxButton.OK, MessageBoxImage.Warning);
                cbStatuses.Focus();
                return;
            }

            try
            {
                if (cbStatuses.SelectedItem is TaskStatus newStatus)
                {
                    facade.UpdateTaskStatus(task.TaskId, newStatus.TaskStatusId, LoginWindow.CurrentUser.UserId,
                        string.IsNullOrWhiteSpace(txtComment.Text) ? "Статус изменен" : txtComment.Text.Trim());
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось изменить статус задачи.\n\n{ex.Message}", "Статус задачи", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
