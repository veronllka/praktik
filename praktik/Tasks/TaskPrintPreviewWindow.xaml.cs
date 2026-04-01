using System;
using System.Linq;
using System.Windows;
using praktik.Models;
using praktik.Models.Patterns;

namespace praktik
{
    public partial class TaskPrintPreviewWindow : Window
    {
        private const string DefaultTemplateName = "Наряд-задача";

        private readonly WorkPlannerFacade facade = new WorkPlannerFacade();
        private readonly string templateName;
        private Task task;
        private TaskReport lastReport;

        public TaskPrintPreviewWindow(int taskId, string templateName = DefaultTemplateName)
        {
            InitializeComponent();

            this.templateName = string.IsNullOrWhiteSpace(templateName)
                ? DefaultTemplateName
                : templateName.Trim();

            task = facade.GetTaskById(taskId);

            if (task == null)
            {
                MessageBox.Show("Задача не найдена", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                DialogResult = false;
                Close();
                return;
            }

            LoadTaskData();
            UpdatePrintAuditInfo(DateTime.Now);
            LoadLastReport();
        }

        private void LoadTaskData()
        {
            if (task == null)
            {
                return;
            }

            txtTitle.Text = task.Title;

            txtSite.Text = task.Site?.SiteName ?? string.Empty;
            if (!string.IsNullOrEmpty(task.Site?.Address))
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
                    txtBrigadier.Text = "Бригадир не назначен";
                    txtBrigadier.Visibility = Visibility.Visible;
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
        }

        private void UpdatePrintAuditInfo(DateTime printedAt)
        {
            txtPrintDate.Text = $"Напечатано: {printedAt:dd.MM.yyyy HH:mm}";
            txtPrintedBy.Text = LoginWindow.CurrentUser != null
                ? $"Печатал: {LoginWindow.CurrentUser.DisplayName}"
                : string.Empty;
        }

        private void LoadLastReport()
        {
            if (task == null)
            {
                return;
            }

            var reports = facade.GetTaskReports(task.TaskId);
            lastReport = reports.FirstOrDefault();

            if (lastReport != null && (!string.IsNullOrEmpty(lastReport.ReportText) || lastReport.ProgressPercent.HasValue))
            {
                pnlProgress.Visibility = Visibility.Visible;
                txtProgress.Text = lastReport.ProgressPercent.HasValue
                    ? $"{lastReport.ProgressPercent.Value}%"
                    : "—";
                txtComment.Text = !string.IsNullOrEmpty(lastReport.ReportText)
                    ? lastReport.ReportText
                    : "—";
                return;
            }

            pnlProgress.Visibility = Visibility.Collapsed;
        }

        private string GenerateTaskHTML()
        {
            if (task == null)
            {
                return string.Empty;
            }

            string EscapeHtml(string text)
            {
                if (string.IsNullOrEmpty(text))
                {
                    return string.Empty;
                }

                return text
                    .Replace("&", "&amp;")
                    .Replace("<", "&lt;")
                    .Replace(">", "&gt;")
                    .Replace("\"", "&quot;")
                    .Replace("'", "&#39;");
            }

            var siteInfo = EscapeHtml(task.Site?.SiteName ?? string.Empty);
            if (!string.IsNullOrEmpty(task.Site?.Address))
            {
                siteInfo += "<br>" + EscapeHtml(task.Site.Address);
            }

            var crewInfo = task.Crew != null ? EscapeHtml(task.Crew.CrewName) : "Не назначена";
            var brigadierInfo = string.Empty;
            if (task.Crew?.Brigadier != null)
            {
                brigadierInfo = $"<p><b>Бригадир:</b> {EscapeHtml(task.Crew.Brigadier.Username)}</p>";
            }

            var periodInfo = $"{task.StartDate:dd.MM.yyyy} — {task.EndDate:dd.MM.yyyy}";
            var priorityInfo = EscapeHtml(task.Priority?.PriorityName ?? "—");
            var statusInfo = EscapeHtml(task.TaskStatus?.TaskStatusName ?? "—");

            var progressInfo = string.Empty;
            if (lastReport != null && (!string.IsNullOrEmpty(lastReport.ReportText) || lastReport.ProgressPercent.HasValue))
            {
                var progressText = lastReport.ProgressPercent.HasValue ? $"{lastReport.ProgressPercent.Value}%" : "—";
                var commentText = !string.IsNullOrEmpty(lastReport.ReportText) ? EscapeHtml(lastReport.ReportText) : "—";
                progressInfo =
                    $"<div style='border-top:1px solid #E0E0E0;padding-top:15px;margin-top:15px'><p><b>Последний прогресс:</b> {progressText}</p><p><b>Комментарий:</b> {commentText}</p></div>";
            }

            var descriptionInfo = !string.IsNullOrEmpty(task.Description)
                ? EscapeHtml(task.Description)
                : "Описание отсутствует";
            var taskTitle = EscapeHtml(task.Title);

            return $@"<!DOCTYPE html><html lang='ru'><head><meta charset='UTF-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>Наряд #{task.TaskId}</title><style>body{{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;max-width:600px;margin:0 auto;padding:20px;background:#F5F5F5;color:#212121;line-height:1.6}}.c{{background:white;border-radius:8px;padding:30px;box-shadow:0 2px 4px rgba(0,0,0,.1)}}h1{{color:#1976D2;text-align:center;margin-bottom:10px;font-size:24px}}.tt{{text-align:center;font-size:18px;font-weight:600;margin-bottom:20px;color:#212121}}.id{{text-align:right;color:#757575;font-size:12px;margin-bottom:20px}}.s{{border-bottom:1px solid #E0E0E0;padding-bottom:15px;margin-bottom:20px}}.s:last-child{{border-bottom:none}}h2{{color:#424242;font-size:13px;font-weight:bold;margin-top:25px;margin-bottom:10px;text-transform:uppercase}}p{{margin:8px 0;font-size:15px}}.tc{{display:grid;grid-template-columns:1fr 1fr;gap:20px}}@media (max-width:600px){{.tc{{grid-template-columns:1fr}}}}</style></head><body><div class='c'><h1>НАРЯД-ЗАДАЧА</h1><div class='tt'>{taskTitle}</div><div class='id'>ID: #{task.TaskId}</div><div class='s'><h2>ОБЪЕКТ</h2><p>{siteInfo}</p></div><div class='s'><h2>БРИГАДА</h2><p>{crewInfo}</p>{brigadierInfo}</div><div class='s'><h2>ПЕРИОД ВЫПОЛНЕНИЯ</h2><p>{periodInfo}</p></div><div class='s tc'><div><h2>ПРИОРИТЕТ</h2><p>{priorityInfo}</p></div><div><h2>СТАТУС</h2><p>{statusInfo}</p></div></div>{progressInfo}<div class='s'><h2>ОПИСАНИЕ</h2><p>{descriptionInfo}</p></div></div></body></html>";
        }

        private void BtnPrint_Click(object sender, RoutedEventArgs e)
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
                var printDialog = new System.Windows.Controls.PrintDialog();

                if (printDialog.ShowDialog() != true)
                {
                    return;
                }

                var printedAt = DateTime.Now;
                UpdatePrintAuditInfo(printedAt);
                PrintDocument(printDialog);

                if (!facade.RecordTaskPrint(task.TaskId, LoginWindow.CurrentUser.UserId, templateName, printedAt, out var recordPrintError))
                {
                    MessageBox.Show(
                        $"Документ напечатан, но журнал печати не сохранен: {recordPrintError}",
                        "Печать",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

                MessageBox.Show("Наряд напечатан", "Печать", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
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

            var pageSize = new Size(printDialog.PrintableAreaWidth, printDialog.PrintableAreaHeight);

            var printContent = PrintContent;
            printContent.Measure(pageSize);
            printContent.Arrange(new Rect(new Point(0, 0), pageSize));
            printContent.UpdateLayout();

            printDialog.PrintVisual(printContent, $"Наряд: {task.Title}");
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
