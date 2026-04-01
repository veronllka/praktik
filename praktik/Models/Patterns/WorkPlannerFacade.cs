using System;
using System.Collections.Generic;
using System.Linq;
using praktik.Models;

namespace praktik.Models.Patterns
{

    public class WorkPlannerFacade
    {
        private readonly WorkPlannerContext db;
        private readonly TaskService taskService;
        private readonly LmStudioTaskDescriptionService taskDescriptionService;
        private readonly MaterialRequestService materialRequestService;
        private readonly ReportService reportService;

        /// <summary>
        /// Инициализирует новый экземпляр фасада с новым контекстом БД.
        /// </summary>
        public WorkPlannerFacade()
        {
            db = new WorkPlannerContext();
            taskService = new TaskService(db);
            taskDescriptionService = new LmStudioTaskDescriptionService();
            materialRequestService = new MaterialRequestService(db);
            reportService = new ReportService(db);
        }

        public WorkPlannerFacade(WorkPlannerContext context)
        {
            db = context;
            taskService = new TaskService(db);
            taskDescriptionService = new LmStudioTaskDescriptionService();
            materialRequestService = new MaterialRequestService(db);
            reportService = new ReportService(db);
        }

        #region User & Role Operations

        public User GetUser(string username, string password)
        {
            return db.GetUser(username, password);
        }

        /// <summary>
        /// Получает список ролей пользователей.
        /// </summary>
        public List<Role> GetRoles()
        {
            return db.GetRoles();
        }

        public List<string> GetRolePermissionCodes(string roleName)
        {
            return db.GetRolePermissionCodes(roleName);
        }

        public List<string> GetRolePermissionCodes(int roleId)
        {
            return db.GetRolePermissionCodes(roleId);
        }

        public void RegisterUser(string loginName, string password, string fullName, int roleId)
        {
            db.RegisterUser(loginName, password, fullName, roleId);
        }

        public int CreateRole(string roleName, IEnumerable<string> permissionCodes)
        {
            return db.CreateRole(roleName, permissionCodes);
        }

        public void UpdateUserRole(int userId, int roleId)
        {
            db.UpdateUserRole(userId, roleId);
        }

        public void UpdateRolePermissions(int roleId, IEnumerable<string> permissionCodes)
        {
            db.UpdateRolePermissions(roleId, permissionCodes);
        }

        public bool CreateCrewEmployeeAndAddToCrew(int crewId, string fullName, DateTime joinedAt, out int createdUserId, out string errorMessage)
        {
            return db.CreateCrewEmployeeAndAddToCrew(crewId, fullName, joinedAt, out createdUserId, out errorMessage);
        }

        #endregion

        #region Task Operations - делегируются в TaskService
        public List<Task> GetTasks()
        {
            return db.GetTasks();
        }
        public List<Task> GetTasksWithFilters(int? siteId = null, int? crewId = null, int? statusId = null)
        {
            return taskService.GetFilteredTasks(siteId, crewId, statusId);
        }

        /// <summary>
        /// Создает новую задачу с валидацией и добавлением отчета.
        /// </summary>
        public bool CreateTask(Task task, int userId, out string errorMessage)
        {
            bool result = taskService.CreateTask(task, userId, out errorMessage);
            
            if (result)
            {
                reportService.AddTaskReport(task.TaskId, userId, $"Задача '{task.Title}' создана");
            }

            return result;
        }

        /// <summary>
        /// Добавляет новую задачу без дополнительной координации.
        /// </summary>
        public void AddTask(Task task)
        {
            db.AddTask(task);
        }

        /// <summary>
        /// Обновляет задачу.
        /// </summary>
        public void UpdateTask(Task task)
        {
            db.UpdateTask(task);
        }

        /// <summary>
        /// Удаляет задачу.
        /// </summary>
        public void DeleteTask(int taskId)
        {
            db.DeleteTask(taskId);
        }

        /// <summary>
        /// Обновляет статус существующей задачи с добавлением отчета.
        /// Координирует работу TaskService и ReportService.
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

                var oldStatus = task.TaskStatus?.TaskStatusName ?? "Unknown";
                
                bool result = taskService.UpdateStatus(taskId, newStatusId, out errorMessage);
                
                if (result)
                {
                    var newStatusObj = db.GetTaskStatuses().FirstOrDefault(s => s.TaskStatusId == newStatusId);
                    var newStatus = newStatusObj?.TaskStatusName ?? "Unknown";
                    reportService.AddTaskReport(taskId, userId, $"Статус изменен: {oldStatus} → {newStatus}");
                }

                return result;
            }
            catch (Exception ex)
            {
                errorMessage = $"Ошибка при обновлении статуса: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Обновляет статус задачи и сохраняет запись в истории.
        /// </summary>
        public void UpdateTaskStatus(int taskId, int statusId, int userId, string comment)
        {
            db.UpdateTaskStatus(taskId, statusId, userId, comment);
        }

        /// <summary>
        /// Получает задачу по идентификатору.
        /// </summary>
        public Task GetTaskById(int taskId)
        {
            return db.GetTaskById(taskId);
        }

        /// <summary>
        /// Получает список отчетов по задачам (при необходимости фильтрует по задаче).
        /// </summary>
        public List<TaskReport> GetTaskReports(int? taskId = null)
        {
            return db.GetTaskReports(taskId);
        }

        /// <summary>
        /// Добавляет отчет по задаче.
        /// </summary>
        public bool RecordTaskPrint(int taskId, int userId, string templateName, DateTime printedAt, out string errorMessage)
        {
            return taskService.RecordTaskPrint(taskId, userId, templateName, printedAt, out errorMessage);
        }

        public List<TaskPrintLog> GetTaskPrintLogs(int taskId)
        {
            return taskService.GetTaskPrintLogs(taskId);
        }

        public bool AddTaskReport(int taskId, int userId, string reportText, int? progressPercent = null, string attachmentSourcePath = null)
        {
            return reportService.AddTaskReport(taskId, userId, reportText, progressPercent, attachmentSourcePath);
        }

        /// <summary>
        /// Получает список задач, активных на указанную дату.
        /// Делегирует вызов в TaskService.
        /// </summary>
        public List<Task> GetTasksByDate(DateTime date)
        {
            return taskService.GetTasksByDate(date);
        }

        public bool CanGenerateTaskDescription => taskDescriptionService.IsEnabled;

        public System.Threading.Tasks.Task<TaskDescriptionGenerationResult> GenerateTaskDescriptionAsync(TaskDescriptionGenerationRequest request)
        {
            return taskDescriptionService.GenerateDescriptionAsync(request);
        }

        #endregion

        #region Material Request Operations - делегируются в MaterialRequestService

        /// <summary>
        /// Получает список заявок на материалы.
        /// </summary>
        public List<MaterialRequest> GetMaterialRequests(int? taskId = null, int? requestId = null)
        {
            return db.GetMaterialRequests(taskId, requestId);
        }

        /// <summary>
        /// Получает каталог материалов.
        /// </summary>
        public List<MaterialCatalog> GetMaterialCatalog(bool activeOnly = true)
        {
            return db.GetMaterialCatalog(activeOnly);
        }

        /// <summary>
        /// Создает заявку на материалы.
        /// </summary>
        public int CreateMaterialRequest(MaterialRequest request)
        {
            return db.CreateMaterialRequest(request);
        }

        /// <summary>
        /// Обновляет заявку на материалы.
        /// </summary>
        public void UpdateMaterialRequest(MaterialRequest request)
        {
            db.UpdateMaterialRequest(request);
        }

        /// <summary>
        /// Меняет статус заявки на материалы.
        /// </summary>
        public void ChangeMaterialRequestStatus(int requestId, string newStatus, int userId, string comment = null)
        {
            db.ChangeMaterialRequestStatus(requestId, newStatus, userId, comment);
        }

        /// <summary>
        /// Добавляет документ по выдаче/доставке материалов.
        /// </summary>
        public void AddMaterialDeliveryDoc(int requestId, string eventType, string docNumber = null, string note = null)
        {
            db.AddMaterialDeliveryDoc(requestId, eventType, docNumber, note);
        }

        /// <summary>
        /// Получает заявки на материалы с возможностью гибкой фильтрации.
        /// Делегирует вызов в MaterialRequestService.
        /// </summary>
        public List<MaterialRequest> GetMaterialRequestsWithDetails(int? taskId = null, int? siteId = null, int? crewId = null, string status = null)
        {
            return materialRequestService.GetFilteredRequests(taskId, siteId, crewId, status);
        }

        /// <summary>
        /// Обрабатывает переход заявки на материалы в новое состояние.
        /// Делегирует вызов в MaterialRequestService, который использует паттерн State.
        /// </summary>
        public bool ProcessMaterialRequest(int requestId, string action, int userId, out string errorMessage)
        {
            return materialRequestService.ProcessRequest(requestId, action, userId, out errorMessage);
        }

        #endregion

        #region Reference Data - прямой доступ к БД для справочных данных

        /// <summary>
        /// Получает список всех строительных площадок.
        /// Прямой доступ к контексту БД для простых справочных данных.
        /// </summary>
        public List<Site> GetSites() => db.GetSites();

        /// <summary>
        /// Получает список всех бригад.
        /// </summary>
        public List<Crew> GetCrews() => db.GetCrews();

        public List<CrewMember> GetCrewMembers(int crewId, bool activeOnly = true)
        {
            return db.GetCrewMembers(crewId, activeOnly);
        }

        public List<User> GetAvailableUsersForCrew(int crewId)
        {
            return db.GetAvailableUsersForCrew(crewId);
        }

        public bool AddCrewMember(int crewId, int userId, DateTime joinedAt, out string errorMessage)
        {
            return db.AddCrewMember(crewId, userId, joinedAt, out errorMessage);
        }

        public bool RemoveCrewMember(int crewId, int userId, DateTime leftAt, out string errorMessage)
        {
            return db.RemoveCrewMember(crewId, userId, leftAt, out errorMessage);
        }

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

        /// <summary>
        /// Возвращает ID статуса задачи по имени.
        /// </summary>
        public int GetNewTaskStatusId(string statusName) => db.GetNewTaskStatusId(statusName);

        /// <summary>
        /// Добавляет новую строительную площадку.
        /// </summary>
        public void AddSite(Site site) => db.AddSite(site);

        /// <summary>
        /// Обновляет строительную площадку.
        /// </summary>
        public void UpdateSite(Site site) => db.UpdateSite(site);

        /// <summary>
        /// Удаляет строительную площадку.
        /// </summary>
        public void DeleteSite(int siteId) => db.DeleteSite(siteId);

        /// <summary>
        /// Добавляет бригаду.
        /// </summary>
        public void AddCrew(Crew crew) => db.AddCrew(crew);

        /// <summary>
        /// Обновляет бригаду.
        /// </summary>
        public void UpdateCrew(Crew crew) => db.UpdateCrew(crew);

        /// <summary>
        /// Удаляет бригаду.
        /// </summary>
        public void DeleteCrew(int crewId) => db.DeleteCrew(crewId);

        #endregion
    }
}
