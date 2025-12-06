using System;
using System.Linq;
using praktik.Models.Patterns.States;

namespace praktik.Models.Patterns
{
    /// <summary>
    /// Контекст для управления состояниями заявки на материалы (паттерн State)
    /// </summary>
    public class MaterialRequestContext
    {
        private IMaterialRequestState currentState;
        private readonly MaterialRequest request;
        private readonly WorkPlannerContext db;

        public MaterialRequest Request => request;

        public MaterialRequestContext(MaterialRequest request, WorkPlannerContext db)
        {
            this.request = request;
            this.db = db;
            this.currentState = GetStateByName(request.Status);
        }

        /// <summary>
        /// Текущее состояние
        /// </summary>
        public IMaterialRequestState CurrentState => currentState;

        /// <summary>
        /// Установить новое состояние
        /// </summary>
        public void SetState(IMaterialRequestState newState)
        {
            currentState = newState;
            request.Status = newState.StateName;
            db.UpdateMaterialRequest(request);
        }

        /// <summary>
        /// Получить объект состояния по названию
        /// </summary>
        private IMaterialRequestState GetStateByName(string stateName)
        {
            switch (stateName)
            {
                case "Draft":
                    return new DraftState();
                case "Submitted":
                    return new SubmittedState();
                case "Approved":
                    return new ApprovedState();
                case "Rejected":
                    return new RejectedState();
                case "Issued":
                    return new IssuedState();
                case "Delivered":
                    return new DeliveredState();
                case "Closed":
                    return new ClosedState();
                default:
                    throw new ArgumentException($"Неизвестное состояние: {stateName}");
            }
        }

        /// <summary>
        /// Логирование действия в журнал задачи
        /// </summary>
        public void LogAction(int userId, string action)
        {
            var logText = $"Заявка на материалы #{request.RequestId}: {action}";
            db.AddTaskReport(request.TaskId, userId, logText);
        }

        #region State Actions

        public void Submit(int userId) => currentState.Submit(this, userId);
        public void Approve(int userId) => currentState.Approve(this, userId);
        public void Reject(int userId) => currentState.Reject(this, userId);
        public void Issue(int userId) => currentState.Issue(this, userId);
        public void Deliver(int userId) => currentState.Deliver(this, userId);
        public void Close(int userId) => currentState.Close(this, userId);

        #endregion
    }
}
