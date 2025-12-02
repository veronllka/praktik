using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.IO;
using System.Printing;
using System.Drawing;
using System.Drawing.Imaging;
using praktik.Models;
using ZXing;
using ZXing.QrCode;
using System.Text;

namespace praktik
{
    public partial class TaskPrintPreviewWindow : Window
    {
        private WorkPlannerContext db = new WorkPlannerContext();
        private Task task;
        private TaskReport lastReport;

        public TaskPrintPreviewWindow(int taskId)
        {
            InitializeComponent();
            task = db.GetTaskById(taskId);
            
            if (task == null)
            {
                MessageBox.Show("Задача не найдена", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                DialogResult = false;
                Close();
                return;
            }

            LoadTaskData();
            LoadLastReport();
        }

        private void LoadTaskData()
        {
            if (task == null) return;

            txtTitle.Text = task.Title;
            
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
                }
                else
                {
                    txtBrigadier.Text = "Бригадир не назначен";
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

            txtPrintDate.Text = $"Напечатано: {DateTime.Now:dd.MM.yyyy HH:mm}";
            if (LoginWindow.CurrentUser != null)
            {
                txtPrintedBy.Text = $"Печатник: {LoginWindow.CurrentUser.Username}";
            }
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

        private string GenerateTaskHTML()
        {
            if (task == null) return "";

            string EscapeHtml(string text)
            {
                if (string.IsNullOrEmpty(text)) return "";
                return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");
            }

            string siteInfo = EscapeHtml(task.Site.SiteName);
            if (!string.IsNullOrEmpty(task.Site.Address))
            {
                siteInfo += "<br>" + EscapeHtml(task.Site.Address);
            }

            string crewInfo = task.Crew != null ? EscapeHtml(task.Crew.CrewName) : "Не назначена";
            string brigadierInfo = "";
            if (task.Crew != null && task.Crew.Brigadier != null)
            {
                brigadierInfo = $"<p><b>Бригадир:</b> {EscapeHtml(task.Crew.Brigadier.Username)}</p>";
            }

            string periodInfo = $"{task.StartDate:dd.MM.yyyy} — {task.EndDate:dd.MM.yyyy}";
            string priorityInfo = EscapeHtml(task.Priority?.PriorityName ?? "—");
            string statusInfo = EscapeHtml(task.TaskStatus?.TaskStatusName ?? "—");

            string progressInfo = "";
            if (lastReport != null && (!string.IsNullOrEmpty(lastReport.ReportText) || lastReport.ProgressPercent.HasValue))
            {
                string progressText = lastReport.ProgressPercent.HasValue ? $"{lastReport.ProgressPercent.Value}%" : "—";
                string commentText = !string.IsNullOrEmpty(lastReport.ReportText) ? EscapeHtml(lastReport.ReportText) : "—";
                progressInfo = $"<div style='border-top:1px solid #E0E0E0;padding-top:15px;margin-top:15px'><p><b>Последний прогресс:</b> {progressText}</p><p><b>Комментарий:</b> {commentText}</p></div>";
            }

            string descriptionInfo = !string.IsNullOrEmpty(task.Description) ? EscapeHtml(task.Description) : "Описание отсутствует";
            string taskTitle = EscapeHtml(task.Title);

            return $@"<!DOCTYPE html><html lang='ru'><head><meta charset='UTF-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>Наряд #{task.TaskId}</title><style>body{{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;max-width:600px;margin:0 auto;padding:20px;background:#F5F5F5;color:#212121;line-height:1.6}}.c{{background:white;border-radius:8px;padding:30px;box-shadow:0 2px 4px rgba(0,0,0,.1)}}h1{{color:#1976D2;text-align:center;margin-bottom:10px;font-size:24px}}.tt{{text-align:center;font-size:18px;font-weight:600;margin-bottom:20px;color:#212121}}.id{{text-align:right;color:#757575;font-size:12px;margin-bottom:20px}}.s{{border-bottom:1px solid #E0E0E0;padding-bottom:15px;margin-bottom:20px}}.s:last-child{{border-bottom:none}}h2{{color:#424242;font-size:13px;font-weight:bold;margin-top:25px;margin-bottom:10px;text-transform:uppercase}}p{{margin:8px 0;font-size:15px}}.tc{{display:grid;grid-template-columns:1fr 1fr;gap:20px}}@media (max-width:600px){{.tc{{grid-template-columns:1fr}}}}</style></head><body><div class='c'><h1>НАРЯД-ЗАДАЧА</h1><div class='tt'>{taskTitle}</div><div class='id'>ID: #{task.TaskId}</div><div class='s'><h2>ОБЪЕКТ</h2><p>{siteInfo}</p></div><div class='s'><h2>БРИГАДА</h2><p>{crewInfo}</p>{brigadierInfo}</div><div class='s'><h2>ПЕРИОД ВЫПОЛНЕНИЯ</h2><p>{periodInfo}</p></div><div class='s tc'><div><h2>ПРИОРИТЕТ</h2><p>{priorityInfo}</p></div><div><h2>СТАТУС</h2><p>{statusInfo}</p></div></div>{progressInfo}<div class='s'><h2>ОПИСАНИЕ</h2><p>{descriptionInfo}</p></div></div></body></html>";
        }


        private void btnPrint_Click(object sender, RoutedEventArgs e)
        {
            if (task == null)
            {
                MessageBox.Show("Задача не найдена", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (LoginWindow.CurrentUser == null)
            {
                MessageBox.Show("Пользователь не авторизован", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                System.Windows.Controls.PrintDialog printDialog = new System.Windows.Controls.PrintDialog();
                
                if (printDialog.ShowDialog() == true)
                {
                    PrintDocument(printDialog);

                    db.AddTaskReport(task.TaskId, LoginWindow.CurrentUser.UserId, 
                        "Задача распечатана");

                    MessageBox.Show("Наряд напечатан", "Печать", MessageBoxButton.OK, MessageBoxImage.Information);
                    DialogResult = true;
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при печати: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PrintDocument(System.Windows.Controls.PrintDialog printDialog)
        {
            if (task == null || PrintContent == null)
            {
                throw new InvalidOperationException("Задача или содержимое для печати не найдено");
            }

            var pageSize = new System.Windows.Size(printDialog.PrintableAreaWidth, printDialog.PrintableAreaHeight);
            
            var printContent = PrintContent;
            printContent.Measure(pageSize);
            printContent.Arrange(new System.Windows.Rect(new System.Windows.Point(0, 0), pageSize));
            printContent.UpdateLayout();

            printDialog.PrintVisual(printContent, $"Наряд: {task.Title}");
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

