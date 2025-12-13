using System;

namespace praktik.Models.Patterns.States
{
    /// <summary>
    /// Состояние "Доставлена" (Delivered).
    /// Материалы доставлены на объект.
    /// Возможные переходы:
    /// - Closed (Закрытие заявки после проверки)
    /// </summary>
    public class DeliveredState : BaseState
    {
        public override string StateName => "Delivered";

        public override bool CanTransitionTo(string targetState)
        {
            return targetState == "Closed";
        }

        /// <summary>
        /// Выполняет действие "Закрыть заявку".
        /// Переводит заявку в финальное состояние 'Closed'.
        /// </summary>
        /// <param name="context">Контекст заявки.</param>
        /// <param name="userId">ID пользователя, закрывающего заявку.</param>
        public override void Close(MaterialRequestContext context, int userId)
        {
            if (!CanTransitionTo("Closed"))
            {
                throw new InvalidOperationException("Недопустимый переход состояния");
            }

            context.SetState(new ClosedState());
            context.LogAction(userId, "Заявка закрыта");
        }
    }
}
