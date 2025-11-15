using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using praktik.Models;
using System.IO;

namespace praktik
{
    public partial class MainWindow : Window
    {
        private WorkPlannerContext db = new WorkPlannerContext();
        private Site selectedSite;
        private Crew selectedCrew;
        private Models.Task selectedTask;

        public MainWindow()
        {
            InitializeComponent();
            LoadData();
            SetupPermissions();
            UpdateUserInfo();
            LoadDashboard();
            // Привязываем обработчик выхода программно, чтобы избежать XAML-ошибок
            if (LogoutBtn != null)
            {
                LogoutBtn.Click += (s, e) => PerformLogout();
            }
        }

        private void UpdateUserInfo()
        {
            if (LoginWindow.CurrentUser != null)
            {
                UserInfo.Text = $"{LoginWindow.CurrentUser.Username} ({LoginWindow.CurrentUser.Role})";
            }
        }

        private void SetupPermissions()
        {
            string role = LoginWindow.CurrentUser?.Role;
            
            foreach (ListBoxItem item in NavigationListBox.Items)
            {
                item.Visibility = Visibility.Collapsed;
            } 
             SetHelpSectionsVisibility(false, false, false, false, false, false, false);
            
             switch (role)
            {
                case "Администратор":
                    // Администратор имеет полный доступ ко всем функциям
                    foreach (ListBoxItem item in NavigationListBox.Items)
                    {
                        item.Visibility = Visibility.Visible;
                    }
                     SetHelpSectionsVisibility(true, true, true, true, true, true, true);
                    break;
                    
                case "Диспетчер":
                     ((ListBoxItem)NavigationListBox.Items[0]).Visibility = Visibility.Visible; 
                    ((ListBoxItem)NavigationListBox.Items[1]).Visibility = Visibility.Visible;  
                    ((ListBoxItem)NavigationListBox.Items[2]).Visibility = Visibility.Visible;  
                    ((ListBoxItem)NavigationListBox.Items[3]).Visibility = Visibility.Visible;  
                    ((ListBoxItem)NavigationListBox.Items[4]).Visibility = Visibility.Visible;  
                    ((ListBoxItem)NavigationListBox.Items[5]).Visibility = Visibility.Visible;  
                    ((ListBoxItem)NavigationListBox.Items[6]).Visibility = Visibility.Visible;  
                    MaterialRequestsMenuItem.Visibility = Visibility.Visible;
                     SetHelpSectionsVisibility(true, true, true, true, true, true, false);
                    break;
                    
                case "Бригадир":
                    // Бригадир имеет доступ только к задачам и календарю
                    ((ListBoxItem)NavigationListBox.Items[3]).Visibility = Visibility.Visible;  
                    ((ListBoxItem)NavigationListBox.Items[4]).Visibility = Visibility.Visible;  
                    
                 btnAddSite.IsEnabled = false;
                btnUpdateSite.IsEnabled = false;
                btnDeleteSite.IsEnabled = false;
                btnAddCrew.IsEnabled = false;
                btnUpdateCrew.IsEnabled = false;
                btnDeleteCrew.IsEnabled = false;
                    btnAddTask.IsEnabled = false; // Бригадир не может создавать задачи
                    btnDeleteTask.IsEnabled = false; // Бригадир не может удалять задачи
                     SetHelpSectionsVisibility(false, false, false, true, true, false, false);
                    break;
            }
            
            // Автоматически выбираем первый доступный пункт меню
            foreach (ListBoxItem item in NavigationListBox.Items)
            {
                if (item.Visibility == Visibility.Visible)
                {
                    item.IsSelected = true;
                    break;
                }
            }
        }

        // Управление видимостью разделов справки по ролям
        private void SetHelpSectionsVisibility(
            bool home,
            bool sites,
            bool crews,
            bool tasks,
            bool calendar,
            bool reports,
            bool journal)
        {
            // null-условные операторы на случай изменений XAML
            HelpHomeExpander.Visibility = home ? Visibility.Visible : Visibility.Collapsed;
            HelpSitesExpander.Visibility = sites ? Visibility.Visible : Visibility.Collapsed;
            HelpCrewsExpander.Visibility = crews ? Visibility.Visible : Visibility.Collapsed;
            HelpTasksExpander.Visibility = tasks ? Visibility.Visible : Visibility.Collapsed;
            HelpCalendarExpander.Visibility = calendar ? Visibility.Visible : Visibility.Collapsed;
            HelpReportsExpander.Visibility = reports ? Visibility.Visible : Visibility.Collapsed;
            HelpJournalExpander.Visibility = journal ? Visibility.Visible : Visibility.Collapsed;
        }

        private void LoadData()
        {
            LoadSites();
            LoadCrews();
            LoadTasks();
            LoadTaskReports();
            LoadFilters();
        }

        private void LoadSites()
        {
            dgSites.ItemsSource = db.GetSites();
        }

        private void LoadTaskReports()
        {
            var reports = db.GetTaskReports();
            dgTaskReports.ItemsSource = reports;
            RecentReportsGrid.ItemsSource = reports.Take(5);
        }

        private void LoadDashboard()
        {
            var tasks = db.GetTasks();
            var crews = db.GetCrews();

            TotalTasksCount.Text = tasks.Count.ToString();
            CompletedTasksCount.Text = tasks.Count(t => t.TaskStatus?.TaskStatusName == "Завершено").ToString();
            OverdueTasksCount.Text = tasks.Count(t => t.EndDate < DateTime.Now && t.TaskStatus?.TaskStatusName != "Завершено").ToString();
            ActiveCrewsCount.Text = crews.Count.ToString();
        }

        // Навигация
        private void NavigationListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NavigationListBox.SelectedIndex < 0) return;

            // Скрываем все страницы
            DashboardContent.Visibility = Visibility.Collapsed;
            SitesContent.Visibility = Visibility.Collapsed;
            CrewsContent.Visibility = Visibility.Collapsed;
            TasksContent.Visibility = Visibility.Collapsed;
            CalendarContent.Visibility = Visibility.Collapsed;
            ReportsContent.Visibility = Visibility.Collapsed;
            JournalContent.Visibility = Visibility.Collapsed;

            // Показываем выбранную
            switch (NavigationListBox.SelectedIndex)
            {
                case 0:
                    DashboardContent.Visibility = Visibility.Visible;
                    PageTitle.Text = "Главная";
                    LoadDashboard();
                    break;
                case 1:
                    SitesContent.Visibility = Visibility.Visible;
                    PageTitle.Text = "Стройплощадки";
                    LoadSites();
                    break;
                case 2:
                    CrewsContent.Visibility = Visibility.Visible;
                    PageTitle.Text = "Бригады";
                    LoadCrews();
                    break;
                case 3:
                    TasksContent.Visibility = Visibility.Visible;
                    PageTitle.Text = "Задачи";
                    LoadTasks();
                    break;
                case 4:
                    CalendarContent.Visibility = Visibility.Visible;
                    PageTitle.Text = "Календарь";
                    LoadCalendar();
                    break;
                case 5:
                    ReportsContent.Visibility = Visibility.Visible;
                    PageTitle.Text = "Отчёты";
                    break;
                case 6:
                    JournalContent.Visibility = Visibility.Visible;
                    PageTitle.Text = "Журнал отчётов";
                    LoadTaskReports();
                    break;
                case 7:
                    // Открываем окно реестра заявок
                    var registryWindow = new MaterialRequestRegistryWindow();
                    registryWindow.ShowDialog();
                    // Сбрасываем выбор, чтобы не было проблем
                    NavigationListBox.SelectedIndex = -1;
                    break;
            }
        }

         private void dgSites_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            selectedSite = dgSites.SelectedItem as Models.Site;
            if (selectedSite != null)
            {
                txtSiteName.Text = selectedSite.SiteName;
                txtSiteAddress.Text = selectedSite.Address;
            }
        }

        private void btnAddSite_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSiteName.Text))
            {
                MessageBox.Show("Введите название стройплощадки");
                return;
            }

            var site = new Site
            {
                SiteName = txtSiteName.Text,
                Address = txtSiteAddress.Text
            };

            db.AddSite(site);
            LoadSites();
            ClearSiteFields();
        }

        private void btnUpdateSite_Click(object sender, RoutedEventArgs e)
        {
            if (selectedSite == null)
            {
                MessageBox.Show("Выберите стройплощадку для обновления");
                return;
            }

            selectedSite.SiteName = txtSiteName.Text;
            selectedSite.Address = txtSiteAddress.Text;

            db.UpdateSite(selectedSite);
            LoadSites();
        }

        private void btnDeleteSite_Click(object sender, RoutedEventArgs e)
        {
            if (selectedSite == null)
            {
                MessageBox.Show("Выберите стройплощадку для удаления");
                return;
            }

            if (MessageBox.Show("Удалить стройплощадку?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                db.DeleteSite(selectedSite.SiteId);
                LoadSites();
                ClearSiteFields();
            }
        }

        private void ClearSiteFields()
        {
            txtSiteName.Text = "";
            txtSiteAddress.Text = "";
            selectedSite = null;
        }

        // Меню
        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
        
        private void PerformLogout()
        {
            LoginWindow.CurrentUser = null;
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }
        
        private void btnCloseHelp_Click(object sender, RoutedEventArgs e)
        {
            HelpButton.IsChecked = false;
        }

        // Управление бригадами
        private void LoadCrews()
        {
            try
            {
                var crews = db.GetCrews();
                dgCrews.ItemsSource = crews;
                cbBrigadiers.ItemsSource = db.GetUsers().Where(u => u.Role == "Бригадир").ToList();
                
                // Убираем сообщение "Страница в разработке" если оно есть
                var children = CrewsContent.Children.OfType<UIElement>().ToList();
                foreach (var child in children)
                {
                    if (child is TextBlock tb && tb.Text == "Страница в разработке")
                    {
                        CrewsContent.Children.Remove(tb);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки бригад: {ex.Message}");
            }
        }

        private void dgCrews_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            selectedCrew = dgCrews.SelectedItem as Models.Crew;
            if (selectedCrew != null)
            {
                txtCrewName.Text = selectedCrew.CrewName;
                cbBrigadiers.SelectedItem = selectedCrew.Brigadier;
            }
        }

        private void btnAddCrew_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtCrewName.Text) || cbBrigadiers.SelectedItem == null)
            {
                MessageBox.Show("Введите название бригады и выберите бригадира");
                return;
            }

            var crew = new Crew
            {
                CrewName = txtCrewName.Text,
                BrigadierId = (cbBrigadiers.SelectedItem as User).UserId
            };

            db.AddCrew(crew);
            LoadCrews();
            ClearCrewFields();
        }

        private void btnUpdateCrew_Click(object sender, RoutedEventArgs e)
        {
            try
        {
            if (selectedCrew == null)
            {
                MessageBox.Show("Выберите бригаду для обновления");
                return;
            }
                
                if (string.IsNullOrWhiteSpace(txtCrewName.Text))
                {
                    MessageBox.Show("Введите название бригады");
                    return;
                }
                
                if (cbBrigadiers.SelectedItem == null)
                {
                    MessageBox.Show("Выберите бригадира");
                    return;
                }
                
                var brigadier = cbBrigadiers.SelectedItem as User;
                if (brigadier == null)
                {
                    MessageBox.Show("Ошибка при получении данных бригадира");
                return;
            }

            selectedCrew.CrewName = txtCrewName.Text;
                selectedCrew.BrigadierId = brigadier.UserId;

            db.UpdateCrew(selectedCrew);
            LoadCrews();
                MessageBox.Show("Бригада успешно обновлена");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении бригады: {ex.Message}");
            }
        }

        private void btnDeleteCrew_Click(object sender, RoutedEventArgs e)
        {
            if (selectedCrew == null)
            {
                MessageBox.Show("Выберите бригаду для удаления");
                return;
            }

            if (MessageBox.Show("Удалить бригаду?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                db.DeleteCrew(selectedCrew.CrewId);
                LoadCrews();
                ClearCrewFields();
            }
        }

        private void ClearCrewFields()
        {
            txtCrewName.Text = "";
            cbBrigadiers.SelectedItem = null;
            selectedCrew = null;
        }

        // Управление задачами
        private void LoadTasks()
        {
            try
            {
                var tasks = db.GetTasks();
                dgTasks.ItemsSource = tasks;
                
                // Убираем сообщение "Страница в разработке" если оно есть
                var children = TasksContent.Children.OfType<UIElement>().ToList();
                foreach (var child in children)
                {
                    if (child is TextBlock tb && tb.Text == "Страница в разработке")
                    {
                        TasksContent.Children.Remove(tb);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки задач: {ex.Message}");
            }
        }

        private void LoadFilters()
        {
            cbSiteFilter.ItemsSource = db.GetSites();
            cbCrewFilter.ItemsSource = db.GetCrews();
            cbCalendarSiteFilter.ItemsSource = db.GetSites();
            cbCalendarCrewFilter.ItemsSource = db.GetCrews();
        }

        private void dgTasks_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            selectedTask = dgTasks.SelectedItem as Models.Task;
        }

        private void btnFilterTasks_Click(object sender, RoutedEventArgs e)
        {
            try
        {
            var tasks = db.GetTasks();

            if (cbSiteFilter.SelectedItem != null)
            {
                var siteId = (cbSiteFilter.SelectedItem as Site).SiteId;
                tasks = tasks.Where(t => t.SiteId == siteId).ToList();
            }

            if (cbCrewFilter.SelectedItem != null)
            {
                var crewId = (cbCrewFilter.SelectedItem as Crew).CrewId;
                tasks = tasks.Where(t => t.CrewId == crewId).ToList();
            }

            dgTasks.ItemsSource = tasks;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при применении фильтра: {ex.Message}");
            }
        }
        
        private void btnResetTaskFilter_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                cbSiteFilter.SelectedItem = null;
                cbCrewFilter.SelectedItem = null;
                dgTasks.ItemsSource = db.GetTasks();
                MessageBox.Show("Фильтры сброшены");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сбросе фильтров: {ex.Message}");
            }
        }

        private void btnAddTask_Click(object sender, RoutedEventArgs e)
        {
            var taskWindow = new TaskWindow(null);
            if (taskWindow.ShowDialog() == true)
            {
                LoadTasks();
            }
        }

        private void btnUpdateTask_Click(object sender, RoutedEventArgs e)
        {
            if (selectedTask == null)
            {
                MessageBox.Show("Выберите задачу для обновления");
                return;
            }

            var taskWindow = new TaskWindow(selectedTask);
            if (taskWindow.ShowDialog() == true)
            {
                LoadTasks();
            }
        }

        private void btnDeleteTask_Click(object sender, RoutedEventArgs e)
        {
            if (selectedTask == null)
            {
                MessageBox.Show("Выберите задачу для удаления");
                return;
            }

            if (MessageBox.Show("Удалить задачу?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                db.DeleteTask(selectedTask.TaskId);
                LoadTasks();
            }
        }

        private void btnUpdateStatus_Click(object sender, RoutedEventArgs e)
        {
            if (selectedTask == null)
            {
                MessageBox.Show("Выберите задачу для изменения статуса");
                return;
            }

            var statusWindow = new TaskStatusWindow(selectedTask);
            if (statusWindow.ShowDialog() == true)
            {
                LoadTasks();
            }
        }

        // Календарь
        private void LoadCalendar()
        {
            try
            {
                TaskCalendar.SelectedDate = DateTime.Today;
                LoadTasksForDate(DateTime.Today);
                
                // Убираем сообщение "Страница в разработке" если оно есть
                var children = CalendarContent.Children.OfType<UIElement>().ToList();
                foreach (var child in children)
                {
                    if (child is TextBlock tb && tb.Text == "Страница в разработке")
                    {
                        CalendarContent.Children.Remove(tb);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки календаря: {ex.Message}");
            }
        }

        private void TaskCalendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TaskCalendar.SelectedDate.HasValue)
            {
                LoadTasksForDate(TaskCalendar.SelectedDate.Value);
            }
        }

        private void CalendarFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (TaskCalendar.SelectedDate.HasValue)
            {
                LoadTasksForDate(TaskCalendar.SelectedDate.Value);
            }
        }

        private void btnResetCalendarFilters_Click(object sender, RoutedEventArgs e)
        {
            cbCalendarSiteFilter.SelectedItem = null;
            cbCalendarCrewFilter.SelectedItem = null;
            if (TaskCalendar.SelectedDate.HasValue)
            {
                LoadTasksForDate(TaskCalendar.SelectedDate.Value);
            }
        }

        private void LoadTasksForDate(DateTime date)
        {
            SelectedDateText.Text = $"Задачи на {date:dd.MM.yyyy}";
            
            var tasks = db.GetTasks().Where(t => 
                t.StartDate <= date && t.EndDate >= date).ToList();

             if (cbCalendarSiteFilter.SelectedItem != null)
            {
                var siteId = (cbCalendarSiteFilter.SelectedItem as Site).SiteId;
                tasks = tasks.Where(t => t.SiteId == siteId).ToList();
            }

            if (cbCalendarCrewFilter.SelectedItem != null)
            {
                var crewId = (cbCalendarCrewFilter.SelectedItem as Crew).CrewId;
                tasks = tasks.Where(t => t.CrewId == crewId).ToList();
            }

            dgCalendarTasks.ItemsSource = tasks;
        }

        // Отчёты
        private void btnTasksBySite_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Устанавливаем текущий тип отчета
                UpdateCurrentReportType("tasks_by_site");
                
            var tasks = db.GetTasks();
            var report = tasks
                .GroupBy(t => t.Site.SiteName)
                .Select(g => new
                {
                    Объект = g.Key,
                    Всего_задач = g.Count(),
                        Выполнено = g.Count(t => t.TaskStatus.TaskStatusName == "Завершено"),
                        В_работе = g.Count(t => t.TaskStatus.TaskStatusName == "В работе"),
                        Просрочено = g.Count(t => t.TaskStatus.TaskStatusName == "Просрочено")
                })
                .ToList();

                ReportTitle.Text = "Отчёт: Задачи по объектам";
            dgReports.ItemsSource = report;
                
                // Убираем сообщение "Страница в разработке" если оно есть
                var children = ReportsContent.Children.OfType<UIElement>().ToList();
                foreach (var child in children)
                {
                    if (child is TextBlock tb && tb.Text == "Страница в разработке")
                    {
                        ReportsContent.Children.Remove(tb);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка формирования отчёта: {ex.Message}");
            }
        }

        private void btnTasksByCrew_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Устанавливаем текущий тип отчета
                UpdateCurrentReportType("tasks_by_crew");
                
            var tasks = db.GetTasks();
            var report = tasks
                .Where(t => t.CrewId != null)
                .GroupBy(t => t.Crew.CrewName)
                .Select(g => new
                {
                    Бригада = g.Key,
                    Всего_задач = g.Count(),
                        Выполнено = g.Count(t => t.TaskStatus.TaskStatusName == "Завершено"),
                        В_работе = g.Count(t => t.TaskStatus.TaskStatusName == "В работе"),
                        Просрочено = g.Count(t => t.TaskStatus.TaskStatusName == "Просрочено")
                })
                .ToList();

                ReportTitle.Text = "Отчёт: Выполнение по бригадам";
            dgReports.ItemsSource = report;
                
                // Убираем сообщение "Страница в разработке" если оно есть
                var children = ReportsContent.Children.OfType<UIElement>().ToList();
                foreach (var child in children)
                {
                    if (child is TextBlock tb && tb.Text == "Страница в разработке")
                    {
                        ReportsContent.Children.Remove(tb);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка формирования отчёта: {ex.Message}");
            }
        }

        private void btnOverdueTasks_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Устанавливаем текущий тип отчета
                UpdateCurrentReportType("overdue_tasks");
                
            var tasks = db.GetTasks();
            var overdueTasks = tasks
                    .Where(t => t.EndDate < DateTime.Now && t.TaskStatus.TaskStatusName != "Завершено")
                .Select(t => new
                {
                    Название = t.Title,
                    Объект = t.Site.SiteName,
                    Бригада = t.Crew != null ? t.Crew.CrewName : "Не назначена",
                    Приоритет = t.Priority.PriorityName,
                    Дата_окончания = t.EndDate,
                    Просрочено_на_дней = (DateTime.Now - t.EndDate).Days
                })
                .ToList();

                ReportTitle.Text = "Отчёт: Просроченные задачи";
            dgReports.ItemsSource = overdueTasks;
                
                // Убираем сообщение "Страница в разработке" если оно есть
                var children = ReportsContent.Children.OfType<UIElement>().ToList();
                foreach (var child in children)
                {
                    if (child is TextBlock tb && tb.Text == "Страница в разработке")
                    {
                        ReportsContent.Children.Remove(tb);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка формирования отчёта: {ex.Message}");
            }
        }

        // Получение пути к папке отчетов
        private string GetReportsFolderPath()
        {
            string folderPath = @"C:\Users\User\Desktop\";
            
            try
            {
                // Проверяем, существует ли папка
                if (!Directory.Exists(folderPath))
                {
                    // Если не существует, используем рабочий стол
                    folderPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                }
                
                return folderPath;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при доступе к папке для отчетов: {ex.Message}\nОтчеты будут сохранены на рабочий стол.");
                return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            }
        }
        
        // Переменная для хранения текущего типа отчета
        private string currentReportType = "tasks";

        // Метод для обновления текущего типа отчета
        private void UpdateCurrentReportType(string reportType)
        {
            currentReportType = reportType;
        }

         private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var lines = new List<string>();
                string reportName = "tasks";
                
                // Определение, какой отчет сейчас отображается
                switch (currentReportType)
                {
                    case "tasks":
                        var tasks = db.GetTasks();
                        lines.Add("Название;Объект;Бригада;Приоритет;Статус;Начало;Окончание");
                        foreach (var t in tasks)
                        {
                            string crew = t.Crew != null ? t.Crew.CrewName : "";
                            lines.Add(string.Join(";", new[]
                            {
                                t.Title,
                                t.Site?.SiteName ?? "",
                                crew,
                                t.Priority?.PriorityName ?? "",
                                t.TaskStatus?.TaskStatusName ?? "",
                                t.StartDate.ToString("dd.MM.yyyy"),
                                t.EndDate.ToString("dd.MM.yyyy")
                            }));
                        }
                        reportName = "tasks";
                        break;
                        
                    case "tasks_by_site":
                        var tasksBySite = db.GetTasks()
                            .GroupBy(t => t.Site.SiteName)
                            .Select(g => new
                            {
                                Объект = g.Key,
                                Всего_задач = g.Count(),
                                Выполнено = g.Count(t => t.TaskStatus.TaskStatusName == "Завершено"),
                                В_работе = g.Count(t => t.TaskStatus.TaskStatusName == "В работе"),
                                Просрочено = g.Count(t => t.TaskStatus.TaskStatusName == "Просрочено")
                            })
                            .ToList();
                            
                        lines.Add("Объект;Всего задач;Выполнено;В работе;Просрочено");
                        foreach (var item in tasksBySite)
                        {
                            lines.Add(string.Join(";", new[]
                            {
                                item.Объект,
                                item.Всего_задач.ToString(),
                                item.Выполнено.ToString(),
                                item.В_работе.ToString(),
                                item.Просрочено.ToString()
                            }));
                        }
                        reportName = "tasks_by_site";
                        break;
                        
                    case "tasks_by_crew":
                        var tasksByCrew = db.GetTasks()
                            .Where(t => t.CrewId != null)
                            .GroupBy(t => t.Crew.CrewName)
                            .Select(g => new
                            {
                                Бригада = g.Key,
                                Всего_задач = g.Count(),
                                Выполнено = g.Count(t => t.TaskStatus.TaskStatusName == "Завершено"),
                                В_работе = g.Count(t => t.TaskStatus.TaskStatusName == "В работе"),
                                Просрочено = g.Count(t => t.TaskStatus.TaskStatusName == "Просрочено")
                            })
                            .ToList();
                            
                        lines.Add("Бригада;Всего задач;Выполнено;В работе;Просрочено");
                        foreach (var item in tasksByCrew)
                        {
                            lines.Add(string.Join(";", new[]
                            {
                                item.Бригада,
                                item.Всего_задач.ToString(),
                                item.Выполнено.ToString(),
                                item.В_работе.ToString(),
                                item.Просрочено.ToString()
                            }));
                        }
                        reportName = "tasks_by_crew";
                        break;
                        
                    case "overdue_tasks":
                        var overdueTasks = db.GetTasks()
                            .Where(t => t.EndDate < DateTime.Now && t.TaskStatus.TaskStatusName != "Завершено")
                            .Select(t => new
                            {
                                Название = t.Title,
                                Объект = t.Site.SiteName,
                                Бригада = t.Crew != null ? t.Crew.CrewName : "Не назначена",
                                Приоритет = t.Priority.PriorityName,
                                Дата_окончания = t.EndDate,
                                Просрочено_на_дней = (DateTime.Now - t.EndDate).Days
                            })
                            .ToList();
                            
                        lines.Add("Название;Объект;Бригада;Приоритет;Дата окончания;Просрочено на дней");
                        foreach (var item in overdueTasks)
                        {
                            lines.Add(string.Join(";", new[]
                            {
                                item.Название,
                                item.Объект,
                                item.Бригада,
                                item.Приоритет,
                                item.Дата_окончания.ToString("dd.MM.yyyy"),
                                item.Просрочено_на_дней.ToString()
                            }));
                        }
                        reportName = "overdue_tasks";
                        break;
                }
                
                string folderPath = GetReportsFolderPath();
                string fileName = $"{reportName}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                string path = Path.Combine(folderPath, fileName);
                
                File.WriteAllLines(path, lines, System.Text.Encoding.UTF8);
                MessageBox.Show($"CSV сохранён: {path}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта CSV: {ex.Message}");
            }
        }

        private void ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var lines = new List<string>();
                string reportName = "tasks";
                
                // Определяем, какой отчет сейчас отображается
                switch (currentReportType)
                {
                    case "tasks":
                        var tasks = db.GetTasks();
                        lines.Add("Название\tОбъект\tБригада\tПриоритет\tСтатус\tНачало\tОкончание");
                        foreach (var t in tasks)
                        {
                            string crew = t.Crew != null ? t.Crew.CrewName : "";
                            lines.Add(string.Join("\t", new[]
                            {
                                t.Title,
                                t.Site?.SiteName ?? "",
                                crew,
                                t.Priority?.PriorityName ?? "",
                                t.TaskStatus?.TaskStatusName ?? "",
                                t.StartDate.ToString("dd.MM.yyyy"),
                                t.EndDate.ToString("dd.MM.yyyy")
                            }));
                        }
                        reportName = "tasks";
                        break;
                        
                    case "tasks_by_site":
                        var tasksBySite = db.GetTasks()
                            .GroupBy(t => t.Site.SiteName)
                            .Select(g => new
                            {
                                Объект = g.Key,
                                Всего_задач = g.Count(),
                                Выполнено = g.Count(t => t.TaskStatus.TaskStatusName == "Завершено"),
                                В_работе = g.Count(t => t.TaskStatus.TaskStatusName == "В работе"),
                                Просрочено = g.Count(t => t.TaskStatus.TaskStatusName == "Просрочено")
                            })
                            .ToList();
                            
                        lines.Add("Объект\tВсего задач\tВыполнено\tВ работе\tПросрочено");
                        foreach (var item in tasksBySite)
                        {
                            lines.Add(string.Join("\t", new[]
                            {
                                item.Объект,
                                item.Всего_задач.ToString(),
                                item.Выполнено.ToString(),
                                item.В_работе.ToString(),
                                item.Просрочено.ToString()
                            }));
                        }
                        reportName = "tasks_by_site";
                        break;
                        
                    case "tasks_by_crew":
                        var tasksByCrew = db.GetTasks()
                            .Where(t => t.CrewId != null)
                            .GroupBy(t => t.Crew.CrewName)
                            .Select(g => new
                            {
                                Бригада = g.Key,
                                Всего_задач = g.Count(),
                                Выполнено = g.Count(t => t.TaskStatus.TaskStatusName == "Завершено"),
                                В_работе = g.Count(t => t.TaskStatus.TaskStatusName == "В работе"),
                                Просрочено = g.Count(t => t.TaskStatus.TaskStatusName == "Просрочено")
                            })
                            .ToList();
                            
                        lines.Add("Бригада\tВсего задач\tВыполнено\tВ работе\tПросрочено");
                        foreach (var item in tasksByCrew)
                        {
                            lines.Add(string.Join("\t", new[]
                            {
                                item.Бригада,
                                item.Всего_задач.ToString(),
                                item.Выполнено.ToString(),
                                item.В_работе.ToString(),
                                item.Просрочено.ToString()
                            }));
                        }
                        reportName = "tasks_by_crew";
                        break;
                        
                    case "overdue_tasks":
                        var overdueTasks = db.GetTasks()
                            .Where(t => t.EndDate < DateTime.Now && t.TaskStatus.TaskStatusName != "Завершено")
                            .Select(t => new
                            {
                                Название = t.Title,
                                Объект = t.Site.SiteName,
                                Бригада = t.Crew != null ? t.Crew.CrewName : "Не назначена",
                                Приоритет = t.Priority.PriorityName,
                                Дата_окончания = t.EndDate,
                                Просрочено_на_дней = (DateTime.Now - t.EndDate).Days
                            })
                            .ToList();
                            
                        lines.Add("Название\tОбъект\tБригада\tПриоритет\tДата окончания\tПросрочено на дней");
                        foreach (var item in overdueTasks)
                        {
                            lines.Add(string.Join("\t", new[]
                            {
                                item.Название,
                                item.Объект,
                                item.Бригада,
                                item.Приоритет,
                                item.Дата_окончания.ToString("dd.MM.yyyy"),
                                item.Просрочено_на_дней.ToString()
                            }));
                        }
                        reportName = "overdue_tasks";
                        break;
                }
                
                string folderPath = GetReportsFolderPath();
                string fileName = $"{reportName}_{DateTime.Now:yyyyMMdd_HHmmss}.xls";
                string path = Path.Combine(folderPath, fileName);
                
                File.WriteAllLines(path, lines, System.Text.Encoding.UTF8);
                MessageBox.Show($"Excel (совместимый) сохранён: {path}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта Excel: {ex.Message}");
            }
        }

        private void ExportPdf_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var lines = new List<string>();
                string reportName = "tasks";
                string reportTitle = "ОТЧЁТ ПО ЗАДАЧАМ";
                
                // Определяем, какой отчет сейчас отображается
                switch (currentReportType)
                {
                    case "tasks":
                        var tasks = db.GetTasks();
                        reportTitle = "ОТЧЁТ ПО ЗАДАЧАМ";
                        lines.Add($"=== {reportTitle} ===\n");
                        lines.Add($"Дата формирования: {DateTime.Now:dd.MM.yyyy HH:mm}\n");
                        foreach (var t in tasks)
                        {
                            lines.Add($"• {t.Title} | {t.Site?.SiteName} | {t.Crew?.CrewName ?? "Без бригады"} | {t.Priority?.PriorityName} | {t.TaskStatus?.TaskStatusName} | {t.StartDate:dd.MM.yyyy}-{t.EndDate:dd.MM.yyyy}");
                        }
                        reportName = "tasks";
                        break;
                        
                    case "tasks_by_site":
                        var tasksBySite = db.GetTasks()
                            .GroupBy(t => t.Site.SiteName)
                            .Select(g => new
                            {
                                Объект = g.Key,
                                Всего_задач = g.Count(),
                                Выполнено = g.Count(t => t.TaskStatus.TaskStatusName == "Завершено"),
                                В_работе = g.Count(t => t.TaskStatus.TaskStatusName == "В работе"),
                                Просрочено = g.Count(t => t.TaskStatus.TaskStatusName == "Просрочено")
                            })
                            .ToList();
                            
                        reportTitle = "ОТЧЁТ: ЗАДАЧИ ПО ОБЪЕКТАМ";
                        lines.Add($"=== {reportTitle} ===\n");
                        lines.Add($"Дата формирования: {DateTime.Now:dd.MM.yyyy HH:mm}\n");
                        foreach (var item in tasksBySite)
                        {
                            lines.Add($"• Объект: {item.Объект}");
                            lines.Add($"  Всего задач: {item.Всего_задач}");
                            lines.Add($"  Выполнено: {item.Выполнено}");
                            lines.Add($"  В работе: {item.В_работе}");
                            lines.Add($"  Просрочено: {item.Просрочено}\n");
                        }
                        reportName = "tasks_by_site";
                        break;
                        
                    case "tasks_by_crew":
                        var tasksByCrew = db.GetTasks()
                            .Where(t => t.CrewId != null)
                            .GroupBy(t => t.Crew.CrewName)
                            .Select(g => new
                            {
                                Бригада = g.Key,
                                Всего_задач = g.Count(),
                                Выполнено = g.Count(t => t.TaskStatus.TaskStatusName == "Завершено"),
                                В_работе = g.Count(t => t.TaskStatus.TaskStatusName == "В работе"),
                                Просрочено = g.Count(t => t.TaskStatus.TaskStatusName == "Просрочено")
                            })
                            .ToList();
                            
                        reportTitle = "ОТЧЁТ: ВЫПОЛНЕНИЕ ПО БРИГАДАМ";
                        lines.Add($"=== {reportTitle} ===\n");
                        lines.Add($"Дата формирования: {DateTime.Now:dd.MM.yyyy HH:mm}\n");
                        foreach (var item in tasksByCrew)
                        {
                            lines.Add($"• Бригада: {item.Бригада}");
                            lines.Add($"  Всего задач: {item.Всего_задач}");
                            lines.Add($"  Выполнено: {item.Выполнено}");
                            lines.Add($"  В работе: {item.В_работе}");
                            lines.Add($"  Просрочено: {item.Просрочено}\n");
                        }
                        reportName = "tasks_by_crew";
                        break;
                        
                    case "overdue_tasks":
                        var overdueTasks = db.GetTasks()
                            .Where(t => t.EndDate < DateTime.Now && t.TaskStatus.TaskStatusName != "Завершено")
                            .Select(t => new
                            {
                                Название = t.Title,
                                Объект = t.Site.SiteName,
                                Бригада = t.Crew != null ? t.Crew.CrewName : "Не назначена",
                                Приоритет = t.Priority.PriorityName,
                                Дата_окончания = t.EndDate,
                                Просрочено_на_дней = (DateTime.Now - t.EndDate).Days
                            })
                            .ToList();
                            
                        reportTitle = "ОТЧЁТ: ПРОСРОЧЕННЫЕ ЗАДАЧИ";
                        lines.Add($"=== {reportTitle} ===\n");
                        lines.Add($"Дата формирования: {DateTime.Now:dd.MM.yyyy HH:mm}\n");
                        foreach (var item in overdueTasks)
                        {
                            lines.Add($"• {item.Название}");
                            lines.Add($"  Объект: {item.Объект}");
                            lines.Add($"  Бригада: {item.Бригада}");
                            lines.Add($"  Приоритет: {item.Приоритет}");
                            lines.Add($"  Дата окончания: {item.Дата_окончания:dd.MM.yyyy}");
                            lines.Add($"  Просрочено на дней: {item.Просрочено_на_дней}\n");
                        }
                        reportName = "overdue_tasks";
                        break;
                }
                
                string folderPath = GetReportsFolderPath();
                string fileName = $"{reportName}_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                string path = Path.Combine(folderPath, fileName);
                
                File.WriteAllLines(path, lines, System.Text.Encoding.UTF8);
                MessageBox.Show($"Отчёт сохранён: {path}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта: {ex.Message}");
            }
        }
    }
}