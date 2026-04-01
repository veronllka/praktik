using System;
using System.Collections.Generic;

namespace praktik.Models
{
    public interface IWorkPlannerContext
    {
        List<Task> GetTasks();
        void AddTask(Task task);
        void UpdateTask(Task task);
        void RecordTaskPrint(int taskId, int userId, string templateName, DateTime printedAt);
        List<TaskPrintLog> GetTaskPrintLogs(int taskId);
        List<TaskReport> GetTaskReports(int? taskId = null);
        void AddTaskReport(int taskId, int userId, string reportText, int? progressPercent = null, string attachmentUrl = null);
        List<MaterialRequest> GetMaterialRequests(int? taskId = null, int? requestId = null);
        void UpdateMaterialRequest(MaterialRequest request);
    }
}
