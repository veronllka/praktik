using System;
using System.Data.SqlClient;
using System.Configuration;
using System.Collections.Generic;
using System.Windows;

namespace praktik.Models
{
    public class WorkPlannerContext : IDisposable
    {
        private string connectionString;
        private SqlConnection connection;

        public void Dispose()
        {
            if (connection != null)
            {
                connection.Dispose();
                connection = null;
            }
        }

        public WorkPlannerContext()
        {
            var cs = ConfigurationManager.ConnectionStrings["WorkPlannerConnection"];
            connectionString = cs != null ? cs.ConnectionString : "Server=WIN-IT3KG728UQJ\\SQLEXPRESS;Database=BrigadePlanner;Trusted_Connection=True;MultipleActiveResultSets=True;";
        }

        // Методы для работы с пользователями
        public List<User> GetUsers()
        {
            var users = new List<User>();
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var command = new SqlCommand(@"SELECT u.UserId, u.LoginName, u.PasswordPlain, r.RoleName
                                              FROM Users u
                                              LEFT JOIN UserRoles ur ON ur.UserId = u.UserId
                                              LEFT JOIN Roles r ON r.RoleId = ur.RoleId", connection);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        users.Add(new User
                        {
                            UserId = (int)reader["UserId"],
                            Username = (string)reader["LoginName"],
                            Password = (string)reader["PasswordPlain"],
                            Role = reader["RoleName"] != DBNull.Value ? (string)reader["RoleName"] : null
                        });
                    }
                }
            }
            return users;
        }

        public User GetUser(string username, string password)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var command = new SqlCommand(@"SELECT TOP 1 u.UserId, u.LoginName, u.PasswordPlain, r.RoleName
                                              FROM Users u
                                              LEFT JOIN UserRoles ur ON ur.UserId = u.UserId
                                              LEFT JOIN Roles r ON r.RoleId = ur.RoleId
                                              WHERE u.LoginName = @username AND u.PasswordPlain = @password", connection);
                command.Parameters.AddWithValue("@username", username);
                command.Parameters.AddWithValue("@password", password);
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return new User
                        {
                            UserId = (int)reader["UserId"],
                            Username = (string)reader["LoginName"],
                            Password = (string)reader["PasswordPlain"],
                            Role = reader["RoleName"] != DBNull.Value ? (string)reader["RoleName"] : null
                        };
                    }
                }
            }
            return null;
        }

        // Методы для работы с объектами
        public List<Site> GetSites()
        {
            var sites = new List<Site>();
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var command = new SqlCommand("SELECT SiteId, SiteName, Address FROM Sites", connection);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        sites.Add(new Site
                        {
                            SiteId = Convert.ToInt32(reader["SiteId"]),
                            SiteName = (string)reader["SiteName"],
                            Address = reader["Address"] as string
                        });
                    }
                }
            }
            return sites;
        }

        public void AddSite(Site site)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var command = new SqlCommand("INSERT INTO Sites (SiteName, Address) VALUES (@name, @address)", connection);
                command.Parameters.AddWithValue("@name", site.SiteName);
                command.Parameters.AddWithValue("@address", (object)(site.Address ?? (string)null) ?? DBNull.Value);
                command.ExecuteNonQuery();
            }
        }

        public void UpdateSite(Site site)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var command = new SqlCommand("UPDATE Sites SET SiteName = @name, Address = @address WHERE SiteId = @id", connection);
                command.Parameters.AddWithValue("@name", site.SiteName);
                command.Parameters.AddWithValue("@address", (object)(site.Address ?? (string)null) ?? DBNull.Value);
                command.Parameters.AddWithValue("@id", site.SiteId);
                command.ExecuteNonQuery();
            }
        }

        public void DeleteSite(int siteId)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var command = new SqlCommand("DELETE FROM Sites WHERE SiteId = @id", connection);
                command.Parameters.AddWithValue("@id", siteId);
                command.ExecuteNonQuery();
            }
        }

        // Аналогичные методы для других сущностей...
        public List<Crew> GetCrews()
        {
            var crews = new List<Crew>();
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var command = new SqlCommand("SELECT c.CrewId, c.CrewName, c.ForemanUserId, u.LoginName as BrigadierName FROM Crews c LEFT JOIN Users u ON c.ForemanUserId = u.UserId", connection);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        crews.Add(new Crew
                        {
                            CrewId = (int)reader["CrewId"],
                            CrewName = (string)reader["CrewName"],
                            BrigadierId = reader["ForemanUserId"] != DBNull.Value ? (int?)((int)reader["ForemanUserId"]) : null,
                            Brigadier = reader["BrigadierName"] != DBNull.Value ? new User { Username = (string)reader["BrigadierName"] } : null
                        });
                    }
                }
            }
            return crews;
        }

        public List<Models.Task> GetTasks()
        {
            var tasks = new List<Models.Task>();
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var command = new SqlCommand(@"
                    SELECT t.TaskId, t.SiteId, t.CrewId, t.Title, t.Description, t.StartDate, t.EndDate, t.PriorityId, t.StatusId,
                           s.SiteName, c.CrewName, p.PriorityName, ts.StatusName AS TaskStatusName
                    FROM Tasks t
                    LEFT JOIN Sites s ON t.SiteId = s.SiteId
                    LEFT JOIN Crews c ON t.CrewId = c.CrewId
                    LEFT JOIN TaskPriorities p ON t.PriorityId = p.PriorityId
                    LEFT JOIN TaskStatuses ts ON t.StatusId = ts.StatusId", connection);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        tasks.Add(new Models.Task
                        {
                            TaskId = Convert.ToInt32(reader["TaskId"]),
                            SiteId = Convert.ToInt32(reader["SiteId"]),
                            CrewId = reader["CrewId"] != DBNull.Value ? Convert.ToInt32(reader["CrewId"]) : (int?)null,
                            Title = reader["Title"] as string,
                            Description = reader["Description"] as string,
                            StartDate = (DateTime)reader["StartDate"],
                            EndDate = (DateTime)reader["EndDate"],
                            PriorityId = Convert.ToInt32(reader["PriorityId"]),
                            TaskStatusId = Convert.ToInt32(reader["StatusId"]),
                            Site = new Site { SiteName = reader["SiteName"] as string ?? string.Empty },
                            Crew = reader["CrewName"] != DBNull.Value ? new Crew { CrewName = reader["CrewName"] as string } : null,
                            Priority = new Priority { PriorityName = reader["PriorityName"] as string ?? string.Empty },
                            TaskStatus = new TaskStatus { TaskStatusName = reader["TaskStatusName"] as string ?? string.Empty }
                        });
                    }
                }
            }
            return tasks;
        }

        public List<Priority> GetPriorities()
        {
            var priorities = new List<Priority>();
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var command = new SqlCommand("SELECT PriorityId, PriorityName FROM TaskPriorities ORDER BY SortOrder", connection);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        priorities.Add(new Priority
                        {
                            PriorityId = Convert.ToInt32(reader["PriorityId"]),
                            PriorityName = (string)reader["PriorityName"]
                        });
                    }
                }
            }
            return priorities;
        }

        public List<TaskStatus> GetTaskStatuses()
        {
            var statuses = new List<TaskStatus>();
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var command = new SqlCommand("SELECT StatusId AS TaskStatusId, StatusName AS TaskStatusName FROM TaskStatuses", connection);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        statuses.Add(new TaskStatus
                        {
                            TaskStatusId = Convert.ToInt32(reader["TaskStatusId"]),
                            TaskStatusName = reader["TaskStatusName"] as string ?? string.Empty
                        });
                    }
                }
            }
            return statuses;
        }

        public int GetNewTaskStatusId(string statusName)
        {
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    var command = new SqlCommand("SELECT StatusId FROM TaskStatuses WHERE StatusName = @name", connection);
                    command.Parameters.AddWithValue("@name", statusName);
                    var result = command.ExecuteScalar();
                    
                    if (result != null && result != DBNull.Value)
                    {
                        return Convert.ToInt32(result);
                    }
                    else
                    {
                        // Если статус не найден, создаем новый
                        command = new SqlCommand("SELECT MIN(StatusId) FROM TaskStatuses", connection);
                        result = command.ExecuteScalar();
                        return result != null ? Convert.ToInt32(result) : 1;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при получении ID статуса задачи: {ex.Message}");
                return 1; // Возвращаем 1 как значение по умолчанию
            }
        }

        public void AddTask(Models.Task task)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var command = new SqlCommand(@"
                    INSERT INTO Tasks (SiteId, CrewId, Title, Description, StartDate, EndDate, PriorityId, StatusId)
                    VALUES (@siteId, @crewId, @title, @description, @startDate, @endDate, @priorityId, @statusId)", connection);
                command.Parameters.AddWithValue("@siteId", task.SiteId);
                command.Parameters.AddWithValue("@crewId", task.CrewId ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@title", task.Title);
                command.Parameters.AddWithValue("@description", (object)(task.Description ?? (string)null) ?? DBNull.Value);
                command.Parameters.AddWithValue("@startDate", task.StartDate);
                command.Parameters.AddWithValue("@endDate", task.EndDate);
                command.Parameters.AddWithValue("@priorityId", task.PriorityId);
                command.Parameters.AddWithValue("@statusId", task.TaskStatusId);
                command.ExecuteNonQuery();
            }
        }

        public void UpdateTask(Models.Task task)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var command = new SqlCommand(@"
                    UPDATE Tasks SET SiteId = @siteId, CrewId = @crewId, Title = @title, Description = @description,
                    StartDate = @startDate, EndDate = @endDate, PriorityId = @priorityId
                    WHERE TaskId = @taskId", connection);
                command.Parameters.AddWithValue("@siteId", task.SiteId);
                command.Parameters.AddWithValue("@crewId", task.CrewId ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@title", task.Title);
                command.Parameters.AddWithValue("@description", (object)(task.Description ?? (string)null) ?? DBNull.Value);
                command.Parameters.AddWithValue("@startDate", task.StartDate);
                command.Parameters.AddWithValue("@endDate", task.EndDate);
                command.Parameters.AddWithValue("@priorityId", task.PriorityId);
                command.Parameters.AddWithValue("@taskId", task.TaskId);
                command.ExecuteNonQuery();
            }
        }

        public void UpdateTaskStatus(int taskId, int statusId, int userId, string comment)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                // Обновляем статус задачи
                var updateCommand = new SqlCommand("UPDATE Tasks SET StatusId = @statusId WHERE TaskId = @taskId", connection);
                updateCommand.Parameters.AddWithValue("@statusId", statusId);
                updateCommand.Parameters.AddWithValue("@taskId", taskId);
                updateCommand.ExecuteNonQuery();

                 var reportCommand = new SqlCommand(@"
                    INSERT INTO TaskReports (TaskId, ReportedByUserId, ReportedAt, ReportText, ProgressPercent)
                    VALUES (@taskId, @userId, @reportedAt, @text, @progress)", connection);
                reportCommand.Parameters.AddWithValue("@taskId", taskId);
                reportCommand.Parameters.AddWithValue("@userId", userId);
                reportCommand.Parameters.AddWithValue("@reportedAt", DateTime.UtcNow);
                reportCommand.Parameters.AddWithValue("@text", (object)(comment ?? "Статус изменён") ?? DBNull.Value);
                reportCommand.Parameters.AddWithValue("@progress", DBNull.Value);
                reportCommand.ExecuteNonQuery();
            }
        }

        public void DeleteTask(int taskId)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var command = new SqlCommand("DELETE FROM Tasks WHERE TaskId = @id", connection);
                command.Parameters.AddWithValue("@id", taskId);
                command.ExecuteNonQuery();
            }
        }

        public void AddCrew(Crew crew)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var command = new SqlCommand("INSERT INTO Crews (CrewName, ForemanUserId) VALUES (@name, @brigadierId)", connection);
                command.Parameters.AddWithValue("@name", crew.CrewName);
                command.Parameters.AddWithValue("@brigadierId", (object)(crew.BrigadierId ?? (int?)null) ?? DBNull.Value);
                command.ExecuteNonQuery();
            }
        }

        public void UpdateCrew(Crew crew)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var command = new SqlCommand("UPDATE Crews SET CrewName = @name, ForemanUserId = @brigadierId WHERE CrewId = @id", connection);
                command.Parameters.AddWithValue("@name", crew.CrewName);
                command.Parameters.AddWithValue("@brigadierId", (object)(crew.BrigadierId ?? (int?)null) ?? DBNull.Value);
                command.Parameters.AddWithValue("@id", crew.CrewId);
                command.ExecuteNonQuery();
            }
        }

        public void DeleteCrew(int crewId)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var command = new SqlCommand("DELETE FROM Crews WHERE CrewId = @id", connection);
                command.Parameters.AddWithValue("@id", crewId);
                command.ExecuteNonQuery();
            }
        }

        // Регистрация пользователей
        public List<Role> GetRoles()
        {
            var roles = new List<Role>();
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var command = new SqlCommand("SELECT RoleId, RoleName FROM Roles", connection);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        roles.Add(new Role
                        {
                            RoleId = Convert.ToInt32(reader["RoleId"]),
                            RoleName = reader["RoleName"] as string
                        });
                    }
                }
            }
            return roles;
        }

        public void RegisterUser(string loginName, string password, string fullName, int roleId)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                
                // Проверяем, не существует ли уже такой логин
                var checkCommand = new SqlCommand("SELECT COUNT(*) FROM Users WHERE LoginName = @login", connection);
                checkCommand.Parameters.AddWithValue("@login", loginName);
                var exists = (int)checkCommand.ExecuteScalar() > 0;
                
                if (exists)
                {
                    throw new Exception("Пользователь с таким логином уже существует");
                }

                // Создаем пользователя
                var insertUserCommand = new SqlCommand(@"
                    INSERT INTO Users (LoginName, PasswordPlain, FullName, IsActive, CreatedAt) 
                    OUTPUT INSERTED.UserId
                    VALUES (@login, @password, @fullName, 1, @createdAt)", connection);
                insertUserCommand.Parameters.AddWithValue("@login", loginName);
                insertUserCommand.Parameters.AddWithValue("@password", password);
                insertUserCommand.Parameters.AddWithValue("@fullName", fullName);
                insertUserCommand.Parameters.AddWithValue("@createdAt", DateTime.UtcNow);
                
                var userId = (int)insertUserCommand.ExecuteScalar();

                // Назначаем роль
                var insertRoleCommand = new SqlCommand(@"
                    INSERT INTO UserRoles (UserId, RoleId) VALUES (@userId, @roleId)", connection);
                insertRoleCommand.Parameters.AddWithValue("@userId", userId);
                insertRoleCommand.Parameters.AddWithValue("@roleId", roleId);
                insertRoleCommand.ExecuteNonQuery();
                
                Logger.Info($"User registered: {loginName}");
            }
        }

        // Получение отчетов по задачам
        public List<TaskReport> GetTaskReports(int? taskId = null)
        {
            var reports = new List<TaskReport>();
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var sql = @"
                    SELECT tr.ReportId, tr.TaskId, tr.ReportedByUserId, tr.ReportedAt, tr.ReportText, tr.ProgressPercent,
                           t.Title as TaskTitle, u.LoginName as ReporterName
                    FROM TaskReports tr
                    LEFT JOIN Tasks t ON tr.TaskId = t.TaskId
                    LEFT JOIN Users u ON tr.ReportedByUserId = u.UserId";
                
                if (taskId.HasValue)
                {
                    sql += " WHERE tr.TaskId = @taskId";
                }
                
                sql += " ORDER BY tr.ReportedAt DESC";
                
                var command = new SqlCommand(sql, connection);
                if (taskId.HasValue)
                {
                    command.Parameters.AddWithValue("@taskId", taskId.Value);
                }
                
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        reports.Add(new TaskReport
                        {
                            ReportId = Convert.ToInt32(reader["ReportId"]),
                            TaskId = Convert.ToInt32(reader["TaskId"]),
                            ReportedByUserId = Convert.ToInt32(reader["ReportedByUserId"]),
                            ReportedAt = (DateTime)reader["ReportedAt"],
                            ReportText = reader["ReportText"] as string,
                            ProgressPercent = reader["ProgressPercent"] != DBNull.Value ? (int?)Convert.ToInt32(reader["ProgressPercent"]) : null,
                            TaskTitle = reader["TaskTitle"] as string,
                            ReporterName = reader["ReporterName"] as string
                        });
                    }
                }
            }
            return reports;
        }
    }
}
