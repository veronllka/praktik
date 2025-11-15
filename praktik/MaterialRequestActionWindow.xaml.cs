using System.Windows;

namespace praktik
{
    public partial class MaterialRequestActionWindow : Window
    {
        public string DocNumber { get; private set; }
        public string Note { get; private set; }

        public MaterialRequestActionWindow(string title, string docNumberLabel, string noteLabel)
        {
            InitializeComponent();
            txtTitle.Text = title;
            lblDocNumber.Text = docNumberLabel + ":";
            lblNote.Text = noteLabel + ":";
        }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            DocNumber = txtDocNumber.Text;
            Note = txtNote.Text;
            DialogResult = true;
            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

