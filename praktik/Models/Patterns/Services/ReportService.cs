using System;
using System.Collections.Generic;
using praktik.Models;

namespace praktik.Models.Patterns
{
    /// <summary>
    /// Сервис для работы с отчетами по задачам.
    /// Подсистема, используемая WorkPlannerFacade.
    /// </summary>
    public class ReportService
    {
        private readonly WorkPlannerContext db;

        public ReportService(WorkPlannerContext context)
        {
            db = context;
        }

        /// <summary>
        /// Добавляет отчет (запись истории) к задаче.
        /// </summary>
        public bool AddTaskReport(int taskId, int userId, string reportText)
        {
            try
            {
                db.AddTaskReport(taskId, userId, reportText);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Получает список отчетов для конкретной задачи.
        /// </summary>
        public List<TaskReport> GetTaskReports(int taskId)
        {
            return db.GetTaskReports(taskId);
        }
    }
}
