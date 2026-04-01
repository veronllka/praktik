using System;
using System.IO;

namespace praktik.Models
{
    public class TaskReport
    {
        public int ReportId { get; set; }
        public int TaskId { get; set; }
        public int ReportedByUserId { get; set; }
        public DateTime ReportedAt { get; set; }
        public string ReportText { get; set; }
        public int? ProgressPercent { get; set; }
        public string AttachmentUrl { get; set; }

        // Дополнительные свойства для отображения
        public string TaskTitle { get; set; }
        public string ReporterName { get; set; }
        public bool HasAttachment => !string.IsNullOrWhiteSpace(AttachmentUrl);
        public string AttachmentFileName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(AttachmentUrl))
                {
                    return string.Empty;
                }

                try
                {
                    return Path.GetFileName(AttachmentUrl);
                }
                catch
                {
                    return AttachmentUrl;
                }
            }
        }
        
        public Task Task { get; set; }
        public User User { get; set; }
    }

    public class TaskPrintLog
    {
        public int LogId { get; set; }
        public int TaskId { get; set; }
        public int PrintedByUserId { get; set; }
        public DateTime PrintedAt { get; set; }
        public string TemplateName { get; set; }
        public string PrintedByName { get; set; }

        public string DisplayTemplateName
        {
            get
            {
                return string.IsNullOrWhiteSpace(TemplateName)
                    ? "Шаблон не указан"
                    : TemplateName;
            }
        }

        public string DisplayPrintedByName
        {
            get
            {
                return string.IsNullOrWhiteSpace(PrintedByName)
                    ? "Пользователь не указан"
                    : PrintedByName;
            }
        }
    }
}
