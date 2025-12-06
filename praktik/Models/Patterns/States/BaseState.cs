using System;

namespace praktik.Models.Patterns.States
{
    /// <summary>
    /// Базовый класс для состояний (общая логика)
    /// </summary>
    public abstract class BaseState : IMaterialRequestState
    {
        public abstract string StateName { get; }

        public abstract bool CanTransitionTo(string targetState);

        public virtual void Submit(MaterialRequestContext context, int userId)
        {
            throw new InvalidOperationException($"Невозможно отправить заявку в состоянии '{StateName}'");
        }

        public virtual void Approve(MaterialRequestContext context, int userId)
        {
            throw new InvalidOperationException($"Невозможно согласовать заявку в состоянии '{StateName}'");
        }

        public virtual void Reject(MaterialRequestContext context, int userId)
        {
            throw new InvalidOperationException($"Невозможно отклонить заявку в состоянии '{StateName}'");
        }

        public virtual void Issue(MaterialRequestContext context, int userId)
        {
            throw new InvalidOperationException($"Невозможно отметить выдачу в состоянии '{StateName}'");
        }

        public virtual void Deliver(MaterialRequestContext context, int userId)
        {
            throw new InvalidOperationException($"Невозможно отметить доставку в состоянии '{StateName}'");
        }

        public virtual void Close(MaterialRequestContext context, int userId)
        {
            throw new InvalidOperationException($"Невозможно закрыть заявку в состоянии '{StateName}'");
        }
    }
}
