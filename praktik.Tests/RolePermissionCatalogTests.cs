using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using praktik.Models;

namespace praktik.Tests
{
    [TestClass]
    public class RolePermissionCatalogTests
    {
        [TestMethod]
        public void AdminDefaultsContainAllKnownPermissions()
        {
            var expected = RolePermissionCatalog.Definitions
                .Select(definition => definition.Code)
                .OrderBy(code => code)
                .ToList();

            var actual = RolePermissionCatalog.GetDefaultPermissions("Администратор")
                .OrderBy(code => code)
                .ToList();

            CollectionAssert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void BrigadierDefaultsContainOnlyTaskAndCalendarAccess()
        {
            var actual = RolePermissionCatalog.GetDefaultPermissions("Бригадир")
                .OrderBy(code => code)
                .ToList();

            var expected = new[]
            {
                RolePermissionCatalog.CalendarView,
                RolePermissionCatalog.TasksView
            }
            .OrderBy(code => code)
            .ToList();

            CollectionAssert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void SanitizeRemovesUnknownValuesAndDuplicates()
        {
            var actual = RolePermissionCatalog.Sanitize(new[]
            {
                RolePermissionCatalog.TasksView,
                "unknown.permission",
                RolePermissionCatalog.TasksView,
                RolePermissionCatalog.ReportsView
            });

            var expected = new[]
            {
                RolePermissionCatalog.TasksView,
                RolePermissionCatalog.ReportsView
            }
            .OrderBy(code => code)
            .ToList();

            CollectionAssert.AreEqual(expected, actual.OrderBy(code => code).ToList());
        }
    }
}
