using System;

namespace praktik.Models.Patterns.States
{
    /// <summary>
    /// Состояние "Закрыта" (Closed).
    /// Конечное состояние жизненного цикла заявки.
    /// Переходы из этого состояния невозможны.
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
