using System;
using System.Linq;
using System.Windows;
using praktik.Models;

namespace praktik
{
    public partial class TaskSelectionWindow : Window
    {
        private WorkPlannerContext db = new WorkPlannerContext();
        public Task SelectedTask { get; private set; }

        public TaskSelectionWindow()
        {
            InitializeComponent();
            LoadTasks();
        }

        private void LoadTasks()
        {
            var tasks = db.GetTasks();
            dgTasks.ItemsSource = tasks;
        }

        private void btnSelect_Click(object sender, RoutedEventArgs e)
        {
            if (dgTasks.SelectedItem is Task task)
            {
                SelectedTask = task;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Выберите задачу из списка", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

