using System;

namespace praktik.Models.Patterns.States
{
    /// <summary>
    /// Состояние "Закрыта"
    /// Конечное состояние - переходов нет
    /// </summary>
    public class ClosedState : BaseState
    {
        public override string StateName => "Closed";

        public override bool CanTransitionTo(string targetState)
        {
            // Конечное состояние - переходов нет
            return false;
        }
    }
}
