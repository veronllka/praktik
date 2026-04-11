using System;
using System.Collections.Generic;
using System.Linq;
using praktik.Models;

namespace praktik.Tests.TestDoubles
{
    internal sealed class FakeWorkPlannerContext : IWorkPlannerContext
    {
        public List<Task> Tasks { get; } = new List<Task>();
        public List<MaterialRequest> MaterialRequests { get; } = new List<MaterialRequest>();
        public List<TaskReport> TaskReports { get; } = new List<TaskReport>();
        public List<TaskPrintLog> TaskPrintLogs { get; } = new List<TaskPrintLog>();
        public List<Task> AddedTasks { get; } = new List<Task>();
        public List<Task> UpdatedTasks { get; } = new List<Task>();
        public List<MaterialRequest> UpdatedRequests { get; } = new List<MaterialRequest>();
        public List<TaskPrintLog> RecordedTaskPrints { get; } = new List<TaskPrintLog>();

        public Exception AddTaskException { get; set; }
        public Exception UpdateTaskException { get; set; }
        public Exception AddTaskReportException { get; set; }
        public Exception RecordTaskPrintException { get; set; }
        public Exception UpdateMaterialRequestException { get; set; }

        public List<Task> GetTasks()
        {
            return Tasks.ToList();
        }

        public void AddTask(Task task)
        {
            if (AddTaskException != null)
            {
                throw AddTaskException;
            }

            AddedTasks.Add(task);
            Tasks.Add(task);
        }

        public void UpdateTask(Task task)
        {
            if (UpdateTaskException != null)
            {
                throw UpdateTaskException;
            }

            UpdatedTasks.Add(task);
            var index = Tasks.FindIndex(existing => existing.TaskId == task.TaskId);
            if (index >= 0)
            {
                Tasks[index] = task;
            }
        }

        public void RecordTaskPrint(int taskId, int userId, string templateName, DateTime printedAt)
        {
            if (RecordTaskPrintException != null)
            {
                throw RecordTaskPrintException;
            }

            var task = Tasks.FirstOrDefault(existing => existing.TaskId == taskId);
            if (task == null)
            {
                throw new InvalidOperationException("Задача не найдена");
            }

            task.LastPrintedAt = printedAt;

            var log = new TaskPrintLog
            {
                LogId = TaskPrintLogs.Count + 1,
                TaskId = taskId,
                PrintedByUserId = userId,
                PrintedAt = printedAt,
                TemplateName = templateName,
                PrintedByName = $"User {userId}"
            };

            TaskPrintLogs.Add(log);
            RecordedTaskPrints.Add(log);
        }

        public List<TaskPrintLog> GetTaskPrintLogs(int taskId)
        {
            return TaskPrintLogs
                .Where(log => log.TaskId == taskId)
                .OrderByDescending(log => log.PrintedAt)
                .ThenByDescending(log => log.LogId)
                .ToList();
        }

        public List<TaskReport> GetTaskReports(int? taskId = null)
        {
            return taskId.HasValue
                ? TaskReports.Where(report => report.TaskId == taskId.Value).ToList()
                : TaskReports.ToList();
        }

        public void AddTaskReport(int taskId, int userId, string reportText, int? progressPercent = null, string attachmentUrl = null)
        {
            if (AddTaskReportException != null)
            {
                throw AddTaskReportException;
            }

            TaskReports.Add(new TaskReport
            {
                TaskId = taskId,
                ReportedByUserId = userId,
                ReportText = reportText,
                ProgressPercent = progressPercent,
                AttachmentUrl = attachmentUrl,
                ReportedAt = DateTime.UtcNow
            });
        }

        public List<MaterialRequest> GetMaterialRequests(int? taskId = null, int? requestId = null)
        {
            IEnumerable<MaterialRequest> requests = MaterialRequests;

            if (taskId.HasValue)
            {
                requests = requests.Where(request => request.TaskId == taskId.Value);
            }

            if (requestId.HasValue)
            {
                requests = requests.Where(request => request.RequestId == requestId.Value);
            }

            return requests.ToList();
        }

        public void UpdateMaterialRequest(MaterialRequest request)
        {
            if (UpdateMaterialRequestException != null)
            {
                throw UpdateMaterialRequestException;
            }

            UpdatedRequests.Add(request);
            var index = MaterialRequests.FindIndex(existing => existing.RequestId == request.RequestId);
            if (index >= 0)
            {
                MaterialRequests[index] = request;
            }
        }

        public List<DailyPlan> DailyPlans { get; } = new List<DailyPlan>();

        public DailyPlan GetDailyPlanByDate(DateTime date)
        {
            return DailyPlans.FirstOrDefault(p => p.PlanDate.Date == date.Date);
        }

        public int SaveDailyPlan(DailyPlan plan, int userId)
        {
            var existing = DailyPlans.FirstOrDefault(p => p.PlanDate.Date == plan.PlanDate.Date);
            if (existing != null)
            {
                DailyPlans.Remove(existing);
            }
            if (plan.PlanId == 0)
            {
                plan.PlanId = DailyPlans.Count + 1;
            }
            DailyPlans.Add(plan);
            return plan.PlanId;
        }

        public void ApproveDailyPlan(int planId)
        {
            var plan = DailyPlans.FirstOrDefault(p => p.PlanId == planId);
            if (plan != null)
            {
                plan.Status = "Утвержден";
            }
        }

        public List<DailyPlanItem> GetApprovedPlanItemsForDate(DateTime date)
        {
            var plan = DailyPlans.FirstOrDefault(p => p.PlanDate.Date == date.Date && p.Status == "Утвержден");
            return plan?.Items ?? new List<DailyPlanItem>();
        }
    }
}
