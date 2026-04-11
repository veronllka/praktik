using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
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
        private readonly HashSet<string> currentPermissionCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly ObservableCollection<BrigadierTaskChecklistItem> brigadierChecklistItems = new ObservableCollection<BrigadierTaskChecklistItem>();
        private readonly Brush calendarTaskHighlightBrush = new SolidColorBrush(Color.FromRgb(30, 136, 229));
        private string roleOverride;
        private DateTime brigadierChecklistDate = DateTime.Today;
        private Site selectedSite;
        private Crew selectedCrew;
        private Models.Task selectedTask;
        private List<Models.Task> allTasks = new List<Models.Task>();
        private readonly List<ReportPreviewSlice> currentReportPreviewSlices = new List<ReportPreviewSlice>();
        private bool isUpdatingTaskFilters;
        private bool isUpdatingReportSelectors;
        private System.Collections.ObjectModel.ObservableCollection<MaterialRequestRegistryDisplay> materialRequests;
        private MaterialRequest selectedMaterialRequest;
        private User selectedRoleManagementUser;
        private Role selectedRoleManagementRole;
        private List<RolePermissionItem> rolePermissionItems = new List<RolePermissionItem>();
        private bool isRolePermissionsViewActive;
        private string currentReportType = "tasks";
        private string currentReportChartType = "bar";
        private bool isUpdatingBrigadierChecklistDate;

        // DailyPlan state
        private DateTime dailyPlanDate = DateTime.Today;
        private DailyPlan currentDailyPlan;
        private int selectedPlanTaskId = -1;
        private readonly ObservableCollection<AvailablePlanTaskItem> availablePlanTasks = new ObservableCollection<AvailablePlanTaskItem>();
        private readonly ObservableCollection<CrewPlanGroup> dailyPlanCrewGroups = new ObservableCollection<CrewPlanGroup>();
        private List<Crew> dailyPlanCrews = new List<Crew>();
        private bool isUpdatingDailyPlanDate;

        public MainWindow()
        {
            InitializeComponent();
            facade = new WorkPlannerFacade();
            icBrigadierTasks.ItemsSource = brigadierChecklistItems;
            icAvailablePlanTasks.ItemsSource = availablePlanTasks;
            icDailyPlanCrews.ItemsSource = dailyPlanCrewGroups;
            AppThemeManager.ApplyTheme(LoginWindow.CurrentUser);
            UpdateRoleManagementView();
            LoadCurrentPermissions();
            LoadData();
            SetupPermissions();
            UpdateUserInfo();
            UpdateThemeToggleButton();
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

        private void OpenTaskDetails(Models.Task task)
        {
            if (task == null)
            {
                return;
            }

            try
            {
                var taskViewWindow = new TaskViewWindow(task.TaskId);
                taskViewWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии задачи: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnScanQR_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureAnyPermission("Сканирование QR-кода доступно только пользователям с доступом к задачам.", RolePermissionCatalog.TasksView, RolePermissionCatalog.TasksManage, RolePermissionCatalog.TasksDuplicate))
            {
                return;
            }

            var qrWindow = new QRCodeScannerWindow();
            if (qrWindow.ShowDialog() == true && qrWindow.TaskId.HasValue)
            {
                TasksMenuItem.IsSelected = true;
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
            if (!TryGetTaskFromEventSource(e.OriginalSource, out var task))
            {
                return;
            }

            SelectTask(task);
            if (CanManageTasks())
            {
                OpenTaskEditor(task);
                return;
            }

            OpenTaskDetails(task);
        }

        private void UpdateUserInfo()
        {
            if (LoginWindow.CurrentUser != null)
            {
                var role = GetCurrentRole();
                var userDisplayName = string.IsNullOrWhiteSpace(LoginWindow.CurrentUser.FullName)
                    ? LoginWindow.CurrentUser.Username
                    : LoginWindow.CurrentUser.FullName;

                UserInfo.Text = $"{userDisplayName} ({role})";
            }

            UpdateThemeToggleButton();
        }

        private string GetCurrentRole()
        {
            return string.IsNullOrWhiteSpace(roleOverride)
                ? LoginWindow.CurrentUser?.Role
                : roleOverride;
        }

        private string GetCurrentRoleNormalized()
        {
            return RolePermissionCatalog.NormalizeRoleName(GetCurrentRole());
        }

        private bool IsBrigadierMode()
        {
            var normalizedRole = GetCurrentRoleNormalized();
            return normalizedRole == "бригадир" || normalizedRole == "foreman";
        }

        private static bool IsAdminRole(string normalizedRole)
        {
            return normalizedRole == "администратор"
                || normalizedRole == "админ"
                || normalizedRole == "admin"
                || normalizedRole == "administrator";
        }

        private void LoadCurrentPermissions()
        {
            currentPermissionCodes.Clear();

            foreach (var permissionCode in facade.GetRolePermissionCodes(GetCurrentRole()))
            {
                if (!string.IsNullOrWhiteSpace(permissionCode))
                {
                    currentPermissionCodes.Add(permissionCode);
                }
            }
        }

        private bool HasPermission(string permissionCode)
        {
            return !string.IsNullOrWhiteSpace(permissionCode) && currentPermissionCodes.Contains(permissionCode);
        }

        private bool HasAnyPermission(params string[] permissionCodes)
        {
            return permissionCodes != null && permissionCodes.Any(HasPermission);
        }

        private bool CanViewDashboard() => HasPermission(RolePermissionCatalog.DashboardView);
        private bool CanViewSites() => HasAnyPermission(RolePermissionCatalog.SitesView, RolePermissionCatalog.SitesManage);
        private bool CanManageSites() => HasPermission(RolePermissionCatalog.SitesManage);
        private bool CanViewCrews() => HasAnyPermission(RolePermissionCatalog.CrewsView, RolePermissionCatalog.CrewsManage, RolePermissionCatalog.CrewMembersManage);
        private bool CanManageCrews() => HasPermission(RolePermissionCatalog.CrewsManage);
        private bool CanManageCrewMembers() => HasPermission(RolePermissionCatalog.CrewMembersManage);
        private bool CanViewTasks() => HasAnyPermission(RolePermissionCatalog.TasksView, RolePermissionCatalog.TasksManage, RolePermissionCatalog.TasksDuplicate);
        private bool CanManageTasks() => HasPermission(RolePermissionCatalog.TasksManage);
        private bool CanDuplicateTasks() => HasPermission(RolePermissionCatalog.TasksDuplicate);
        private bool CanViewCalendar() => HasPermission(RolePermissionCatalog.CalendarView);
        private bool CanViewReports() => HasAnyPermission(RolePermissionCatalog.ReportsView, RolePermissionCatalog.ReportsExport);
        private bool CanExportReports() => HasPermission(RolePermissionCatalog.ReportsExport);
        private bool CanViewMaterialRequests() => HasAnyPermission(RolePermissionCatalog.MaterialRequestsView, RolePermissionCatalog.MaterialRequestsManage);
        private bool CanManageMaterialRequests() => HasPermission(RolePermissionCatalog.MaterialRequestsManage);
        private bool CanViewDailyPlan() => HasPermission(RolePermissionCatalog.DailyPlanView);

        private bool EnsurePermission(string permissionCode, string accessDeniedMessage)
        {
            if (HasPermission(permissionCode))
            {
                return true;
            }

            MessageBox.Show(accessDeniedMessage, "Доступ запрещен", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        private bool EnsureAnyPermission(string accessDeniedMessage, params string[] permissionCodes)
        {
            if (HasAnyPermission(permissionCodes))
            {
                return true;
            }

            MessageBox.Show(accessDeniedMessage, "Доступ запрещен", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        private static void SetMenuVisibility(ListBoxItem menuItem, bool isVisible)
        {
            if (menuItem != null)
            {
                menuItem.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void EnsureVisibleNavigationSelection()
        {
            if (NavigationListBox.SelectedItem is ListBoxItem selectedItem && selectedItem.Visibility == Visibility.Visible)
            {
                return;
            }

            foreach (ListBoxItem item in NavigationListBox.Items)
            {
                if (item.Visibility == Visibility.Visible)
                {
                    item.IsSelected = true;
                    return;
                }
            }

            NavigationListBox.SelectedIndex = -1;
        }

        private void UpdateTaskWorkspaceMode()
        {
            var isBrigadierMode = IsBrigadierMode();

            if (StandardTasksPanel != null)
            {
                StandardTasksPanel.Visibility = isBrigadierMode ? Visibility.Collapsed : Visibility.Visible;
            }

            if (BrigadierTasksPanel != null)
            {
                BrigadierTasksPanel.Visibility = isBrigadierMode ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void SetupPermissions()
        {
            var normalizedRole = GetCurrentRoleNormalized();
            var isAdminRole = IsAdminRole(normalizedRole);

            LoadCurrentPermissions();

            btnAddSite.IsEnabled = CanManageSites();
            btnUpdateSite.IsEnabled = CanManageSites();
            btnDeleteSite.IsEnabled = CanManageSites();
            btnAddCrew.IsEnabled = CanManageCrews();
            btnUpdateCrew.IsEnabled = CanManageCrews();
            btnDeleteCrew.IsEnabled = CanManageCrews();
            btnDuplicateTask.Visibility = Visibility.Collapsed;
            btnDuplicateTask.Visibility = CanDuplicateTasks() ? Visibility.Visible : Visibility.Collapsed;
            btnCrewMembers.IsEnabled = CanManageCrewMembers();
            btnCrewMembers.Visibility = CanManageCrewMembers() ? Visibility.Visible : Visibility.Collapsed;
            btnAddTask.IsEnabled = CanManageTasks();
            btnUpdateTask.IsEnabled = CanManageTasks();
            btnDeleteTask.IsEnabled = CanManageTasks();
            btnUpdateStatus.IsEnabled = CanManageTasks();
            btnPrintTask.IsEnabled = CanManageTasks();
            btnGenerateSelectedReport.IsEnabled = CanViewReports();
            btnExportSelectedReport.IsEnabled = CanExportReports();
            btnMRAddRequest.IsEnabled = CanManageMaterialRequests();
            btnMRExport.IsEnabled = CanViewMaterialRequests();
            btnAddManagedUser.IsEnabled = isAdminRole;
            btnEditManagedUser.IsEnabled = isAdminRole;
            btnDeleteManagedUser.IsEnabled = isAdminRole;
            btnApplyUserRole.IsEnabled = false;
            btnCreateRole.IsEnabled = isAdminRole;
            btnSaveRolePermissions.IsEnabled = isAdminRole;
            btnReloadRoleManagement.IsEnabled = isAdminRole;
            cbRoleAssignment.IsEnabled = false;
            txtNewRoleName.IsEnabled = isAdminRole;
            btnScanQR.Visibility = CanViewTasks() ? Visibility.Visible : Visibility.Collapsed;

            foreach (ListBoxItem item in NavigationListBox.Items)
            {
                item.Visibility = Visibility.Collapsed;
            }

            var canViewDashboard = CanViewDashboard();
            var canViewSites = CanViewSites();
            var canViewCrews = CanViewCrews();
            var canViewTasks = CanViewTasks();
            var canViewCalendar = CanViewCalendar();
            var canViewReports = CanViewReports();
            var canViewMaterialRequests = CanViewMaterialRequests();

            SetMenuVisibility(DashboardMenuItem, canViewDashboard);
            SetMenuVisibility(SitesMenuItem, canViewSites);
            SetMenuVisibility(CrewsMenuItem, canViewCrews);
            SetMenuVisibility(TasksMenuItem, canViewTasks);
            SetMenuVisibility(CalendarMenuItem, canViewCalendar);
            SetMenuVisibility(ReportsMenuItem, canViewReports);
            SetMenuVisibility(MaterialRequestsMenuItem, canViewMaterialRequests);
            SetMenuVisibility(RolesMenuItem, isAdminRole);
            SetMenuVisibility(DailyPlanMenuItem, CanViewDailyPlan());

            SetHelpSectionsVisibility(
                canViewDashboard,
                canViewSites,
                canViewCrews,
                canViewTasks,
                canViewCalendar,
                canViewReports);

            UpdateTaskWorkspaceMode();
            UpdateMaterialRequestActionButtons();
            EnsureVisibleNavigationSelection();
        }

        public void ApplyRole(string role)
        {
            roleOverride = role;
            SetupPermissions();
            UpdateUserInfo();
        }

        private void UpdateThemeToggleButton()
        {
            if (QuickThemeToggleIcon == null || btnQuickThemeToggle == null)
            {
                return;
            }

            var baseTheme = AppThemeManager.NormalizeBaseTheme(LoginWindow.CurrentUser?.PreferredTheme);
            var isDarkTheme = AppThemeManager.IsDarkTheme(baseTheme);

            QuickThemeToggleIcon.Kind = isDarkTheme
                ? MaterialDesignThemes.Wpf.PackIconKind.WhiteBalanceSunny
                : MaterialDesignThemes.Wpf.PackIconKind.WeatherNight;
            btnQuickThemeToggle.ToolTip = isDarkTheme
                ? "Переключить на светлую тему"
                : "Переключить на тёмную тему";
        }

        private void BtnOpenSettings_Click(object sender, RoutedEventArgs e)
        {
            if (LoginWindow.CurrentUser == null)
            {
                return;
            }

            var settingsWindow = new SettingsWindow();
            var ownerWindow = Window.GetWindow(this);
            if (ownerWindow != null)
            {
                settingsWindow.Owner = ownerWindow;
            }

            settingsWindow.ShowDialog();
            UpdateUserInfo();
        }

        private void BtnQuickThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            if (LoginWindow.CurrentUser == null)
            {
                return;
            }

            var nextTheme = AppThemeManager.ToggleBaseTheme(LoginWindow.CurrentUser.PreferredTheme);
            var accentColor = AppThemeManager.NormalizeAccent(LoginWindow.CurrentUser.AccentColor);

            try
            {
                facade.UpdateUserSettings(LoginWindow.CurrentUser.UserId, null, nextTheme, accentColor);
                LoginWindow.CurrentUser.PreferredTheme = nextTheme;
                LoginWindow.CurrentUser.AccentColor = accentColor;
                AppThemeManager.ApplyTheme(LoginWindow.CurrentUser);
                UpdateThemeToggleButton();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при смене темы: {ex.Message}", "Настройки", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetHelpSectionsVisibility(
            bool home,
            bool sites,
            bool crews,
            bool tasks,
            bool calendar,
            bool reports)
        {
            HelpHomeExpander.Visibility = home ? Visibility.Visible : Visibility.Collapsed;
            HelpSitesExpander.Visibility = sites ? Visibility.Visible : Visibility.Collapsed;
            HelpCrewsExpander.Visibility = crews ? Visibility.Visible : Visibility.Collapsed;
            HelpTasksExpander.Visibility = tasks ? Visibility.Visible : Visibility.Collapsed;
            HelpCalendarExpander.Visibility = calendar ? Visibility.Visible : Visibility.Collapsed;
            HelpReportsExpander.Visibility = reports ? Visibility.Visible : Visibility.Collapsed;
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
            RecentReportsGrid.ItemsSource = reports.Take(5);
        }

        private void BtnOpenTaskReportAttachment_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is FrameworkElement element) || !(element.DataContext is TaskReport report))
            {
                return;
            }

            if (!TaskReportAttachmentService.TryOpenAttachment(report.AttachmentUrl, out var errorMessage))
            {
                MessageBox.Show(errorMessage, "Вложение", MessageBoxButton.OK, MessageBoxImage.Information);
            }
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
            MaterialRequestsContent.Visibility = Visibility.Collapsed;
            RolesContent.Visibility = Visibility.Collapsed;
            DailyPlanContent.Visibility = Visibility.Collapsed;

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
                    PageTitle.Text = IsBrigadierMode() ? "Чек-лист бригады" : "Задачи";
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
                    LoadCurrentReportView();
                    break;
                case 6:
                    MaterialRequestsContent.Visibility = Visibility.Visible;
                    PageTitle.Text = "Заявки на материалы";
                    LoadMaterialRequests();
                    break;
                case 7:
                    RolesContent.Visibility = Visibility.Visible;
                    PageTitle.Text = "Управление пользователями";
                    LoadRoleManagementData();
                    break;
                case 8:
                    DailyPlanContent.Visibility = Visibility.Visible;
                    PageTitle.Text = "План на день";
                    LoadDailyPlan();
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
            if (!EnsurePermission(RolePermissionCatalog.SitesManage, "Добавление стройплощадок запрещено для вашей роли."))
            {
                return;
            }

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
            if (!EnsurePermission(RolePermissionCatalog.SitesManage, "Изменение стройплощадок запрещено для вашей роли."))
            {
                return;
            }

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
            if (!EnsurePermission(RolePermissionCatalog.SitesManage, "Удаление стройплощадок запрещено для вашей роли."))
            {
                return;
            }

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
            AppThemeManager.ApplyTheme(AppThemeManager.DefaultBaseTheme, AppThemeManager.DefaultAccentColor);
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
                btnCrewMembers.IsEnabled = CanManageCrewMembers();

                if (CanManageCrewMembers() && crews.Count > 0)
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
            var canManageMembers = CanManageCrewMembers();
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
            if (!EnsurePermission(RolePermissionCatalog.CrewsManage, "Создание бригад запрещено для вашей роли."))
            {
                return;
            }

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
            if (!EnsurePermission(RolePermissionCatalog.CrewsManage, "Изменение бригад запрещено для вашей роли."))
            {
                return;
            }

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
            if (!EnsurePermission(RolePermissionCatalog.CrewsManage, "Удаление бригад запрещено для вашей роли."))
            {
                return;
            }

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
            if (!EnsurePermission(RolePermissionCatalog.CrewMembersManage, "Редактирование состава бригады доступно только пользователям с правом управления составом бригад."))
            {
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
                LoadBrigadierChecklist();
                UpdateCalendarHighlightsAsync();
                
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

        private void LoadBrigadierChecklist()
        {
            brigadierChecklistItems.Clear();

            if (!IsBrigadierMode())
            {
                ResetBrigadierChecklistView();
                return;
            }

            EnsureBrigadierChecklistDateSync();
            var selectedDate = brigadierChecklistDate.Date;
            var weekStart = GetStartOfWeek(selectedDate);
            var checklistTasks = BuildBrigadierChecklistTasks(selectedDate);

            // Try to get approved plan for this date to show plan order in checklist
            List<DailyPlanItem> approvedPlanItems;
            try { approvedPlanItems = facade.GetApprovedPlanItemsForDate(selectedDate); }
            catch { approvedPlanItems = new List<DailyPlanItem>(); }

            // Get current user's crew (brigadier is foreman of their crew)
            int currentUserId = LoginWindow.CurrentUser?.UserId ?? 0;
            var userCrew = facade.GetCrews().FirstOrDefault(c => c.BrigadierId == currentUserId);
            int myCrewId = userCrew?.CrewId ?? 0;

            // Filter plan items for this brigadier's crew
            var myPlanItems = approvedPlanItems
                .Where(p => p.CrewId == myCrewId || myCrewId == 0)
                .ToDictionary(p => p.TaskId, p => p);

            // Sort: plan items first (by order), then remaining tasks
            List<Models.Task> orderedTasks;
            if (myPlanItems.Count > 0)
            {
                var inPlan = checklistTasks
                    .Where(t => myPlanItems.ContainsKey(t.TaskId))
                    .OrderBy(t => myPlanItems[t.TaskId].SortOrder)
                    .ToList();
                var notInPlan = checklistTasks
                    .Where(t => !myPlanItems.ContainsKey(t.TaskId))
                    .ToList();
                orderedTasks = inPlan.Concat(notInPlan).ToList();
            }
            else
            {
                orderedTasks = checklistTasks;
            }

            foreach (var task in orderedTasks)
            {
                var item = CreateBrigadierChecklistItem(task, selectedDate);
                if (myPlanItems.TryGetValue(task.TaskId, out var planItem))
                {
                    item.PlanOrderText = $"Пункт плана №{planItem.SortOrder}";
                    item.PlanNote = string.IsNullOrWhiteSpace(planItem.Note) ? null : planItem.Note;
                }
                brigadierChecklistItems.Add(item);
            }

            UpdateBrigadierChecklistSummary(selectedDate, weekStart, orderedTasks.Count);
        }

        private void ResetBrigadierChecklistView()
        {
            if (txtBrigadierTaskSummary != null)
            {
                txtBrigadierTaskSummary.Text = string.Empty;
            }

            if (txtBrigadierWeekRange != null)
            {
                txtBrigadierWeekRange.Text = string.Empty;
            }

            if (txtBrigadierChecklistEmpty != null)
            {
                txtBrigadierChecklistEmpty.Visibility = Visibility.Collapsed;
            }
        }

        private List<Models.Task> BuildBrigadierChecklistTasks(DateTime selectedDate)
        {
            return (allTasks ?? new List<Models.Task>())
                .Where(task => IsTaskVisibleInBrigadierChecklist(task, selectedDate))
                .Where(task => !IsTaskCompleted(task))
                .OrderByDescending(task => task.EndDate.Date < selectedDate)
                .ThenByDescending(task => task.PriorityId)
                .ThenBy(task => task.EndDate)
                .ThenBy(task => task.Title)
                .ToList();
        }

        private static BrigadierTaskChecklistItem CreateBrigadierChecklistItem(Models.Task task, DateTime selectedDate)
        {
            var isOverdue = task.EndDate.Date < selectedDate;
            return new BrigadierTaskChecklistItem
            {
                Task = task,
                IsOverdue = isOverdue,
                ChecklistStatusText = isOverdue ? "Просрочена" : "На выбранную дату",
                IsMarkedCompleted = false,
                CompletionComment = string.Empty
            };
        }

        private void UpdateBrigadierChecklistSummary(DateTime selectedDate, DateTime weekStart, int taskCount)
        {
            if (txtBrigadierWeekRange != null)
            {
                txtBrigadierWeekRange.Text = $"Неделя: {weekStart:dd.MM.yyyy} - {weekStart.AddDays(6):dd.MM.yyyy}";
            }

            if (txtBrigadierTaskSummary != null)
            {
                txtBrigadierTaskSummary.Text = taskCount == 0
                    ? $"На {selectedDate:dd.MM.yyyy} задач нет."
                    : $"На {selectedDate:dd.MM.yyyy} задач в чек-листе: {taskCount}.";
            }

            if (txtBrigadierChecklistEmpty != null)
            {
                txtBrigadierChecklistEmpty.Visibility = taskCount == 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private void EnsureBrigadierChecklistDateSync()
        {
            if (dpBrigadierChecklistDate == null)
            {
                return;
            }

            var pickerDate = dpBrigadierChecklistDate.SelectedDate?.Date;
            if (pickerDate.HasValue)
            {
                brigadierChecklistDate = pickerDate.Value;
                return;
            }

            isUpdatingBrigadierChecklistDate = true;
            dpBrigadierChecklistDate.SelectedDate = brigadierChecklistDate;
            isUpdatingBrigadierChecklistDate = false;
        }

        private static DateTime GetStartOfWeek(DateTime date)
        {
            var diff = ((7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7);
            return date.Date.AddDays(-diff);
        }

        private bool IsTaskVisibleInBrigadierChecklist(Models.Task task, DateTime selectedDate)
        {
            if (task == null || IsTaskCompleted(task))
            {
                return false;
            }

            var date = selectedDate.Date;
            var isActiveOnSelectedDate = task.StartDate.Date <= date && task.EndDate.Date >= date;
            var isOverdueForSelectedDate = task.EndDate.Date < date;

            return isActiveOnSelectedDate || isOverdueForSelectedDate;
        }

        private void SetBrigadierChecklistDate(DateTime date)
        {
            brigadierChecklistDate = date.Date;

            if (dpBrigadierChecklistDate != null)
            {
                isUpdatingBrigadierChecklistDate = true;
                dpBrigadierChecklistDate.SelectedDate = brigadierChecklistDate;
                isUpdatingBrigadierChecklistDate = false;
            }

            LoadBrigadierChecklist();
        }

        private void DpBrigadierChecklistDate_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingBrigadierChecklistDate || dpBrigadierChecklistDate?.SelectedDate == null)
            {
                return;
            }

            brigadierChecklistDate = dpBrigadierChecklistDate.SelectedDate.Value.Date;
            LoadBrigadierChecklist();
        }

        private void BtnBrigadierChecklistPrevDay_Click(object sender, RoutedEventArgs e)
        {
            SetBrigadierChecklistDate(brigadierChecklistDate.AddDays(-1));
        }

        private void BtnBrigadierChecklistToday_Click(object sender, RoutedEventArgs e)
        {
            SetBrigadierChecklistDate(DateTime.Today);
        }

        private void BtnBrigadierChecklistNextDay_Click(object sender, RoutedEventArgs e)
        {
            SetBrigadierChecklistDate(brigadierChecklistDate.AddDays(1));
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

        private void DgTasksRow_PreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is DataGridRow row && row.Item is Models.Task task)
            {
                SelectTask(task);
                row.Focus();
            }
        }

        private void DgTasks_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (!TryGetTaskFromEventSource(e.OriginalSource, out var task))
            {
                e.Handled = true;
                return;
            }

            SelectTask(task);
        }

        private void TaskQrMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureAnyPermission("Просмотр QR-кода доступен только пользователям с доступом к задачам.", RolePermissionCatalog.TasksView, RolePermissionCatalog.TasksManage, RolePermissionCatalog.TasksDuplicate))
            {
                return;
            }

            if (selectedTask == null)
            {
                MessageBox.Show("Выберите задачу для открытия QR-кода");
                return;
            }

            OpenTaskQr(selectedTask);
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
                var result = BuildFilteredTasks().ToList();
                dgTasks.ItemsSource = result;
                txtTaskFilterNoResults.Visibility = result.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при фильтрации задач: {ex.Message}");
            }
        }

        private IEnumerable<Models.Task> BuildFilteredTasks()
        {
            IEnumerable<Models.Task> filteredTasks = allTasks ?? new List<Models.Task>();
            var searchText = txtTaskSearch?.Text?.Trim();

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filteredTasks = ApplyTaskSearchFilter(filteredTasks, searchText);
            }

            if (cbSiteFilter?.SelectedItem is Site selectedSiteFilter)
            {
                filteredTasks = filteredTasks.Where(task => task.SiteId == selectedSiteFilter.SiteId);
            }

            if (cbCrewFilter?.SelectedItem is Crew selectedCrewFilter)
            {
                filteredTasks = filteredTasks.Where(task => task.CrewId.HasValue && task.CrewId.Value == selectedCrewFilter.CrewId);
            }

            if (cbStatusFilter?.SelectedItem is TaskStatus selectedStatusFilter)
            {
                filteredTasks = filteredTasks.Where(task => task.TaskStatusId == selectedStatusFilter.TaskStatusId);
            }

            if (cbPriorityFilter?.SelectedItem is Priority selectedPriorityFilter)
            {
                filteredTasks = filteredTasks.Where(task => task.PriorityId == selectedPriorityFilter.PriorityId);
            }

            return ApplyTaskScopeFilter(filteredTasks);
        }

        private static IEnumerable<Models.Task> ApplyTaskSearchFilter(IEnumerable<Models.Task> tasks, string searchText)
        {
            return tasks.Where(task =>
                !string.IsNullOrWhiteSpace(task.Title) &&
                task.Title.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private IEnumerable<Models.Task> ApplyTaskScopeFilter(IEnumerable<Models.Task> tasks)
        {
            switch (GetTaskScopeFilterValue())
            {
                case "active":
                    return tasks.Where(IsTaskActive);
                case "completed":
                    return tasks.Where(IsTaskCompleted);
                case "overdue":
                    return tasks.Where(IsTaskOverdue);
                default:
                    return tasks;
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
            var role = RolePermissionCatalog.NormalizeRoleName(roleOverride ?? LoginWindow.CurrentUser?.Role);
            var currentUser = LoginWindow.CurrentUser;

            if ((role == "бригадир" || role == "foreman") && currentUser != null)
            {
                return crews
                    .Where(c => c.BrigadierId.HasValue && c.BrigadierId.Value == currentUser.UserId)
                    .ToList();
            }

            return crews;
        }

        private List<Models.Task> GetTasksVisibleForCurrentUser(List<Models.Task> tasks)
        {
            var role = RolePermissionCatalog.NormalizeRoleName(roleOverride ?? LoginWindow.CurrentUser?.Role);
            var currentUser = LoginWindow.CurrentUser;

            if ((role == "бригадир" || role == "foreman") && currentUser != null)
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

        private bool TryResolveTaskStatusId(out int statusId, params string[] statusNames)
        {
            statusId = 0;
            if (statusNames == null || statusNames.Length == 0)
            {
                return false;
            }

            var status = facade.GetTaskStatuses()
                .FirstOrDefault(item => statusNames.Any(name =>
                    string.Equals(item.TaskStatusName, name, StringComparison.OrdinalIgnoreCase)));

            if (status == null)
            {
                return false;
            }

            statusId = status.TaskStatusId;
            return true;
        }

        private int ResolveChecklistIncompleteStatusId(Models.Task task)
        {
            if (TryResolveTaskStatusId(out var statusId, "В работе", "In Progress"))
            {
                return statusId;
            }

            if (task != null && !IsTaskCompleted(task) && task.TaskStatusId > 0)
            {
                return task.TaskStatusId;
            }

            if (TryResolveTaskStatusId(out statusId, "Новая", "New"))
            {
                return statusId;
            }

            return task?.TaskStatusId ?? 0;
        }

        private void RefreshViewsAfterTaskMutation()
        {
            LoadTasks();
            LoadTaskReports();
            LoadDashboard();

            if (TaskCalendar != null)
            {
                if (TaskCalendar.SelectedDate.HasValue)
                {
                    LoadTasksForDate(TaskCalendar.SelectedDate.Value);
                }

                UpdateCalendarHighlightsAsync();
            }
        }

        private void BtnSaveBrigadierChecklistDecision_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is FrameworkElement element) || !(element.Tag is BrigadierTaskChecklistItem checklistItem))
            {
                return;
            }

            var task = facade.GetTaskById(checklistItem.TaskId);
            if (task == null)
            {
                MessageBox.Show("Задача не найдена", "Чек-лист", MessageBoxButton.OK, MessageBoxImage.Warning);
                LoadTasks();
                return;
            }

            if (!TryResolveTaskStatusId(out var completedStatusId, "Завершено", "Completed"))
            {
                MessageBox.Show("Не найден статус завершения задачи.", "Чек-лист", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var incompleteStatusId = ResolveChecklistIncompleteStatusId(task);
            if (!facade.ApplyTaskChecklistDecision(
                    task,
                    checklistItem.IsMarkedCompleted,
                    completedStatusId,
                    incompleteStatusId,
                    LoginWindow.CurrentUser?.UserId ?? 0,
                    checklistItem.CompletionComment,
                    out var errorMessage))
            {
                MessageBox.Show(errorMessage, "Чек-лист", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            RefreshViewsAfterTaskMutation();
        }

        private void BtnBrigadierOpenTask_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is FrameworkElement element) || !(element.Tag is int taskId))
            {
                return;
            }

            var task = facade.GetTaskById(taskId);
            if (task == null)
            {
                MessageBox.Show("Задача не найдена", "Задачи", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            OpenTaskDetails(task);
        }

        private void BtnAddTask_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsurePermission(RolePermissionCatalog.TasksManage, "Создание задач запрещено для вашей роли."))
            {
                return;
            }

            var taskWindow = new TaskWindow(null);
            if (taskWindow.ShowDialog() == true)
            {
                RefreshViewsAfterTaskMutation();
            }
        }

        private void BtnUpdateTask_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsurePermission(RolePermissionCatalog.TasksManage, "Редактирование задач запрещено для вашей роли."))
            {
                return;
            }

            if (selectedTask == null)
            {
                MessageBox.Show("Выберите задачу для обновления");
                return;
            }

            OpenTaskEditor(selectedTask);
        }

        private void BtnDuplicateTask_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsurePermission(RolePermissionCatalog.TasksDuplicate, "Дублирование задач запрещено для вашей роли."))
            {
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
                RefreshViewsAfterTaskMutation();
                if (taskWindow.SavedTaskId.HasValue)
                {
                    HighlightTask(taskWindow.SavedTaskId.Value);
                }
            }
        }

        private void BtnDeleteTask_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsurePermission(RolePermissionCatalog.TasksManage, "Удаление задач запрещено для вашей роли."))
            {
                return;
            }

            if (selectedTask == null)
            {
                MessageBox.Show("Выберите задачу для удаления");
                return;
            }

            if (MessageBox.Show("Удалить задачу?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                facade.DeleteTask(selectedTask.TaskId);
                RefreshViewsAfterTaskMutation();
            }
        }

        private void BtnUpdateStatus_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsurePermission(RolePermissionCatalog.TasksManage, "Изменение статуса задач запрещено для вашей роли."))
            {
                return;
            }

            if (selectedTask == null)
            {
                MessageBox.Show("Выберите задачу для изменения статуса");
                return;
            }

            var statusWindow = new TaskStatusWindow(selectedTask);
            if (statusWindow.ShowDialog() == true)
            {
                RefreshViewsAfterTaskMutation();
            }
        }

        private void BtnPrintTask_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsurePermission(RolePermissionCatalog.TasksManage, "Печать задач запрещена для вашей роли."))
            {
                return;
            }

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

        private void OpenTaskEditor(Models.Task task)
        {
            if (!CanManageTasks())
            {
                MessageBox.Show("Редактирование задач запрещено для вашей роли.", "Доступ запрещен", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (task == null)
            {
                return;
            }

            var taskToEdit = facade.GetTaskById(task.TaskId) ?? task;
            var taskWindow = new TaskWindow(taskToEdit);
            if (taskWindow.ShowDialog() == true)
            {
                RefreshViewsAfterTaskMutation();
                HighlightTask(taskToEdit.TaskId);
            }
        }

        private void OpenTaskQr(Models.Task task)
        {
            if (!CanViewTasks())
            {
                MessageBox.Show("Просмотр QR-кода запрещен для вашей роли.", "Доступ запрещен", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (task == null)
            {
                return;
            }

            var qrWindow = new TaskQRCodeWindow(task.TaskId);
            qrWindow.ShowDialog();
        }

        private void SelectTask(Models.Task task)
        {
            if (task == null)
            {
                return;
            }

            selectedTask = task;
            dgTasks.SelectedItem = task;
            dgTasks.CurrentItem = task;
        }

        private bool TryGetTaskFromEventSource(object originalSource, out Models.Task task)
        {
            task = null;

            if (!(originalSource is DependencyObject dependencyObject))
            {
                return false;
            }

            var row = FindParent<DataGridRow>(dependencyObject);
            if (row?.Item is Models.Task rowTask)
            {
                task = rowTask;
                return true;
            }

            return false;
        }

        private static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            var current = child;
            while (current != null)
            {
                if (current is T match)
                {
                    return match;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private void BtnQuickNoteInList_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsurePermission(RolePermissionCatalog.TasksManage, "Добавление заметок к задаче запрещено для вашей роли."))
            {
                return;
            }

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

            var taskViewWindow = new TaskViewWindow(taskId);
            taskViewWindow.ShowDialog();
            LoadTasks();
        }

        private void LoadCalendar()
        {
            try
            {
                var selectedDate = TaskCalendar.SelectedDate ?? DateTime.Today;
                TaskCalendar.SelectedDate = selectedDate;
                LoadTasksForDate(selectedDate);
                UpdateCalendarHighlightsAsync();
                
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

            UpdateCalendarHighlightsAsync();
        }

        private void CalendarFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (TaskCalendar.SelectedDate.HasValue)
            {
                LoadTasksForDate(TaskCalendar.SelectedDate.Value);
            }

            UpdateCalendarHighlightsAsync();
        }

        private void BtnResetCalendarFilters_Click(object sender, RoutedEventArgs e)
        {
            cbCalendarSiteFilter.SelectedItem = null;
            cbCalendarCrewFilter.SelectedItem = null;
            if (TaskCalendar.SelectedDate.HasValue)
            {
                LoadTasksForDate(TaskCalendar.SelectedDate.Value);
            }

            UpdateCalendarHighlightsAsync();
        }

        private void LoadTasksForDate(DateTime date)
        {
            SelectedDateText.Text = $"Задачи на {date:dd.MM.yyyy}";
            
            var tasks = GetTasksVisibleForCurrentUser(facade.GetTasks()).Where(t => 
                t.StartDate.Date <= date.Date && t.EndDate.Date >= date.Date).ToList();

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

        private void TaskCalendar_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateCalendarHighlightsAsync();
        }

        private void TaskCalendar_DisplayDateChanged(object sender, CalendarDateChangedEventArgs e)
        {
            UpdateCalendarHighlightsAsync();
        }

        private void UpdateCalendarHighlightsAsync()
        {
            if (TaskCalendar == null)
            {
                return;
            }

            Dispatcher.BeginInvoke(new Action(UpdateCalendarHighlights), DispatcherPriority.Background);
        }

        private void UpdateCalendarHighlights()
        {
            if (TaskCalendar == null)
            {
                return;
            }

            var taskMap = BuildCalendarTaskMap();

            foreach (var button in FindVisualChildren<CalendarDayButton>(TaskCalendar))
            {
                button.ClearValue(Control.BorderBrushProperty);
                button.ClearValue(Control.BorderThicknessProperty);
                button.ClearValue(Control.ToolTipProperty);
                button.ClearValue(Control.FontWeightProperty);

                if (!(button.DataContext is DateTime day))
                {
                    continue;
                }

                if (!taskMap.TryGetValue(day.Date, out var dayTasks) || dayTasks.Count == 0)
                {
                    continue;
                }

                button.BorderBrush = calendarTaskHighlightBrush;
                button.BorderThickness = new Thickness(2);
                button.FontWeight = FontWeights.SemiBold;
                button.ToolTip = BuildCalendarTooltip(day.Date, dayTasks);
            }
        }

        private Dictionary<DateTime, List<Models.Task>> BuildCalendarTaskMap()
        {
            IEnumerable<Models.Task> tasks = GetTasksVisibleForCurrentUser(facade.GetTasks());

            if (cbCalendarSiteFilter?.SelectedItem is Site selectedSiteFilter)
            {
                tasks = tasks.Where(task => task.SiteId == selectedSiteFilter.SiteId);
            }

            if (cbCalendarCrewFilter?.SelectedItem is Crew selectedCrewFilter)
            {
                tasks = tasks.Where(task => task.CrewId.HasValue && task.CrewId.Value == selectedCrewFilter.CrewId);
            }

            var map = new Dictionary<DateTime, List<Models.Task>>();
            foreach (var task in tasks)
            {
                for (var day = task.StartDate.Date; day <= task.EndDate.Date; day = day.AddDays(1))
                {
                    if (!map.TryGetValue(day, out var dayTasks))
                    {
                        dayTasks = new List<Models.Task>();
                        map[day] = dayTasks;
                    }

                    dayTasks.Add(task);
                }
            }

            return map;
        }

        private static string BuildCalendarTooltip(DateTime date, List<Models.Task> tasks)
        {
            var titles = tasks
                .Select(task => $"• {task.Title}")
                .Take(3)
                .ToList();

            if (tasks.Count > 3)
            {
                titles.Add($"• Еще задач: {tasks.Count - 3}");
            }

            return $"Задачи на {date:dd.MM.yyyy}:{Environment.NewLine}{string.Join(Environment.NewLine, titles)}";
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
        {
            if (root == null)
            {
                yield break;
            }

            var childCount = VisualTreeHelper.GetChildrenCount(root);
            for (var index = 0; index < childCount; index++)
            {
                var child = VisualTreeHelper.GetChild(root, index);
                if (child is T match)
                {
                    yield return match;
                }

                foreach (var nestedChild in FindVisualChildren<T>(child))
                {
                    yield return nestedChild;
                }
            }
        }

        private void BtnAllTasksReport_Click(object sender, RoutedEventArgs e)
        {
            UpdateCurrentReportType("tasks");
            SelectComboBoxItemByTag(cbReportTypeSelector, currentReportType);
            GenerateSelectedReport();
        }

        private void BtnTasksBySite_Click(object sender, RoutedEventArgs e)
        {
            UpdateCurrentReportType("tasks_by_site");
            SelectComboBoxItemByTag(cbReportTypeSelector, currentReportType);
            GenerateSelectedReport();
        }

        private void BtnTasksByCrew_Click(object sender, RoutedEventArgs e)
        {
            UpdateCurrentReportType("tasks_by_crew");
            SelectComboBoxItemByTag(cbReportTypeSelector, currentReportType);
            GenerateSelectedReport();
        }

        private void BtnOverdueTasks_Click(object sender, RoutedEventArgs e)
        {
            UpdateCurrentReportType("overdue_tasks");
            SelectComboBoxItemByTag(cbReportTypeSelector, currentReportType);
            GenerateSelectedReport();
        }

        private void BtnGenerateSelectedReport_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureAnyPermission("Просмотр отчетов запрещен для вашей роли.", RolePermissionCatalog.ReportsView, RolePermissionCatalog.ReportsExport))
            {
                return;
            }

            try
            {
                GenerateSelectedReport();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка формирования отчёта: {ex.Message}");
            }
        }

        private void BtnExportSelectedReport_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsurePermission(RolePermissionCatalog.ReportsExport, "Экспорт отчетов запрещен для вашей роли."))
            {
                return;
            }

            try
            {
                switch (GetSelectedComboTag(cbReportExportFormat, "png"))
                {
                    case "csv":
                        ExportCsv_Click(sender, e);
                        break;
                    case "excel":
                        ExportExcel_Click(sender, e);
                        break;
                    case "pdf":
                        ExportPdf_Click(sender, e);
                        break;
                    default:
                        ExportChartToPng();
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта отчёта: {ex.Message}");
            }
        }

        private void ReportPreviewHost_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            DrawReportPreview();
        }

        private void LoadCurrentReportView()
        {
            if (!CanViewReports())
            {
                return;
            }

            if (cbReportTypeSelector == null || cbReportChartType == null || cbReportExportFormat == null)
            {
                return;
            }

            isUpdatingReportSelectors = true;
            SelectComboBoxItemByTag(cbReportTypeSelector, currentReportType);
            SelectComboBoxItemByTag(cbReportChartType, currentReportChartType);
            SelectComboBoxItemByTag(cbReportExportFormat, GetSelectedComboTag(cbReportExportFormat, "png"));
            isUpdatingReportSelectors = false;

            GenerateSelectedReport();
        }

        private void GenerateSelectedReport()
        {
            if (isUpdatingReportSelectors)
            {
                return;
            }

            UpdateCurrentReportType(GetSelectedComboTag(cbReportTypeSelector, currentReportType));
            currentReportChartType = GetSelectedComboTag(cbReportChartType, currentReportChartType);

            switch (currentReportType)
            {
                case "tasks_by_site":
                    ShowTasksBySiteReport();
                    break;
                case "tasks_by_crew":
                    ShowTasksByCrewReport();
                    break;
                case "overdue_tasks":
                    ShowOverdueTasksReport();
                    break;
                default:
                    ShowTasksReport();
                    break;
            }
        }

        private string GetSelectedComboTag(ComboBox comboBox, string fallback)
        {
            if (comboBox?.SelectedItem is ComboBoxItem comboBoxItem && comboBoxItem.Tag != null)
            {
                return comboBoxItem.Tag.ToString();
            }

            return fallback;
        }

        private void SelectComboBoxItemByTag(ComboBox comboBox, string tag)
        {
            if (comboBox == null || string.IsNullOrWhiteSpace(tag))
            {
                return;
            }

            foreach (var item in comboBox.Items)
            {
                if (item is ComboBoxItem comboBoxItem
                    && string.Equals(comboBoxItem.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
                {
                    comboBox.SelectedItem = comboBoxItem;
                    return;
                }
            }
        }

        private void DgReports_AutoGeneratedColumns(object sender, EventArgs e)
        {
            if (dgReports == null || dgReports.Columns.Count == 0)
            {
                return;
            }

            foreach (var column in dgReports.Columns)
            {
                var header = (column.Header?.ToString() ?? string.Empty).Replace("_", " ");
                column.Header = header;
                column.MinWidth = GetReportColumnMinWidth(header);
                column.Width = GetReportColumnWidth(header);

                if (column is DataGridTextColumn textColumn)
                {
                    textColumn.ElementStyle = CreateReportColumnStyle(GetReportColumnAlignment(header));
                }
            }
        }

        private void ShowTasksReport()
        {
            UpdateCurrentReportType("tasks");

            var tasks = facade.GetTasks();
            var rows = BuildTaskOverviewRows(tasks);
            var report = rows.Select(row => new
            {
                Название = row.Title,
                Объект = row.SiteName,
                Бригада = row.CrewName,
                Приоритет = row.PriorityName,
                Статус = row.StatusName,
                Начало = row.StartDateText,
                Окончание = row.EndDateText
            }).ToList();

            var completed = rows.Count(row => IsTaskStatus(row.StatusName, "Завершено"));
            var activeCrews = tasks.Where(t => t.CrewId.HasValue).Select(t => t.CrewId.Value).Distinct().Count();
            var statusSlices = rows
                .GroupBy(row => row.StatusName)
                .Select(g => new ReportPreviewSlice { Label = g.Key, Value = g.Count() })
                .OrderByDescending(g => g.Value)
                .ToList();

            dgReports.ItemsSource = report;
            ApplyReportPresentation(
                "Общий список задач",
                "Сводка по задачам, срокам и текущему состоянию исполнения.",
                tasks.Count.ToString(),
                "Всего задач",
                FormatPercent(completed, tasks.Count),
                "Доля завершения",
                activeCrews.ToString(),
                "Активных бригад",
                $"Срез: все задачи. В экспорт попадут {report.Count} строк.",
                statusSlices);
        }

        private void ShowTasksBySiteReport()
        {
            UpdateCurrentReportType("tasks_by_site");

            var tasks = facade.GetTasks();
            var rows = BuildTasksBySiteRows(tasks);
            var report = rows
                .Select(row => new
                {
                    Объект = row.SiteName,
                    Всего_задач = row.TotalTasks,
                    Выполнено = row.CompletedCount,
                    В_работе = row.InProgressCount,
                    Просрочено = row.OverdueCount
                })
                .ToList();

            var leader = rows.FirstOrDefault();
            var completed = rows.Sum(row => row.CompletedCount);
            var slices = rows
                .Select(row => new ReportPreviewSlice { Label = row.SiteName, Value = row.TotalTasks })
                .ToList();

            dgReports.ItemsSource = report;
            ApplyReportPresentation(
                "Задачи по объектам",
                "Показывает плотность задач по стройплощадкам и помогает быстро увидеть самый загруженный объект.",
                rows.Count.ToString(),
                "Объектов в срезе",
                leader != null ? leader.TotalTasks.ToString() : "0",
                leader != null ? $"Лидер: {leader.SiteName}" : "Лидер не определён",
                FormatPercent(completed, tasks.Count),
                "Завершено по всем объектам",
                $"Срез по объектам. В экспорт попадут {rows.Count} записей.",
                slices);
        }

        private void ShowTasksByCrewReport()
        {
            UpdateCurrentReportType("tasks_by_crew");

            var tasks = facade.GetTasks();
            var rows = BuildTasksByCrewRows(tasks);
            var report = rows
                .Select(row => new
                {
                    Бригада = row.CrewName,
                    Всего_задач = row.TotalTasks,
                    Выполнено = row.CompletedCount,
                    В_работе = row.InProgressCount,
                    Просрочено = row.OverdueCount
                })
                .ToList();

            var leader = rows.FirstOrDefault();
            var average = rows.Count > 0 ? rows.Average(row => row.TotalTasks) : 0;
            var completed = rows.Sum(row => row.CompletedCount);
            var slices = rows
                .Select(row => new ReportPreviewSlice { Label = row.CrewName, Value = row.TotalTasks })
                .ToList();

            dgReports.ItemsSource = report;
            ApplyReportPresentation(
                "Выполнение по бригадам",
                "Сводка по загрузке бригад с акцентом на равномерность распределения и фактическое исполнение.",
                rows.Count.ToString(),
                "Бригад в отчёте",
                leader != null ? leader.TotalTasks.ToString() : "0",
                leader != null ? $"Лидер: {leader.CrewName}" : "Лидер не определён",
                average > 0 ? average.ToString("0.0") : "0",
                "Среднее задач на бригаду",
                $"Срез по бригадам. В экспорт попадут {rows.Count} записей.",
                slices);
        }

        private void ShowOverdueTasksReport()
        {
            UpdateCurrentReportType("overdue_tasks");

            var overdueTasks = BuildOverdueTaskRows(facade.GetTasks(), DateTime.Now);
            var report = overdueTasks
                .Select(row => new
                {
                    Название = row.Title,
                    Объект = row.SiteName,
                    Бригада = row.CrewName,
                    Приоритет = row.PriorityName,
                    Дата_окончания = row.EndDate,
                    Просрочено_на_дней = row.DelayDays
                })
                .ToList();

            var affectedSites = overdueTasks
                .GroupBy(row => row.SiteName)
                .Select(g => new ReportPreviewSlice { Label = g.Key, Value = g.Count() })
                .OrderByDescending(g => g.Value)
                .ToList();
            var maxDelay = overdueTasks.Count > 0 ? overdueTasks.Max(row => row.DelayDays) : 0;

            dgReports.ItemsSource = report;
            ApplyReportPresentation(
                "Просроченные задачи",
                "Фокус на рисках по срокам: где скопились задержки и какой объект требует внимания в первую очередь.",
                overdueTasks.Count.ToString(),
                "Просроченных задач",
                maxDelay.ToString(),
                "Макс. задержка, дней",
                affectedSites.Count.ToString(),
                "Объектов с риском",
                $"Срез по просрочке. В экспорт попадут {overdueTasks.Count} записей.",
                affectedSites);
        }

        private List<TaskOverviewReportRow> BuildTaskOverviewRows(IEnumerable<Models.Task> tasks)
        {
            return tasks.Select(task => new TaskOverviewReportRow
            {
                Title = task.Title,
                SiteName = task.Site?.SiteName ?? "Не указан",
                CrewName = task.Crew?.CrewName ?? "Не назначена",
                PriorityName = task.Priority?.PriorityName ?? "—",
                StatusName = task.TaskStatus?.TaskStatusName ?? "Без статуса",
                StartDateText = task.StartDate.ToString("dd.MM.yyyy"),
                EndDateText = task.EndDate.ToString("dd.MM.yyyy")
            }).ToList();
        }

        private List<TasksBySiteReportRow> BuildTasksBySiteRows(IEnumerable<Models.Task> tasks)
        {
            return tasks
                .GroupBy(task => task.Site?.SiteName ?? "Не указан")
                .Select(group => new TasksBySiteReportRow
                {
                    SiteName = group.Key,
                    TotalTasks = group.Count(),
                    CompletedCount = group.Count(task => IsTaskStatus(task.TaskStatus?.TaskStatusName, "Завершено")),
                    InProgressCount = group.Count(task => IsTaskStatus(task.TaskStatus?.TaskStatusName, "В работе")),
                    OverdueCount = group.Count(task => IsTaskStatus(task.TaskStatus?.TaskStatusName, "Просрочено"))
                })
                .OrderByDescending(row => row.TotalTasks)
                .ToList();
        }

        private List<TasksByCrewReportRow> BuildTasksByCrewRows(IEnumerable<Models.Task> tasks)
        {
            return tasks
                .Where(task => task.CrewId != null)
                .GroupBy(task => task.Crew?.CrewName ?? "Не назначена")
                .Select(group => new TasksByCrewReportRow
                {
                    CrewName = group.Key,
                    TotalTasks = group.Count(),
                    CompletedCount = group.Count(task => IsTaskStatus(task.TaskStatus?.TaskStatusName, "Завершено")),
                    InProgressCount = group.Count(task => IsTaskStatus(task.TaskStatus?.TaskStatusName, "В работе")),
                    OverdueCount = group.Count(task => IsTaskStatus(task.TaskStatus?.TaskStatusName, "Просрочено"))
                })
                .OrderByDescending(row => row.TotalTasks)
                .ToList();
        }

        private List<OverdueTaskReportRow> BuildOverdueTaskRows(IEnumerable<Models.Task> tasks, DateTime now)
        {
            return tasks
                .Where(task => task.EndDate < now && !IsTaskStatus(task.TaskStatus?.TaskStatusName, "Завершено"))
                .Select(task => new OverdueTaskReportRow
                {
                    Title = task.Title,
                    SiteName = task.Site?.SiteName ?? "Не указан",
                    CrewName = task.Crew?.CrewName ?? "Не назначена",
                    PriorityName = task.Priority?.PriorityName ?? "—",
                    EndDate = task.EndDate,
                    DelayDays = (now - task.EndDate).Days
                })
                .OrderByDescending(row => row.DelayDays)
                .ToList();
        }

        private static bool IsTaskStatus(string statusName, string expectedStatusName)
        {
            return string.Equals(statusName, expectedStatusName, StringComparison.OrdinalIgnoreCase);
        }

        private void ApplyReportPresentation(
            string title,
            string subtitle,
            string primaryValue,
            string primaryLabel,
            string secondaryValue,
            string secondaryLabel,
            string tertiaryValue,
            string tertiaryLabel,
            string tableCaption,
            IEnumerable<ReportPreviewSlice> slices)
        {
            ReportTitle.Text = title;
            ReportSubtitle.Text = subtitle;
            ReportMetricPrimaryValue.Text = primaryValue;
            ReportMetricPrimaryLabel.Text = primaryLabel;
            ReportMetricSecondaryValue.Text = secondaryValue;
            ReportMetricSecondaryLabel.Text = secondaryLabel;
            ReportMetricTertiaryValue.Text = tertiaryValue;
            ReportMetricTertiaryLabel.Text = tertiaryLabel;
            ReportTableCaption.Text = tableCaption;

            SelectComboBoxItemByTag(cbReportTypeSelector, currentReportType);
            UpdateReportPreview(slices);
        }

        private void UpdateReportPreview(IEnumerable<ReportPreviewSlice> slices)
        {
            currentReportPreviewSlices.Clear();

            var palette = GetReportPreviewPalette();
            var preparedSlices = (slices ?? Enumerable.Empty<ReportPreviewSlice>())
                .Where(slice => slice != null && slice.Value > 0)
                .Take(6)
                .ToList();

            for (int i = 0; i < preparedSlices.Count; i++)
            {
                currentReportPreviewSlices.Add(new ReportPreviewSlice
                {
                    Label = preparedSlices[i].Label,
                    Value = preparedSlices[i].Value,
                    Brush = palette[i % palette.Length]
                });
            }

            UpdateLegendRow(ReportLegendRow1, ReportLegendSwatch1, ReportLegendLabel1, ReportLegendValue1, currentReportPreviewSlices.ElementAtOrDefault(0));
            UpdateLegendRow(ReportLegendRow2, ReportLegendSwatch2, ReportLegendLabel2, ReportLegendValue2, currentReportPreviewSlices.ElementAtOrDefault(1));
            UpdateLegendRow(ReportLegendRow3, ReportLegendSwatch3, ReportLegendLabel3, ReportLegendValue3, currentReportPreviewSlices.ElementAtOrDefault(2));

            if (ReportLegendPanel != null)
            {
                ReportLegendPanel.Visibility = currentReportChartType == "pie"
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }

            DrawReportPreview();
        }

        private void UpdateLegendRow(
            FrameworkElement row,
            Border swatch,
            TextBlock label,
            TextBlock value,
            ReportPreviewSlice slice)
        {
            if (row == null || swatch == null || label == null || value == null)
            {
                return;
            }

            if (slice == null)
            {
                row.Visibility = Visibility.Collapsed;
                return;
            }

            row.Visibility = Visibility.Visible;
            swatch.Background = slice.Brush;
            label.Text = slice.Label;
            value.Text = slice.Value.ToString();
        }

        private void DrawReportPreview()
        {
            if (ReportPreviewCanvas == null)
            {
                return;
            }

            ReportPreviewCanvas.Children.Clear();
            ReportPreviewHost?.UpdateLayout();

            double width = (ReportPreviewHost?.ActualWidth ?? 0) - 28;
            double height = (ReportPreviewHost?.ActualHeight ?? 0) - 28;

            if (width < 120 || height < 120)
            {
                width = 720;
                height = currentReportChartType == "pie" ? 340 : 320;
            }

            ReportPreviewCanvas.Width = width;
            ReportPreviewCanvas.Height = height;

            if (currentReportPreviewSlices.Count == 0)
            {
                DrawEmptyReportPreview(width, height);
                return;
            }

            switch (currentReportChartType)
            {
                case "pie":
                    DrawPieReportPreview(width, height);
                    break;
                case "line":
                    DrawLineReportPreview(width, height);
                    break;
                default:
                    DrawBarReportPreview(width, height);
                    break;
            }
        }

        private void DrawEmptyReportPreview(double width, double height)
        {
            var emptyText = new TextBlock
            {
                Text = "Нет данных для построения отчёта",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetThemeBrush("AppMutedTextBrush", "#ADB6C3")
            };
            emptyText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(emptyText, width / 2 - emptyText.DesiredSize.Width / 2);
            Canvas.SetTop(emptyText, height / 2 - emptyText.DesiredSize.Height / 2);
            ReportPreviewCanvas.Children.Add(emptyText);
        }

        private void DrawBarReportPreview(double width, double height)
        {
            double left = 68;
            double top = 24;
            double right = 24;
            double bottom = 64;
            double plotWidth = Math.Max(160, width - left - right);
            double plotHeight = Math.Max(160, height - top - bottom);
            double axisMax = GetChartAxisMaximum(currentReportPreviewSlices.Max(slice => slice.Value));

            DrawChartGrid(left, top, plotWidth, plotHeight, axisMax);

            double slotWidth = plotWidth / Math.Max(1, currentReportPreviewSlices.Count);
            double barWidth = Math.Min(74, Math.Max(32, slotWidth * 0.58));

            for (int i = 0; i < currentReportPreviewSlices.Count; i++)
            {
                var slice = currentReportPreviewSlices[i];
                double barHeight = axisMax <= 0 ? 0 : plotHeight * (slice.Value / axisMax);
                double x = left + i * slotWidth + (slotWidth - barWidth) / 2;
                double y = top + plotHeight - barHeight;

                var bar = new System.Windows.Shapes.Rectangle
                {
                    Width = barWidth,
                    Height = Math.Max(4, barHeight),
                    RadiusX = 8,
                    RadiusY = 8,
                    Fill = slice.Brush,
                    Opacity = 0.9
                };
                Canvas.SetLeft(bar, x);
                Canvas.SetTop(bar, y);
                ReportPreviewCanvas.Children.Add(bar);

                var valueText = new TextBlock
                {
                    Text = slice.Value.ToString(),
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = GetThemeBrush("AppTextBrush", "#F5F7FB")
                };
                valueText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(valueText, x + barWidth / 2 - valueText.DesiredSize.Width / 2);
                Canvas.SetTop(valueText, Math.Max(0, y - 18));
                ReportPreviewCanvas.Children.Add(valueText);

                var labelText = new TextBlock
                {
                    Text = TrimChartLabel(slice.Label, 18),
                    Width = Math.Max(slotWidth - 6, 48),
                    FontSize = 11,
                    Foreground = GetThemeBrush("AppMutedTextBrush", "#ADB6C3"),
                    TextAlignment = TextAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                Canvas.SetLeft(labelText, left + i * slotWidth);
                Canvas.SetTop(labelText, top + plotHeight + 10);
                ReportPreviewCanvas.Children.Add(labelText);
            }
        }

        private void DrawLineReportPreview(double width, double height)
        {
            double left = 68;
            double top = 24;
            double right = 24;
            double bottom = 64;
            double plotWidth = Math.Max(160, width - left - right);
            double plotHeight = Math.Max(160, height - top - bottom);
            double axisMax = GetChartAxisMaximum(currentReportPreviewSlices.Max(slice => slice.Value));

            DrawChartGrid(left, top, plotWidth, plotHeight, axisMax);

            double slotWidth = currentReportPreviewSlices.Count > 1
                ? plotWidth / (currentReportPreviewSlices.Count - 1)
                : plotWidth / 2;

            var polyline = new System.Windows.Shapes.Polyline
            {
                Stroke = GetThemeBrush("AppAccentBrush", "#8D6E63"),
                StrokeThickness = 3
            };

            for (int i = 0; i < currentReportPreviewSlices.Count; i++)
            {
                var slice = currentReportPreviewSlices[i];
                double x = left + (currentReportPreviewSlices.Count > 1 ? i * slotWidth : plotWidth / 2);
                double y = top + plotHeight - (axisMax <= 0 ? 0 : plotHeight * (slice.Value / axisMax));
                polyline.Points.Add(new Point(x, y));

                var point = new System.Windows.Shapes.Ellipse
                {
                    Width = 10,
                    Height = 10,
                    Fill = slice.Brush,
                    Stroke = GetThemeBrush("AppSurfaceBrush", "#1C2027"),
                    StrokeThickness = 2
                };
                Canvas.SetLeft(point, x - 5);
                Canvas.SetTop(point, y - 5);
                ReportPreviewCanvas.Children.Add(point);

                var valueText = new TextBlock
                {
                    Text = slice.Value.ToString(),
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = GetThemeBrush("AppTextBrush", "#F5F7FB")
                };
                valueText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(valueText, x - valueText.DesiredSize.Width / 2);
                Canvas.SetTop(valueText, Math.Max(0, y - 22));
                ReportPreviewCanvas.Children.Add(valueText);

                var labelText = new TextBlock
                {
                    Text = TrimChartLabel(slice.Label, 18),
                    Width = 90,
                    FontSize = 11,
                    Foreground = GetThemeBrush("AppMutedTextBrush", "#ADB6C3"),
                    TextAlignment = TextAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                Canvas.SetLeft(labelText, x - 45);
                Canvas.SetTop(labelText, top + plotHeight + 10);
                ReportPreviewCanvas.Children.Add(labelText);
            }

            ReportPreviewCanvas.Children.Insert(0, polyline);
        }

        private void DrawPieReportPreview(double width, double height)
        {
            double total = Math.Max(1, currentReportPreviewSlices.Sum(slice => slice.Value));
            double radius = Math.Max(76, Math.Min(Math.Min(width * 0.18, height * 0.30), 118));
            Point center = new Point(width / 2, height / 2);
            var callouts = new List<PieCalloutLayout>();
            double startAngle = -90;

            if (currentReportPreviewSlices.Count == 1)
            {
                var circle = new System.Windows.Shapes.Ellipse
                {
                    Width = radius * 2,
                    Height = radius * 2,
                    Fill = currentReportPreviewSlices[0].Brush
                };
                Canvas.SetLeft(circle, center.X - radius);
                Canvas.SetTop(circle, center.Y - radius);
                ReportPreviewCanvas.Children.Add(circle);
            }
            else
            {
                foreach (var slice in currentReportPreviewSlices)
                {
                    double sweepAngle = (slice.Value / total) * 360d;
                    ReportPreviewCanvas.Children.Add(CreatePieSlice(center, radius, startAngle, sweepAngle, slice.Brush));

                    double midAngle = startAngle + sweepAngle / 2d;
                    double midRadians = midAngle * Math.PI / 180d;
                    callouts.Add(new PieCalloutLayout
                    {
                        Slice = slice,
                        Side = Math.Cos(midRadians) >= 0 ? 1 : -1,
                        AnchorPoint = new Point(
                            center.X + Math.Cos(midRadians) * radius,
                            center.Y + Math.Sin(midRadians) * radius),
                        TargetY = center.Y + Math.Sin(midRadians) * (radius + 34)
                    });

                    startAngle += sweepAngle;
                }
            }

            if (currentReportPreviewSlices.Count == 1)
            {
                callouts.Add(new PieCalloutLayout
                {
                    Slice = currentReportPreviewSlices[0],
                    Side = 1,
                    AnchorPoint = new Point(center.X + radius, center.Y),
                    TargetY = center.Y
                });
            }

            ArrangePieCallouts(callouts.Where(layout => layout.Side < 0).ToList(), 28, height - 28, 44);
            ArrangePieCallouts(callouts.Where(layout => layout.Side > 0).ToList(), 28, height - 28, 44);

            foreach (var callout in callouts.OrderBy(layout => layout.TargetY))
            {
                DrawPieCallout(callout, center, radius, total, width);
            }
        }

        private void ArrangePieCallouts(List<PieCalloutLayout> callouts, double minY, double maxY, double gap)
        {
            if (callouts == null || callouts.Count == 0)
            {
                return;
            }

            callouts.Sort((left, right) => left.TargetY.CompareTo(right.TargetY));

            for (int i = 0; i < callouts.Count; i++)
            {
                if (i == 0)
                {
                    callouts[i].TargetY = Math.Max(minY, callouts[i].TargetY);
                    continue;
                }

                callouts[i].TargetY = Math.Max(callouts[i].TargetY, callouts[i - 1].TargetY + gap);
            }

            double overflow = callouts[callouts.Count - 1].TargetY - maxY;
            if (overflow > 0)
            {
                foreach (var callout in callouts)
                {
                    callout.TargetY -= overflow;
                }
            }

            if (callouts[0].TargetY < minY)
            {
                double shift = minY - callouts[0].TargetY;
                foreach (var callout in callouts)
                {
                    callout.TargetY += shift;
                }
            }
        }

        private void DrawPieCallout(PieCalloutLayout callout, Point center, double radius, double total, double width)
        {
            if (callout?.Slice == null)
            {
                return;
            }

            double textWidth = Math.Min(190, Math.Max(145, width * 0.2));
            double outerPadding = 18;
            double textLeft = callout.Side > 0 ? width - textWidth - outerPadding : outerPadding;
            double lineEndX = callout.Side > 0 ? textLeft - 10 : textLeft + textWidth + 10;
            double elbowX = center.X + callout.Side * (radius + 28);
            var connectorBrush = GetThemeBrush("AppBorderBrush", "#343C49");
            string percentText = $"{Math.Round(callout.Slice.Value * 100d / total)}%";

            var connectorStart = new System.Windows.Shapes.Line
            {
                X1 = callout.AnchorPoint.X,
                Y1 = callout.AnchorPoint.Y,
                X2 = elbowX,
                Y2 = callout.TargetY,
                Stroke = connectorBrush,
                StrokeThickness = 1.5
            };

            var connectorEnd = new System.Windows.Shapes.Line
            {
                X1 = elbowX,
                Y1 = callout.TargetY,
                X2 = lineEndX,
                Y2 = callout.TargetY,
                Stroke = connectorBrush,
                StrokeThickness = 1.5
            };

            var anchorDot = new System.Windows.Shapes.Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = callout.Slice.Brush,
                Stroke = GetThemeBrush("AppSurfaceBrush", "#1C2027"),
                StrokeThickness = 1.5
            };

            Canvas.SetLeft(anchorDot, callout.AnchorPoint.X - 4);
            Canvas.SetTop(anchorDot, callout.AnchorPoint.Y - 4);
            ReportPreviewCanvas.Children.Add(connectorStart);
            ReportPreviewCanvas.Children.Add(connectorEnd);
            ReportPreviewCanvas.Children.Add(anchorDot);

            var labelText = new TextBlock
            {
                Text = callout.Slice.Label,
                Width = textWidth,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetThemeBrush("AppTextBrush", "#F5F7FB"),
                TextAlignment = callout.Side > 0 ? TextAlignment.Left : TextAlignment.Right
            };

            labelText.Measure(new Size(textWidth, double.PositiveInfinity));

            var valueText = new TextBlock
            {
                Text = $"{callout.Slice.Value} задач, {percentText}",
                Width = textWidth,
                FontSize = 11,
                Foreground = GetThemeBrush("AppMutedTextBrush", "#ADB6C3"),
                TextAlignment = callout.Side > 0 ? TextAlignment.Left : TextAlignment.Right
            };

            valueText.Measure(new Size(textWidth, double.PositiveInfinity));

            Canvas.SetLeft(labelText, textLeft);
            Canvas.SetTop(labelText, callout.TargetY - labelText.DesiredSize.Height - 4);
            Canvas.SetLeft(valueText, textLeft);
            Canvas.SetTop(valueText, callout.TargetY + 2);

            ReportPreviewCanvas.Children.Add(labelText);
            ReportPreviewCanvas.Children.Add(valueText);
        }

        private void DrawChartGrid(double left, double top, double plotWidth, double plotHeight, double axisMax)
        {
            var axisBrush = GetThemeBrush("AppBorderBrush", "#343C49");
            var labelBrush = GetThemeBrush("AppMutedTextBrush", "#ADB6C3");

            for (int step = 0; step <= 4; step++)
            {
                double ratio = step / 4d;
                double y = top + plotHeight - plotHeight * ratio;
                var line = new System.Windows.Shapes.Line
                {
                    X1 = left,
                    X2 = left + plotWidth,
                    Y1 = y,
                    Y2 = y,
                    Stroke = axisBrush,
                    StrokeThickness = 1
                };
                ReportPreviewCanvas.Children.Add(line);

                var valueText = new TextBlock
                {
                    Text = Math.Round(axisMax * ratio).ToString("0"),
                    FontSize = 11,
                    Foreground = labelBrush
                };
                valueText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(valueText, Math.Max(0, left - valueText.DesiredSize.Width - 10));
                Canvas.SetTop(valueText, y - valueText.DesiredSize.Height / 2);
                ReportPreviewCanvas.Children.Add(valueText);
            }

            var yAxis = new System.Windows.Shapes.Line
            {
                X1 = left,
                X2 = left,
                Y1 = top,
                Y2 = top + plotHeight,
                Stroke = axisBrush,
                StrokeThickness = 1.2
            };
            var xAxis = new System.Windows.Shapes.Line
            {
                X1 = left,
                X2 = left + plotWidth,
                Y1 = top + plotHeight,
                Y2 = top + plotHeight,
                Stroke = axisBrush,
                StrokeThickness = 1.2
            };

            ReportPreviewCanvas.Children.Add(yAxis);
            ReportPreviewCanvas.Children.Add(xAxis);
        }

        private double GetChartAxisMaximum(double value)
        {
            if (value <= 5)
            {
                return 5;
            }

            var magnitude = Math.Pow(10, Math.Floor(Math.Log10(value)));
            var normalized = value / magnitude;

            if (normalized <= 1)
            {
                return 1 * magnitude;
            }

            if (normalized <= 2)
            {
                return 2 * magnitude;
            }

            if (normalized <= 5)
            {
                return 5 * magnitude;
            }

            return 10 * magnitude;
        }

        private string TrimChartLabel(string text, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length <= maxLength)
            {
                return text;
            }

            return text.Substring(0, maxLength - 1) + "…";
        }

        private System.Windows.Shapes.Path CreatePieSlice(Point center, double radius, double startAngle, double sweepAngle, Brush fill)
        {
            double startRadians = startAngle * Math.PI / 180;
            double endRadians = (startAngle + sweepAngle) * Math.PI / 180;

            var startPoint = new Point(center.X + radius * Math.Cos(startRadians), center.Y + radius * Math.Sin(startRadians));
            var endPoint = new Point(center.X + radius * Math.Cos(endRadians), center.Y + radius * Math.Sin(endRadians));

            var figure = new PathFigure
            {
                StartPoint = center,
                Segments = new PathSegmentCollection
                {
                    new LineSegment(startPoint, true),
                    new ArcSegment(endPoint, new Size(radius, radius), 0, sweepAngle > 180, SweepDirection.Clockwise, true),
                    new LineSegment(center, true)
                },
                IsClosed = true
            };

            return new System.Windows.Shapes.Path
            {
                Data = new PathGeometry(new[] { figure }),
                Fill = fill,
                Stroke = GetThemeBrush("AppSurfaceBrush", "#1C2027"),
                StrokeThickness = 2
            };
        }

        private Brush[] GetReportPreviewPalette()
        {
            return new[]
            {
                GetThemeBrush("AppChartBrush1", "#8D6E63"),
                GetThemeBrush("AppChartBrush2", "#B88B5B"),
                GetThemeBrush("AppChartBrush3", "#A9877D"),
                GetThemeBrush("AppChartBrush4", "#6A5148"),
                GetThemeBrush("AppChartBrush5", "#8E765E"),
                GetThemeBrush("AppChartBrush6", "#C7A88D")
            };
        }

        private SolidColorBrush CreateBrush(string colorHex)
        {
            return (SolidColorBrush)new BrushConverter().ConvertFrom(colorHex);
        }

        private SolidColorBrush GetThemeBrush(string resourceKey, string fallbackHex)
        {
            return AppThemeManager.ResolveBrush(resourceKey, fallbackHex);
        }

        private string FormatPercent(int value, int total)
        {
            if (total <= 0)
            {
                return "0%";
            }

            return $"{Math.Round((double)value / total * 100)}%";
        }

        private DataGridLength GetReportColumnWidth(string header)
        {
            if (header == "Название")
            {
                return new DataGridLength(1.7, DataGridLengthUnitType.Star);
            }

            if (header.Contains("Объект") || header.Contains("Бригада"))
            {
                return new DataGridLength(1.25, DataGridLengthUnitType.Star);
            }

            if (header.Contains("Дата") || header.Contains("Начало") || header.Contains("Окончание"))
            {
                return new DataGridLength(0.95, DataGridLengthUnitType.Star);
            }

            return new DataGridLength(1, DataGridLengthUnitType.Star);
        }

        private double GetReportColumnMinWidth(string header)
        {
            if (header == "Название")
            {
                return 190;
            }

            if (header.Contains("Объект") || header.Contains("Бригада"))
            {
                return 145;
            }

            return 105;
        }

        private TextAlignment GetReportColumnAlignment(string header)
        {
            if (header.Contains("Всего")
                || header.Contains("Выполнено")
                || header.Contains("Просрочено")
                || header.Contains("В работе")
                || header.IndexOf("дней", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return TextAlignment.Center;
            }

            return TextAlignment.Left;
        }

        private Style CreateReportColumnStyle(TextAlignment alignment)
        {
            var style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.NoWrap));
            style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
            style.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, alignment));
            return style;
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

        private void ExportChartToPng()
        {
            if (ReportPreviewCanvas == null || currentReportPreviewSlices.Count == 0)
            {
                MessageBox.Show("Нет данных для экспорта изображения.");
                return;
            }

            ReportPreviewCanvas.UpdateLayout();

            double canvasWidth = ReportPreviewCanvas.ActualWidth > 1 ? ReportPreviewCanvas.ActualWidth : ReportPreviewCanvas.Width;
            double canvasHeight = ReportPreviewCanvas.ActualHeight > 1 ? ReportPreviewCanvas.ActualHeight : ReportPreviewCanvas.Height;

            if (canvasWidth < 1 || canvasHeight < 1)
            {
                MessageBox.Show("Нет данных для экспорта изображения.");
                return;
            }

            string folderPath = GetReportsFolderPath();
            string fileName = $"report_{currentReportType}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            string path = Path.Combine(folderPath, fileName);
            double exportWidth = Math.Ceiling(canvasWidth + 28);
            double exportHeight = Math.Ceiling(canvasHeight + 28);

            var renderTarget = new RenderTargetBitmap(
                (int)exportWidth,
                (int)exportHeight,
                96,
                96,
                PixelFormats.Pbgra32);

            var drawingVisual = new DrawingVisual();
            using (var drawingContext = drawingVisual.RenderOpen())
            {
                drawingContext.DrawRoundedRectangle(
                    GetThemeBrush("AppSurfaceBrush", "#1C2027"),
                    new Pen(GetThemeBrush("AppBorderBrush", "#343C49"), 1),
                    new Rect(0, 0, exportWidth, exportHeight),
                    18,
                    18);

                var canvasBrush = new VisualBrush(ReportPreviewCanvas)
                {
                    Stretch = Stretch.None,
                    AlignmentX = AlignmentX.Left,
                    AlignmentY = AlignmentY.Top
                };

                drawingContext.DrawRectangle(canvasBrush, null, new Rect(14, 14, canvasWidth, canvasHeight));
            }

            renderTarget.Render(drawingVisual);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(renderTarget));

            using (var stream = File.Create(path))
            {
                encoder.Save(stream);
            }

            OfferToOpenExportedFile(path, "PNG");
        }

        private void OfferToOpenExportedFile(string path, string fileLabel)
        {
            var result = MessageBox.Show(
                $"{fileLabel} сохранён:\n{path}\n\nОткрыть файл сейчас?",
                "Экспорт завершён",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(path)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Файл сохранён, но открыть его не удалось: {ex.Message}",
                    "Экспорт завершён",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void UpdateCurrentReportType(string reportType)
        {
            currentReportType = reportType;
        }

        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            ExportDelimitedReport("csv", "CSV", ";");
        }

        private void ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            ExportDelimitedReport("xls", "Excel", "\t");
        }

        private void ExportPdf_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var export = BuildTextReportExport();
                var path = BuildReportFilePath(export.ReportName, "txt");
                File.WriteAllLines(path, export.Lines, System.Text.Encoding.UTF8);
                OfferToOpenExportedFile(path, "Файл отчёта");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта: {ex.Message}");
            }
        }

        private void ExportDelimitedReport(string extension, string fileLabel, string separator)
        {
            try
            {
                var export = BuildDelimitedReportExport(separator);
                var path = BuildReportFilePath(export.ReportName, extension);
                File.WriteAllLines(path, export.Lines, System.Text.Encoding.UTF8);
                OfferToOpenExportedFile(path, fileLabel);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта {fileLabel}: {ex.Message}");
            }
        }

        private ReportFileExport BuildDelimitedReportExport(string separator)
        {
            var tasks = facade.GetTasks();

            switch (currentReportType)
            {
                case "tasks_by_site":
                    return new ReportFileExport("tasks_by_site", BuildTasksBySiteDelimitedLines(BuildTasksBySiteRows(tasks), separator));
                case "tasks_by_crew":
                    return new ReportFileExport("tasks_by_crew", BuildTasksByCrewDelimitedLines(BuildTasksByCrewRows(tasks), separator));
                case "overdue_tasks":
                    return new ReportFileExport("overdue_tasks", BuildOverdueTaskDelimitedLines(BuildOverdueTaskRows(tasks, DateTime.Now), separator));
                default:
                    return new ReportFileExport("tasks", BuildTaskOverviewDelimitedLines(BuildTaskOverviewRows(tasks), separator));
            }
        }

        private ReportFileExport BuildTextReportExport()
        {
            var tasks = facade.GetTasks();

            switch (currentReportType)
            {
                case "tasks_by_site":
                    return new ReportFileExport("tasks_by_site", BuildTasksBySiteTextLines(BuildTasksBySiteRows(tasks)));
                case "tasks_by_crew":
                    return new ReportFileExport("tasks_by_crew", BuildTasksByCrewTextLines(BuildTasksByCrewRows(tasks)));
                case "overdue_tasks":
                    return new ReportFileExport("overdue_tasks", BuildOverdueTaskTextLines(BuildOverdueTaskRows(tasks, DateTime.Now)));
                default:
                    return new ReportFileExport("tasks", BuildTaskOverviewTextLines(BuildTaskOverviewRows(tasks)));
            }
        }

        private List<string> BuildTaskOverviewDelimitedLines(IEnumerable<TaskOverviewReportRow> rows, string separator)
        {
            var lines = new List<string>
            {
                JoinReportLine(separator, "Название", "Объект", "Бригада", "Приоритет", "Статус", "Начало", "Окончание")
            };

            lines.AddRange(rows.Select(row => JoinReportLine(
                separator,
                row.Title,
                row.SiteName,
                row.CrewName,
                row.PriorityName,
                row.StatusName,
                row.StartDateText,
                row.EndDateText)));

            return lines;
        }

        private List<string> BuildTasksBySiteDelimitedLines(IEnumerable<TasksBySiteReportRow> rows, string separator)
        {
            var lines = new List<string>
            {
                JoinReportLine(separator, "Объект", "Всего задач", "Выполнено", "В работе", "Просрочено")
            };

            lines.AddRange(rows.Select(row => JoinReportLine(
                separator,
                row.SiteName,
                row.TotalTasks.ToString(),
                row.CompletedCount.ToString(),
                row.InProgressCount.ToString(),
                row.OverdueCount.ToString())));

            return lines;
        }

        private List<string> BuildTasksByCrewDelimitedLines(IEnumerable<TasksByCrewReportRow> rows, string separator)
        {
            var lines = new List<string>
            {
                JoinReportLine(separator, "Бригада", "Всего задач", "Выполнено", "В работе", "Просрочено")
            };

            lines.AddRange(rows.Select(row => JoinReportLine(
                separator,
                row.CrewName,
                row.TotalTasks.ToString(),
                row.CompletedCount.ToString(),
                row.InProgressCount.ToString(),
                row.OverdueCount.ToString())));

            return lines;
        }

        private List<string> BuildOverdueTaskDelimitedLines(IEnumerable<OverdueTaskReportRow> rows, string separator)
        {
            var lines = new List<string>
            {
                JoinReportLine(separator, "Название", "Объект", "Бригада", "Приоритет", "Дата окончания", "Просрочено на дней")
            };

            lines.AddRange(rows.Select(row => JoinReportLine(
                separator,
                row.Title,
                row.SiteName,
                row.CrewName,
                row.PriorityName,
                row.EndDate.ToString("dd.MM.yyyy"),
                row.DelayDays.ToString())));

            return lines;
        }

        private List<string> BuildTaskOverviewTextLines(IEnumerable<TaskOverviewReportRow> rows)
        {
            var lines = CreateTextReportHeader("ОТЧЁТ ПО ЗАДАЧАМ");
            lines.AddRange(rows.Select(row =>
                $"• {row.Title} | {row.SiteName} | {row.CrewName} | {row.PriorityName} | {row.StatusName} | {row.StartDateText}-{row.EndDateText}"));
            return lines;
        }

        private List<string> BuildTasksBySiteTextLines(IEnumerable<TasksBySiteReportRow> rows)
        {
            var lines = CreateTextReportHeader("ОТЧЁТ: ЗАДАЧИ ПО ОБЪЕКТАМ");

            foreach (var row in rows)
            {
                lines.Add($"• Объект: {row.SiteName}");
                lines.Add($"  Всего задач: {row.TotalTasks}");
                lines.Add($"  Выполнено: {row.CompletedCount}");
                lines.Add($"  В работе: {row.InProgressCount}");
                lines.Add($"  Просрочено: {row.OverdueCount}\n");
            }

            return lines;
        }

        private List<string> BuildTasksByCrewTextLines(IEnumerable<TasksByCrewReportRow> rows)
        {
            var lines = CreateTextReportHeader("ОТЧЁТ: ВЫПОЛНЕНИЕ ПО БРИГАДАМ");

            foreach (var row in rows)
            {
                lines.Add($"• Бригада: {row.CrewName}");
                lines.Add($"  Всего задач: {row.TotalTasks}");
                lines.Add($"  Выполнено: {row.CompletedCount}");
                lines.Add($"  В работе: {row.InProgressCount}");
                lines.Add($"  Просрочено: {row.OverdueCount}\n");
            }

            return lines;
        }

        private List<string> BuildOverdueTaskTextLines(IEnumerable<OverdueTaskReportRow> rows)
        {
            var lines = CreateTextReportHeader("ОТЧЁТ: ПРОСРОЧЕННЫЕ ЗАДАЧИ");

            foreach (var row in rows)
            {
                lines.Add($"• {row.Title}");
                lines.Add($"  Объект: {row.SiteName}");
                lines.Add($"  Бригада: {row.CrewName}");
                lines.Add($"  Приоритет: {row.PriorityName}");
                lines.Add($"  Дата окончания: {row.EndDate:dd.MM.yyyy}");
                lines.Add($"  Просрочено на дней: {row.DelayDays}\n");
            }

            return lines;
        }

        private static List<string> CreateTextReportHeader(string reportTitle)
        {
            return new List<string>
            {
                $"=== {reportTitle} ===\n",
                $"Дата формирования: {DateTime.Now:dd.MM.yyyy HH:mm}\n"
            };
        }

        private static string JoinReportLine(string separator, params string[] values)
        {
            return string.Join(separator, values.Select(value => value ?? string.Empty));
        }

        private string BuildReportFilePath(string reportName, string extension)
        {
            var folderPath = GetReportsFolderPath();
            var fileName = $"{reportName}_{DateTime.Now:yyyyMMdd_HHmmss}.{extension}";
            return Path.Combine(folderPath, fileName);
        }

        private bool EnsureRoleManagementAccess()
        {
            if (IsAdminRole(GetCurrentRoleNormalized()))
            {
                return true;
            }

            MessageBox.Show("Управление пользователями доступно только администратору.", "Доступ запрещен", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        private static bool CanEditRolePermissions(string roleName)
        {
            var normalizedRole = RolePermissionCatalog.NormalizeRoleName(roleName);
            return normalizedRole == "диспетчер"
                || normalizedRole == "dispatcher"
                || normalizedRole == "бригадир"
                || normalizedRole == "foreman";
        }

        private bool IsCurrentSessionUser(User user)
        {
            return LoginWindow.CurrentUser != null &&
                   user != null &&
                   LoginWindow.CurrentUser.UserId == user.UserId;
        }

        private bool CanDeleteRoleManagementUser(User user, IEnumerable<User> users, out string restrictionMessage)
        {
            if (user == null)
            {
                restrictionMessage = "Сначала выберите пользователя.";
                return false;
            }

            if (IsCurrentSessionUser(user))
            {
                restrictionMessage = "Нельзя удалить собственную учетную запись.";
                return false;
            }

            var activeUsers = (users ?? Enumerable.Empty<User>()).ToList();
            var activeAdminCount = activeUsers.Count(candidate =>
                IsAdminRole(RolePermissionCatalog.NormalizeRoleName(candidate.Role)));

            if (IsAdminRole(RolePermissionCatalog.NormalizeRoleName(user.Role)) && activeAdminCount <= 1)
            {
                restrictionMessage = "Нельзя удалить последнего администратора.";
                return false;
            }

            restrictionMessage = null;
            return true;
        }

        private void SelectRoleManagementUserByUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username) || !(dgRoleUsers.ItemsSource is IEnumerable<User> users))
            {
                return;
            }

            var createdUser = users.FirstOrDefault(user =>
                string.Equals(user.Username, username, StringComparison.OrdinalIgnoreCase));

            if (createdUser == null)
            {
                return;
            }

            dgRoleUsers.SelectedItem = createdUser;
            dgRoleUsers.ScrollIntoView(createdUser);
            selectedRoleManagementUser = createdUser;
            cbRoleAssignment.SelectedValue = createdUser.RoleId;
            UpdateRoleManagementSummary();
        }

        private void UpdateRoleManagementView()
        {
            if (RoleAssignmentsPanel == null ||
                RolePermissionsPanel == null ||
                btnRoleAssignmentsView == null ||
                btnRolePermissionsView == null)
            {
                return;
            }

            RoleAssignmentsPanel.Visibility = isRolePermissionsViewActive ? Visibility.Collapsed : Visibility.Visible;
            RolePermissionsPanel.Visibility = isRolePermissionsViewActive ? Visibility.Visible : Visibility.Collapsed;

            btnRoleAssignmentsView.Style = (Style)FindResource(
                isRolePermissionsViewActive ? "MaterialDesignOutlinedButton" : "MaterialDesignRaisedButton");
            btnRolePermissionsView.Style = (Style)FindResource(
                isRolePermissionsViewActive ? "MaterialDesignRaisedButton" : "MaterialDesignOutlinedButton");
        }

        private void BtnRoleAssignmentsView_Click(object sender, RoutedEventArgs e)
        {
            isRolePermissionsViewActive = false;
            UpdateRoleManagementView();
        }

        private void BtnRolePermissionsView_Click(object sender, RoutedEventArgs e)
        {
            isRolePermissionsViewActive = true;
            UpdateRoleManagementView();
        }

        private void LoadRoleManagementData()
        {
            if (!EnsureRoleManagementAccess())
            {
                return;
            }

            var selectedUserId = selectedRoleManagementUser?.UserId;
            var selectedRoleId = selectedRoleManagementRole?.RoleId;

            var users = facade.GetUsers()
                .OrderBy(user => user.FullName ?? user.Username)
                .ToList();
            var roles = facade.GetRoles()
                .OrderBy(role => role.RoleName)
                .ToList();
            var editableRoles = roles
                .Where(role => CanEditRolePermissions(role.RoleName))
                .ToList();

            cbRoleAssignment.SelectedValuePath = "RoleId";
            cbRoleAssignment.ItemsSource = roles;
            dgRoleUsers.ItemsSource = users;
            dgRoles.ItemsSource = editableRoles;

            dgRoleUsers.SelectedItem = users.FirstOrDefault(user => user.UserId == selectedUserId) ?? users.FirstOrDefault();
            dgRoles.SelectedItem = editableRoles.FirstOrDefault(role => role.RoleId == selectedRoleId) ?? editableRoles.FirstOrDefault();

            selectedRoleManagementUser = dgRoleUsers.SelectedItem as User;
            selectedRoleManagementRole = dgRoles.SelectedItem as Role;

            if (selectedRoleManagementUser == null)
            {
                cbRoleAssignment.SelectedItem = null;
            }
            else
            {
                cbRoleAssignment.SelectedValue = selectedRoleManagementUser.RoleId;
            }

            if (selectedRoleManagementRole == null)
            {
                rolePermissionItems = new List<RolePermissionItem>();
                dgRolePermissions.ItemsSource = rolePermissionItems;
            }
            else
            {
                var grantedPermissions = new HashSet<string>(
                    facade.GetRolePermissionCodes(selectedRoleManagementRole.RoleId),
                    StringComparer.OrdinalIgnoreCase);

                rolePermissionItems = RolePermissionCatalog.Definitions
                    .Select(definition => new RolePermissionItem
                    {
                        Code = definition.Code,
                        Category = definition.Category,
                        DisplayName = definition.DisplayName,
                        Description = definition.Description,
                        IsGranted = grantedPermissions.Contains(definition.Code)
                    })
                    .ToList();

                dgRolePermissions.ItemsSource = rolePermissionItems;
            }

            UpdateRoleManagementView();
            UpdateRoleManagementSummary(users, editableRoles);
        }

        private void UpdateRoleManagementSummary(IEnumerable<User> users = null, IEnumerable<Role> roles = null)
        {
            if (txtRoleUsersSummary == null ||
                txtRoleAssignmentSummary == null ||
                txtRoleAccessOverview == null ||
                txtRolePermissionSummary == null ||
                txtRoleCreationInfo == null ||
                txtRoleCatalogSummary == null ||
                btnAddManagedUser == null ||
                btnEditManagedUser == null ||
                btnDeleteManagedUser == null)
            {
                return;
            }

            var userList = (users ?? Enumerable.Empty<User>()).ToList();
            var roleList = (roles ?? Enumerable.Empty<Role>()).ToList();

            if (userList.Count == 0 && dgRoleUsers.ItemsSource is IEnumerable<User> currentUsers)
            {
                userList = currentUsers.ToList();
            }

            if (roleList.Count == 0 && dgRoles.ItemsSource is IEnumerable<Role> currentRoles)
            {
                roleList = currentRoles.ToList();
            }

            txtRoleUsersSummary.Text = userList.Count == 0
                ? "Активных пользователей нет"
                : $"Активных пользователей: {userList.Count}";

            txtRoleCatalogSummary.Text = string.Empty;

            var isEditingOwnRole = IsCurrentSessionUser(selectedRoleManagementUser);
            var canDeleteSelectedUser = CanDeleteRoleManagementUser(selectedRoleManagementUser, userList, out var deleteRestriction);

            if (selectedRoleManagementUser == null)
            {
                txtSelectedRoleUserInfo.Text = "Добавьте нового пользователя или выберите пользователя слева для редактирования или удаления.";
                txtRoleAssignmentSummary.Text = userList.Count == 0
                    ? "Список пользователей пуст"
                    : "Пользователь не выбран";
            }
            else
            {
                var currentRoleName = selectedRoleManagementUser.Role ?? "не назначена";

                txtSelectedRoleUserInfo.Text =
                    $"Логин: {selectedRoleManagementUser.Username}\n" +
                    $"Имя: {selectedRoleManagementUser.DisplayName}\n" +
                    $"Роль: {currentRoleName}";

                if (isEditingOwnRole)
                {
                    txtSelectedRoleUserInfo.Text += "\nСвою роль в текущем сеансе менять нельзя.";
                }
                else if (!canDeleteSelectedUser && !string.IsNullOrWhiteSpace(deleteRestriction))
                {
                    txtSelectedRoleUserInfo.Text += $"\n{deleteRestriction}";
                }

                txtRoleAssignmentSummary.Text = $"Выбран пользователь: {selectedRoleManagementUser.DisplayName}";
            }

            btnApplyUserRole.IsEnabled = false;
            btnAddManagedUser.IsEnabled = true;
            btnEditManagedUser.IsEnabled = selectedRoleManagementUser != null;
            btnDeleteManagedUser.IsEnabled = canDeleteSelectedUser;

            if (selectedRoleManagementRole == null)
            {
                txtSelectedRoleInfo.Text = "Права роли";
                txtRolePermissionSummary.Text = string.Empty;
                txtRoleAccessOverview.Text = string.Empty;
                txtRoleCreationInfo.Text = string.Empty;
                btnSaveRolePermissions.IsEnabled = false;
                btnCreateRole.IsEnabled = false;
                return;
            }

            var grantedCount = rolePermissionItems.Count(item => item.IsGranted);
            var totalCount = rolePermissionItems.Count;

            txtSelectedRoleInfo.Text = totalCount == 0
                ? $"Права роли: {selectedRoleManagementRole.RoleName}"
                : $"Права роли: {selectedRoleManagementRole.RoleName} ({grantedCount}/{totalCount})";
            txtRolePermissionSummary.Text = string.Empty;
            txtRoleAccessOverview.Text = string.Empty;
            txtRoleCreationInfo.Text = string.Empty;
            btnSaveRolePermissions.IsEnabled = true;
            btnCreateRole.IsEnabled = false;
        }

        private static string GetRoleManagementPlural(int number, string one, string few, string many)
        {
            var remainder100 = number % 100;
            if (remainder100 >= 11 && remainder100 <= 14)
            {
                return many;
            }

            switch (number % 10)
            {
                case 1:
                    return one;
                case 2:
                case 3:
                case 4:
                    return few;
                default:
                    return many;
            }
        }

        private void DgRoleUsers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            selectedRoleManagementUser = dgRoleUsers.SelectedItem as User;

            if (selectedRoleManagementUser == null)
            {
                cbRoleAssignment.SelectedItem = null;
                UpdateRoleManagementSummary();
                return;
            }

            cbRoleAssignment.SelectedValue = selectedRoleManagementUser.RoleId;
            UpdateRoleManagementSummary();
        }

        private void CbRoleAssignment_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateRoleManagementSummary();
        }

        private void TxtNewRoleName_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateRoleManagementSummary();
        }

        private void DgRoles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            selectedRoleManagementRole = dgRoles.SelectedItem as Role;

            if (selectedRoleManagementRole == null)
            {
                rolePermissionItems = new List<RolePermissionItem>();
                dgRolePermissions.ItemsSource = rolePermissionItems;
                UpdateRoleManagementSummary();
                return;
            }

            var grantedPermissions = new HashSet<string>(
                facade.GetRolePermissionCodes(selectedRoleManagementRole.RoleId),
                StringComparer.OrdinalIgnoreCase);

            rolePermissionItems = RolePermissionCatalog.Definitions
                .Select(definition => new RolePermissionItem
                {
                    Code = definition.Code,
                    Category = definition.Category,
                    DisplayName = definition.DisplayName,
                    Description = definition.Description,
                    IsGranted = grantedPermissions.Contains(definition.Code)
                })
                .ToList();

            dgRolePermissions.ItemsSource = rolePermissionItems;
            UpdateRoleManagementSummary();
        }

        private void DgRolePermissions_CurrentCellChanged(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() => UpdateRoleManagementSummary()));
        }

        private void BtnAddManagedUser_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureRoleManagementAccess())
            {
                return;
            }

            try
            {
                var registerWindow = new RegisterWindow(true);
                var ownerWindow = Window.GetWindow(this);
                if (ownerWindow != null)
                {
                    registerWindow.Owner = ownerWindow;
                }

                if (registerWindow.ShowDialog() != true)
                {
                    return;
                }

                LoadRoleManagementData();
                LoadCrews();
                SelectRoleManagementUserByUsername(registerWindow.CreatedUsername);
                MessageBox.Show("Пользователь добавлен.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка добавления пользователя: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnEditManagedUser_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureRoleManagementAccess())
            {
                return;
            }

            if (selectedRoleManagementUser == null)
            {
                MessageBox.Show("Выберите пользователя.");
                return;
            }

            try
            {
                var editWindow = new RegisterWindow(true, new User
                {
                    UserId = selectedRoleManagementUser.UserId,
                    Username = selectedRoleManagementUser.Username,
                    FullName = selectedRoleManagementUser.FullName,
                    Password = selectedRoleManagementUser.Password,
                    RoleId = selectedRoleManagementUser.RoleId,
                    Role = selectedRoleManagementUser.Role
                });

                var ownerWindow = Window.GetWindow(this);
                if (ownerWindow != null)
                {
                    editWindow.Owner = ownerWindow;
                }

                if (editWindow.ShowDialog() != true)
                {
                    return;
                }

                if (editWindow.SavedUser != null &&
                    LoginWindow.CurrentUser != null &&
                    editWindow.SavedUser.UserId == LoginWindow.CurrentUser.UserId)
                {
                    LoginWindow.CurrentUser.Username = editWindow.SavedUser.Username;
                    LoginWindow.CurrentUser.FullName = editWindow.SavedUser.FullName;
                    LoginWindow.CurrentUser.Password = editWindow.SavedUser.Password;
                    LoginWindow.CurrentUser.RoleId = editWindow.SavedUser.RoleId;
                    LoginWindow.CurrentUser.Role = editWindow.SavedUser.Role;
                    roleOverride = null;
                    LoadCurrentPermissions();
                    SetupPermissions();
                    UpdateUserInfo();
                }

                LoadRoleManagementData();
                LoadCrews();
                SelectRoleManagementUserByUsername(editWindow.CreatedUsername);
                MessageBox.Show("Данные пользователя сохранены.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка редактирования пользователя: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnDeleteManagedUser_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureRoleManagementAccess())
            {
                return;
            }

            var users = (dgRoleUsers.ItemsSource as IEnumerable<User>)?.ToList() ?? facade.GetUsers();
            if (!CanDeleteRoleManagementUser(selectedRoleManagementUser, users, out var restrictionMessage))
            {
                MessageBox.Show(restrictionMessage, "Удаление пользователя", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var userName = selectedRoleManagementUser.DisplayName;
            var confirmResult = MessageBox.Show(
                $"Удалить пользователя \"{userName}\"?\nПользователь будет скрыт из активного списка и не сможет войти в систему.",
                "Удаление пользователя",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmResult != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                facade.DeleteUser(selectedRoleManagementUser.UserId);
                selectedRoleManagementUser = null;
                LoadRoleManagementData();
                LoadCrews();
                MessageBox.Show("Пользователь удален.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка удаления пользователя: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnApplyUserRole_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureRoleManagementAccess())
            {
                return;
            }

            if (selectedRoleManagementUser == null)
            {
                MessageBox.Show("Выберите пользователя.");
                return;
            }

            if (!(cbRoleAssignment.SelectedItem is Role role))
            {
                MessageBox.Show("Выберите роль для назначения.");
                return;
            }

            if (selectedRoleManagementUser.RoleId == role.RoleId)
            {
                MessageBox.Show("У пользователя уже назначена выбранная роль.");
                return;
            }

            if (LoginWindow.CurrentUser != null && selectedRoleManagementUser.UserId == LoginWindow.CurrentUser.UserId)
            {
                MessageBox.Show("Нельзя менять собственную роль в текущем сеансе.");
                return;
            }

            try
            {
                facade.UpdateUserRole(selectedRoleManagementUser.UserId, role.RoleId);
                MessageBox.Show("Роль пользователя обновлена.");
                LoadRoleManagementData();
                LoadCrews();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка назначения роли: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCreateRole_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureRoleManagementAccess())
            {
                return;
            }

            var roleName = (txtNewRoleName.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(roleName))
            {
                MessageBox.Show("Введите название роли.");
                return;
            }

            if (selectedRoleManagementRole == null)
            {
                MessageBox.Show("Сначала выберите роль-шаблон.");
                return;
            }

            var permissionCodes = rolePermissionItems
                .Where(item => item.IsGranted)
                .Select(item => item.Code)
                .ToList();

            try
            {
                var createdRoleId = facade.CreateRole(roleName, permissionCodes);
                txtNewRoleName.Text = string.Empty;
                MessageBox.Show("Роль создана.");
                LoadRoleManagementData();

                if (dgRoles.ItemsSource is IEnumerable<Role> roles)
                {
                    dgRoles.SelectedItem = roles.FirstOrDefault(role => role.RoleId == createdRoleId);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка создания роли: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSaveRolePermissions_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureRoleManagementAccess())
            {
                return;
            }

            if (selectedRoleManagementRole == null)
            {
                MessageBox.Show("Выберите роль для настройки прав.");
                return;
            }

            try
            {
                var permissionCodes = rolePermissionItems
                    .Where(item => item.IsGranted)
                    .Select(item => item.Code)
                    .ToList();

                facade.UpdateRolePermissions(selectedRoleManagementRole.RoleId, permissionCodes);

                if (string.Equals(selectedRoleManagementRole.RoleName, GetCurrentRole(), StringComparison.OrdinalIgnoreCase))
                {
                    SetupPermissions();
                    RolesMenuItem.IsSelected = true;
                }

                MessageBox.Show("Права роли сохранены.");
                LoadRoleManagementData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения прав роли: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnReloadRoleManagement_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureRoleManagementAccess())
            {
                return;
            }

            LoadRoleManagementData();
        }

        private void LoadMaterialRequests()
        {
            if (!CanViewMaterialRequests())
            {
                if (materialRequests != null)
                {
                    materialRequests.Clear();
                }

                if (cardMRDetailsPanel != null)
                {
                    cardMRDetailsPanel.Visibility = Visibility.Collapsed;
                }

                return;
            }

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
            var canManageMaterialRequests = CanManageMaterialRequests();

            if (selectedMaterialRequest == null)
            {
                btnMRApprove.IsEnabled = false;
                btnMRReject.IsEnabled = false;
                btnMRIssue.IsEnabled = false;
                btnMRDeliver.IsEnabled = false;
                btnMRClose.IsEnabled = false;
                btnMREdit.IsEnabled = false;
                btnMRAddRequest.IsEnabled = canManageMaterialRequests;
                btnMRExport.IsEnabled = CanViewMaterialRequests();
                return;
            }

            btnMRApprove.IsEnabled = canManageMaterialRequests && selectedMaterialRequest.Status == "Submitted";
            btnMRReject.IsEnabled = canManageMaterialRequests && selectedMaterialRequest.Status == "Submitted";
            btnMRIssue.IsEnabled = canManageMaterialRequests && selectedMaterialRequest.Status == "Approved";
            btnMRDeliver.IsEnabled = canManageMaterialRequests && selectedMaterialRequest.Status == "Issued";
            btnMRClose.IsEnabled = canManageMaterialRequests && selectedMaterialRequest.Status == "Delivered";
            btnMREdit.IsEnabled = canManageMaterialRequests && (selectedMaterialRequest.Status == "Draft" || selectedMaterialRequest.Status == "Submitted" || selectedMaterialRequest.Status == "Approved");
            btnMRAddRequest.IsEnabled = canManageMaterialRequests;
            btnMRExport.IsEnabled = CanViewMaterialRequests();
        }

        private void BtnMRApprove_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsurePermission(RolePermissionCatalog.MaterialRequestsManage, "Согласование заявок на материалы запрещено для вашей роли."))
            {
                return;
            }

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
            if (!EnsurePermission(RolePermissionCatalog.MaterialRequestsManage, "Отклонение заявок на материалы запрещено для вашей роли."))
            {
                return;
            }

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
            if (!EnsurePermission(RolePermissionCatalog.MaterialRequestsManage, "Выдача материалов запрещена для вашей роли."))
            {
                return;
            }

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
            if (!EnsurePermission(RolePermissionCatalog.MaterialRequestsManage, "Отметка доставки материалов запрещена для вашей роли."))
            {
                return;
            }

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
            if (!EnsurePermission(RolePermissionCatalog.MaterialRequestsManage, "Закрытие заявок на материалы запрещено для вашей роли."))
            {
                return;
            }

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
            if (!EnsurePermission(RolePermissionCatalog.MaterialRequestsManage, "Редактирование заявок на материалы запрещено для вашей роли."))
            {
                return;
            }

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
            if (!EnsurePermission(RolePermissionCatalog.MaterialRequestsManage, "Создание заявок на материалы запрещено для вашей роли."))
            {
                return;
            }

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
            if (!EnsureAnyPermission("Просмотр реестра заявок на материалы запрещен для вашей роли.", RolePermissionCatalog.MaterialRequestsView, RolePermissionCatalog.MaterialRequestsManage))
            {
                return;
            }

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

        // =====================================================================
        // ПЛАН НА ДЕНЬ — логика
        // =====================================================================

        private void LoadDailyPlan()
        {
            try
            {
                isUpdatingDailyPlanDate = true;
                dpDailyPlanDate.SelectedDate = dailyPlanDate;
                isUpdatingDailyPlanDate = false;

                // Загрузить список бригад
                dailyPlanCrews = facade.GetCrews();
                cbPlanCrewSelect.ItemsSource = dailyPlanCrews;

                // Загрузить сохранённый план
                currentDailyPlan = facade.GetDailyPlanByDate(dailyPlanDate);

                // Построить группы бригад из плана
                RebuildPlanCrewGroups();

                // Построить список доступных задач
                RefreshAvailablePlanTasks();

                // Обновить статус
                UpdateDailyPlanStatusDisplay();

                // Скрыть панель добавления
                pnlAddToPlan.Visibility = Visibility.Collapsed;
                selectedPlanTaskId = -1;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки плана: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RebuildPlanCrewGroups()
        {
            dailyPlanCrewGroups.Clear();

            if (currentDailyPlan == null) return;

            var crews = facade.GetCrews();
            var tasks = allTasks ?? new List<Models.Task>();

            var grouped = currentDailyPlan.Items
                .GroupBy(i => i.CrewId)
                .OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                var crew = crews.FirstOrDefault(c => c.CrewId == group.Key);
                var crewGroup = new CrewPlanGroup
                {
                    CrewId   = group.Key,
                    CrewName = crew?.CrewName ?? $"Бригада #{group.Key}"
                };

                foreach (var item in group.OrderBy(i => i.SortOrder))
                {
                    var task = tasks.FirstOrDefault(t => t.TaskId == item.TaskId);
                    crewGroup.Items.Add(new DailyPlanItemViewModel
                    {
                        PlanItemId     = item.PlanItemId,
                        TaskId         = item.TaskId,
                        Title          = task?.Title ?? $"Задача #{item.TaskId}",
                        SiteName       = task?.Site?.SiteName ?? "—",
                        CrewId         = item.CrewId,
                        CrewName       = crewGroup.CrewName,
                        SortOrder      = item.SortOrder,
                        Note           = item.Note ?? string.Empty,
                        MaterialsReady = item.MaterialsReady
                    });
                }

                dailyPlanCrewGroups.Add(crewGroup);
            }

            txtNoPlanItems.Visibility = dailyPlanCrewGroups.Count == 0
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RefreshAvailablePlanTasks()
        {
            availablePlanTasks.Clear();

            var plannedTaskIds = new HashSet<int>(
                dailyPlanCrewGroups.SelectMany(g => g.Items).Select(i => i.TaskId));

            // Загрузить все заявки на материалы (для предупреждений)
            List<MaterialRequest> allRequests;
            try { allRequests = facade.GetMaterialRequests(); }
            catch { allRequests = new List<MaterialRequest>(); }

            var eligibleTasks = GetEligiblePlanTasks(dailyPlanDate);

            foreach (var task in eligibleTasks)
            {
                bool hasMaterialWarning = HasMaterialWarningForTask(task.TaskId, allRequests);
                availablePlanTasks.Add(new AvailablePlanTaskItem
                {
                    Task               = task,
                    IsInPlan           = plannedTaskIds.Contains(task.TaskId),
                    HasMaterialWarning = hasMaterialWarning
                });
            }

            txtNoAvailableTasks.Visibility = availablePlanTasks.Count == 0
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private List<Models.Task> GetEligiblePlanTasks(DateTime date)
        {
            var d = date.Date;
            return (allTasks ?? new List<Models.Task>())
                .Where(t => t.TaskStatus?.TaskStatusName != "Завершено")
                .Where(t => (t.StartDate.Date <= d && t.EndDate.Date >= d)
                         || t.EndDate.Date < d)
                .OrderByDescending(t => t.PriorityId)
                .ThenBy(t => t.EndDate)
                .ThenBy(t => t.Title)
                .ToList();
        }

        private static bool HasMaterialWarningForTask(int taskId, List<MaterialRequest> requests)
        {
            var taskReqs = requests.Where(r => r.TaskId == taskId).ToList();
            if (!taskReqs.Any()) return false;
            var pendingStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "Draft", "Submitted", "Approved" };
            return taskReqs.Any(r => pendingStatuses.Contains(r.Status ?? string.Empty));
        }

        private void UpdateDailyPlanStatusDisplay()
        {
            bool isApproved = currentDailyPlan?.IsApproved == true;
            bool hasPlan    = currentDailyPlan != null;

            if (isApproved)
            {
                txtDailyPlanStatus.Text = "Утверждён";
                bdDailyPlanStatus.Background = new SolidColorBrush(Color.FromRgb(67, 160, 71));
                txtDailyPlanStatus.Foreground = System.Windows.Media.Brushes.White;
            }
            else if (hasPlan)
            {
                txtDailyPlanStatus.Text = "Черновик";
                bdDailyPlanStatus.Background = (Brush)FindResource("AppPanelBrush");
                txtDailyPlanStatus.Foreground = (Brush)FindResource("AppTextBrush");
            }
            else
            {
                txtDailyPlanStatus.Text = "Нет плана";
                bdDailyPlanStatus.Background = (Brush)FindResource("AppPanelBrush");
                txtDailyPlanStatus.Foreground = (Brush)FindResource("AppMutedTextBrush");
            }

            int itemCount = dailyPlanCrewGroups.Sum(g => g.Items.Count);
            txtDailyPlanInfo.Text = itemCount == 0
                ? $"Дата: {dailyPlanDate:dd.MM.yyyy (dddd)}"
                : $"Дата: {dailyPlanDate:dd.MM.yyyy (dddd)} — задач в плане: {itemCount}";

            btnSaveDailyPlan.IsEnabled    = !isApproved;
            btnApproveDailyPlan.IsEnabled = hasPlan && !isApproved;
            icDailyPlanCrews.IsEnabled    = !isApproved;
            pnlAddToPlan.IsEnabled        = !isApproved;
        }

        private void SetDailyPlanDate(DateTime date)
        {
            dailyPlanDate = date.Date;
            isUpdatingDailyPlanDate = true;
            dpDailyPlanDate.SelectedDate = dailyPlanDate;
            isUpdatingDailyPlanDate = false;
            selectedPlanTaskId = -1;
            pnlAddToPlan.Visibility = Visibility.Collapsed;
            LoadDailyPlan();
        }

        private void DpDailyPlanDate_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingDailyPlanDate || dpDailyPlanDate?.SelectedDate == null) return;
            dailyPlanDate = dpDailyPlanDate.SelectedDate.Value.Date;
            selectedPlanTaskId = -1;
            pnlAddToPlan.Visibility = Visibility.Collapsed;
            LoadDailyPlan();
        }

        private void BtnDailyPlanPrevDay_Click(object sender, RoutedEventArgs e)
            => SetDailyPlanDate(dailyPlanDate.AddDays(-1));

        private void BtnDailyPlanToday_Click(object sender, RoutedEventArgs e)
            => SetDailyPlanDate(DateTime.Today);

        private void BtnDailyPlanNextDay_Click(object sender, RoutedEventArgs e)
            => SetDailyPlanDate(dailyPlanDate.AddDays(1));

        private void BtnSelectPlanTask_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is FrameworkElement btn) || !(btn.Tag is int taskId)) return;
            if (currentDailyPlan?.IsApproved == true) return;

            var task = availablePlanTasks.FirstOrDefault(t => t.TaskId == taskId);
            if (task == null || task.IsInPlan) return;

            selectedPlanTaskId = taskId;
            txtAddToPlanTaskName.Text = task.Title;
            txtPlanItemNote.Text = string.Empty;
            chkPlanItemMaterials.IsChecked = !task.HasMaterialWarning;
            cbPlanCrewSelect.SelectedItem = null;

            // Если у задачи есть бригада, предвыбрать её
            var taskObj = task.Task;
            if (taskObj?.CrewId.HasValue == true)
            {
                cbPlanCrewSelect.SelectedItem =
                    dailyPlanCrews.FirstOrDefault(c => c.CrewId == taskObj.CrewId.Value);
            }

            pnlAddToPlan.Visibility = Visibility.Visible;
        }

        private void BtnConfirmAddToPlan_Click(object sender, RoutedEventArgs e)
        {
            if (!(cbPlanCrewSelect.SelectedItem is Crew crew))
            {
                MessageBox.Show("Выберите бригаду.", "Добавление в план", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (selectedPlanTaskId < 0) return;

            var task = availablePlanTasks.FirstOrDefault(t => t.TaskId == selectedPlanTaskId);
            if (task == null) return;

            // Найти или создать группу для этой бригады
            var group = dailyPlanCrewGroups.FirstOrDefault(g => g.CrewId == crew.CrewId);
            if (group == null)
            {
                group = new CrewPlanGroup { CrewId = crew.CrewId, CrewName = crew.CrewName };
                dailyPlanCrewGroups.Add(group);
            }

            int nextOrder = group.Items.Count == 0 ? 1 : group.Items.Max(i => i.SortOrder) + 1;

            group.Items.Add(new DailyPlanItemViewModel
            {
                TaskId         = task.TaskId,
                Title          = task.Title,
                SiteName       = task.SiteName,
                CrewId         = crew.CrewId,
                CrewName       = crew.CrewName,
                SortOrder      = nextOrder,
                Note           = txtPlanItemNote.Text.Trim(),
                MaterialsReady = chkPlanItemMaterials.IsChecked == true
            });

            txtNoPlanItems.Visibility = Visibility.Collapsed;

            // Скрыть панель
            pnlAddToPlan.Visibility = Visibility.Collapsed;
            selectedPlanTaskId = -1;

            // Обновить флаги доступных задач
            RefreshAvailablePlanTasks();
            UpdateDailyPlanStatusDisplay();
            txtDailyPlanSaveStatus.Text = "Есть несохранённые изменения";
        }

        private void BtnCancelAddToPlan_Click(object sender, RoutedEventArgs e)
        {
            pnlAddToPlan.Visibility = Visibility.Collapsed;
            selectedPlanTaskId = -1;
        }

        private void BtnPlanItemRemove_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is FrameworkElement btn) || !(btn.Tag is DailyPlanItemViewModel item)) return;

            var group = dailyPlanCrewGroups.FirstOrDefault(g => g.CrewId == item.CrewId);
            if (group == null) return;

            group.Items.Remove(item);
            if (group.Items.Count == 0)
                dailyPlanCrewGroups.Remove(group);
            else
                RecalcSortOrders(group);

            txtNoPlanItems.Visibility = dailyPlanCrewGroups.Count == 0
                ? Visibility.Visible : Visibility.Collapsed;

            RefreshAvailablePlanTasks();
            UpdateDailyPlanStatusDisplay();
            txtDailyPlanSaveStatus.Text = "Есть несохранённые изменения";
        }

        private void BtnPlanItemMoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is FrameworkElement btn) || !(btn.Tag is DailyPlanItemViewModel item)) return;
            var group = dailyPlanCrewGroups.FirstOrDefault(g => g.CrewId == item.CrewId);
            if (group == null) return;

            int idx = group.Items.IndexOf(item);
            if (idx <= 0) return;

            group.Items.Move(idx, idx - 1);
            RecalcSortOrders(group);
            txtDailyPlanSaveStatus.Text = "Есть несохранённые изменения";
        }

        private void BtnPlanItemMoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is FrameworkElement btn) || !(btn.Tag is DailyPlanItemViewModel item)) return;
            var group = dailyPlanCrewGroups.FirstOrDefault(g => g.CrewId == item.CrewId);
            if (group == null) return;

            int idx = group.Items.IndexOf(item);
            if (idx < 0 || idx >= group.Items.Count - 1) return;

            group.Items.Move(idx, idx + 1);
            RecalcSortOrders(group);
            txtDailyPlanSaveStatus.Text = "Есть несохранённые изменения";
        }

        private static void RecalcSortOrders(CrewPlanGroup group)
        {
            for (int i = 0; i < group.Items.Count; i++)
                group.Items[i].SortOrder = i + 1;
        }

        private void BtnSaveDailyPlan_Click(object sender, RoutedEventArgs e)
        {
            if (currentDailyPlan?.IsApproved == true)
            {
                MessageBox.Show("Утверждённый план нельзя редактировать.", "Сохранение", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var plan = BuildPlanFromUI();
                int userId = LoginWindow.CurrentUser?.UserId ?? 0;
                int planId = facade.SaveDailyPlan(plan, userId);
                plan.PlanId = planId;
                currentDailyPlan = plan;
                txtDailyPlanSaveStatus.Text = $"Сохранено {DateTime.Now:HH:mm}";
                UpdateDailyPlanStatusDisplay();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnApproveDailyPlan_Click(object sender, RoutedEventArgs e)
        {
            if (currentDailyPlan?.PlanId == 0 || currentDailyPlan == null)
            {
                MessageBox.Show("Сначала сохраните план, затем утвердите его.", "Утверждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (currentDailyPlan.IsApproved)
            {
                MessageBox.Show("План уже утверждён.", "Утверждение", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (dailyPlanCrewGroups.Sum(g => g.Items.Count) == 0)
            {
                MessageBox.Show("Нельзя утвердить пустой план. Добавьте хотя бы одну задачу.", "Утверждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                $"Утвердить план на {dailyPlanDate:dd.MM.yyyy}?\nПосле утверждения план нельзя будет изменить.",
                "Утверждение плана", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                // Сначала сохранить текущее состояние
                var plan = BuildPlanFromUI();
                plan.PlanId = currentDailyPlan.PlanId;
                int userId = LoginWindow.CurrentUser?.UserId ?? 0;
                facade.SaveDailyPlan(plan, userId);

                // Затем утвердить
                facade.ApproveDailyPlan(currentDailyPlan.PlanId);
                currentDailyPlan.Status = "Утвержден";
                txtDailyPlanSaveStatus.Text = "План утверждён";
                UpdateDailyPlanStatusDisplay();
                MessageBox.Show("План утверждён. Бригадиры увидят задачи в своём чек-листе.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при утверждении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private DailyPlan BuildPlanFromUI()
        {
            var plan = new DailyPlan
            {
                PlanId          = currentDailyPlan?.PlanId ?? 0,
                PlanDate        = dailyPlanDate,
                CreatedByUserId = LoginWindow.CurrentUser?.UserId ?? 0,
                Status          = currentDailyPlan?.Status ?? "Черновик"
            };

            foreach (var group in dailyPlanCrewGroups)
            {
                int order = 1;
                foreach (var item in group.Items)
                {
                    plan.Items.Add(new DailyPlanItem
                    {
                        TaskId         = item.TaskId,
                        CrewId         = group.CrewId,
                        SortOrder      = order++,
                        Note           = item.Note,
                        MaterialsReady = item.MaterialsReady
                    });
                }
            }

            return plan;
        }

        // =====================================================================
        // Inner view-model classes for DailyPlan
        // =====================================================================

        private sealed class AvailablePlanTaskItem
        {
            public Models.Task Task         { get; set; }
            public bool        IsInPlan     { get; set; }
            public bool        HasMaterialWarning { get; set; }

            public int    TaskId      => Task?.TaskId ?? 0;
            public string Title       => Task?.Title ?? string.Empty;
            public string SiteName    => Task?.Site?.SiteName ?? "—";
            public string PriorityName => Task?.Priority?.PriorityName ?? "—";
            public string StatusName  => Task?.TaskStatus?.TaskStatusName ?? "—";
            public string DateRange   => $"{Task?.StartDate:dd.MM} – {Task?.EndDate:dd.MM.yyyy}";

            public Visibility MaterialWarningVisibility => HasMaterialWarning ? Visibility.Visible : Visibility.Collapsed;
            public Visibility NotInPlanVisibility       => IsInPlan ? Visibility.Collapsed : Visibility.Visible;
            public Visibility InPlanVisibility          => IsInPlan ? Visibility.Visible : Visibility.Collapsed;
        }

        private sealed class DailyPlanItemViewModel
        {
            public int    PlanItemId     { get; set; }
            public int    TaskId         { get; set; }
            public string Title          { get; set; }
            public string SiteName       { get; set; }
            public int    CrewId         { get; set; }
            public string CrewName       { get; set; }
            public int    SortOrder      { get; set; }
            public string Note           { get; set; }
            public bool   MaterialsReady { get; set; }

            public string SortOrderText => $"#{SortOrder}";
        }

        private sealed class CrewPlanGroup
        {
            public int    CrewId   { get; set; }
            public string CrewName { get; set; }
            public ObservableCollection<DailyPlanItemViewModel> Items { get; set; }
                = new ObservableCollection<DailyPlanItemViewModel>();
        }

        private sealed class ReportFileExport
        {
            public ReportFileExport(string reportName, List<string> lines)
            {
                ReportName = reportName;
                Lines = lines ?? new List<string>();
            }

            public string ReportName { get; }
            public List<string> Lines { get; }
        }

        private sealed class TaskOverviewReportRow
        {
            public string Title { get; set; }
            public string SiteName { get; set; }
            public string CrewName { get; set; }
            public string PriorityName { get; set; }
            public string StatusName { get; set; }
            public string StartDateText { get; set; }
            public string EndDateText { get; set; }
        }

        private sealed class TasksBySiteReportRow
        {
            public string SiteName { get; set; }
            public int TotalTasks { get; set; }
            public int CompletedCount { get; set; }
            public int InProgressCount { get; set; }
            public int OverdueCount { get; set; }
        }

        private sealed class TasksByCrewReportRow
        {
            public string CrewName { get; set; }
            public int TotalTasks { get; set; }
            public int CompletedCount { get; set; }
            public int InProgressCount { get; set; }
            public int OverdueCount { get; set; }
        }

        private sealed class OverdueTaskReportRow
        {
            public string Title { get; set; }
            public string SiteName { get; set; }
            public string CrewName { get; set; }
            public string PriorityName { get; set; }
            public DateTime EndDate { get; set; }
            public int DelayDays { get; set; }
        }

        private sealed class BrigadierTaskChecklistItem
        {
            public Models.Task Task { get; set; }

            public int TaskId => Task?.TaskId ?? 0;
            public string Title => Task?.Title ?? string.Empty;
            public string DescriptionPreview
            {
                get
                {
                    var description = Task?.Description?.Trim();
                    if (string.IsNullOrWhiteSpace(description))
                    {
                        return "Описание не заполнено.";
                    }

                    return description.Length > 180
                        ? description.Substring(0, 177) + "..."
                        : description;
                }
            }
            public string SiteName => Task?.Site?.SiteName ?? "—";
            public string CrewName => Task?.Crew?.CrewName ?? "—";
            public string PriorityName => Task?.Priority?.PriorityName ?? "—";
            public string StatusName => Task?.TaskStatus?.TaskStatusName ?? "—";
            public string DateRangeText => $"{Task?.StartDate:dd.MM.yyyy} - {Task?.EndDate:dd.MM.yyyy}";
            public string LastNoteText => string.IsNullOrWhiteSpace(Task?.LastNoteText) ? "—" : Task.LastNoteText;
            public string LastNoteTooltip => string.IsNullOrWhiteSpace(Task?.LastNoteTooltip) ? "Заметок нет" : Task.LastNoteTooltip;
            public bool IsOverdue { get; set; }
            public string ChecklistStatusText { get; set; }

            // DailyPlan integration
            public string PlanOrderText { get; set; }
            public Visibility PlanOrderBadgeVisibility
                => string.IsNullOrEmpty(PlanOrderText) ? Visibility.Collapsed : Visibility.Visible;
            public string PlanNote { get; set; }
            public Visibility PlanNoteVisibility
                => string.IsNullOrEmpty(PlanNote) ? Visibility.Collapsed : Visibility.Visible;

            public bool IsMarkedCompleted { get; set; }
            public string CompletionComment { get; set; }
        }

        private sealed class RolePermissionItem
        {
            public string Code { get; set; }
            public string Category { get; set; }
            public string DisplayName { get; set; }
            public string Description { get; set; }
            public bool IsGranted { get; set; }
        }

        private sealed class ReportPreviewSlice
        {
            public string Label { get; set; }
            public int Value { get; set; }
            public Brush Brush { get; set; }
        }

        private sealed class PieCalloutLayout
        {
            public ReportPreviewSlice Slice { get; set; }
            public int Side { get; set; }
            public Point AnchorPoint { get; set; }
            public double TargetY { get; set; }
        }
    }
}
