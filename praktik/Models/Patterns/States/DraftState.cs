using System;

namespace praktik.Models.Patterns.States
{
    /// <summary>
    /// Состояние "Черновик" (Draft).
    /// Начальное состояние заявки. Можно редактировать.
    /// Возможные переходы:
    /// - Submitted (Отправка на согласование)
    /// </summary>
    public class DraftState : BaseState
    {
        public override string StateName => "Draft";

        public override bool CanTransitionTo(string targetState)
        {
            return targetState == "Submitted";
        }

        /// <summary>
        /// Выполняет действие "Отправить на согласование".
        /// Переводит заявку в состояние 'Submitted'.
        /// </summary>
        /// <param name="context">Контекст заявки.</param>
        /// <param name="userId">ID пользователя, отправляющего заявку.</param>
        public override void Submit(MaterialRequestContext context, int userId)
        {
            if (!CanTransitionTo("Submitted"))
            {
                throw new InvalidOperationException("Недопустимый переход состояния");
            }

            context.SetState(new SubmittedState());
            context.LogAction(userId, "Заявка отправлена на согласование");
        }
    }
}
