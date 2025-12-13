using System;

namespace praktik.Models.Patterns.States
{
    /// <summary>
    /// Состояние "Отклонена" (Rejected).
    /// Заявка отклонена.
    /// Конечное состояние (в текущей реализации).
    /// </summary>
    public class RejectedState : BaseState
    {
        public override string StateName => "Rejected";

        public override bool CanTransitionTo(string targetState)
        {
            // Конечное состояние - переходов нет
            return false;
        }
    }
}
