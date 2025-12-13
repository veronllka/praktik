using System;

namespace praktik.Models.Patterns.States
{
    /// <summary>
    /// Состояние "Согласована" (Approved).
    /// Заявка успешно прошла согласование.
    /// Возможные переходы:
    /// - Issued (Материалы выданы)
    /// - Rejected (Отклонена, например, при отсутствии материалов)
    /// </summary>
    public class ApprovedState : BaseState
    {
        public override string StateName => "Approved";

        public override bool CanTransitionTo(string targetState)
        {
            return targetState == "Issued" || targetState == "Rejected";
        }

        /// <summary>
        /// Выполняет действие "Выдать материалы".
        /// Переводит заявку в состояние 'Issued'.
        /// </summary>
        /// <param name="context">Контекст заявки.</param>
        /// <param name="userId">ID пользователя, выдающего материалы.</param>
        public override void Issue(MaterialRequestContext context, int userId)
        {
            if (!CanTransitionTo("Issued"))
            {
                throw new InvalidOperationException("Недопустимый переход состояния");
            }

            context.SetState(new IssuedState());
            context.LogAction(userId, "Материалы выданы");
        }

        /// <summary>
        /// Выполняет действие "Отклонить".
        /// Позволяет отклонить заявку даже после согласования (например, нет на складе).
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
            context.LogAction(userId, "Заявка отклонена после согласования");
        }
    }
}
