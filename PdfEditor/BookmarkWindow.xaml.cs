using System.Windows;

namespace PdfEditor
{
    public partial class BookmarkWindow : Window
    {
        public string TitleText { get; set; }
        public int PageNumber { get; set; }

        public BookmarkWindow()
        {
            InitializeComponent();
        }

        public BookmarkWindow(string title, int pageNumber)
        {
            InitializeComponent();
            TitleTextBox.Text = title;
            PageTextBox.Text = pageNumber.ToString();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TitleTextBox.Text))
            {
                System.Windows.MessageBox.Show("请输入标题", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!int.TryParse(PageTextBox.Text, out int pageNum) || pageNum < 1)
            {
                System.Windows.MessageBox.Show("请输入有效的页码", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            TitleText = TitleTextBox.Text;
            PageNumber = pageNum - 1;
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