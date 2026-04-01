using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using praktik.Models;
using praktik.Models.Patterns;
using praktik.Tests.TestDoubles;

namespace praktik.Tests
{
    [TestClass]
    public class ReportServiceTests
    {
        [TestMethod]
        public void AddTaskReport_ReturnsTrue_WhenContextAcceptsReport()
        {
            var context = new FakeWorkPlannerContext();
            var service = new ReportService(context);

            var result = service.AddTaskReport(10, 3, "Ready");

            Assert.IsTrue(result);
            Assert.AreEqual(1, context.TaskReports.Count);
            Assert.AreEqual("Ready", context.TaskReports[0].ReportText);
        }

        [TestMethod]
        public void AddTaskReport_ReturnsFalse_WhenContextThrows()
        {
            var context = new FakeWorkPlannerContext
            {
                AddTaskReportException = new InvalidOperationException("insert failed")
            };
            var service = new ReportService(context);

            var result = service.AddTaskReport(10, 3, "Ready");

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void GetTaskReports_ReturnsReportsForRequestedTask()
        {
            var context = new FakeWorkPlannerContext();
            context.TaskReports.AddRange(new[]
            {
                new TaskReport { TaskId = 10, ReportText = "A" },
                new TaskReport { TaskId = 10, ReportText = "B" },
                new TaskReport { TaskId = 11, ReportText = "C" }
            });
            var service = new ReportService(context);

            var reports = service.GetTaskReports(10);

            Assert.AreEqual(2, reports.Count);
        }

        [TestMethod]
        public void AddTaskReport_CopiesAttachmentAndStoresManagedPath()
        {
            var context = new FakeWorkPlannerContext();
            var storageRoot = Path.Combine(Path.GetTempPath(), "praktik-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(storageRoot);

            var sourceFile = Path.Combine(storageRoot, "source.txt");
            File.WriteAllText(sourceFile, "attachment");

            try
            {
                var attachmentService = new TaskReportAttachmentService(storageRoot);
                var service = new ReportService(context, attachmentService);

                var result = service.AddTaskReport(10, 3, "Ready", 25, sourceFile);

                Assert.IsTrue(result);
                Assert.AreEqual(1, context.TaskReports.Count);
                Assert.IsFalse(string.IsNullOrWhiteSpace(context.TaskReports[0].AttachmentUrl));
                Assert.AreNotEqual(sourceFile, context.TaskReports[0].AttachmentUrl);
                Assert.AreEqual(Path.GetFileName(sourceFile), Path.GetFileName(context.TaskReports[0].AttachmentUrl));
                Assert.IsTrue(File.Exists(context.TaskReports[0].AttachmentUrl));
            }
            finally
            {
                if (Directory.Exists(storageRoot))
                {
                    Directory.Delete(storageRoot, true);
                }
            }
        }

        [TestMethod]
        public void AddTaskReport_DeletesCopiedAttachment_WhenContextThrows()
        {
            var context = new FakeWorkPlannerContext
            {
                AddTaskReportException = new InvalidOperationException("insert failed")
            };
            var storageRoot = Path.Combine(Path.GetTempPath(), "praktik-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(storageRoot);

            var sourceFile = Path.Combine(storageRoot, "source.txt");
            File.WriteAllText(sourceFile, "attachment");

            try
            {
                var attachmentService = new TaskReportAttachmentService(storageRoot);
                var service = new ReportService(context, attachmentService);

                var result = service.AddTaskReport(10, 3, "Ready", 25, sourceFile);

                Assert.IsFalse(result);
                var copiedFiles = Directory
                    .GetFiles(storageRoot, "*", SearchOption.AllDirectories)
                    .Where(path => !string.Equals(path, sourceFile, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                Assert.AreEqual(0, copiedFiles.Count);
            }
            finally
            {
                if (Directory.Exists(storageRoot))
                {
                    Directory.Delete(storageRoot, true);
                }
            }
        }
    }
}
