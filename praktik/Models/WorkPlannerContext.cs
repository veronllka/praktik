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
                    SELECT t.TaskId, t.SiteId, t.CrewId, t.Title, t.Description, t.StartDate, t.EndDate, t.PriorityId, t.StatusId, t.LabelId,
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
                            LabelId = reader["LabelId"] != DBNull.Value ? (int?)Convert.ToInt32(reader["LabelId"]) : null,
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

        public Models.Task GetTaskById(int taskId)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var command = new SqlCommand(@"
                    SELECT t.TaskId, t.SiteId, t.CrewId, t.Title, t.Description, t.StartDate, t.EndDate, 
                           t.PriorityId, t.StatusId, t.LabelId,
                           s.SiteName, s.Address,
                           c.CrewName, c.ForemanUserId,
                           u.LoginName as BrigadierName,
                           p.PriorityName, 
                           ts.StatusName AS TaskStatusName
                    FROM Tasks t
                    LEFT JOIN Sites s ON t.SiteId = s.SiteId
                    LEFT JOIN Crews c ON t.CrewId = c.CrewId
                    LEFT JOIN Users u ON c.ForemanUserId = u.UserId
                    LEFT JOIN TaskPriorities p ON t.PriorityId = p.PriorityId
                    LEFT JOIN TaskStatuses ts ON t.StatusId = ts.StatusId
                    WHERE t.TaskId = @taskId", connection);
                command.Parameters.AddWithValue("@taskId", taskId);
                
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        var task = new Models.Task
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
                            LabelId = reader["LabelId"] != DBNull.Value ? (int?)Convert.ToInt32(reader["LabelId"]) : null,
                            CreatedBy = 0,
                            CreatedAt = DateTime.Now,
                            UpdatedAt = null,
                            Site = new Site 
                            { 
                                SiteName = reader["SiteName"] as string ?? string.Empty,
                                Address = reader["Address"] as string ?? string.Empty
                            },
                            Priority = new Priority { PriorityName = reader["PriorityName"] as string ?? string.Empty },
                            TaskStatus = new TaskStatus { TaskStatusName = reader["TaskStatusName"] as string ?? string.Empty }
                        };

                        if (reader["CrewName"] != DBNull.Value)
                        {
                            task.Crew = new Crew 
                            { 
                                CrewName = reader["CrewName"] as string,
                                BrigadierId = reader["ForemanUserId"] != DBNull.Value ? (int?)Convert.ToInt32(reader["ForemanUserId"]) : null,
                                Brigadier = reader["BrigadierName"] != DBNull.Value ? new User 
                                { 
                                    Username = reader["BrigadierName"] as string
                                } : null
                            };
                        }

                        return task;
                    }
                }
            }
            return null;
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
                        command = new SqlCommand("SELECT MIN(StatusId) FROM TaskStatuses", connection);
                        result = command.ExecuteScalar();
                        return result != null ? Convert.ToInt32(result) : 1;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при получении ID статуса задачи: {ex.Message}");
                return 1;
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

        public void AddTaskReport(int taskId, int userId, string reportText, int? progressPercent = null)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var command = new SqlCommand(@"
                    INSERT INTO TaskReports (TaskId, ReportedByUserId, ReportedAt, ReportText, ProgressPercent)
                    VALUES (@taskId, @userId, @reportedAt, @text, @progress)", connection);
                command.Parameters.AddWithValue("@taskId", taskId);
                command.Parameters.AddWithValue("@userId", userId);
                command.Parameters.AddWithValue("@reportedAt", DateTime.UtcNow);
                command.Parameters.AddWithValue("@text", (object)reportText ?? DBNull.Value);
                command.Parameters.AddWithValue("@progress", (object)progressPercent ?? DBNull.Value);
                command.ExecuteNonQuery();
            }
        }
        public List<MaterialCatalog> GetMaterialCatalog(bool activeOnly = true)
        {
            var materials = new List<MaterialCatalog>();
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var sql = "SELECT MaterialId, Name, Unit, Code, IsActive, CreatedAt FROM MaterialCatalog";
                if (activeOnly)
                    sql += " WHERE IsActive = 1";
                sql += " ORDER BY Name";
                
                var command = new SqlCommand(sql, connection);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        materials.Add(new MaterialCatalog
                        {
                            MaterialId = Convert.ToInt32(reader["MaterialId"]),
                            Name = reader["Name"] as string ?? string.Empty,
                            Unit = reader["Unit"] as string,
                            Code = reader["Code"] as string,
                            IsActive = Convert.ToBoolean(reader["IsActive"]),
                            CreatedAt = (DateTime)reader["CreatedAt"]
                        });
                    }
                }
            }
            return materials;
        }

        public List<MaterialRequest> GetMaterialRequests(int? taskId = null, int? requestId = null)
        {
            var requests = new List<MaterialRequest>();
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var sql = @"
                    SELECT mr.RequestId, mr.TaskId, mr.CreatedByUserId, mr.CreatedAt, mr.RequiredDate, mr.Status, mr.Comment,
                           t.Title as TaskTitle, t.SiteId, t.CrewId,
                           s.SiteName, c.CrewName,
                           u.LoginName as CreatedByName
                    FROM MaterialRequests mr
                    LEFT JOIN Tasks t ON mr.TaskId = t.TaskId
                    LEFT JOIN Sites s ON t.SiteId = s.SiteId
                    LEFT JOIN Crews c ON t.CrewId = c.CrewId
                    LEFT JOIN Users u ON mr.CreatedByUserId = u.UserId
                    WHERE 1=1";
                
                if (taskId.HasValue)
                    sql += " AND mr.TaskId = @taskId";
                if (requestId.HasValue)
                    sql += " AND mr.RequestId = @requestId";
                
                sql += " ORDER BY mr.CreatedAt DESC";
                
                var command = new SqlCommand(sql, connection);
                if (taskId.HasValue)
                    command.Parameters.AddWithValue("@taskId", taskId.Value);
                if (requestId.HasValue)
                    command.Parameters.AddWithValue("@requestId", requestId.Value);
                
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var request = new MaterialRequest
                        {
                            RequestId = Convert.ToInt32(reader["RequestId"]),
                            TaskId = Convert.ToInt32(reader["TaskId"]),
                            CreatedByUserId = Convert.ToInt32(reader["CreatedByUserId"]),
                            CreatedAt = (DateTime)reader["CreatedAt"],
                            RequiredDate = reader["RequiredDate"] != DBNull.Value ? (DateTime?)reader["RequiredDate"] : null,
                            Status = reader["Status"] as string ?? string.Empty,
                            Comment = reader["Comment"] as string,
                            Task = new Task 
                            { 
                                TaskId = Convert.ToInt32(reader["TaskId"]), 
                                Title = reader["TaskTitle"] as string,
                                SiteId = reader["SiteId"] != DBNull.Value ? Convert.ToInt32(reader["SiteId"]) : 0,
                                CrewId = reader["CrewId"] != DBNull.Value ? (int?)Convert.ToInt32(reader["CrewId"]) : null,
                                Site = reader["SiteName"] != DBNull.Value ? new Site { SiteName = reader["SiteName"] as string } : null,
                                Crew = reader["CrewName"] != DBNull.Value ? new Crew { CrewName = reader["CrewName"] as string } : null
                            },
                            CreatedByUser = new User { UserId = Convert.ToInt32(reader["CreatedByUserId"]), Username = reader["CreatedByName"] as string }
                        };
                        
                        request.Items = GetMaterialRequestItems(request.RequestId);
                        requests.Add(request);
                    }
                }
            }
            return requests;
        }

        // Получение позиций заявки
        public List<MaterialRequestItem> GetMaterialRequestItems(int requestId)
        {
            var items = new List<MaterialRequestItem>();
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var command = new SqlCommand(@"
                    SELECT mri.RequestItemId, mri.RequestId, mri.MaterialId, mri.Qty, mri.Comment,
                           mc.Name as MaterialName, mc.Unit as MaterialUnit
                    FROM MaterialRequestItems mri
                    LEFT JOIN MaterialCatalog mc ON mri.MaterialId = mc.MaterialId
                    WHERE mri.RequestId = @requestId
                    ORDER BY mri.RequestItemId", connection);
                command.Parameters.AddWithValue("@requestId", requestId);
                
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        items.Add(new MaterialRequestItem
                        {
                            RequestItemId = Convert.ToInt32(reader["RequestItemId"]),
                            RequestId = Convert.ToInt32(reader["RequestId"]),
                            MaterialId = Convert.ToInt32(reader["MaterialId"]),
                            Qty = Convert.ToDecimal(reader["Qty"]),
                            Comment = reader["Comment"] as string,
                            Material = new MaterialCatalog 
                            { 
                                MaterialId = Convert.ToInt32(reader["MaterialId"]),
                                Name = reader["MaterialName"] as string ?? string.Empty,
                                Unit = reader["MaterialUnit"] as string
                            }
                        });
                    }
                }
            }
            return items;
        }

        public int CreateMaterialRequest(MaterialRequest request)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        var command = new SqlCommand(@"
                            INSERT INTO MaterialRequests (TaskId, CreatedByUserId, CreatedAt, RequiredDate, Status, Comment)
                            OUTPUT INSERTED.RequestId
                            VALUES (@taskId, @createdBy, @createdAt, @requiredDate, @status, @comment)", connection, transaction);
                        command.Parameters.AddWithValue("@taskId", request.TaskId);
                        command.Parameters.AddWithValue("@createdBy", request.CreatedByUserId);
                        command.Parameters.AddWithValue("@createdAt", DateTime.UtcNow);
                        command.Parameters.AddWithValue("@requiredDate", (object)request.RequiredDate ?? DBNull.Value);
                        command.Parameters.AddWithValue("@status", request.Status);
                        command.Parameters.AddWithValue("@comment", (object)request.Comment ?? DBNull.Value);
                        
                        var requestId = (int)command.ExecuteScalar();
                        
                        foreach (var item in request.Items)
                        {
                            var itemCommand = new SqlCommand(@"
                                INSERT INTO MaterialRequestItems (RequestId, MaterialId, Qty, Comment)
                                VALUES (@requestId, @materialId, @qty, @comment)", connection, transaction);
                            itemCommand.Parameters.AddWithValue("@requestId", requestId);
                            itemCommand.Parameters.AddWithValue("@materialId", item.MaterialId);
                            itemCommand.Parameters.AddWithValue("@qty", item.Qty);
                            itemCommand.Parameters.AddWithValue("@comment", (object)item.Comment ?? DBNull.Value);
                            itemCommand.ExecuteNonQuery();
                        }
                        
                        LogMaterialRequestStatusChange(requestId, null, request.Status, request.CreatedByUserId, "Создан черновик заявки", connection, transaction);
                        
                        AddServiceTaskReport(request.TaskId, request.CreatedByUserId, "Создан черновик заявки на материалы", connection, transaction);
                        
                        transaction.Commit();
                        return requestId;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        // Обновление заявки на материалы
        public void UpdateMaterialRequest(MaterialRequest request)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        var command = new SqlCommand(@"
                            UPDATE MaterialRequests 
                            SET RequiredDate = @requiredDate, Status = @status, Comment = @comment
                            WHERE RequestId = @requestId", connection, transaction);
                        command.Parameters.AddWithValue("@requestId", request.RequestId);
                        command.Parameters.AddWithValue("@requiredDate", (object)request.RequiredDate ?? DBNull.Value);
                        command.Parameters.AddWithValue("@status", request.Status);
                        command.Parameters.AddWithValue("@comment", (object)request.Comment ?? DBNull.Value);
                        command.ExecuteNonQuery();
                        
                        var deleteCommand = new SqlCommand("DELETE FROM MaterialRequestItems WHERE RequestId = @requestId", connection, transaction);
                        deleteCommand.Parameters.AddWithValue("@requestId", request.RequestId);
                        deleteCommand.ExecuteNonQuery();
                        
                        foreach (var item in request.Items)
                        {
                            var itemCommand = new SqlCommand(@"
                                INSERT INTO MaterialRequestItems (RequestId, MaterialId, Qty, Comment)
                                VALUES (@requestId, @materialId, @qty, @comment)", connection, transaction);
                            itemCommand.Parameters.AddWithValue("@requestId", request.RequestId);
                            itemCommand.Parameters.AddWithValue("@materialId", item.MaterialId);
                            itemCommand.Parameters.AddWithValue("@qty", item.Qty);
                            itemCommand.Parameters.AddWithValue("@comment", (object)item.Comment ?? DBNull.Value);
                            itemCommand.ExecuteNonQuery();
                        }
                        
                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        // Изменение статуса заявки
        public void ChangeMaterialRequestStatus(int requestId, string newStatus, int userId, string comment = null)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        var getCommand = new SqlCommand("SELECT Status, TaskId FROM MaterialRequests WHERE RequestId = @requestId", connection, transaction);
                        getCommand.Parameters.AddWithValue("@requestId", requestId);
                        string oldStatus = null;
                        int taskId = 0;
                        using (var reader = getCommand.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                oldStatus = reader["Status"] as string;
                                taskId = Convert.ToInt32(reader["TaskId"]);
                            }
                        }
                        
                        var updateCommand = new SqlCommand("UPDATE MaterialRequests SET Status = @status WHERE RequestId = @requestId", connection, transaction);
                        updateCommand.Parameters.AddWithValue("@status", newStatus);
                        updateCommand.Parameters.AddWithValue("@requestId", requestId);
                        updateCommand.ExecuteNonQuery();
                        
                        LogMaterialRequestStatusChange(requestId, oldStatus, newStatus, userId, comment, connection, transaction);
                        
                        string reportText = GetStatusChangeReportText(oldStatus, newStatus, comment);
                        AddServiceTaskReport(taskId, userId, reportText, connection, transaction);
                        
                        UpdateTaskLabelForMaterialRequest(taskId, newStatus, connection, transaction);
                        
                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        // Логирование изменения статуса заявки
        private void LogMaterialRequestStatusChange(int requestId, string oldStatus, string newStatus, int userId, string comment, SqlConnection connection, SqlTransaction transaction)
        {
            var command = new SqlCommand(@"
                INSERT INTO MaterialRequestStatusLog (RequestId, OldStatus, NewStatus, ChangedByUserId, ChangedAt, Comment)
                VALUES (@requestId, @oldStatus, @newStatus, @userId, @changedAt, @comment)", connection, transaction);
            command.Parameters.AddWithValue("@requestId", requestId);
            command.Parameters.AddWithValue("@oldStatus", (object)oldStatus ?? DBNull.Value);
            command.Parameters.AddWithValue("@newStatus", newStatus);
            command.Parameters.AddWithValue("@userId", userId);
            command.Parameters.AddWithValue("@changedAt", DateTime.UtcNow);
            command.Parameters.AddWithValue("@comment", (object)comment ?? DBNull.Value);
            command.ExecuteNonQuery();
        }

        // Создание служебной записи в TaskReports
        private void AddServiceTaskReport(int taskId, int userId, string text, SqlConnection connection, SqlTransaction transaction)
        {
            var command = new SqlCommand(@"
                INSERT INTO TaskReports (TaskId, ReportedByUserId, ReportedAt, ReportText, ProgressPercent)
                VALUES (@taskId, @userId, @reportedAt, @text, @progress)", connection, transaction);
            command.Parameters.AddWithValue("@taskId", taskId);
            command.Parameters.AddWithValue("@userId", userId);
            command.Parameters.AddWithValue("@reportedAt", DateTime.UtcNow);
            command.Parameters.AddWithValue("@text", text);
            command.Parameters.AddWithValue("@progress", DBNull.Value);
            command.ExecuteNonQuery();
        }

        // Получение текста для отчета при изменении статуса
        private string GetStatusChangeReportText(string oldStatus, string newStatus, string comment)
        {
            switch (newStatus)
            {
                case "Submitted":
                    return "Заявка отправлена на согласование";
                case "Approved":
                    return "Заявка согласована";
                case "Rejected":
                    return $"Заявка отклонена: {comment ?? "без указания причины"}";
                case "Issued":
                    return "Материалы выданы";
                case "Delivered":
                    return $"Доставлено: {comment ?? ""}";
                case "Closed":
                    return "Заявка закрыта";
                default:
                    return $"Статус заявки изменен: {newStatus}";
            }
        }

        private void UpdateTaskLabelForMaterialRequest(int taskId, string requestStatus, SqlConnection connection, System.Data.SqlClient.SqlTransaction transaction)
        {
            var labelCommand = new SqlCommand("SELECT LabelId FROM TaskLabels WHERE Code = 'await_mts'", connection, transaction);
            var labelIdObj = labelCommand.ExecuteScalar();
            
            if (labelIdObj == null || labelIdObj == DBNull.Value)
                return; 
            
            int labelId = Convert.ToInt32(labelIdObj);
            
            var updateCommand = new SqlCommand("UPDATE Tasks SET LabelId = @labelId WHERE TaskId = @taskId", connection, transaction);
            if (requestStatus == "Delivered" || requestStatus == "Closed")
            {
                updateCommand.Parameters.AddWithValue("@labelId", DBNull.Value);
            }
            else if (requestStatus == "Submitted" || requestStatus == "Approved" || requestStatus == "Issued")
            {
                updateCommand.Parameters.AddWithValue("@labelId", labelId);
            }
            else
            {
                return;
            }
            updateCommand.Parameters.AddWithValue("@taskId", taskId);
            updateCommand.ExecuteNonQuery();
        }

        public int? GetTaskLabelIdByCode(string code)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var command = new SqlCommand("SELECT LabelId FROM TaskLabels WHERE Code = @code", connection);
                command.Parameters.AddWithValue("@code", code);
                var result = command.ExecuteScalar();
                return result != null && result != DBNull.Value ? (int?)Convert.ToInt32(result) : null;
            }
        }

        public void AddMaterialDeliveryDoc(int requestId, string eventType, string docNumber = null, string note = null)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var command = new SqlCommand(@"
                    INSERT INTO MaterialDeliveryDocs (RequestId, Event, EventAt, DocNumber, Note)
                    VALUES (@requestId, @event, @eventAt, @docNumber, @note)", connection);
                command.Parameters.AddWithValue("@requestId", requestId);
                command.Parameters.AddWithValue("@event", eventType);
                command.Parameters.AddWithValue("@eventAt", DateTime.UtcNow);
                command.Parameters.AddWithValue("@docNumber", (object)docNumber ?? DBNull.Value);
                command.Parameters.AddWithValue("@note", (object)note ?? DBNull.Value);
                command.ExecuteNonQuery();
            }
        }
    }
}
