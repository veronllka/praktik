using System;

namespace praktik.Models.Patterns.States
{
    /// <summary>
    /// Состояние "Отправлена" (Submitted).
    /// Заявка находится на рассмотрении.
    /// Возможные переходы:
    /// - Approved (Согласование заявки)
    /// - Rejected (Отклонение заявки)
    /// </summary>
    public class SubmittedState : BaseState
    {
        public override string StateName => "Submitted";

        public override bool CanTransitionTo(string targetState)
        {
            return targetState == "Approved" || targetState == "Rejected";
        }

        /// <summary>
        /// Выполняет действие "Согласовать".
        /// Переводит заявку в состояние 'Approved'.
        /// </summary>
        /// <param name="context">Контекст заявки.</param>
        /// <param name="userId">ID пользователя, согласующего заявку.</param>
        public override void Approve(MaterialRequestContext context, int userId)
        {
            if (!CanTransitionTo("Approved"))
            {
                throw new InvalidOperationException("Недопустимый переход состояния");
            }

            context.SetState(new ApprovedState());
            context.LogAction(userId, "Заявка согласована");
        }

        /// <summary>
        /// Выполняет действие "Отклонить".
        /// Переводит заявку в состояние 'Rejected'.
        /// </summary>
        /// <param name="context">Контекст заявки.</param>
        /// <param name="userId">ID пользователя, отклоняющего заявку.</param>
        public override void Reject(MaterialRequestContext context, int userId)
        {
            if (!CanTransitionTo("Rejected"))
            {
                throw new InvalidOperationException("Недопустимый переход состояния");
            }

            context.SetState(new RejectedState());
            context.LogAction(userId, "Заявка отклонена");
        }
    }
}
