using System;
using System.Linq;
using System.Windows;
using praktik.Models;
using praktik.Models.Patterns;

namespace praktik
{
    public partial class TaskSelectionWindow : Window
    {
        private readonly WorkPlannerFacade facade = new WorkPlannerFacade();
        public Task SelectedTask { get; private set; }

        public TaskSelectionWindow()
        {
            InitializeComponent();
            LoadTasks();
        }

        private void LoadTasks()
        {
            var tasks = facade.GetTasks();
            dgTasks.ItemsSource = tasks;
        }

        private void BtnSelect_Click(object sender, RoutedEventArgs e)
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

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

