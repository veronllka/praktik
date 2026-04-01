using System;
using System.Collections.Generic;
using System.Linq;

namespace praktik.Models
{
    public class Role
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; }

        public User User
        {
            get => default;
            set
            {
            }
        }

        public Task Task
        {
            get => default;
            set
            {
            }
        }
    }

    public sealed class RolePermissionDefinition
    {
        public RolePermissionDefinition(string code, string category, string displayName, string description)
        {
            Code = code;
            Category = category;
            DisplayName = displayName;
            Description = description;
        }

        public string Code { get; }
        public string Category { get; }
        public string DisplayName { get; }
        public string Description { get; }
    }

    public static class RolePermissionCatalog
    {
        public const string DashboardView = "dashboard.view";
        public const string SitesView = "sites.view";
        public const string SitesManage = "sites.manage";
        public const string CrewsView = "crews.view";
        public const string CrewsManage = "crews.manage";
        public const string CrewMembersManage = "crew_members.manage";
        public const string TasksView = "tasks.view";
        public const string TasksManage = "tasks.manage";
        public const string TasksDuplicate = "tasks.duplicate";
        public const string CalendarView = "calendar.view";
        public const string ReportsView = "reports.view";
        public const string ReportsExport = "reports.export";
        public const string MaterialRequestsView = "material_requests.view";
        public const string MaterialRequestsManage = "material_requests.manage";

        private static readonly IReadOnlyList<RolePermissionDefinition> definitions =
            new List<RolePermissionDefinition>
            {
                new RolePermissionDefinition(DashboardView, "Разделы", "Главная", "Доступ к главной странице и сводной статистике."),
                new RolePermissionDefinition(SitesView, "Разделы", "Стройплощадки", "Просмотр раздела стройплощадок."),
                new RolePermissionDefinition(SitesManage, "Операции", "Управление стройплощадками", "Создание, изменение и удаление стройплощадок."),
                new RolePermissionDefinition(CrewsView, "Разделы", "Бригады", "Просмотр раздела бригад."),
                new RolePermissionDefinition(CrewsManage, "Операции", "Управление бригадами", "Создание, изменение и удаление бригад."),
                new RolePermissionDefinition(CrewMembersManage, "Операции", "Состав бригад", "Управление составом бригад и назначением сотрудников."),
                new RolePermissionDefinition(TasksView, "Разделы", "Задачи", "Просмотр списка задач и карточек задач."),
                new RolePermissionDefinition(TasksManage, "Операции", "Управление задачами", "Создание, редактирование, смена статуса и печать задач."),
                new RolePermissionDefinition(TasksDuplicate, "Операции", "Дублирование задач", "Создание новой задачи на основе существующей."),
                new RolePermissionDefinition(CalendarView, "Разделы", "Календарь", "Доступ к календарному представлению задач."),
                new RolePermissionDefinition(ReportsView, "Разделы", "Отчеты", "Просмотр аналитики и формирование отчетов."),
                new RolePermissionDefinition(ReportsExport, "Операции", "Экспорт отчетов", "Сохранение отчетов в файл."),
                new RolePermissionDefinition(MaterialRequestsView, "Разделы", "Заявки на материалы", "Просмотр реестра заявок на материалы."),
                new RolePermissionDefinition(MaterialRequestsManage, "Операции", "Обработка заявок на материалы", "Создание, редактирование и согласование заявок.")
            };

        private static readonly IReadOnlyDictionary<string, RolePermissionDefinition> definitionMap =
            definitions.ToDictionary(d => d.Code, StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyList<RolePermissionDefinition> Definitions => definitions;

        public static string NormalizeRoleName(string roleName)
        {
            return (roleName ?? string.Empty).Trim().ToLowerInvariant();
        }

        public static bool IsKnownPermission(string permissionCode)
        {
            return !string.IsNullOrWhiteSpace(permissionCode) && definitionMap.ContainsKey(permissionCode.Trim());
        }

        public static IReadOnlyCollection<string> GetDefaultPermissions(string roleName)
        {
            switch (NormalizeRoleName(roleName))
            {
                case "администратор":
                case "админ":
                case "admin":
                case "administrator":
                    return definitions.Select(d => d.Code).ToArray();

                case "диспетчер":
                case "dispatcher":
                    return new[]
                    {
                        DashboardView,
                        SitesView,
                        SitesManage,
                        CrewsView,
                        CrewsManage,
                        CrewMembersManage,
                        TasksView,
                        TasksManage,
                        TasksDuplicate,
                        CalendarView,
                        ReportsView,
                        ReportsExport,
                        MaterialRequestsView,
                        MaterialRequestsManage
                    };

                case "бригадир":
                case "foreman":
                    return new[]
                    {
                        TasksView,
                        CalendarView
                    };

                default:
                    return Array.Empty<string>();
            }
        }

        public static IReadOnlyList<string> Sanitize(IEnumerable<string> permissionCodes)
        {
            if (permissionCodes == null)
            {
                return Array.Empty<string>();
            }

            return permissionCodes
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => code.Trim())
                .Where(code => definitionMap.ContainsKey(code))
                .Select(code => definitionMap[code].Code)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}



