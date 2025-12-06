using System;

namespace praktik.Models.Patterns.States
{
    /// <summary>
    /// Состояние "Отклонена"
    /// Конечное состояние - переходов нет
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
