using System;

namespace praktik.Models.Patterns.States
{
    /// <summary>
    /// Состояние "Доставлена"
    /// Возможные переходы: → Closed
    /// </summary>
    public class DeliveredState : BaseState
    {
        public override string StateName => "Delivered";

        public override bool CanTransitionTo(string targetState)
        {
            return targetState == "Closed";
        }

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
