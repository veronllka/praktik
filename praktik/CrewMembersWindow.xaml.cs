using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using praktik.Models;
using praktik.Models.Patterns;

namespace praktik
{
    public partial class CrewMembersWindow : Window
    {
        private readonly WorkPlannerFacade facade = new WorkPlannerFacade();
        private readonly Crew crew;
        private List<CrewMemberDisplay> currentMembers = new List<CrewMemberDisplay>();

        public CrewMembersWindow(Crew crew)
        {
            InitializeComponent();

            this.crew = crew ?? throw new ArgumentNullException(nameof(crew));
            txtTitle.Text = $"Состав бригады: {crew.CrewName}";

            dpJoinedAt.SelectedDate = DateTime.Today;
            dpLeftAt.SelectedDate = DateTime.Today;

            LoadMembers();
        }

        private void LoadMembers()
        {
            var activeOnly = chkShowHistory.IsChecked != true;
            var members = facade.GetCrewMembers(crew.CrewId, activeOnly) ?? new List<CrewMember>();

            currentMembers = members
                .Select(m => new CrewMemberDisplay(m))
                .ToList();

            dgCrewMembers.ItemsSource = currentMembers;
            txtNoData.Visibility = currentMembers.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            UpdateRemoveState();
        }

        private void UpdateRemoveState()
        {
            var selected = dgCrewMembers.SelectedItem as CrewMemberDisplay;
            var canRemove = selected != null && selected.IsActive;

            btnRemoveMember.IsEnabled = canRemove;
            dpLeftAt.IsEnabled = canRemove;
        }

        private void BtnAddMember_Click(object sender, RoutedEventArgs e)
        {
            var createWindow = new CrewMemberCreateWindow(dpJoinedAt.SelectedDate)
            {
                Owner = this
            };

            if (createWindow.ShowDialog() != true)
            {
                return;
            }

            if (!facade.CreateCrewEmployeeAndAddToCrew(
                    crew.CrewId,
                    createWindow.FullName,
                    createWindow.JoinedAt,
                    out _,
                    out var createError))
            {
                MessageBox.Show(string.IsNullOrWhiteSpace(createError) ? "Не удалось создать сотрудника" : createError);
                return;
            }

            dpJoinedAt.SelectedDate = createWindow.JoinedAt;
            LoadMembers();
        }

        private void BtnRemoveMember_Click(object sender, RoutedEventArgs e)
        {
            if (!(dgCrewMembers.SelectedItem is CrewMemberDisplay selected))
            {
                MessageBox.Show("Выберите сотрудника в списке");
                return;
            }

            if (!selected.IsActive)
            {
                MessageBox.Show("Сотрудник уже исключен из бригады");
                return;
            }

            var leftAt = dpLeftAt.SelectedDate ?? DateTime.Today;
            if (!facade.RemoveCrewMember(crew.CrewId, selected.UserId, leftAt, out var errorMessage))
            {
                MessageBox.Show(string.IsNullOrWhiteSpace(errorMessage) ? "Не удалось исключить сотрудника" : errorMessage);
                return;
            }

            LoadMembers();
        }

        private void ChkShowHistory_Changed(object sender, RoutedEventArgs e)
        {
            LoadMembers();
        }

        private void DgCrewMembers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateRemoveState();
        }

        private sealed class CrewMemberDisplay
        {
            private readonly CrewMember member;

            public CrewMemberDisplay(CrewMember member)
            {
                this.member = member;
            }

            public int UserId => member.UserId;
            public bool IsActive => !member.LeftAt.HasValue;
            public string EmployeeName => member.User?.DisplayName ?? (member.User?.Username ?? string.Empty);
            public string JoinedAtDisplay => member.JoinedAt.ToString("dd.MM.yyyy");
            public string LeftAtDisplay => member.LeftAt.HasValue ? member.LeftAt.Value.ToString("dd.MM.yyyy") : "—";
            public string MemberState => IsActive ? "Активен" : "Выбыл";
        }
    }
}
