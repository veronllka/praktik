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
        List<MaterialCatalog> GetMaterialCatalog(bool activeOnly = true);
        int CreateMaterialCatalogItem(MaterialCatalog material);
        List<MaterialRequest> GetMaterialRequests(int? taskId = null, int? requestId = null);
        void UpdateMaterialRequest(MaterialRequest request);
        DailyPlan GetDailyPlanByDate(DateTime date);
        int SaveDailyPlan(DailyPlan plan, int userId);
        void ApproveDailyPlan(int planId);
        List<DailyPlanItem> GetApprovedPlanItemsForDate(DateTime date);
    }
}
