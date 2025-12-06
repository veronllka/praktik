using System;

namespace praktik.Models.Patterns
{
    /// <summary>
    /// Паттерн Состояние (State) - интерфейс для состояний заявки на материалы
    /// </summary>
    public interface IMaterialRequestState
    {
        /// <summary>
        /// Название состояния
        /// </summary>
        string StateName { get; }

        /// <summary>
        /// Проверка возможности перехода в указанное состояние
        /// </summary>
        bool CanTransitionTo(string targetState);

        /// <summary>
        /// Отправить заявку на согласование
        /// </summary>
        void Submit(MaterialRequestContext context, int userId);

        /// <summary>
        /// Согласовать заявку
        /// </summary>
        void Approve(MaterialRequestContext context, int userId);

        /// <summary>
        /// Отклонить заявку
        /// </summary>
        void Reject(MaterialRequestContext context, int userId);

        /// <summary>
        /// Отметить выдачу материалов
        /// </summary>
        void Issue(MaterialRequestContext context, int userId);

        /// <summary>
        /// Отметить доставку материалов
        /// </summary>
        void Deliver(MaterialRequestContext context, int userId);

        /// <summary>
        /// Закрыть заявку
        /// </summary>
        void Close(MaterialRequestContext context, int userId);
    }
}
