using System;
using System.Linq;
using System.Windows;
using praktik.Models;

namespace praktik
{
    public partial class TaskViewWindow : Window
    {
        private WorkPlannerContext db = new WorkPlannerContext();
        private Task task;
        private TaskReport lastReport;

        public TaskViewWindow(int taskId)
        {
            InitializeComponent();
            LoadTask(taskId);
        }

        private void LoadTask(int taskId)
        {
            try
            {
                task = db.GetTaskById(taskId);
                
                if (task == null)
                {
                    MessageBox.Show("Задача не найдена", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    Close();
                    return;
                }

                LoadTaskData();
                LoadLastReport();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке задачи: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private void LoadTaskData()
        {
            if (task == null) return;

            txtTitle.Text = task.Title;
            txtTaskId.Text = $"ID задачи: #{task.TaskId}";
            
            txtSite.Text = $"{task.Site.SiteName}";
            if (!string.IsNullOrEmpty(task.Site.Address))
            {
                txtSite.Text += $"\n{task.Site.Address}";
            }

            if (task.Crew != null)
            {
                txtCrew.Text = task.Crew.CrewName;
                if (task.Crew.Brigadier != null)
                {
                    txtBrigadier.Text = $"Бригадир: {task.Crew.Brigadier.Username}";
                    txtBrigadier.Visibility = Visibility.Visible;
                }
                else
                {
                    txtBrigadier.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                txtCrew.Text = "Не назначена";
                txtBrigadier.Visibility = Visibility.Collapsed;
            }

            txtPeriod.Text = $"{task.StartDate:dd.MM.yyyy} — {task.EndDate:dd.MM.yyyy}";

            txtPriority.Text = task.Priority?.PriorityName ?? "—";
            txtStatus.Text = task.TaskStatus?.TaskStatusName ?? "—";

            txtDescription.Text = !string.IsNullOrEmpty(task.Description) ? task.Description : "Описание отсутствует";
            
            LoadNotes();
        }

        private void LoadLastReport()
        {
            if (task == null) return;

            var reports = db.GetTaskReports(task.TaskId);
            lastReport = reports.Count > 0 ? reports[0] : null;

            if (lastReport != null && (!string.IsNullOrEmpty(lastReport.ReportText) || lastReport.ProgressPercent.HasValue))
            {
                pnlProgress.Visibility = Visibility.Visible;
                
                if (lastReport.ProgressPercent.HasValue)
                {
                    txtProgress.Text = $"{lastReport.ProgressPercent.Value}%";
                }
                else
                {
                    txtProgress.Text = "—";
                }

                txtComment.Text = !string.IsNullOrEmpty(lastReport.ReportText) ? lastReport.ReportText : "—";
            }
            else
            {
                pnlProgress.Visibility = Visibility.Collapsed;
            }
        }

        private void LoadNotes()
        {
            if (task == null) return;

            var reports = db.GetTaskReports(task.TaskId);
            icNotes.ItemsSource = reports;
        }

        private void txtNewNote_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            var text = txtNewNote.Text?.Trim() ?? "";
            btnSaveNote.IsEnabled = text.Length >= 3 && text.Length <= 500;
        }

        private void btnSaveNote_Click(object sender, RoutedEventArgs e)
        {
            var noteText = txtNewNote.Text?.Trim() ?? "";
            
            if (noteText.Length < 3 || noteText.Length > 500)
            {
                MessageBox.Show("Заметка должна содержать от 3 до 500 символов", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var userId = LoginWindow.CurrentUser?.UserId ?? 0;
                db.AddTaskReport(task.TaskId, userId, noteText);

                txtNewNote.Clear();
                LoadNotes();
                
                MessageBox.Show("Заметка добавлена", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении заметки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnCancelNote_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtNewNote.Text))
            {
                var result = MessageBox.Show("Отменить ввод заметки?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    txtNewNote.Clear();
                }
            }
        }

        private void btnShowQR_Click(object sender, RoutedEventArgs e)
        {
            if (task != null)
            {
                var qrWindow = new TaskQRCodeWindow(task.TaskId);
                qrWindow.ShowDialog();
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

