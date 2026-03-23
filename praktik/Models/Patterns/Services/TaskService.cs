using System;
using System.Collections.Generic;
using System.Linq;
using praktik.Models;

namespace praktik.Models.Patterns
{
    /// <summary>
    /// Сервис для работы с задачами.
    /// Подсистема, используемая WorkPlannerFacade.
    /// </summary>
    public class TaskService
    {
        private readonly WorkPlannerContext db;

        public TaskService(WorkPlannerContext context)
        {
            db = context;
        }

        /// <summary>
        /// Получает задачи с фильтрацией.
        /// </summary>
        public List<Task> GetFilteredTasks(int? siteId = null, int? crewId = null, int? statusId = null)
        {
            var tasks = db.GetTasks();

            if (siteId.HasValue && siteId.Value > 0)
            {
                tasks = tasks.Where(t => t.SiteId == siteId.Value).ToList();
            }

            if (crewId.HasValue && crewId.Value > 0)
            {
                tasks = tasks.Where(t => t.CrewId == crewId.Value).ToList();
            }

            if (statusId.HasValue && statusId.Value > 0)
            {
                tasks = tasks.Where(t => t.TaskStatusId == statusId.Value).ToList();
            }

            return tasks;
        }

        /// <summary>
        /// Создает новую задачу с валидацией.
        /// </summary>
        public bool CreateTask(Task task, int userId, out string errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(task.Title))
            {
                errorMessage = "Название задачи не может быть пустым";
                return false;
            }

            if (task.StartDate > task.EndDate)
            {
                errorMessage = "Дата окончания должна быть позже даты начала";
                return false;
            }

            try
            {
                task.CreatedBy = userId;
                task.CreatedAt = DateTime.Now;
                db.AddTask(task);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Ошибка при создании задачи: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Обновляет статус задачи.
        /// </summary>
        public bool UpdateStatus(int taskId, int newStatusId, out string errorMessage)
        {
            errorMessage = null;

            try
            {
                var task = db.GetTasks().FirstOrDefault(t => t.TaskId == taskId);
                if (task == null)
                {
                    errorMessage = "Задача не найдена";
                    return false;
                }

                task.TaskStatusId = newStatusId;
                task.UpdatedAt = DateTime.Now;
                db.UpdateTask(task);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Ошибка при обновлении статуса: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Получает задачи, активные на указанную дату.
        /// </summary>
        public List<Task> GetTasksByDate(DateTime date)
        {
            return db.GetTasks()
                .Where(t => t.StartDate.Date <= date.Date && t.EndDate.Date >= date.Date)
                .ToList();
        }
    }
}
