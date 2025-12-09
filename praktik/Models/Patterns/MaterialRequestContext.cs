using System;
using System.Collections.Generic;
using System.Linq;
using praktik.Models.Patterns.States;

namespace praktik.Models.Patterns
{
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

        public MaterialRequest Request => request;

        public MaterialRequestContext(MaterialRequest request, WorkPlannerContext db)
        {
            this.request = request;
            this.db = db;
            currentState = GetStateByName(request.Status);
        }

        public IMaterialRequestState CurrentState => currentState;

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
