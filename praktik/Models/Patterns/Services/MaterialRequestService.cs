using System;
using System.Collections.Generic;
using System.Linq;
using praktik.Models;
using praktik.Models.Patterns.States;

namespace praktik.Models.Patterns
{
    /// <summary>
    /// Сервис для работы с заявками на материалы.
    /// Подсистема, используемая WorkPlannerFacade.
    /// </summary>
    public class MaterialRequestService
    {
        private readonly WorkPlannerContext db;

        public MaterialRequestService(WorkPlannerContext context)
        {
            db = context;
        }

        /// <summary>
        /// Получает заявки на материалы с фильтрацией.
        /// </summary>
        public List<MaterialRequest> GetFilteredRequests(int? taskId = null, int? siteId = null, int? crewId = null, string status = null)
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
        /// Использует паттерн State для управления переходами.
        /// </summary>
        public bool ProcessRequest(int requestId, string action, int userId, out string errorMessage)
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
    }
}
