using System;
using System.Linq;
using System.Windows;
using praktik.Models;

namespace praktik
{
    public partial class TaskStatusWindow : Window
    {
        private WorkPlannerContext db = new WorkPlannerContext();
        private Task task;

        public TaskStatusWindow(Task task)
        {
            InitializeComponent();
            this.task = task;
            LoadData();
        }

        private void LoadData()
        {
            txtTaskInfo.Text = $"Задача: {task.Title}\nТекущий статус: {task.TaskStatus?.TaskStatusName ?? "Не указан"}";
            cbStatuses.ItemsSource = db.GetTaskStatuses();
            // Выбираем статус по ID, так как объекты разные
            var statuses = db.GetTaskStatuses();
            cbStatuses.SelectedItem = statuses.FirstOrDefault(s => s.TaskStatusId == task.TaskStatusId);
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            if (cbStatuses.SelectedItem == null)
            {
                MessageBox.Show("Выберите новый статус");
                return;
            }

            try
            {
                var newStatus = cbStatuses.SelectedItem as TaskStatus;
                if (newStatus == null)
                {
                    MessageBox.Show("Ошибка при получении выбранного статуса");
                    return;
                }

                // Проверяем, что пользователь авторизован
                if (LoginWindow.CurrentUser == null)
                {
                    MessageBox.Show("Ошибка авторизации пользователя");
                    return;
                }

                // Обновляем статус задачи
                db.UpdateTaskStatus(
                    task.TaskId, 
                    newStatus.TaskStatusId, 
                    LoginWindow.CurrentUser.UserId,
                    string.IsNullOrWhiteSpace(txtComment.Text) ? "Статус изменен" : txtComment.Text
                );

                MessageBox.Show("Статус задачи успешно обновлен");
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
