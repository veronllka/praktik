using System;

namespace praktik.Models
{
    /// <summary>
    /// Сущность "Задача" (Task).
    /// Основная единица планирования работ.
    /// </summary>
    public class Task
    {
        public int TaskId { get; set; }

        /// <summary>
        /// ID строительной площадки.
        /// </summary>
        public int SiteId { get; set; }

        /// <summary>
        /// ID назначенной бригады
        /// </summary>
        public int? CrewId { get; set; }

        /// <summary>
        /// Заголовок/название задачи.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Подробное описание задачи.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Дата начала работ.
        /// </summary>
        public DateTime StartDate { get; set; }

        /// <summary>
        /// Дата окончания работ.
        /// </summary>
        public DateTime EndDate { get; set; }

        /// <summary>
        /// ID приоритета задачи.
        /// </summary>
        public int PriorityId { get; set; }

        /// <summary>
        /// ID текущего статуса задачи.
        /// </summary>
        public int TaskStatusId { get; set; }

        public int? LabelId { get; set; } 

        /// <summary>
        /// ID пользователя, создавшего задачу.
        /// </summary>
        public int CreatedBy { get; set; }

        /// <summary>
        /// Дата создания задачи.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Дата последнего обновления задачи.
        /// </summary>
        public DateTime? UpdatedAt { get; set; }
        public DateTime? LastPrintedAt { get; set; }

        public Site Site { get; set; }
        public Crew Crew { get; set; }
        public Priority Priority { get; set; }
        public TaskStatus TaskStatus { get; set; }
        public User Creator { get; set; }

        /// <summary>
        /// Генерирует JSON-строку с данными задачи для QR-кода.
        /// </summary>
        /// <returns>JSON-строка с основными полями задачи.</returns>
        public string GenerateQRData()
        {
            var siteName = Site?.SiteName?.Replace("\"", "'") ?? "";
            var crewName = Crew?.CrewName?.Replace("\"", "'") ?? "Не назначена";
            var priorityName = Priority?.PriorityName?.Replace("\"", "'") ?? "";
            var title = Title?.Replace("\"", "'") ?? "";
            
            return $"{{\"type\":\"TASK\",\"id\":{TaskId},\"title\":\"{title}\",\"site\":\"{siteName}\",\"crew\":\"{crewName}\",\"priority\":\"{priorityName}\",\"start\":\"{StartDate:yyyy-MM-dd}\",\"end\":\"{EndDate:yyyy-MM-dd}\"}}";
        }

        public TaskReport TaskReport
        {
            get => default;
            set
            {
            }
        }

        public string LastNoteText { get; set; }
        public string LastNoteTooltip { get; set; }
    }
}
