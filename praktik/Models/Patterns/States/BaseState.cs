using System;

namespace praktik.Models.Patterns.States
{
    /// <summary>
    /// Базовый абстрактный класс для всех состояний заявки на материалы.
    /// Реализует паттерн State, определяя поведение по умолчанию (выброс исключения) для всех действий.
    /// Конкретные состояния переопределяют только допустимые для них действия.
    /// </summary>
    public abstract class BaseState : IMaterialRequestState
    {
        /// <summary>
        /// Получает строковое название текущего состояния.
        /// </summary>
        public abstract string StateName { get; }

        /// <summary>
        /// Проверяет, возможен ли переход в указанное целевое состояние.
        /// </summary>
        /// <param name="targetState">Название целевого состояния.</param>
        /// <returns>True, если переход возможен; иначе False.</returns>
        public abstract bool CanTransitionTo(string targetState);

        /// <summary>
        /// Выполняет действие "Отправить на согласование".
        /// </summary>
        /// <param name="context">Контекст заявки.</param>
        /// <param name="userId">ID пользователя, выполняющего действие.</param>
        /// <exception cref="InvalidOperationException">Выбрасывается, если действие недопустимо в текущем состоянии.</exception>
        public virtual void Submit(MaterialRequestContext context, int userId)
        {
            throw new InvalidOperationException($"Невозможно отправить заявку в состоянии '{StateName}'");
        }

        /// <summary>
        /// Выполняет действие "Согласовать".
        /// </summary>
        /// <param name="context">Контекст заявки.</param>
        /// <param name="userId">ID пользователя, выполняющего действие.</param>
        /// <exception cref="InvalidOperationException">Выбрасывается, если действие недопустимо в текущем состоянии.</exception>
        public virtual void Approve(MaterialRequestContext context, int userId)
        {
            throw new InvalidOperationException($"Невозможно согласовать заявку в состоянии '{StateName}'");
        }

        /// <summary>
        /// Выполняет действие "Отклонить".
        /// </summary>
        /// <param name="context">Контекст заявки.</param>
        /// <param name="userId">ID пользователя, выполняющего действие.</param>
        /// <exception cref="InvalidOperationException">Выбрасывается, если действие недопустимо в текущем состоянии.</exception>
        public virtual void Reject(MaterialRequestContext context, int userId)
        {
            throw new InvalidOperationException($"Невозможно отклонить заявку в состоянии '{StateName}'");
        }

        /// <summary>
        /// Выполняет действие "Выдать материалы".
        /// </summary>
        /// <param name="context">Контекст заявки.</param>
        /// <param name="userId">ID пользователя, выполняющего действие.</param>
        /// <exception cref="InvalidOperationException">Выбрасывается, если действие недопустимо в текущем состоянии.</exception>
        public virtual void Issue(MaterialRequestContext context, int userId)
        {
            throw new InvalidOperationException($"Невозможно отметить выдачу в состоянии '{StateName}'");
        }

        /// <summary>
        /// Выполняет действие "Доставить материалы".
        /// </summary>
        /// <param name="context">Контекст заявки.</param>
        /// <param name="userId">ID пользователя, выполняющего действие.</param>
        /// <exception cref="InvalidOperationException">Выбрасывается, если действие недопустимо в текущем состоянии.</exception>
        public virtual void Deliver(MaterialRequestContext context, int userId)
        {
            throw new InvalidOperationException($"Невозможно отметить доставку в состоянии '{StateName}'");
        }

        /// <summary>
        /// Выполняет действие "Закрыть заявку".
        /// </summary>
        /// <param name="context">Контекст заявки.</param>
        /// <param name="userId">ID пользователя, выполняющего действие.</param>
        /// <exception cref="InvalidOperationException">Выбрасывается, если действие недопустимо в текущем состоянии.</exception>
        public virtual void Close(MaterialRequestContext context, int userId)
        {
            throw new InvalidOperationException($"Невозможно закрыть заявку в состоянии '{StateName}'");
        }
    }
}
