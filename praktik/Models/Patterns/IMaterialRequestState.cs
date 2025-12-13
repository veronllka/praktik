using System;

namespace praktik.Models.Patterns
{
    /// <summary>
    /// Интерфейс состояния заявки на материалы (Паттерн State).
    /// Определяет методы для всех действий, которые можно выполнить над заявкой.
    /// </summary>
    public interface IMaterialRequestState
    {
        /// <summary>
        /// Название текущего состояния.
        /// </summary>
        string StateName { get; }

        /// <summary>
        /// Проверяет, возможен ли переход в целевое состояние.
        /// </summary>
        /// <param name="targetState">Название целевого состояния.</param>
        /// <returns>True, если переход возможен.</returns>
        bool CanTransitionTo(string targetState);

        /// <summary>
        /// Действие: Отправить на согласование.
        /// </summary>
        /// <param name="context">Контекст заявки.</param>
        /// <param name="userId">ID пользователя.</param>
        void Submit(MaterialRequestContext context, int userId);

        /// <summary>
        /// Действие: Согласовать заявку.
        /// </summary>
        /// <param name="context">Контекст заявки.</param>
        /// <param name="userId">ID пользователя.</param>
        void Approve(MaterialRequestContext context, int userId);

        /// <summary>
        /// Действие: Отклонить заявку.
        /// </summary>
        /// <param name="context">Контекст заявки.</param>
        /// <param name="userId">ID пользователя.</param>
        void Reject(MaterialRequestContext context, int userId);

        /// <summary>
        /// Действие: Выдать материалы.
        /// </summary>
        /// <param name="context">Контекст заявки.</param>
        /// <param name="userId">ID пользователя.</param>
        void Issue(MaterialRequestContext context, int userId);

        /// <summary>
        /// Действие: Доставить материалы.
        /// </summary>
        /// <param name="context">Контекст заявки.</param>
        /// <param name="userId">ID пользователя.</param>
        void Deliver(MaterialRequestContext context, int userId);

        /// <summary>
        /// Действие: Закрыть заявку.
        /// </summary>
        /// <param name="context">Контекст заявки.</param>
        /// <param name="userId">ID пользователя.</param>
        void Close(MaterialRequestContext context, int userId);
    }
}
