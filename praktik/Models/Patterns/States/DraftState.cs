using System;

namespace praktik.Models.Patterns.States
{
    /// <summary>
    /// Состояние "Черновик"
    /// Возможные переходы: → Submitted
    /// </summary>
    public class DraftState : BaseState
    {
        public override string StateName => "Draft";

        public override bool CanTransitionTo(string targetState)
        {
            return targetState == "Submitted";
        }

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
