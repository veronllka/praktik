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

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

