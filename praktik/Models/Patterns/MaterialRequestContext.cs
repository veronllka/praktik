using System;
using System.Collections.Generic;
using System.Linq;
using praktik.Models.Patterns.States;

namespace praktik.Models.Patterns
{
    /// <summary>
    /// Контекст заявки на материалы (Паттерн State).
    /// Управляет текущим состоянием заявки и делегирует выполнение действий текущему состоянию.
    /// </summary>
    public class MaterialRequestContext
    {
        private static readonly Dictionary<string, Func<IMaterialRequestState>> stateFactories =
            new Dictionary<string, Func<IMaterialRequestState>>(StringComparer.OrdinalIgnoreCase)
            {
                { "Draft", () => new DraftState() },
                { "Submitted", () => new SubmittedState() },
                { "Approved", () => new ApprovedState() },
                { "Rejected", () => new RejectedState() },
                { "Issued", () => new IssuedState() },
                { "Delivered", () => new DeliveredState() },
                { "Closed", () => new ClosedState() }
            };

        private IMaterialRequestState currentState;
        private readonly MaterialRequest request;
        private readonly WorkPlannerContext db;

        /// <summary>
        /// Текущий объект заявки.
        /// </summary>
        public MaterialRequest Request => request;

        /// <summary>
        /// Инициализирует новый экземпляр контекста для указанной заявки.
        /// </summary>
        /// <param name="request">Заявка на материалы.</param>
        /// <param name="db">Контекст базы данных.</param>
        public MaterialRequestContext(MaterialRequest request, WorkPlannerContext db)
        {
            this.request = request;
            this.db = db;
            currentState = GetStateByName(request.Status);
        }

        /// <summary>
        /// Текущее состояние заявки (объект state).
        /// </summary>
        public IMaterialRequestState CurrentState => currentState;

        /// <summary>
        /// Устанавливает новое состояние заявки.
        /// Обновляет поле статуса в объекте заявки и сохраняет изменения в БД.
        /// </summary>
        /// <param name="newState">Новый объект состояния.</param>
        public void SetState(IMaterialRequestState newState)
        {
            currentState = newState;
            request.Status = newState.StateName;
            db.UpdateMaterialRequest(request);
        }

        private IMaterialRequestState GetStateByName(string stateName)
        {
            var normalized = stateName?.Trim();
            if (!string.IsNullOrEmpty(normalized) && stateFactories.TryGetValue(normalized, out var factory))
            {
                return factory();
            }

            throw new ArgumentException($"Неизвестное состояние: {stateName}");
        }

        /// <summary>
        /// Записывает действие в историю задачи.
        /// </summary>
        /// <param name="userId">ID пользователя.</param>
        /// <param name="action">Описание действия.</param>
        public void LogAction(int userId, string action)
        {
            var logText = $"Заявка на материалы #{request.RequestId}: {action}";
            db.AddTaskReport(request.TaskId, userId, logText);
        }

        #region State Actions

        /// <summary>
        /// Отправить заявку.
        /// </summary>
        public void Submit(int userId) => currentState.Submit(this, userId);

        /// <summary>
        /// Согласовать заявку.
        /// </summary>
        public void Approve(int userId) => currentState.Approve(this, userId);

        /// <summary>
        /// Отклонить заявку.
        /// </summary>
        public void Reject(int userId) => currentState.Reject(this, userId);

        /// <summary>
        /// Выдать материалы.
        /// </summary>
        public void Issue(int userId) => currentState.Issue(this, userId);

        /// <summary>
        /// Доставить материалы.
        /// </summary>
        public void Deliver(int userId) => currentState.Deliver(this, userId);

        /// <summary>
        /// Закрыть заявку.
        /// </summary>
        public void Close(int userId) => currentState.Close(this, userId);

        #endregion
    }
}
