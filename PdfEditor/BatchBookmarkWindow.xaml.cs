using System.Windows;

namespace PdfEditor
{
    public partial class BatchBookmarkWindow : Window
    {
        public int StartPage { get; set; }
        public int EndPage { get; set; }
        public string TitlePrefix { get; set; }

        public BatchBookmarkWindow()
        {
            InitializeComponent();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(StartPageTextBox.Text, out int startPage) || startPage < 1)
            {
                MessageBox.Show("请输入有效的起始页码", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!int.TryParse(EndPageTextBox.Text, out int endPage) || endPage < startPage)
            {
                MessageBox.Show("请输入有效的结束页码（必须大于等于起始页码）", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            StartPage = startPage;
            EndPage = endPage;
            TitlePrefix = TitlePrefixTextBox.Text.Trim();
            
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}