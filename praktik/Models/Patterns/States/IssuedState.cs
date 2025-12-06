using System;

namespace praktik.Models.Patterns.States
{
    /// <summary>
    /// Состояние "Выдана"
    /// Возможные переходы: → Delivered
    /// </summary>
    public class IssuedState : BaseState
    {
        public override string StateName => "Issued";

        public override bool CanTransitionTo(string targetState)
        {
            return targetState == "Delivered";
        }

        public override void Deliver(MaterialRequestContext context, int userId)
        {
            if (!CanTransitionTo("Delivered"))
            {
                throw new InvalidOperationException("Недопустимый переход состояния");
            }

            context.SetState(new DeliveredState());
            context.LogAction(userId, "Материалы доставлены");
        }
    }
}
