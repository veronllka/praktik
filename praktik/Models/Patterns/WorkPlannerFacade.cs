using System;
using System.Collections.Generic;
using System.Linq;
using praktik.Models;

namespace praktik.Models.Patterns
{
   
    public class WorkPlannerFacade
    {
        private readonly WorkPlannerContext db;

        public WorkPlannerFacade()
        {
            db = new WorkPlannerContext();
        }

        public WorkPlannerFacade(WorkPlannerContext context)
        {
            db = context;
        }

        #region Task Operations

        /// <summary>
        /// Получить все задачи с применением фильтров
        /// </summary>
        public List<Task> GetTasksWithFilters(int? siteId = null, int? crewId = null, int? statusId = null)
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
        /// Создать новую задачу с валидацией
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

                AddTaskReport(task.TaskId, userId, $"Задача '{task.Title}' создана");

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Ошибка при создании задачи: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Обновить статус задачи с логированием
        /// </summary>
        public bool UpdateTaskStatus(int taskId, int newStatusId, int userId, out string errorMessage)
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

                var oldStatusObj = db.GetTaskStatuses().FirstOrDefault(s => s.TaskStatusId == task.TaskStatusId);
                var oldStatus = oldStatusObj?.TaskStatusName ?? "Unknown";
                task.TaskStatusId = newStatusId;
                task.UpdatedAt = DateTime.Now;
                db.UpdateTask(task);

                var newStatusObj = db.GetTaskStatuses().FirstOrDefault(s => s.TaskStatusId == newStatusId);
                var newStatus = newStatusObj?.TaskStatusName ?? "Unknown";
                AddTaskReport(taskId, userId, $"Статус изменен: {oldStatus} → {newStatus}");

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Ошибка при обновлении статуса: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Получить задачи по дате
        /// </summary>
        public List<Task> GetTasksByDate(DateTime date)
        {
            return db.GetTasks()
                .Where(t => t.StartDate.Date <= date.Date && t.EndDate.Date >= date.Date)
                .ToList();
        }

        #endregion

        #region Material Request Operations

        /// <summary>
        /// Получить заявки на материалы с деталями
        /// </summary>
        public List<MaterialRequest> GetMaterialRequestsWithDetails(int? taskId = null, int? siteId = null, int? crewId = null, string status = null)
        {
            var requests = db.GetMaterialRequests(taskId, null);

            if (siteId.HasValue && siteId.Value > 0)
            {
                requests = requests.Where(r => r.Task != null && r.Task.SiteId == siteId.Value).ToList();
            }

            if (crewId.HasValue && crewId.Value > 0)
            {
                requests = requests.Where(r => r.Task != null && r.Task.CrewId == crewId.Value).ToList();
            }

            if (!string.IsNullOrEmpty(status) && status != "Все")
            {
                requests = requests.Where(r => r.Status == status).ToList();
            }

            return requests;
        }

        /// <summary>
        /// Обработать заявку на материалы с использованием паттерна State
        /// </summary>
        public bool ProcessMaterialRequest(int requestId, string action, int userId, out string errorMessage)
        {
            errorMessage = null;

            try
            {
                var request = db.GetMaterialRequests(null, requestId).FirstOrDefault();
                if (request == null)
                {
                    errorMessage = "Заявка не найдена";
                    return false;
                }

                var context = new MaterialRequestContext(request, db);

                switch (action.ToLower())
                {
                    case "submit":
                        context.Submit(userId);
                        break;
                    case "approve":
                        context.Approve(userId);
                        break;
                    case "reject":
                        context.Reject(userId);
                        break;
                    case "issue":
                        context.Issue(userId);
                        break;
                    case "deliver":
                        context.Deliver(userId);
                        break;
                    case "close":
                        context.Close(userId);
                        break;
                    default:
                        errorMessage = $"Неизвестное действие: {action}";
                        return false;
                }

                return true;
            }
            catch (InvalidOperationException ex)
            {
                errorMessage = ex.Message;
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = $"Ошибка при обработке заявки: {ex.Message}";
                return false;
            }
        }

        #endregion

        #region Report Operations

        /// <summary>
        /// Добавить отчет/заметку к задаче
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
        /// Получить отчеты по задаче
        /// </summary>
        public List<TaskReport> GetTaskReports(int taskId)
        {
            return db.GetTaskReports(taskId);
        }

        #endregion

        #region Reference Data

        public List<Site> GetSites() => db.GetSites();
        public List<Crew> GetCrews() => db.GetCrews();
        public List<Priority> GetPriorities() => db.GetPriorities();
        public List<TaskStatus> GetTaskStatuses() => db.GetTaskStatuses();
        public List<User> GetUsers() => db.GetUsers();

        #endregion
    }
}
