using System;
using System.Windows;

namespace praktik
{
    public partial class CrewMemberCreateWindow : Window
    {
        public string FullName { get; private set; }
        public DateTime JoinedAt { get; private set; }

        public CrewMemberCreateWindow(DateTime? defaultJoinedAt = null)
        {
            InitializeComponent();
            dpJoinedAt.SelectedDate = defaultJoinedAt ?? DateTime.Today;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var fullName = (txtFullName.Text ?? string.Empty).Trim();
            var joinedAt = dpJoinedAt.SelectedDate ?? DateTime.Today;

            if (string.IsNullOrWhiteSpace(fullName))
            {
                MessageBox.Show("Введите ФИО сотрудника");
                txtFullName.Focus();
                return;
            }

            FullName = fullName;
            JoinedAt = joinedAt.Date;

            DialogResult = true;
            Close();
        }
    }
}
