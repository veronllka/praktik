using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using praktik.Models.Patterns;
using praktik.Tests.TestDoubles;
using TaskEntity = praktik.Models.Task;

namespace praktik.Tests
{
    [TestClass]
    public class TaskServiceTests
    {
        [TestMethod]
        public void GetFilteredTasks_AppliesAllProvidedFilters()
        {
            var context = new FakeWorkPlannerContext();
            context.Tasks.AddRange(new[]
            {
                new TaskEntity { TaskId = 1, SiteId = 1, CrewId = 2, TaskStatusId = 3, Title = "A" },
                new TaskEntity { TaskId = 2, SiteId = 1, CrewId = 3, TaskStatusId = 3, Title = "B" },
                new TaskEntity { TaskId = 3, SiteId = 2, CrewId = 2, TaskStatusId = 3, Title = "C" }
            });

            var service = new TaskService(context);

            var result = service.GetFilteredTasks(siteId: 1, crewId: 2, statusId: 3);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(1, result[0].TaskId);
        }

        [TestMethod]
        public void CreateTask_ReturnsFalse_WhenTitleIsEmpty()
        {
            var context = new FakeWorkPlannerContext();
            var service = new TaskService(context);
            var task = new TaskEntity
            {
                Title = "   ",
                StartDate = new DateTime(2026, 3, 24),
                EndDate = new DateTime(2026, 3, 25)
            };

            var result = service.CreateTask(task, 7, out var errorMessage);

            Assert.IsFalse(result);
            Assert.IsFalse(string.IsNullOrWhiteSpace(errorMessage));
            Assert.AreEqual(0, context.AddedTasks.Count);
        }

        [TestMethod]
        public void CreateTask_ReturnsFalse_WhenDatesAreInvalid()
        {
            var context = new FakeWorkPlannerContext();
            var service = new TaskService(context);
            var task = new TaskEntity
            {
                Title = "Test",
                StartDate = new DateTime(2026, 3, 25),
                EndDate = new DateTime(2026, 3, 24)
            };

            var result = service.CreateTask(task, 7, out var errorMessage);

            Assert.IsFalse(result);
            Assert.IsFalse(string.IsNullOrWhiteSpace(errorMessage));
            Assert.AreEqual(0, context.AddedTasks.Count);
        }

        [TestMethod]
        public void CreateTask_SetsAuditFields_AndPersistsTask()
        {
            var context = new FakeWorkPlannerContext();
            var service = new TaskService(context);
            var task = new TaskEntity
            {
                TaskId = 15,
                Title = "Build wall",
                StartDate = new DateTime(2026, 3, 24),
                EndDate = new DateTime(2026, 3, 26)
            };
            var before = DateTime.Now;

            var result = service.CreateTask(task, 55, out var errorMessage);

            var after = DateTime.Now;
            Assert.IsTrue(result);
            Assert.IsNull(errorMessage);
            Assert.AreEqual(55, task.CreatedBy);
            Assert.IsTrue(task.CreatedAt >= before && task.CreatedAt <= after);
            Assert.AreEqual(1, context.AddedTasks.Count);
            Assert.AreSame(task, context.AddedTasks[0]);
        }

        [TestMethod]
        public void CreateTask_ReturnsFalse_WhenContextThrows()
        {
            var context = new FakeWorkPlannerContext
            {
                AddTaskException = new InvalidOperationException("db failure")
            };
            var service = new TaskService(context);
            var task = new TaskEntity
            {
                Title = "Build wall",
                StartDate = new DateTime(2026, 3, 24),
                EndDate = new DateTime(2026, 3, 26)
            };

            var result = service.CreateTask(task, 55, out var errorMessage);

            Assert.IsFalse(result);
            StringAssert.Contains(errorMessage, "db failure");
        }

        [TestMethod]
        public void UpdateStatus_UpdatesExistingTask()
        {
            var context = new FakeWorkPlannerContext();
            var task = new TaskEntity
            {
                TaskId = 7,
                SiteId = 1,
                CrewId = 2,
                TaskStatusId = 1,
                Title = "Install roof",
                StartDate = new DateTime(2026, 3, 24),
                EndDate = new DateTime(2026, 3, 26)
            };
            context.Tasks.Add(task);
            var service = new TaskService(context);

            var result = service.UpdateStatus(7, 5, out var errorMessage);

            Assert.IsTrue(result);
            Assert.IsNull(errorMessage);
            Assert.AreEqual(5, task.TaskStatusId);
            Assert.IsTrue(task.UpdatedAt.HasValue);
            Assert.AreEqual(1, context.UpdatedTasks.Count);
        }

        [TestMethod]
        public void UpdateStatus_ReturnsFalse_WhenTaskDoesNotExist()
        {
            var context = new FakeWorkPlannerContext();
            var service = new TaskService(context);

            var result = service.UpdateStatus(404, 5, out var errorMessage);

            Assert.IsFalse(result);
            Assert.IsFalse(string.IsNullOrWhiteSpace(errorMessage));
            Assert.AreEqual(0, context.UpdatedTasks.Count);
        }

        [TestMethod]
        public void RecordTaskPrint_CreatesLog_AndUpdatesLastPrintedAt()
        {
            var context = new FakeWorkPlannerContext();
            var task = new TaskEntity
            {
                TaskId = 7,
                SiteId = 1,
                CrewId = 2,
                TaskStatusId = 1,
                Title = "Install roof",
                StartDate = new DateTime(2026, 3, 24),
                EndDate = new DateTime(2026, 3, 26)
            };
            context.Tasks.Add(task);
            var service = new TaskService(context);
            var printedAt = new DateTime(2026, 3, 31, 15, 45, 0);

            var result = service.RecordTaskPrint(task.TaskId, 12, "Наряд-задача", printedAt, out var errorMessage);

            Assert.IsTrue(result);
            Assert.IsNull(errorMessage);
            Assert.AreEqual(printedAt, task.LastPrintedAt);
            Assert.AreEqual(1, context.RecordedTaskPrints.Count);
            Assert.AreEqual("Наряд-задача", context.RecordedTaskPrints[0].TemplateName);
        }

        [TestMethod]
        public void RecordTaskPrint_ReturnsFalse_WhenTaskDoesNotExist()
        {
            var context = new FakeWorkPlannerContext();
            var service = new TaskService(context);

            var result = service.RecordTaskPrint(404, 12, "Наряд-задача", new DateTime(2026, 3, 31, 15, 45, 0), out var errorMessage);

            Assert.IsFalse(result);
            Assert.IsFalse(string.IsNullOrWhiteSpace(errorMessage));
            Assert.AreEqual(0, context.RecordedTaskPrints.Count);
        }

        [TestMethod]
        public void RecordTaskPrint_ReturnsFalse_WhenContextThrows()
        {
            var context = new FakeWorkPlannerContext
            {
                RecordTaskPrintException = new InvalidOperationException("log failure")
            };
            context.Tasks.Add(new TaskEntity
            {
                TaskId = 7,
                SiteId = 1,
                CrewId = 2,
                TaskStatusId = 1,
                Title = "Install roof",
                StartDate = new DateTime(2026, 3, 24),
                EndDate = new DateTime(2026, 3, 26)
            });

            var service = new TaskService(context);

            var result = service.RecordTaskPrint(7, 12, "Наряд-задача", new DateTime(2026, 3, 31, 15, 45, 0), out var errorMessage);

            Assert.IsFalse(result);
            StringAssert.Contains(errorMessage, "log failure");
        }

        [TestMethod]
        public void GetTasksByDate_ReturnsOnlyTasksActiveOnRequestedDay()
        {
            var context = new FakeWorkPlannerContext();
            context.Tasks.AddRange(new[]
            {
                new TaskEntity { TaskId = 1, Title = "A", StartDate = new DateTime(2026, 3, 20), EndDate = new DateTime(2026, 3, 24) },
                new TaskEntity { TaskId = 2, Title = "B", StartDate = new DateTime(2026, 3, 25), EndDate = new DateTime(2026, 3, 27) },
                new TaskEntity { TaskId = 3, Title = "C", StartDate = new DateTime(2026, 3, 24), EndDate = new DateTime(2026, 3, 24) }
            });
            var service = new TaskService(context);

            var result = service.GetTasksByDate(new DateTime(2026, 3, 24));

            CollectionAssert.AreEquivalent(new[] { 1, 3 }, result.Select(task => task.TaskId).ToArray());
        }
    }
}
