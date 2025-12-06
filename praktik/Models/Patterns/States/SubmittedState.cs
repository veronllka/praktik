using System;

namespace praktik.Models.Patterns.States
{
    /// <summary>
    /// Состояние "Отправлена"
    /// Возможные переходы: → Approved, → Rejected
    /// </summary>
    public class SubmittedState : BaseState
    {
        public override string StateName => "Submitted";

        public override bool CanTransitionTo(string targetState)
        {
            return targetState == "Approved" || targetState == "Rejected";
        }

        public override void Approve(MaterialRequestContext context, int userId)
        {
            if (!CanTransitionTo("Approved"))
            {
                throw new InvalidOperationException("Недопустимый переход состояния");
            }

            context.SetState(new ApprovedState());
            context.LogAction(userId, "Заявка согласована");
        }

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
