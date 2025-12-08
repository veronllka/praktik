using System;

namespace praktik.Models.Patterns
{
  
    public interface IMaterialRequestState
    {
       
        string StateName { get; }

        bool CanTransitionTo(string targetState);

     
        void Submit(MaterialRequestContext context, int userId);

        void Approve(MaterialRequestContext context, int userId);

        void Reject(MaterialRequestContext context, int userId);

        void Issue(MaterialRequestContext context, int userId);

        void Deliver(MaterialRequestContext context, int userId);

        void Close(MaterialRequestContext context, int userId);
    }
}
