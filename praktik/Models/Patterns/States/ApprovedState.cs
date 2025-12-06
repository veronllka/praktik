using System;

namespace praktik.Models.Patterns.States
{
    /// <summary>
    /// Состояние "Согласована"
    /// Возможные переходы: → Issued, → Rejected
    /// </summary>
    public class ApprovedState : BaseState
    {
        public override string StateName => "Approved";

        public override bool CanTransitionTo(string targetState)
        {
            return targetState == "Issued" || targetState == "Rejected";
        }

        public override void Issue(MaterialRequestContext context, int userId)
        {
            if (!CanTransitionTo("Issued"))
            {
                throw new InvalidOperationException("Недопустимый переход состояния");
            }

            context.SetState(new IssuedState());
            context.LogAction(userId, "Материалы выданы");
        }

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
