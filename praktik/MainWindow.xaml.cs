using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using praktik.Models;
using praktik.Models.Patterns;
using praktik.Models.Patterns.States;
using System.IO;

namespace praktik
{
    /// <summary>
    /// Главное окно приложения.
    /// Управляет отображением задач, навигацией и взаимодействием ролей пользователей.
    /// </summary>
    public partial class MainWindow : UserControl
    {
        private readonly WorkPlannerFacade facade;
        private string roleOverride;
        private Site selectedSite;
        private Crew selectedCrew;
        private Models.Task selectedTask;
        private List<Models.Task> allTasks = new List<Models.Task>();
        private bool isUpdatingTaskFilters;
        private System.Collections.ObjectModel.ObservableCollection<MaterialRequestRegistryDisplay> materialRequests;
        private MaterialRequest selectedMaterialRequest;

        public MainWindow()
        {
            InitializeComponent();
            facade = new WorkPlannerFacade();
            LoadData();
            SetupPermissions();
            UpdateUserInfo();
            LoadDashboard();
            if (LogoutBtn != null)
            {
                LogoutBtn.Click += (s, e) => PerformLogout();
            }

            if (App.TaskIdFromQR.HasValue)
            {
                this.Loaded += (s, e) => 
                {
                    var taskId = App.TaskIdFromQR.Value;
                    App.TaskIdFromQR = null;
                    OpenTaskFromQR(taskId);
                };
            }
        }

        private void OpenTaskFromQR(int taskId)
        {
            try
            {
                var taskViewWindow = new TaskViewWindow(taskId);
                taskViewWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии задачи: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnScanQR_Click(object sender, RoutedEventArgs e)
        {
            var qrWindow = new QRCodeScannerWindow();
            if (qrWindow.ShowDialog() == true && qrWindow.TaskId.HasValue)
            {
                ((ListBoxItem)NavigationListBox.Items[3]).IsSelected = true;
                HighlightTask(qrWindow.TaskId.Value);
            }
        }

        private void HighlightTask(int taskId)
        {
            try
            {
                if (dgTasks.ItemsSource is System.Collections.IList tasks)
                {
                    var task = tasks.Cast<Models.Task>().FirstOrDefault(t => t.TaskId == taskId);
                    if (task != null)
                    {
                        dgTasks.SelectedItem = task;
                        dgTasks.ScrollIntoView(task);
                        dgTasks.Focus();
                    }
                    else
                    {
                        MessageBox.Show("Задача не найдена в списке. Возможно, нужно сбросить фильтры.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при поиске задачи: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DgTasks_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (selectedTask != null)
            {
                var qrWindow = new TaskQRCodeWindow(selectedTask.TaskId);
                qrWindow.ShowDialog();
            }
        }

        private void UpdateUserInfo()
        {
            if (LoginWindow.CurrentUser != null)
            {
                var role = roleOverride ?? LoginWindow.CurrentUser.Role;
                UserInfo.Text = $"{LoginWindow.CurrentUser.Username} ({role})";
            }
        }

        private string GetCurrentRoleNormalized()
        {
            var role = roleOverride;
            if (string.IsNullOrWhiteSpace(role))
            {
                role = LoginWindow.CurrentUser?.Role;
            }

            return (role ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static bool IsAdminRole(string normalizedRole)
        {
            return normalizedRole == "администратор"
                || normalizedRole == "админ"
                || normalizedRole == "admin"
                || normalizedRole == "administrator";
        }

        private static bool IsDispatcherRole(string normalizedRole)
        {
            return normalizedRole == "диспетчер"
                || normalizedRole == "dispatcher";
        }

        private static bool CanManageCrewMembersRole(string normalizedRole)
        {
            return IsAdminRole(normalizedRole) || IsDispatcherRole(normalizedRole);
        }

        private void SetupPermissions()
        {
            string role = GetCurrentRoleNormalized();

            btnDuplicateTask.Visibility = Visibility.Collapsed;
            btnCrewMembers.IsEnabled = false;
            btnCrewMembers.Visibility = Visibility.Collapsed;
            
            foreach (ListBoxItem item in NavigationListBox.Items)
            {
                item.Visibility = Visibility.Collapsed;
            } 
             SetHelpSectionsVisibility(false, false, false, false, false, false, false);
            
             switch (role)
            {
                case "администратор":
                case "админ":
                case "admin":
                case "administrator":
                    btnDuplicateTask.Visibility = Visibility.Visible;
                    btnCrewMembers.IsEnabled = true;
                    btnCrewMembers.Visibility = Visibility.Visible;
                    foreach (ListBoxItem item in NavigationListBox.Items)
                    {
                        item.Visibility = Visibility.Visible;
                    }
                     SetHelpSectionsVisibility(true, true, true, true, true, true, true);
                    break;
                    
                case "диспетчер":
                case "dispatcher":
                    btnDuplicateTask.Visibility = Visibility.Visible;
                    btnCrewMembers.IsEnabled = true;
                    btnCrewMembers.Visibility = Visibility.Visible;
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
                    
                case "бригадир":
                case "foreman":
                    ((ListBoxItem)NavigationListBox.Items[3]).Visibility = Visibility.Visible;  
                    ((ListBoxItem)NavigationListBox.Items[4]).Visibility = Visibility.Visible;  
                    
                 btnAddSite.IsEnabled = false;
                btnUpdateSite.IsEnabled = false;
                btnDeleteSite.IsEnabled = false;
                btnAddCrew.IsEnabled = false;
                btnUpdateCrew.IsEnabled = false;
                btnDeleteCrew.IsEnabled = false;
                    btnCrewMembers.IsEnabled = false;
                    btnCrewMembers.Visibility = Visibility.Collapsed;
                    btnAddTask.IsEnabled = false;
                    btnDeleteTask.IsEnabled = false;
                    btnDuplicateTask.Visibility = Visibility.Collapsed;
                     SetHelpSectionsVisibility(false, false, false, true, true, false, false);
                    break;
            }
            
            foreach (ListBoxItem item in NavigationListBox.Items)
            {
                if (item.Visibility == Visibility.Visible)
                {
                    item.IsSelected = true;
                    break;
                }
            }
        }

        public void ApplyRole(string role)
        {
            roleOverride = role;
            SetupPermissions();
            UpdateUserInfo();
        }

        private void SetHelpSectionsVisibility(
            bool home,
            bool sites,
            bool crews,
            bool tasks,
            bool calendar,
            bool reports,
            bool journal)
        {
            HelpHomeExpander.Visibility = home ? Visibility.Visible : Visibility.Collapsed;
            HelpSitesExpander.Visibility = sites ? Visibility.Visible : Visibility.Collapsed;
            HelpCrewsExpander.Visibility = crews ? Visibility.Visible : Visibility.Collapsed;
            HelpTasksExpander.Visibility = tasks ? Visibility.Visible : Visibility.Collapsed;
            HelpCalendarExpander.Visibility = calendar ? Visibility.Visible : Visibility.Collapsed;
            HelpReportsExpander.Visibility = reports ? Visibility.Visible : Visibility.Collapsed;
            HelpJournalExpander.Visibility = journal ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Загружает все необходимые данные для отображения (задачи, площадки, бригады и отчеты).
        /// </summary>
        private void LoadData()
        {
            CheckOverdueTasks();
            LoadSites();
            LoadCrews();
            LoadTasks();
            LoadTaskReports();
            LoadFilters();
        }

        /// <summary>
        /// Проверяет задачи на просрочено.
        /// Если дата окончания задачи прошла, её статус автоматически меняется на "Просрочено".
        /// </summary>
        private void CheckOverdueTasks()
        {
            try
            {
                var tasks = facade.GetTasks();
                var overdueStatus = facade.GetTaskStatuses().FirstOrDefault(s => s.TaskStatusName == "Просрочено");

                if (overdueStatus == null) return;

                bool hasChanges = false;
                foreach (var task in tasks)
                {
                    if (task.TaskStatus?.TaskStatusName != "Завершено" && 
                        task.TaskStatus?.TaskStatusName != "Просрочено" &&
                        task.EndDate < DateTime.Now)
                    {
                        task.TaskStatusId = overdueStatus.TaskStatusId;
                        facade.UpdateTask(task);
                        hasChanges = true;
                    }
                }

                if (hasChanges)
                {
                    // No explicit save needed if methods save immediately, but if EF context is shared it might be needed.
                    // Assuming facade.UpdateTask handles saving.
                }
            }
            catch (Exception ex)
            {
                // Silently fail or log, to not disrupt startup
                Console.WriteLine($"Ошибка обновления статусов задач: {ex.Message}");
            }
        }

        private void LoadSites()
        {
            dgSites.ItemsSource = facade.GetSites();
        }

        private void LoadTaskReports()
        {
            var reports = facade.GetTaskReports();
            dgTaskReports.ItemsSource = reports;
            RecentReportsGrid.ItemsSource = reports.Take(5);
        }

        private void LoadDashboard()
        {
            var tasks = facade.GetTasks();
            var crews = facade.GetCrews();

            TotalTasksCount.Text = tasks.Count.ToString();
            CompletedTasksCount.Text = tasks.Count(t => t.TaskStatus?.TaskStatusName == "Завершено").ToString();
            OverdueTasksCount.Text = tasks.Count(t => t.EndDate < DateTime.Now && t.TaskStatus?.TaskStatusName != "Завершено").ToString();
            ActiveCrewsCount.Text = crews.Count.ToString();
        }

        private void NavigationListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NavigationListBox.SelectedIndex < 0) return;

            DashboardContent.Visibility = Visibility.Collapsed;
            SitesContent.Visibility = Visibility.Collapsed;
            CrewsContent.Visibility = Visibility.Collapsed;
            TasksContent.Visibility = Visibility.Collapsed;
            CalendarContent.Visibility = Visibility.Collapsed;
            ReportsContent.Visibility = Visibility.Collapsed;
            JournalContent.Visibility = Visibility.Collapsed;
            MaterialRequestsContent.Visibility = Visibility.Collapsed;

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
                    MaterialRequestsContent.Visibility = Visibility.Visible;
                    PageTitle.Text = "Заявки на материалы";
                    LoadMaterialRequests();
                    break;
            }
        }

         private void DgSites_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgSites.SelectedItem is Models.Site site)
            {
                selectedSite = site;
                txtSiteName.Text = site.SiteName;
                txtSiteAddress.Text = site.Address;
            }
            else
            {
                selectedSite = null;
            }
        }

        private void BtnAddSite_Click(object sender, RoutedEventArgs e)
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

            facade.AddSite(site);
            LoadSites();
            ClearSiteFields();
        }

        private void BtnUpdateSite_Click(object sender, RoutedEventArgs e)
        {
            if (selectedSite == null)
            {
                MessageBox.Show("Выберите стройплощадку для обновления");
                return;
            }

            selectedSite.SiteName = txtSiteName.Text;
            selectedSite.Address = txtSiteAddress.Text;

            facade.UpdateSite(selectedSite);
            LoadSites();
        }

        private void BtnDeleteSite_Click(object sender, RoutedEventArgs e)
        {
            if (selectedSite == null)
            {
                MessageBox.Show("Выберите стройплощадку для удаления");
                return;
            }

            if (MessageBox.Show("Удалить стройплощадку?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                facade.DeleteSite(selectedSite.SiteId);
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

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
        
        private void PerformLogout()
        {
            LoginWindow.CurrentUser = null;
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            Window.GetWindow(this)?.Close();
        }
        
        private void BtnCloseHelp_Click(object sender, RoutedEventArgs e)
        {
            HelpButton.IsChecked = false;
        }

        private void LoadCrews()
        {
            try
            {
                var crews = facade.GetCrews();
                dgCrews.ItemsSource = crews;
                cbBrigadiers.ItemsSource = facade.GetUsers().Where(u => u.Role == "Бригадир").ToList();
                selectedCrew = null;
                btnCrewMembers.IsEnabled = CanManageCrewMembersRole(GetCurrentRoleNormalized());

                if (CanManageCrewMembersRole(GetCurrentRoleNormalized()) && crews.Count > 0)
                {
                    dgCrews.SelectedIndex = 0;
                }
                
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

        private void DgCrews_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var canManageMembers = CanManageCrewMembersRole(GetCurrentRoleNormalized());
            btnCrewMembers.IsEnabled = canManageMembers;

            if (dgCrews.SelectedItem is Models.Crew crew)
            {
                selectedCrew = crew;
                txtCrewName.Text = crew.CrewName;
                cbBrigadiers.SelectedItem = crew.Brigadier;
            }
            else
            {
                selectedCrew = null;
            }
        }

        private void BtnAddCrew_Click(object sender, RoutedEventArgs e)
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

            facade.AddCrew(crew);
            LoadCrews();
            ClearCrewFields();
        }

        private void BtnUpdateCrew_Click(object sender, RoutedEventArgs e)
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
                
                if (!(cbBrigadiers.SelectedItem is User brigadier))
                {
                    MessageBox.Show("Ошибка при получении данных бригадира");
                return;
            }

            selectedCrew.CrewName = txtCrewName.Text;
                selectedCrew.BrigadierId = brigadier.UserId;

            facade.UpdateCrew(selectedCrew);
            LoadCrews();
                MessageBox.Show("Бригада успешно обновлена");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении бригады: {ex.Message}");
            }
        }

        private void BtnDeleteCrew_Click(object sender, RoutedEventArgs e)
        {
            if (selectedCrew == null)
            {
                MessageBox.Show("Выберите бригаду для удаления");
                return;
            }

            if (MessageBox.Show("Удалить бригаду?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                facade.DeleteCrew(selectedCrew.CrewId);
                LoadCrews();
                ClearCrewFields();
            }
        }

        private void BtnCrewMembers_Click(object sender, RoutedEventArgs e)
        {
            var canManage = CanManageCrewMembersRole(GetCurrentRoleNormalized());

            if (!canManage)
            {
                MessageBox.Show("Редактирование состава бригады доступно только администратору и диспетчеру");
                return;
            }

            if (selectedCrew == null && dgCrews.SelectedItem is Models.Crew crewFromGrid)
            {
                selectedCrew = crewFromGrid;
            }

            if (selectedCrew == null)
            {
                MessageBox.Show("Выберите бригаду");
                return;
            }

            var window = new CrewMembersWindow(selectedCrew);
            window.ShowDialog();
            LoadCrews();
        }

        private void ClearCrewFields()
        {
            txtCrewName.Text = "";
            cbBrigadiers.SelectedItem = null;
            selectedCrew = null;
        }

        private void LoadTasks()
        {
            try
            {
                var tasks = GetTasksVisibleForCurrentUser(facade.GetTasks());
                
                var allReports = facade.GetTaskReports();
                foreach (var task in tasks)
                {
                    var lastReport = allReports
                        .Where(r => r.TaskId == task.TaskId)
                        .OrderByDescending(r => r.ReportedAt)
                        .FirstOrDefault();
                    
                    if (lastReport != null && !string.IsNullOrWhiteSpace(lastReport.ReportText))
                    {
                        task.LastNoteText = lastReport.ReportText.Length > 50 
                            ? lastReport.ReportText.Substring(0, 47) + "..." 
                            : lastReport.ReportText;
                        task.LastNoteTooltip = $"{lastReport.ReportedAt:dd.MM.yyyy HH:mm} - {lastReport.ReporterName}\n{lastReport.ReportText}";
                    }
                    else
                    {
                        task.LastNoteText = "—";
                        task.LastNoteTooltip = "Заметок нет";
                    }
                }

                allTasks = tasks;
                ApplyTaskFilters();
                
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
            try
            {
                isUpdatingTaskFilters = true;

                var sites = facade.GetSites();
                var crews = GetCrewsVisibleForCurrentUser(facade.GetCrews());

                cbSiteFilter.ItemsSource = sites;
                cbCrewFilter.ItemsSource = crews;
                cbStatusFilter.ItemsSource = facade.GetTaskStatuses();
                cbPriorityFilter.ItemsSource = facade.GetPriorities();

                cbCalendarSiteFilter.ItemsSource = sites;
                cbCalendarCrewFilter.ItemsSource = crews;

                if (cbTaskScopeFilter.SelectedIndex < 0)
                {
                    cbTaskScopeFilter.SelectedIndex = 0;
                }
            }
            finally
            {
                isUpdatingTaskFilters = false;
            }

            ApplyTaskFilters();
        }

        private void DgTasks_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            selectedTask = dgTasks.SelectedItem as Models.Task;
        }

        private void BtnResetTaskFilter_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                isUpdatingTaskFilters = true;

                if (txtTaskSearch != null)
                {
                    txtTaskSearch.Text = string.Empty;
                }

                cbSiteFilter.SelectedItem = null;
                cbCrewFilter.SelectedItem = null;
                cbStatusFilter.SelectedItem = null;
                cbPriorityFilter.SelectedItem = null;

                if (cbTaskScopeFilter != null)
                {
                    cbTaskScopeFilter.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сбросе фильтров: {ex.Message}");
            }
            finally
            {
                isUpdatingTaskFilters = false;
            }

            ApplyTaskFilters();
        }

        private void TaskSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyTaskFilters();
        }

        private void TaskFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            ApplyTaskFilters();
        }

        private void ApplyTaskFilters()
        {
            if (isUpdatingTaskFilters)
            {
                return;
            }

            try
            {
                IEnumerable<Models.Task> filteredTasks = allTasks ?? new List<Models.Task>();

                var searchText = txtTaskSearch?.Text?.Trim();
                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    filteredTasks = filteredTasks.Where(t =>
                        !string.IsNullOrWhiteSpace(t.Title) &&
                        t.Title.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0);
                }

                if (cbSiteFilter?.SelectedItem is Site selectedSiteFilter)
                {
                    filteredTasks = filteredTasks.Where(t => t.SiteId == selectedSiteFilter.SiteId);
                }

                if (cbCrewFilter?.SelectedItem is Crew selectedCrewFilter)
                {
                    filteredTasks = filteredTasks.Where(t => t.CrewId.HasValue && t.CrewId.Value == selectedCrewFilter.CrewId);
                }

                if (cbStatusFilter?.SelectedItem is TaskStatus selectedStatusFilter)
                {
                    filteredTasks = filteredTasks.Where(t => t.TaskStatusId == selectedStatusFilter.TaskStatusId);
                }

                if (cbPriorityFilter?.SelectedItem is Priority selectedPriorityFilter)
                {
                    filteredTasks = filteredTasks.Where(t => t.PriorityId == selectedPriorityFilter.PriorityId);
                }

                switch (GetTaskScopeFilterValue())
                {
                    case "active":
                        filteredTasks = filteredTasks.Where(IsTaskActive);
                        break;
                    case "completed":
                        filteredTasks = filteredTasks.Where(IsTaskCompleted);
                        break;
                    case "overdue":
                        filteredTasks = filteredTasks.Where(IsTaskOverdue);
                        break;
                }

                var result = filteredTasks.ToList();
                dgTasks.ItemsSource = result;
                txtTaskFilterNoResults.Visibility = result.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при фильтрации задач: {ex.Message}");
            }
        }

        private string GetTaskScopeFilterValue()
        {
            if (cbTaskScopeFilter?.SelectedItem is ComboBoxItem selectedItem &&
                selectedItem.Tag is string scopeValue &&
                !string.IsNullOrWhiteSpace(scopeValue))
            {
                return scopeValue;
            }

            return "all";
        }

        private List<Crew> GetCrewsVisibleForCurrentUser(List<Crew> crews)
        {
            var role = roleOverride ?? LoginWindow.CurrentUser?.Role;
            var currentUser = LoginWindow.CurrentUser;

            if (role == "Бригадир" && currentUser != null)
            {
                return crews
                    .Where(c => c.BrigadierId.HasValue && c.BrigadierId.Value == currentUser.UserId)
                    .ToList();
            }

            return crews;
        }

        private List<Models.Task> GetTasksVisibleForCurrentUser(List<Models.Task> tasks)
        {
            var role = roleOverride ?? LoginWindow.CurrentUser?.Role;
            var currentUser = LoginWindow.CurrentUser;

            if (role == "Бригадир" && currentUser != null)
            {
                var brigadierCrewIds = new HashSet<int>(
                    facade.GetCrews()
                        .Where(c => c.BrigadierId.HasValue && c.BrigadierId.Value == currentUser.UserId)
                        .Select(c => c.CrewId));

                return tasks
                    .Where(t => t.CrewId.HasValue && brigadierCrewIds.Contains(t.CrewId.Value))
                    .ToList();
            }

            return tasks;
        }

        private bool IsTaskCompleted(Models.Task task)
        {
            return string.Equals(task.TaskStatus?.TaskStatusName, "Завершено", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsTaskOverdue(Models.Task task)
        {
            return task.EndDate.Date < DateTime.Today && !IsTaskCompleted(task);
        }

        private bool IsTaskActive(Models.Task task)
        {
            return !IsTaskCompleted(task) && !IsTaskOverdue(task);
        }

        private void BtnAddTask_Click(object sender, RoutedEventArgs e)
        {
            var taskWindow = new TaskWindow(null);
            if (taskWindow.ShowDialog() == true)
            {
                LoadTasks();
            }
        }

        private void BtnUpdateTask_Click(object sender, RoutedEventArgs e)
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

        private void BtnDuplicateTask_Click(object sender, RoutedEventArgs e)
        {
            var role = roleOverride ?? LoginWindow.CurrentUser?.Role;
            var canDuplicate = role == "Администратор" || role == "Админ" || role == "Диспетчер";

            if (!canDuplicate)
            {
                MessageBox.Show("Дублирование доступно только администратору и диспетчеру");
                return;
            }

            if (selectedTask == null)
            {
                MessageBox.Show("Выберите задачу для дублирования");
                return;
            }

            var sourceTask = facade.GetTaskById(selectedTask.TaskId) ?? selectedTask;
            var taskWindow = new TaskWindow(sourceTask, true);
            if (taskWindow.ShowDialog() == true)
            {
                LoadTasks();
                if (taskWindow.SavedTaskId.HasValue)
                {
                    HighlightTask(taskWindow.SavedTaskId.Value);
                }
            }
        }

        private void BtnDeleteTask_Click(object sender, RoutedEventArgs e)
        {
            if (selectedTask == null)
            {
                MessageBox.Show("Выберите задачу для удаления");
                return;
            }

            if (MessageBox.Show("Удалить задачу?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                facade.DeleteTask(selectedTask.TaskId);
                LoadTasks();
            }
        }

        private void BtnUpdateStatus_Click(object sender, RoutedEventArgs e)
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

        private void BtnPrintTask_Click(object sender, RoutedEventArgs e)
        {
            var selectedTasks = dgTasks.SelectedItems.Cast<Models.Task>().ToList();
            
            if (selectedTasks.Count == 0)
            {
                if (selectedTask == null)
                {
                    MessageBox.Show("Выберите задачу для печати");
                    return;
                }
                selectedTasks = new List<Models.Task> { selectedTask };
            }

            int printedCount = 0;
            foreach (var task in selectedTasks)
            {
                var printWindow = new TaskPrintPreviewWindow(task.TaskId);
                if (printWindow.ShowDialog() == true)
                {
                    printedCount++;
                }
            }

            if (printedCount > 0)
            {
                MessageBox.Show($"Напечатано задач: {printedCount}", "Печать", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnQuickNoteInList_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is System.Windows.Controls.Button button) || !(button.Tag is int taskId))
            {
                return;
            }
            var task = facade.GetTaskById(taskId);
            
            if (task == null)
            {
                MessageBox.Show("Задача не найдена", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var user = LoginWindow.CurrentUser;
            if (user == null || (user.Role != "Бригадир" && user.Role != "Диспетчер" && user.Role != "Админ" && user.Role != "Администратор"))
            {
                MessageBox.Show("У вас нет прав для добавления заметок", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var taskViewWindow = new TaskViewWindow(taskId);
            taskViewWindow.ShowDialog();
            LoadTasks();
        }

        private void LoadCalendar()
        {
            try
            {
                TaskCalendar.SelectedDate = DateTime.Today;
                LoadTasksForDate(DateTime.Today);
                
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

        private void BtnResetCalendarFilters_Click(object sender, RoutedEventArgs e)
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
            
            var tasks = GetTasksVisibleForCurrentUser(facade.GetTasks()).Where(t => 
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

        private void BtnTasksBySite_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateCurrentReportType("tasks_by_site");
                
            var tasks = facade.GetTasks();
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

        private void BtnTasksByCrew_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateCurrentReportType("tasks_by_crew");
                
            var tasks = facade.GetTasks();
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

        private void BtnOverdueTasks_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateCurrentReportType("overdue_tasks");
                
            var tasks = facade.GetTasks();
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

        private void BtnOpenReports_Click(object sender, RoutedEventArgs e)
        {
            var wnd = new ReportsWindow();
            wnd.ShowDialog();
        }

        private string GetReportsFolderPath()
        {
            string folderPath = @"C:\Users\User\Desktop\";
            
            try
            {
                if (!Directory.Exists(folderPath))
                {
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
        
        private string currentReportType = "tasks";

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
                
                switch (currentReportType)
                {
                    case "tasks":
                        var tasks = facade.GetTasks();
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
                        var tasksBySite = facade.GetTasks()
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
                        var tasksByCrew = facade.GetTasks()
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
                        var overdueTasks = facade.GetTasks()
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
                
                switch (currentReportType)
                {
                    case "tasks":
                        var tasks = facade.GetTasks();
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
                        var tasksBySite = facade.GetTasks()
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
                        var tasksByCrew = facade.GetTasks()
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
                        var overdueTasks = facade.GetTasks()
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
                
                switch (currentReportType)
                {
                    case "tasks":
                        var tasks = facade.GetTasks();
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
                        var tasksBySite = facade.GetTasks()
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
                        var tasksByCrew = facade.GetTasks()
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
                        var overdueTasks = facade.GetTasks()
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

        private void LoadMaterialRequests()
        {
            if (cbMRSiteFilter == null || cbMRCrewFilter == null || cbMRStatusFilter == null || dgMaterialRequests == null)
            {
                return;
            }

            cbMRSiteFilter.ItemsSource = facade.GetSites();
            cbMRCrewFilter.ItemsSource = facade.GetCrews();

            if (materialRequests == null)
            {
                materialRequests = new System.Collections.ObjectModel.ObservableCollection<MaterialRequestRegistryDisplay>();
                dgMaterialRequests.ItemsSource = materialRequests;
            }

            materialRequests.Clear();

            var allRequests = facade.GetMaterialRequests();
            var filtered = allRequests.AsQueryable();

            if (cbMRStatusFilter != null && cbMRStatusFilter.SelectedItem is ComboBoxItem statusItem &&
                statusItem.Tag != null)
            {
                filtered = filtered.Where(r => r.Status == statusItem.Tag.ToString());
            }

            if (cbMRSiteFilter != null && cbMRSiteFilter.SelectedItem is Site site)
            {
                filtered = filtered.Where(r => r.Task != null && r.Task.SiteId == site.SiteId);
            }

            if (cbMRCrewFilter != null && cbMRCrewFilter.SelectedItem is Crew crew)
            {
                filtered = filtered.Where(r => r.Task != null && r.Task.CrewId.HasValue && r.Task.CrewId.Value == crew.CrewId);
            }

            if (dpMRRequiredDateFilter != null && dpMRRequiredDateFilter.SelectedDate.HasValue)
            {
                var date = dpMRRequiredDateFilter.SelectedDate.Value;
                filtered = filtered.Where(r => r.RequiredDate.HasValue && r.RequiredDate.Value >= date);
            }

            var tasks = facade.GetTasks();
            foreach (var req in filtered.ToList())
            {
                var task = tasks.FirstOrDefault(t => t.TaskId == req.TaskId);
                materialRequests.Add(new MaterialRequestRegistryDisplay(req, task));
            }

            UpdateMaterialRequestsPlaceholder();
        }

        private void UpdateMaterialRequestsPlaceholder()
        {
            if (MaterialRequestsPlaceholder == null || materialRequests == null)
            {
                return;
            }

            MaterialRequestsPlaceholder.Visibility = materialRequests.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void MRFilter_Changed(object sender, EventArgs e)
        {
            LoadMaterialRequests();
        }

        private void BtnMRResetFilter_Click(object sender, RoutedEventArgs e)
        {
            if (cbMRStatusFilter != null)
                cbMRStatusFilter.SelectedIndex = 0;
            if (cbMRSiteFilter != null)
                cbMRSiteFilter.SelectedItem = null;
            if (cbMRCrewFilter != null)
                cbMRCrewFilter.SelectedItem = null;
            if (dpMRRequiredDateFilter != null)
                dpMRRequiredDateFilter.SelectedDate = null;
            LoadMaterialRequests();
        }

        private void DgMaterialRequests_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgMaterialRequests.SelectedItem is MaterialRequestRegistryDisplay display)
            {
                var allRequests = facade.GetMaterialRequests();
                selectedMaterialRequest = allRequests.FirstOrDefault(r => r.RequestId == display.RequestId);
            }
            else
            {
                selectedMaterialRequest = null;
                if (cardMRDetailsPanel != null)
                {
                    cardMRDetailsPanel.Visibility = Visibility.Collapsed;
                }
                txtMRSelectedInfo.Text = "Выберите заявку для просмотра деталей";
                dgMRItems.Visibility = Visibility.Collapsed;
            }
        }

        private void DgMaterialRequests_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgMaterialRequests.SelectedItem == null) return;

            if (dgMaterialRequests.SelectedItem is MaterialRequestRegistryDisplay display)
            {
                var allRequests = facade.GetMaterialRequests();
                selectedMaterialRequest = allRequests.FirstOrDefault(r => r.RequestId == display.RequestId);
                if (selectedMaterialRequest != null)
                {
                    cardMRDetailsPanel.Visibility = Visibility.Visible;
                    ShowMaterialRequestDetails(selectedMaterialRequest);
                    UpdateMaterialRequestActionButtons();
                }
            }
        }

        private void ShowMaterialRequestDetails(MaterialRequest request)
        {
            var task = facade.GetTasks().FirstOrDefault(t => t.TaskId == request.TaskId);
            txtMRSelectedInfo.Text = $"Заявка #{request.RequestId}\n" +
                                    $"Задача: {task?.Title ?? "Не найдена"}\n" +
                                    $"Статус: {GetMRStatusName(request.Status)}\n" +
                                    $"Создана: {request.CreatedAt:dd.MM.yyyy HH:mm}\n" +
                                    $"Требуется к: {request.RequiredDate?.ToString("dd.MM.yyyy") ?? "не указано"}";

            dgMRItems.ItemsSource = request.Items;
            dgMRItems.Visibility = Visibility.Visible;
        }

        private void UpdateMaterialRequestActionButtons()
        {
            if (selectedMaterialRequest == null)
            {
                btnMRApprove.IsEnabled = false;
                btnMRReject.IsEnabled = false;
                btnMRIssue.IsEnabled = false;
                btnMRDeliver.IsEnabled = false;
                btnMRClose.IsEnabled = false;
                btnMREdit.IsEnabled = false;
                return;
            }

            btnMRApprove.IsEnabled = selectedMaterialRequest.Status == "Submitted";
            btnMRReject.IsEnabled = selectedMaterialRequest.Status == "Submitted";
            btnMRIssue.IsEnabled = selectedMaterialRequest.Status == "Approved";
            btnMRDeliver.IsEnabled = selectedMaterialRequest.Status == "Issued";
            btnMRClose.IsEnabled = selectedMaterialRequest.Status == "Delivered";
            btnMREdit.IsEnabled = selectedMaterialRequest.Status == "Draft" || selectedMaterialRequest.Status == "Submitted" || selectedMaterialRequest.Status == "Approved";
        }

        private void BtnMRApprove_Click(object sender, RoutedEventArgs e)
        {
            if (selectedMaterialRequest == null) return;

            if (facade.ProcessMaterialRequest(selectedMaterialRequest.RequestId, "approve", LoginWindow.CurrentUser.UserId, out string errorMessage))
            {
                MessageBox.Show("Заявка согласована");
                LoadMaterialRequests();
                selectedMaterialRequest = null;
                cardMRDetailsPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                MessageBox.Show($"Ошибка: {errorMessage}");
            }
        }

        private void BtnMRReject_Click(object sender, RoutedEventArgs e)
        {
            if (selectedMaterialRequest == null) return;

            var dialog = new MaterialRequestActionWindow("Отклонение заявки", "Причина отклонения", "Комментарий");
            if (dialog.ShowDialog() == true)
            {
                if (string.IsNullOrWhiteSpace(dialog.Note))
                {
                    MessageBox.Show("Укажите причину отклонения");
                    return;
                }

                if (facade.ProcessMaterialRequest(selectedMaterialRequest.RequestId, "reject", LoginWindow.CurrentUser.UserId, out string errorMessage))
                {
                    MessageBox.Show("Заявка отклонена");
                    LoadMaterialRequests();
                    selectedMaterialRequest = null;
                    cardMRDetailsPanel.Visibility = Visibility.Collapsed;
                }
                else
                {
                    MessageBox.Show($"Ошибка: {errorMessage}");
                }
            }
        }

        private void BtnMRIssue_Click(object sender, RoutedEventArgs e)
        {
            if (selectedMaterialRequest == null) return;

            var dialog = new MaterialRequestActionWindow("Выдача материалов", "Номер документа", "Примечание");
            if (dialog.ShowDialog() == true)
            {
                if (facade.ProcessMaterialRequest(selectedMaterialRequest.RequestId, "issue", LoginWindow.CurrentUser.UserId, out string errorMessage))
                {
                    if (!string.IsNullOrEmpty(dialog.DocNumber))
                    {
                        facade.AddMaterialDeliveryDoc(selectedMaterialRequest.RequestId, "Issued", dialog.DocNumber, dialog.Note);
                    }
                    MessageBox.Show("Выдача отмечена");
                    LoadMaterialRequests();
                    selectedMaterialRequest = null;
                    cardMRDetailsPanel.Visibility = Visibility.Collapsed;
                }
                else
                {
                    MessageBox.Show($"Ошибка: {errorMessage}");
                }
            }
        }

        private void BtnMRDeliver_Click(object sender, RoutedEventArgs e)
        {
            if (selectedMaterialRequest == null) return;

            var dialog = new MaterialRequestActionWindow("Доставка материалов", "Номер документа", "Примечание");
            if (dialog.ShowDialog() == true)
            {
                if (facade.ProcessMaterialRequest(selectedMaterialRequest.RequestId, "deliver", LoginWindow.CurrentUser.UserId, out string errorMessage))
                {
                    if (!string.IsNullOrEmpty(dialog.DocNumber))
                    {
                        facade.AddMaterialDeliveryDoc(selectedMaterialRequest.RequestId, "Delivered", dialog.DocNumber, dialog.Note);
                    }
                    MessageBox.Show("Доставка отмечена");
                    LoadMaterialRequests();
                    selectedMaterialRequest = null;
                    cardMRDetailsPanel.Visibility = Visibility.Collapsed;
                }
                else
                {
                    MessageBox.Show($"Ошибка: {errorMessage}");
                }
            }
        }

        private void BtnMRClose_Click(object sender, RoutedEventArgs e)
        {
            if (selectedMaterialRequest == null) return;

            var result = MessageBox.Show("Закрыть заявку?", "Подтверждение", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                if (facade.ProcessMaterialRequest(selectedMaterialRequest.RequestId, "close", LoginWindow.CurrentUser.UserId, out string errorMessage))
                {
                    MessageBox.Show("Заявка закрыта");
                    LoadMaterialRequests();
                    selectedMaterialRequest = null;
                    cardMRDetailsPanel.Visibility = Visibility.Collapsed;
                }
                else
                {
                    MessageBox.Show($"Ошибка: {errorMessage}");
                }
            }
        }

        private void BtnMREdit_Click(object sender, RoutedEventArgs e)
        {
            if (selectedMaterialRequest == null) return;

            var window = new MaterialRequestEditWindow(selectedMaterialRequest.TaskId, selectedMaterialRequest);
            if (window.ShowDialog() == true)
            {
                LoadMaterialRequests();
                cardMRDetailsPanel.Visibility = Visibility.Collapsed;
                selectedMaterialRequest = null;
            }
        }

        private void BtnMRAddRequest_Click(object sender, RoutedEventArgs e)
        {
            var taskSelectionWindow = new TaskSelectionWindow();
            if (taskSelectionWindow.ShowDialog() == true && taskSelectionWindow.SelectedTask != null)
            {
                var window = new MaterialRequestEditWindow(taskSelectionWindow.SelectedTask.TaskId);
                if (window.ShowDialog() == true)
                {
                    LoadMaterialRequests();
                }
            }
        }

        private void BtnMRExport_Click(object sender, RoutedEventArgs e)
        {
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV файлы (*.csv)|*.csv",
                FileName = $"Заявки_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    ExportMaterialRequestsToFile(saveDialog.FileName);
                    MessageBox.Show("Экспорт выполнен успешно");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при экспорте: {ex.Message}");
                }
            }
        }

        private void ExportMaterialRequestsToFile(string fileName)
        {
            var tasks = facade.GetTasks();
            var sites = facade.GetSites();
            var crews = facade.GetCrews();

            using (var writer = new StreamWriter(fileName, false, System.Text.Encoding.UTF8))
            {
                writer.WriteLine("ID;Задача;Объект;Бригада;Статус;Требуется к;Создана;Позиции");

                foreach (var req in materialRequests)
                {
                    var task = tasks.FirstOrDefault(t => t.TaskId == req.TaskId);
                    var site = task != null ? sites.FirstOrDefault(s => s.SiteId == task.SiteId) : null;
                    var crew = task != null && task.CrewId.HasValue ? crews.FirstOrDefault(c => c.CrewId == task.CrewId.Value) : null;

                    var items = selectedMaterialRequest?.Items != null && selectedMaterialRequest.RequestId == req.RequestId
                        ? string.Join("; ", selectedMaterialRequest.Items.Select(i => $"{i.Material.Name} {i.Qty} {i.Material.Unit}"))
                        : $"{req.ItemsCount} позиций";

                    writer.WriteLine($"{req.RequestId};{req.TaskTitle};{site?.SiteName ?? ""};{crew?.CrewName ?? ""};{req.StatusDisplay};{req.RequiredDateDisplay};{req.CreatedAtDisplay};{items}");
                }
            }
        }

        private string GetMRStatusName(string status)
        {
            switch (status)
            {
                case "Draft": return "Черновик";
                case "Submitted": return "Отправлена";
                case "Approved": return "Согласована";
                case "Rejected": return "Отклонена";
                case "Issued": return "Выдана";
                case "Delivered": return "Доставлена";
                case "Closed": return "Закрыта";
                default: return status;
            }
        }
    }
}
