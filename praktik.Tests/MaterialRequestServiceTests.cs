using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using praktik.Models;
using praktik.Models.Patterns;
using praktik.Tests.TestDoubles;
using TaskEntity = praktik.Models.Task;

namespace praktik.Tests
{
    [TestClass]
    public class MaterialRequestServiceTests
    {
        [TestMethod]
        public void GetFilteredRequests_AppliesSiteCrewAndStatusFilters()
        {
            var context = new FakeWorkPlannerContext();
            context.MaterialRequests.AddRange(new[]
            {
                CreateRequest(1, 10, "Draft", 1, 2),
                CreateRequest(2, 11, "Approved", 1, 2),
                CreateRequest(3, 12, "Draft", 2, 3)
            });
            var service = new MaterialRequestService(context);

            var result = service.GetFilteredRequests(siteId: 1, crewId: 2, status: "Draft");

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(1, result[0].RequestId);
        }

        [TestMethod]
        public void ProcessRequest_ReturnsFalse_WhenRequestDoesNotExist()
        {
            var context = new FakeWorkPlannerContext();
            var service = new MaterialRequestService(context);

            var result = service.ProcessRequest(404, "submit", 7, out var errorMessage);

            Assert.IsFalse(result);
            Assert.IsFalse(string.IsNullOrWhiteSpace(errorMessage));
        }

        [TestMethod]
        public void ProcessRequest_ReturnsFalse_ForUnknownAction()
        {
            var context = new FakeWorkPlannerContext();
            context.MaterialRequests.Add(CreateRequest(1, 10, "Draft", 1, 2));
            var service = new MaterialRequestService(context);

            var result = service.ProcessRequest(1, "archive", 7, out var errorMessage);

            Assert.IsFalse(result);
            StringAssert.Contains(errorMessage, "archive");
            Assert.AreEqual(0, context.UpdatedRequests.Count);
        }

        [TestMethod]
        public void ProcessRequest_ReturnsFalse_ForInvalidTransition()
        {
            var context = new FakeWorkPlannerContext();
            context.MaterialRequests.Add(CreateRequest(1, 10, "Draft", 1, 2));
            var service = new MaterialRequestService(context);

            var result = service.ProcessRequest(1, "approve", 7, out var errorMessage);

            Assert.IsFalse(result);
            Assert.IsFalse(string.IsNullOrWhiteSpace(errorMessage));
            Assert.AreEqual(0, context.UpdatedRequests.Count);
            Assert.AreEqual(0, context.TaskReports.Count);
        }

        [TestMethod]
        public void ProcessRequest_Submit_UpdatesStatusAndLogsAction()
        {
            var context = new FakeWorkPlannerContext();
            var request = CreateRequest(1, 10, "Draft", 1, 2);
            context.MaterialRequests.Add(request);
            var service = new MaterialRequestService(context);

            var result = service.ProcessRequest(1, "submit", 21, out var errorMessage);

            Assert.IsTrue(result);
            Assert.IsNull(errorMessage);
            Assert.AreEqual("Submitted", request.Status);
            Assert.AreEqual(1, context.UpdatedRequests.Count);
            Assert.AreEqual(1, context.TaskReports.Count);
            Assert.AreEqual(10, context.TaskReports[0].TaskId);
            Assert.AreEqual(21, context.TaskReports[0].ReportedByUserId);
        }

        [TestMethod]
        public void ProcessRequest_CanExecuteFullLifecycle()
        {
            var context = new FakeWorkPlannerContext();
            var request = CreateRequest(1, 10, "Draft", 1, 2);
            context.MaterialRequests.Add(request);
            var service = new MaterialRequestService(context);

            Assert.IsTrue(service.ProcessRequest(1, "submit", 1, out var submitError), submitError);
            Assert.IsTrue(service.ProcessRequest(1, "approve", 1, out var approveError), approveError);
            Assert.IsTrue(service.ProcessRequest(1, "issue", 1, out var issueError), issueError);
            Assert.IsTrue(service.ProcessRequest(1, "deliver", 1, out var deliverError), deliverError);
            Assert.IsTrue(service.ProcessRequest(1, "close", 1, out var closeError), closeError);

            Assert.AreEqual("Closed", request.Status);
            Assert.AreEqual(5, context.UpdatedRequests.Count);
            Assert.AreEqual(5, context.TaskReports.Count);
        }

        [TestMethod]
        public void ProcessRequest_ReturnsFalse_WhenPersistenceFails()
        {
            var context = new FakeWorkPlannerContext
            {
                UpdateMaterialRequestException = new InvalidOperationException("save failed")
            };
            context.MaterialRequests.Add(CreateRequest(1, 10, "Draft", 1, 2));
            var service = new MaterialRequestService(context);

            var result = service.ProcessRequest(1, "submit", 7, out var errorMessage);

            Assert.IsFalse(result);
            StringAssert.Contains(errorMessage, "save failed");
        }

        private static MaterialRequest CreateRequest(int requestId, int taskId, string status, int siteId, int? crewId)
        {
            return new MaterialRequest
            {
                RequestId = requestId,
                TaskId = taskId,
                Status = status,
                Task = new TaskEntity
                {
                    TaskId = taskId,
                    SiteId = siteId,
                    CrewId = crewId,
                    Title = $"Task {taskId}"
                }
            };
        }
    }
}
