using System;
using System.Collections.Generic;
using System.Linq;
using praktik.Models;

namespace praktik.Models.Patterns
{
    /// <summary>
    /// Фасад для упрощения взаимодействия с бизнес-логикой приложения.
    /// Предоставляет методы для работы с задачами, заявками на материалы и отчетами.
    /// </summary>
    public class WorkPlannerFacade
    {
        private readonly WorkPlannerContext db;

        /// <summary>
        /// Инициализирует новый экземпляр класса <see cref="WorkPlannerFacade"/>.
        /// Создает новый контекст базы данных.
        /// </summary>
        public WorkPlannerFacade()
        {
            db = new WorkPlannerContext();
        }

        /// <summary>
        /// Инициализирует новый экземпляр класса <see cref="WorkPlannerFacade"/> с существующим контекстом.
        /// </summary>
        /// <param name="context">Контекст базы данных.</param>
        public WorkPlannerFacade(WorkPlannerContext context)
        {
            db = context;
        }

        #region Task Operations

        /// <summary>
        /// Получает список задач с возможностью фильтрации.
        /// </summary>
        /// <param name="siteId">ID площадки (опционально).</param>
        /// <param name="crewId">ID бригады (опционально).</param>
        /// <param name="statusId">ID статуса задачи (опционально).</param>
        /// <returns>Список задач, соответствующих критериям фильтрации.</returns>
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
        /// Создает новую задачу.
        /// </summary>
        /// <param name="task">Объект задачи.</param>
        /// <param name="userId">ID пользователя, создающего задачу.</param>
        /// <param name="errorMessage">Сообщение об ошибке, если создание не удалось.</param>
        /// <returns>True, если задача успешно создана; иначе False.</returns>
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
        /// Обновляет статус существующей задачи.
        /// </summary>
        /// <param name="taskId">ID задачи.</param>
        /// <param name="newStatusId">Новый ID статуса.</param>
        /// <param name="userId">ID пользователя, выполняющего обновление.</param>
        /// <param name="errorMessage">Сообщение об ошибке, если обновление не удалось.</param>
        /// <returns>True, если статус успешно обновлен; иначе False.</returns>
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

                var oldStatus = task.TaskStatus?.TaskStatusName ?? "Unknown";
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
        /// Получает список задач, активных на указанную дату.
        /// </summary>
        /// <param name="date">Дата для поиска активных задач.</param>
        /// <returns>Список задач, активных в указанную дату.</returns>
        public List<Task> GetTasksByDate(DateTime date)
        {
            return db.GetTasks()
                .Where(t => t.StartDate.Date <= date.Date && t.EndDate.Date >= date.Date)
                .ToList();
        }

        #endregion

        #region Material Request Operations

        /// <summary>
        /// Получает заявки на материалы с возможностью гибкой фильтрации.
        /// </summary>
        /// <param name="taskId">ID задачи (опционально).</param>
        /// <param name="siteId">ID площадки (опционально).</param>
        /// <param name="crewId">ID бригады (опционально).</param>
        /// <param name="status">Статус заявки текстом ("Все" для отмены фильтра).</param>
        /// <returns>Список отфильтрованных заявок на материалы.</returns>
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
        /// Обрабатывает переход заявки на материалы в новое состояние.
        /// </summary>
        /// <param name="requestId">ID заявки.</param>
        /// <param name="action">Действие (submit, approve, reject, issue, deliver, close).</param>
        /// <param name="userId">ID пользователя, выполняющего действие.</param>
        /// <param name="errorMessage">Сообщение об ошибке, если действие не удалось.</param>
        /// <returns>True, если действие выполнено успешно; иначе False.</returns>
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
        /// Добавляет отчет (запись истории) к задаче.
        /// </summary>
        /// <param name="taskId">ID задачи.</param>
        /// <param name="userId">ID пользователя, создающего запись.</param>
        /// <param name="reportText">Текст отчета.</param>
        /// <returns>True, если отчет добавлен успешно; иначе False.</returns>
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
        /// <param name="taskId">ID задачи.</param>
        /// <returns>Список отчетов.</returns>
        public List<TaskReport> GetTaskReports(int taskId)
        {
            return db.GetTaskReports(taskId);
        }

        #endregion

        #region Reference Data

        /// <summary>
        /// Получает список всех строительных площадок.
        /// </summary>
        public List<Site> GetSites() => db.GetSites();

        /// <summary>
        /// Получает список всех бригад.
        /// </summary>
        public List<Crew> GetCrews() => db.GetCrews();

        /// <summary>
        /// Получает список всех приоритетов задач.
        /// </summary>
        public List<Priority> GetPriorities() => db.GetPriorities();

        /// <summary>
        /// Получает список всех возможных статусов задач.
        /// </summary>
        public List<TaskStatus> GetTaskStatuses() => db.GetTaskStatuses();

        /// <summary>
        /// Получает список всех пользователей.
        /// </summary>
        public List<User> GetUsers() => db.GetUsers();

        #endregion
    }
}
