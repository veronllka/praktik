using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using praktik.Models;

namespace praktik.Models.Patterns
{
    /// <summary>
    /// Сервис для работы с отчетами по задачам.
    /// Подсистема, используемая WorkPlannerFacade.
    /// </summary>
    public class ReportService
    {
        private readonly IWorkPlannerContext db;
        private readonly TaskReportAttachmentService attachmentService;

        public ReportService(IWorkPlannerContext context, TaskReportAttachmentService attachmentService = null)
        {
            db = context;
            this.attachmentService = attachmentService ?? new TaskReportAttachmentService();
        }

        /// <summary>
        /// Добавляет отчет (запись истории) к задаче.
        /// </summary>
        public bool AddTaskReport(int taskId, int userId, string reportText, int? progressPercent = null, string attachmentSourcePath = null)
        {
            string storedAttachmentPath = null;

            try
            {
                storedAttachmentPath = attachmentService.SaveAttachment(attachmentSourcePath, taskId);
                db.AddTaskReport(taskId, userId, reportText, progressPercent, storedAttachmentPath);
                return true;
            }
            catch
            {
                attachmentService.DeleteManagedAttachment(storedAttachmentPath);
                return false;
            }
        }

        /// <summary>
        /// Получает список отчетов для конкретной задачи.
        /// </summary>
        public List<TaskReport> GetTaskReports(int taskId)
        {
            return db.GetTaskReports(taskId);
        }
    }

    public sealed class TaskReportAttachmentService
    {
        private static readonly string[] SupportedExtensions =
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".bmp",
            ".gif",
            ".pdf",
            ".doc",
            ".docx",
            ".xls",
            ".xlsx",
            ".txt"
        };

        private readonly string storageRoot;

        public TaskReportAttachmentService(string storageRoot = null)
        {
            this.storageRoot = string.IsNullOrWhiteSpace(storageRoot)
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "praktik",
                    "TaskReportAttachments")
                : storageRoot;
        }

        public static string BuildFileDialogFilter()
        {
            return "Поддерживаемые файлы|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.pdf;*.doc;*.docx;*.xls;*.xlsx;*.txt|Изображения|*.jpg;*.jpeg;*.png;*.bmp;*.gif|Документы|*.pdf;*.doc;*.docx;*.xls;*.xlsx;*.txt";
        }

        public static string GetAttachmentFileName(string attachmentUrl)
        {
            if (string.IsNullOrWhiteSpace(attachmentUrl))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFileName(attachmentUrl);
            }
            catch
            {
                return attachmentUrl;
            }
        }

        public static bool TryValidateSourceFile(string sourceFilePath, out string errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(sourceFilePath))
            {
                return true;
            }

            if (!File.Exists(sourceFilePath))
            {
                errorMessage = "Выбранный файл не найден. Возможно, он был удален или перемещен.";
                return false;
            }

            var extension = Path.GetExtension(sourceFilePath);
            if (string.IsNullOrWhiteSpace(extension) ||
                Array.IndexOf(SupportedExtensions, extension.ToLowerInvariant()) < 0)
            {
                errorMessage = "Допустимы только файлы JPG, PNG, BMP, GIF, PDF, DOC, DOCX, XLS, XLSX и TXT.";
                return false;
            }

            return true;
        }

        public static bool TryOpenAttachment(string attachmentUrl, out string errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(attachmentUrl))
            {
                errorMessage = "Для этого отчета вложение не задано.";
                return false;
            }

            if (!File.Exists(attachmentUrl))
            {
                errorMessage = "Вложение не найдено. Возможно, файл был удален или перемещен.";
                return false;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = attachmentUrl,
                    UseShellExecute = true
                });
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Не удалось открыть вложение: {ex.Message}";
                return false;
            }
        }

        public string SaveAttachment(string sourceFilePath, int taskId)
        {
            if (string.IsNullOrWhiteSpace(sourceFilePath))
            {
                return null;
            }

            if (!TryValidateSourceFile(sourceFilePath, out var errorMessage))
            {
                throw new InvalidOperationException(errorMessage);
            }

            var taskFolder = Path.Combine(storageRoot, $"Task_{taskId}");
            Directory.CreateDirectory(taskFolder);

            var attachmentFolder = Path.Combine(taskFolder, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(attachmentFolder);

            var fileName = Path.GetFileName(sourceFilePath);
            var destinationPath = Path.Combine(attachmentFolder, fileName);

            File.Copy(sourceFilePath, destinationPath, false);
            return Path.GetFullPath(destinationPath);
        }

        public void DeleteManagedAttachment(string attachmentPath)
        {
            if (string.IsNullOrWhiteSpace(attachmentPath) || !File.Exists(attachmentPath))
            {
                return;
            }

            try
            {
                File.Delete(attachmentPath);

                var parentDirectory = Path.GetDirectoryName(attachmentPath);
                if (!string.IsNullOrWhiteSpace(parentDirectory) &&
                    Directory.Exists(parentDirectory) &&
                    !Directory.EnumerateFileSystemEntries(parentDirectory).Any())
                {
                    Directory.Delete(parentDirectory, false);
                }
            }
            catch
            {
            }
        }
    }
}
