using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Microsoft.Web.WebView2.Core;
using iTextSharp.text;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.draw;
using OfficeOpenXml;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;
using P = DocumentFormat.OpenXml.Presentation;
using D = DocumentFormat.OpenXml.Drawing;
using PDFiumCore;
using System.Drawing;

namespace PdfEditor
{
    public class BookmarkItem
    {
        public string Title { get; set; }
        public int PageNumber { get; set; }
        public string PageDisplay { get; set; }
        public System.Windows.Media.Color Color { get; set; }
        public FontWeight FontWeight { get; set; }
        public List<BookmarkItem> Children { get; set; } = new List<BookmarkItem>();
    }

    public partial class MainWindow : Window
    {
        private string currentPdfPath;
        private List<BookmarkItem> bookmarkItems = new List<BookmarkItem>();
        private int currentPreviewPage = 1;
        private int totalPages = 1;
        private string pendingBookmarkPdfData = null;

        public MainWindow()
        {
            InitializeComponent();
            InitializeWebView2();
            InitializeBookmarkPreview();
        }

        private async void InitializeWebView2()
        {
            try
            {
                await PdfPreview.EnsureCoreWebView2Async();
                PdfPreview.CoreWebView2.SetVirtualHostNameToFolderMapping("app.local", AppDomain.CurrentDomain.BaseDirectory, CoreWebView2HostResourceAccessKind.Allow);
                PdfPreview.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
                PreviewStatus.Content = "预览就绪";
            }
            catch (Exception ex)
            {
                PreviewStatus.Content = $"初始化失败: {ex.Message}";
            }
        }

        private async void InitializeBookmarkPreview()
        {
            try
            {
                await BookmarkPreview.EnsureCoreWebView2Async();
                BookmarkPreview.CoreWebView2.SetVirtualHostNameToFolderMapping("app.local", AppDomain.CurrentDomain.BaseDirectory, CoreWebView2HostResourceAccessKind.Allow);
                BookmarkPreview.CoreWebView2.NavigationCompleted += BookmarkPreview_NavigationCompleted;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"预览初始化失败: {ex.Message}";
            }
        }

        private void BookmarkPreview_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!string.IsNullOrEmpty(pendingBookmarkPdfData))
            {
                Dispatcher.BeginInvoke(new Action(async () =>
                {
                    await System.Threading.Tasks.Task.Delay(300);
                    try
                    {
                        string escapedData = pendingBookmarkPdfData.Replace("\\", "\\\\").Replace("'", "\\'");
                        string script = $"window.postMessage({{ type: 'loadPdf', base64Data: '{escapedData}' }}, '*');";
                        await BookmarkPreview.CoreWebView2.ExecuteScriptAsync(script);

                        if (currentPreviewPage > 1)
                        {
                            await System.Threading.Tasks.Task.Delay(500);
                            string gotoScript = $"window.postMessage({{ type: 'gotoPage', pageNumber: {currentPreviewPage} }}, '*');";
                            await BookmarkPreview.CoreWebView2.ExecuteScriptAsync(gotoScript);
                        }
                    }
                    catch { }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private int pendingPageNumber = -1;

        private void CoreWebView2_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!string.IsNullOrEmpty(pendingPdfData) && PdfPreview.CoreWebView2 != null)
            {
                Dispatcher.BeginInvoke(new Action(async () =>
                {
                    await System.Threading.Tasks.Task.Delay(300);
                    try
                    {
                        string escapedData = pendingPdfData.Replace("\\", "\\\\").Replace("'", "\\'");
                        string script = $"window.postMessage({{ type: 'loadPdf', base64Data: '{escapedData}' }}, '*');";
                        await PdfPreview.CoreWebView2.ExecuteScriptAsync(script);
                        
                        if (pendingPageNumber > 0)
                        {
                            await System.Threading.Tasks.Task.Delay(500);
                            string gotoScript = $"window.postMessage({{ type: 'gotoPage', pageNumber: {pendingPageNumber} }}, '*');";
                            await PdfPreview.CoreWebView2.ExecuteScriptAsync(gotoScript);
                        }
                    }
                    catch (Exception ex)
                    {
                        StatusText.Text = $"注入数据失败: {ex.Message}";
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private void PreviewPdfInBookmarkWindow(string filePath)
        {
            try
            {
                if (BookmarkPreview.CoreWebView2 != null)
                {
                    byte[] pdfBytes = File.ReadAllBytes(filePath);
                    string base64Pdf = Convert.ToBase64String(pdfBytes);
                    pendingBookmarkPdfData = base64Pdf;
                    totalPages = GetPdfPageCount(filePath);
                    UpdateCurrentPageDisplay();
                    
                    BookmarkPreview.Source = new Uri("https://app.local/pdfviewer.html");
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"预览失败: {ex.Message}";
            }
        }

        private int GetPdfPageCount(string filePath)
        {
            try
            {
                using (PdfReader reader = new PdfReader(filePath))
                {
                    return reader.NumberOfPages;
                }
            }
            catch
            {
                return 1;
            }
        }

        private void UpdateCurrentPageDisplay()
        {
            CurrentPageText.Text = $"第 {currentPreviewPage} / {totalPages} 页";
        }

        private void OpenPdf_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "PDF 文件 (*.pdf)|*.pdf";
            if (openFileDialog.ShowDialog() == true)
            {
                LoadPdf(openFileDialog.FileName);
            }
        }

        private void LoadPdf(string filePath)
        {
            currentPdfPath = filePath;
            PageList.Items.Clear();
            bookmarkItems.Clear();

            try
            {
                using (PdfWrapper.PdfDocument pdfDoc = new PdfWrapper.PdfDocument(filePath))
                {
                    totalPages = pdfDoc.GetPageCount();
                    for (int i = 0; i < totalPages; i++)
                    {
                        PageList.Items.Add($"第 {i + 1} 页");
                    }
                    
                    bookmarkItems.Clear();
                    var outlines = pdfDoc.GetBookmarks();
                    if (outlines != null)
                    {
                        foreach (var outline in outlines)
                        {
                            BookmarkItem item = ConvertToBookmarkItem(outline);
                            if (item != null)
                            {
                                bookmarkItems.Add(item);
                            }
                        }
                    }
                }
                StatusText.Text = $"已打开: {Path.GetFileName(filePath)}";
                PreviewPdf(filePath);
                PreviewPdfInBookmarkWindow(filePath);
                UpdateBookmarkTree();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开 PDF 失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private BookmarkItem ConvertToBookmarkItem(PdfWrapper.PdfBookmark pdfBookmark)
        {
            if (pdfBookmark == null) return null;
            
            BookmarkItem item = new BookmarkItem();
            item.Title = pdfBookmark.Title;
            item.PageNumber = pdfBookmark.PageNumber;
            
            if (pdfBookmark.Children != null)
            {
                foreach (var child in pdfBookmark.Children)
                {
                    BookmarkItem childItem = ConvertToBookmarkItem(child);
                    if (childItem != null)
                    {
                        item.Children.Add(childItem);
                    }
                }
            }
            
            return item;
        }

        private void PreviewPdf(string filePath)
        {
            try
            {
                if (PdfPreview.CoreWebView2 != null)
                {
                    byte[] pdfBytes = File.ReadAllBytes(filePath);
                    string base64Pdf = Convert.ToBase64String(pdfBytes);
                    pendingPdfData = base64Pdf;
                    
                    PdfPreview.Source = new Uri("https://app.local/pdfviewer.html");
                    pendingPageNumber = 1;
                    PreviewStatus.Content = "预览中...";
                }
                else
                {
                    PreviewStatus.Content = "WebView2 未就绪";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"预览失败: {ex.Message}";
                PreviewStatus.Content = $"预览失败: {ex.Message}";
            }
        }

        private string pendingPdfData = null;

        private void PageList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PageList.SelectedItem != null && PdfPreview.CoreWebView2 != null)
            {
                string selectedItem = PageList.SelectedItem.ToString();
                if (int.TryParse(selectedItem.Replace("第", "").Replace("页", "").Trim(), out int pageNumber))
                {
                    pendingPageNumber = pageNumber;
                    NavigateToPage(pageNumber);
                    PreviewStatus.Content = $"预览第 {pageNumber} 页";
                }
            }
        }

        private async void NavigateToPage(int pageNumber)
        {
            try
            {
                if (PdfPreview.CoreWebView2 != null)
                {
                    string script = $"window.postMessage({{ type: 'gotoPage', pageNumber: {pageNumber} }}, '*');";
                    await PdfPreview.CoreWebView2.ExecuteScriptAsync(script);
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"页面跳转失败: {ex.Message}";
            }
        }

        // ==================== 书签管理功能 ====================

        private BookmarkItem ParseBookmarkDict(Dictionary<string, object> dict)
        {
            if (!dict.ContainsKey("Title"))
                return null;

            BookmarkItem item = new BookmarkItem();
            item.Title = dict["Title"].ToString();

            if (dict.ContainsKey("Page"))
            {
                string pageStr = dict["Page"].ToString();
                int pageNum = ParsePageNumber(pageStr);
                item.PageNumber = pageNum;
            }
            else
            {
                item.PageNumber = 0;
            }

            if (dict.ContainsKey("Kids") && dict["Kids"] is IList<object> kids)
            {
                foreach (var kidObj in kids)
                {
                    if (kidObj is Dictionary<string, object> kidDict)
                    {
                        BookmarkItem child = ParseBookmarkDict(kidDict);
                        if (child != null)
                        {
                            item.Children.Add(child);
                        }
                    }
                }
            }

            return item;
        }

        private int ParsePageNumber(string pageStr)
        {
            try
            {
                string[] parts = pageStr.Split(new[] { ' ', '[', ']', '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0 && int.TryParse(parts[0], out int pageNum))
                {
                    return pageNum - 1;
                }
            }
            catch { }
            return 0;
        }

        private void UpdateBookmarkTree()
        {
            BookmarkTree.ItemsSource = null;
            BookmarkTree.ItemsSource = bookmarkItems;
        }

        private void BookmarkTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            BookmarkItem item = GetSelectedBookmarkItem();
            if (item != null)
            {
                currentPreviewPage = item.PageNumber + 1;
                NavigateBookmarkPreview(currentPreviewPage);
                StatusText.Text = $"已跳转到: {item.Title}";
            }
        }

        private void AddBookmark_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(currentPdfPath))
            {
                MessageBox.Show("请先打开一个 PDF 文件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            BookmarkWindow window = new BookmarkWindow();
            window.Owner = this;
            if (window.ShowDialog() == true)
            {
                var newBookmark = new BookmarkItem
                {
                    Title = window.TitleText,
                    PageNumber = window.PageNumber,
                    PageDisplay = (window.PageNumber + 1).ToString(),
                    Color = Colors.Gray,
                    FontWeight = FontWeights.Normal
                };
                bookmarkItems.Add(newBookmark);
                bookmarkItems = bookmarkItems.OrderBy(b => b.PageNumber).ToList();
                UpdateBookmarkTree();
                StatusText.Text = $"已添加书签: {newBookmark.Title}";
            }
        }

        private void AddChildBookmark_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(currentPdfPath))
            {
                MessageBox.Show("请先打开一个 PDF 文件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            BookmarkItem parent = GetSelectedBookmarkItem();
            if (parent == null)
            {
                MessageBox.Show("请先选择一个书签作为父书签", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            BookmarkWindow window = new BookmarkWindow();
            window.Owner = this;
            if (window.ShowDialog() == true)
            {
                var newBookmark = new BookmarkItem
                {
                    Title = window.TitleText,
                    PageNumber = window.PageNumber,
                    PageDisplay = (window.PageNumber + 1).ToString(),
                    Color = Colors.Gray,
                    FontWeight = FontWeights.Normal
                };
                parent.Children.Add(newBookmark);
                UpdateBookmarkTree();
                StatusText.Text = $"已添加子书签: {newBookmark.Title}";
            }
        }

        private void EditBookmark_Click(object sender, RoutedEventArgs e)
        {
            BookmarkItem item = GetSelectedBookmarkItem();
            if (item != null)
            {
                BookmarkWindow window = new BookmarkWindow(item.Title, item.PageNumber + 1);
                window.Owner = this;
                if (window.ShowDialog() == true)
                {
                    item.Title = window.TitleText;
                    item.PageNumber = window.PageNumber;
                    item.PageDisplay = (window.PageNumber + 1).ToString();
                    UpdateBookmarkTree();
                    StatusText.Text = $"已更新书签: {item.Title}";
                }
            }
            else
            {
                MessageBox.Show("请选择要编辑的书签", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void DeleteBookmark_Click(object sender, RoutedEventArgs e)
        {
            BookmarkItem item = GetSelectedBookmarkItem();
            if (item != null)
            {
                RemoveBookmarkItem(bookmarkItems, item);
                UpdateBookmarkTree();
                StatusText.Text = $"已删除书签: {item.Title}";
            }
            else
            {
                MessageBox.Show("请选择要删除的书签", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private bool RemoveBookmarkItem(List<BookmarkItem> items, BookmarkItem target)
        {
            if (items.Remove(target))
                return true;
            
            foreach (var item in items)
            {
                if (RemoveBookmarkItem(item.Children, target))
                    return true;
            }
            return false;
        }

        private BookmarkItem GetSelectedBookmarkItem()
        {
            if (BookmarkTree.SelectedItem is BookmarkItem item)
                return item;
            return null;
        }

        private void SearchBookmarks_Click(object sender, RoutedEventArgs e)
        {
            string searchText = BookmarkSearchBox.Text.Trim().ToLower();
            if (string.IsNullOrEmpty(searchText))
            {
                UpdateBookmarkTree();
                return;
            }

            var filtered = FilterBookmarks(bookmarkItems, searchText);
            BookmarkTree.ItemsSource = null;
            BookmarkTree.ItemsSource = filtered;
            StatusText.Text = $"找到 {CountBookmarks(filtered)} 个书签";
        }

        private List<BookmarkItem> FilterBookmarks(List<BookmarkItem> items, string searchText)
        {
            var result = new List<BookmarkItem>();
            foreach (var item in items)
            {
                if (item.Title.ToLower().Contains(searchText))
                {
                    result.Add(item);
                }
                else
                {
                    var filteredChildren = FilterBookmarks(item.Children, searchText);
                    if (filteredChildren.Count > 0)
                    {
                        var newItem = new BookmarkItem
                        {
                            Title = item.Title,
                            PageNumber = item.PageNumber,
                            PageDisplay = item.PageDisplay,
                            Color = item.Color,
                            FontWeight = item.FontWeight,
                            Children = filteredChildren
                        };
                        result.Add(newItem);
                    }
                }
            }
            return result;
        }

        private int CountBookmarks(List<BookmarkItem> items)
        {
            int count = items.Count;
            foreach (var item in items)
            {
                count += CountBookmarks(item.Children);
            }
            return count;
        }

        // 页面导航
        private void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (currentPreviewPage > 1)
            {
                currentPreviewPage--;
                NavigateBookmarkPreview(currentPreviewPage);
            }
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            if (currentPreviewPage < totalPages)
            {
                currentPreviewPage++;
                NavigateBookmarkPreview(currentPreviewPage);
            }
        }

        private void JumpToBookmarkPage_Click(object sender, RoutedEventArgs e)
        {
            BookmarkItem item = GetSelectedBookmarkItem();
            if (item != null)
            {
                currentPreviewPage = item.PageNumber + 1;
                NavigateBookmarkPreview(currentPreviewPage);
            }
            else
            {
                MessageBox.Show("请选择一个书签", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void NavigateBookmarkPreview(int pageNumber)
        {
            try
            {
                if (BookmarkPreview.CoreWebView2 != null)
                {
                    string script = $"window.postMessage({{ type: 'gotoPage', pageNumber: {pageNumber} }}, '*');";
                    await BookmarkPreview.CoreWebView2.ExecuteScriptAsync(script);
                    UpdateCurrentPageDisplay();
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"页面跳转失败: {ex.Message}";
            }
        }

        // 快速添加书签
        private void QuickAddBookmark_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(currentPdfPath))
            {
                MessageBox.Show("请先打开一个 PDF 文件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string title = QuickBookmarkTitle.Text.Trim();
            if (string.IsNullOrEmpty(title))
            {
                title = $"第 {currentPreviewPage} 页";
            }

            var newBookmark = new BookmarkItem
            {
                Title = title,
                PageNumber = currentPreviewPage - 1,
                PageDisplay = currentPreviewPage.ToString(),
                Color = Colors.Gray,
                FontWeight = FontWeights.Normal
            };
            
            bookmarkItems.Add(newBookmark);
            bookmarkItems = bookmarkItems.OrderBy(b => b.PageNumber).ToList();
            UpdateBookmarkTree();
            QuickBookmarkTitle.Text = "";
            StatusText.Text = $"已添加书签: {newBookmark.Title}";
        }

        // 批量添加连续页面为书签
        private void BatchAddBookmarks_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(currentPdfPath))
            {
                MessageBox.Show("请先打开一个 PDF 文件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            BatchBookmarkWindow window = new BatchBookmarkWindow();
            window.Owner = this;
            if (window.ShowDialog() == true)
            {
                int startPage = window.StartPage;
                int endPage = window.EndPage;
                string titlePrefix = window.TitlePrefix;

                for (int i = startPage; i <= endPage; i++)
                {
                    var newBookmark = new BookmarkItem
                    {
                        Title = $"{titlePrefix} {i}",
                        PageNumber = i - 1,
                        PageDisplay = i.ToString(),
                        Color = Colors.Gray,
                        FontWeight = FontWeights.Normal
                    };
                    bookmarkItems.Add(newBookmark);
                }
                
                bookmarkItems = bookmarkItems.OrderBy(b => b.PageNumber).ToList();
                UpdateBookmarkTree();
                StatusText.Text = $"已批量添加书签: 第 {startPage}-{endPage} 页";
            }
        }

        // 应用书签样式
        private void ApplyBookmarkStyle_Click(object sender, RoutedEventArgs e)
        {
            BookmarkItem item = GetSelectedBookmarkItem();
            if (item == null)
            {
                MessageBox.Show("请选择要设置样式的书签", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var selectedColor = (BookmarkColorCombo.SelectedItem as ComboBoxItem);
            if (selectedColor != null)
            {
                string colorHex = selectedColor.Tag.ToString();
                item.Color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorHex);
            }

            item.FontWeight = BookmarkBoldCheck.IsChecked == true ? FontWeights.Bold : FontWeights.Normal;
            UpdateBookmarkTree();
            StatusText.Text = $"已更新书签样式: {item.Title}";
        }

        // 拖拽排序
        private System.Windows.Point dragStartPoint;
        private BookmarkItem dragItem;

        private void BookmarkTree_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                dragItem = GetSelectedBookmarkItem();
                if (dragItem != null)
                {
                    DragDrop.DoDragDrop(BookmarkTree, dragItem, DragDropEffects.Move);
                }
            }
        }

        private void BookmarkTree_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(BookmarkItem)))
            {
                e.Effects = DragDropEffects.Move;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void BookmarkTree_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(BookmarkItem)))
            {
                BookmarkItem droppedItem = e.Data.GetData(typeof(BookmarkItem)) as BookmarkItem;
                
                // 获取目标位置
                var target = BookmarkTree.SelectedItem as BookmarkItem;
                if (target != null && target != droppedItem)
                {
                    // 从原位置移除
                    RemoveBookmarkItem(bookmarkItems, droppedItem);
                    
                    // 添加到新位置（作为同级书签，在目标之后）
                    int index = bookmarkItems.IndexOf(target);
                    if (index >= 0)
                    {
                        bookmarkItems.Insert(index + 1, droppedItem);
                    }
                    else
                    {
                        // 尝试在子节点中查找位置
                        InsertAfterItem(bookmarkItems, target, droppedItem);
                    }
                    
                    UpdateBookmarkTree();
                    StatusText.Text = $"已移动书签: {droppedItem.Title}";
                }
            }
        }

        private bool InsertAfterItem(List<BookmarkItem> items, BookmarkItem target, BookmarkItem newItem)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == target)
                {
                    items.Insert(i + 1, newItem);
                    return true;
                }
                if (InsertAfterItem(items[i].Children, target, newItem))
                    return true;
            }
            return false;
        }

        // ==================== 其他功能 ====================

        private void AddMergeFiles_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "PDF 文件 (*.pdf)|*.pdf";
            openFileDialog.Multiselect = true;
            if (openFileDialog.ShowDialog() == true)
            {
                foreach (string file in openFileDialog.FileNames)
                {
                    if (!MergeFileList.Items.Contains(file))
                    {
                        MergeFileList.Items.Add(file);
                    }
                }
            }
        }

        private void RemoveMergeFiles_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = MergeFileList.SelectedItems.Cast<object>().ToList();
            foreach (var item in selectedItems)
            {
                MergeFileList.Items.Remove(item);
            }
        }

        private void MoveMergeUp_Click(object sender, RoutedEventArgs e)
        {
            MoveItemUp(MergeFileList);
        }

        private void MoveMergeDown_Click(object sender, RoutedEventArgs e)
        {
            MoveItemDown(MergeFileList);
        }

        private void MovePageUp_Click(object sender, RoutedEventArgs e)
        {
            MoveItemUp(PageList);
        }

        private void MovePageDown_Click(object sender, RoutedEventArgs e)
        {
            MoveItemDown(PageList);
        }

        private void MoveItemUp(ListBox listBox)
        {
            if (listBox.SelectedIndex > 0)
            {
                int selectedIndex = listBox.SelectedIndex;
                object selectedItem = listBox.SelectedItem;
                listBox.Items.Remove(selectedItem);
                listBox.Items.Insert(selectedIndex - 1, selectedItem);
                listBox.SelectedIndex = selectedIndex - 1;
            }
        }

        private void MoveItemDown(ListBox listBox)
        {
            if (listBox.SelectedIndex < listBox.Items.Count - 1)
            {
                int selectedIndex = listBox.SelectedIndex;
                object selectedItem = listBox.SelectedItem;
                listBox.Items.Remove(selectedItem);
                listBox.Items.Insert(selectedIndex + 1, selectedItem);
                listBox.SelectedIndex = selectedIndex + 1;
            }
        }

        private void MergePdfs_Click(object sender, RoutedEventArgs e)
        {
            if (MergeFileList.Items.Count < 2)
            {
                MessageBox.Show("请至少添加两个 PDF 文件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "PDF 文件 (*.pdf)|*.pdf";
            if (saveFileDialog.ShowDialog() == true)
            {
                MergeProgress.Visibility = Visibility.Visible;
                MergeProgress.Value = 0;

                try
                {
                    using (FileStream fs = new FileStream(saveFileDialog.FileName, FileMode.Create))
                    {
                        iTextSharp.text.Document document = new iTextSharp.text.Document();
                        PdfCopy copy = new PdfCopy(document, fs);
                        document.Open();

                        int total = MergeFileList.Items.Count;
                        for (int i = 0; i < total; i++)
                        {
                            string pdfPath = MergeFileList.Items[i].ToString();
                            using (PdfReader reader = new PdfReader(pdfPath))
                            {
                                for (int j = 1; j <= reader.NumberOfPages; j++)
                                {
                                    copy.AddPage(copy.GetImportedPage(reader, j));
                                }
                            }
                            MergeProgress.Value = (i + 1) * 100 / total;
                            StatusText.Text = $"正在合并: {Path.GetFileName(pdfPath)}";
                        }

                        document.Close();
                    }

                    MergeProgress.Visibility = Visibility.Hidden;
                    MessageBox.Show($"合并完成！已保存到: {saveFileDialog.FileName}", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
                    MergeFileList.Items.Clear();
                }
                catch (Exception ex)
                {
                    MergeProgress.Visibility = Visibility.Hidden;
                    MessageBox.Show($"合并失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void DeletePages_Click(object sender, RoutedEventArgs e)
        {
            if (PageList.SelectedItems.Count == 0)
            {
                MessageBox.Show("请选择要删除的页面", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var selectedItems = PageList.SelectedItems.Cast<object>().ToList();
            foreach (var item in selectedItems)
            {
                PageList.Items.Remove(item);
            }
        }

        private void SaveEditedPdf_Click(object sender, RoutedEventArgs e)
        {
            if (PageList.Items.Count == 0)
            {
                MessageBox.Show("没有可保存的页面", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (string.IsNullOrEmpty(currentPdfPath))
            {
                MessageBox.Show("请先打开一个 PDF 文件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "PDF 文件 (*.pdf)|*.pdf";
            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    using (FileStream fs = new FileStream(saveFileDialog.FileName, FileMode.Create))
                    {
                        iTextSharp.text.Document document = new iTextSharp.text.Document();
                        PdfCopy copy = new PdfCopy(document, fs);
                        document.Open();

                        using (PdfReader reader = new PdfReader(currentPdfPath))
                        {
                            foreach (var item in PageList.Items)
                            {
                                string pageText = item.ToString();
                                int pageNum = int.Parse(pageText.Split('第', '页')[1].Trim());
                                copy.AddPage(copy.GetImportedPage(reader, pageNum));
                            }
                        }

                        document.Close();
                    }

                    MessageBox.Show($"已保存到: {saveFileDialog.FileName}", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SelectExportPdf_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "PDF 文件 (*.pdf)|*.pdf";
            if (openFileDialog.ShowDialog() == true)
            {
                ExportPdfPath.Text = openFileDialog.FileName;
            }
        }

        private void ExportFiles_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(ExportPdfPath.Text))
            {
                MessageBox.Show("请先选择要导出的 PDF 文件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!ExportWord.IsChecked.HasValue && !ExportExcel.IsChecked.HasValue && !ExportPpt.IsChecked.HasValue)
            {
                MessageBox.Show("请至少选择一种导出格式", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string baseName = Path.GetFileNameWithoutExtension(ExportPdfPath.Text);
            string directory = Path.GetDirectoryName(ExportPdfPath.Text);

            if (ExportWord.IsChecked.Value)
            {
                ExportToWord(Path.Combine(directory, $"{baseName}_word.docx"));
            }

            if (ExportExcel.IsChecked.Value)
            {
                ExportToExcel(Path.Combine(directory, $"{baseName}_excel.xlsx"));
            }

            if (ExportPpt.IsChecked.Value)
            {
                ExportToPpt(Path.Combine(directory, $"{baseName}_ppt.pptx"));
            }
        }

        private void ExportToWord(string outputPath)
        {
            try
            {
                string text = ExtractTextFromPdf(ExportPdfPath.Text);
                
                using (WordprocessingDocument doc = WordprocessingDocument.Create(outputPath, WordprocessingDocumentType.Document))
                {
                    MainDocumentPart mainPart = doc.AddMainDocumentPart();
                    mainPart.Document = new W.Document();
                    W.Body body = new W.Body();
                    
                    string[] paragraphs = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string paragraphText in paragraphs)
                    {
                        W.Paragraph paragraph = new W.Paragraph();
                        W.Run run = new W.Run();
                        W.Text textElement = new W.Text(paragraphText);
                        run.Append(textElement);
                        paragraph.Append(run);
                        body.Append(paragraph);
                    }
                    
                    mainPart.Document.Append(body);
                    mainPart.Document.Save();
                }
                MessageBox.Show($"Word 导出完成！已保存到: {outputPath}", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Word 导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportToExcel(string outputPath)
        {
            try
            {
                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("PDF 内容");
                    string text = ExtractTextFromPdf(ExportPdfPath.Text);
                    string[] lines = text.Split('\n');
                    worksheet.Cells["A1"].Value = "行号";
                    worksheet.Cells["B1"].Value = "内容";
                    for (int i = 0; i < lines.Length; i++)
                    {
                        worksheet.Cells[$"A{i + 2}"].Value = i + 1;
                        worksheet.Cells[$"B{i + 2}"].Value = lines[i];
                    }
                    package.SaveAs(new FileInfo(outputPath));
                }
                MessageBox.Show($"Excel 导出完成！已保存到: {outputPath}", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Excel 导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string ExtractTextFromPdf(string pdfPath)
        {
            using (PdfReader reader = new PdfReader(pdfPath))
            {
                string text = "";
                for (int i = 1; i <= reader.NumberOfPages; i++)
                {
                    text += iTextSharp.text.pdf.parser.PdfTextExtractor.GetTextFromPage(reader, i);
                }
                return text;
            }
        }

        private void ExportToPpt(string outputPath)
        {
            try
            {
                string pdfPath = ExportPdfPath.Text;
                
                fpdfview.FPDF_InitLibrary();
                
                try
                {
                    FpdfDocumentT doc = fpdfview.FPDF_LoadDocument(pdfPath, null);
                    if (doc == null)
                    {
                        throw new Exception("无法加载PDF文档");
                    }
                    
                    try
                    {
                        int pageCount = fpdfview.FPDF_GetPageCount(doc);
                        
                        using (var presentationDocument = PresentationDocument.Create(outputPath, PresentationDocumentType.Presentation))
                        {
                            var presentationPart = presentationDocument.AddPresentationPart();
                            presentationPart.Presentation = new P.Presentation();

                            var slideMasterIdList = new P.SlideMasterIdList();
                            var slideMasterId = new P.SlideMasterId() { Id = 2147483648U };
                            slideMasterIdList.Append(slideMasterId);

                            var slideIdList = new P.SlideIdList();
                            var slideSize = new P.SlideSize() { Cx = 9144000, Cy = 6858000, Type = P.SlideSizeValues.Screen4x3 };
                            var notesSize = new P.NotesSize() { Cx = 6858000L, Cy = 9144000L };

                            presentationPart.Presentation.Append(slideMasterIdList);
                            presentationPart.Presentation.Append(slideIdList);
                            presentationPart.Presentation.Append(slideSize);
                            presentationPart.Presentation.Append(notesSize);

                            var slideMasterPart = presentationPart.AddNewPart<SlideMasterPart>("rId1");
                            GenerateSlideMasterPartContent(slideMasterPart);

                            var themePart = slideMasterPart.AddNewPart<ThemePart>("rId2");
                            GenerateThemePartContent(themePart);

                            var slideLayoutPart = slideMasterPart.AddNewPart<SlideLayoutPart>("rId1");
                            GenerateSlideLayoutPartContent(slideLayoutPart);

                            for (int i = 0; i < pageCount; i++)
                            {
                                int slideNum = i + 1;
                                var slidePart = presentationPart.AddNewPart<SlidePart>($"rId{slideNum + 10}");

                                FpdfPageT page = fpdfview.FPDF_LoadPage(doc, i);
                                if (page != null)
                                {
                                    try
                                    {
                                        float width = (float)fpdfview.FPDF_GetPageWidth(page);
                                        float height = (float)fpdfview.FPDF_GetPageHeight(page);
                                        
                                        int renderWidth = (int)(width * 2);
                                        int renderHeight = (int)(height * 2);
                                        
                                        FpdfBitmapT bitmap = fpdfview.FPDFBitmapCreateEx(renderWidth, renderHeight, (int)FPDFBitmapFormat.BGRA, IntPtr.Zero, 0);
                                        fpdfview.FPDFBitmapFillRect(bitmap, 0, 0, renderWidth, renderHeight, 0xFFFFFFFF);
                                        fpdfview.FPDF_RenderPageBitmap(bitmap, page, 0, 0, renderWidth, renderHeight, 0, 0);

                                        IntPtr buffer = fpdfview.FPDFBitmapGetBuffer(bitmap);
                                        int stride = fpdfview.FPDFBitmapGetStride(bitmap);

                                        using (var bmp = new System.Drawing.Bitmap(renderWidth, renderHeight, stride, System.Drawing.Imaging.PixelFormat.Format32bppArgb, buffer))
                                        {
                                            using (var ms = new System.IO.MemoryStream())
                                            {
                                                bmp.RotateFlip(System.Drawing.RotateFlipType.Rotate180FlipX);
                                                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                                                ms.Position = 0;

                                                var imagePart = slidePart.AddNewPart<ImagePart>("image/png", $"rId2");
                                                imagePart.FeedData(ms);
                                            }
                                        }
                                    }
                                    finally
                                    {
                                        fpdfview.FPDF_ClosePage(page);
                                    }
                                }

                                GenerateSlidePartContent(slidePart, slideNum);

                                slidePart.AddPart(slideLayoutPart, "rId1");

                                var newSlideId = new P.SlideId() { Id = (uint)(255 + slideNum), RelationshipId = presentationPart.GetIdOfPart(slidePart) };
                                slideIdList.Append(newSlideId);
                            }

                            presentationPart.Presentation.Save();
                        }

                        MessageBox.Show($"PowerPoint 导出完成！共 {pageCount} 页，已保存到: {outputPath}", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    finally
                    {
                        fpdfview.FPDF_CloseDocument(doc);
                    }
                }
                finally
                {
                    fpdfview.FPDF_DestroyLibrary();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"PowerPoint 导出失败: {ex.Message}\n{ex.StackTrace}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void GenerateSlideMasterPartContent(SlideMasterPart slideMasterPart)
        {
            var slideMaster = new P.SlideMaster(
                new P.CommonSlideData(
                    new P.ShapeTree(
                        new P.NonVisualGroupShapeProperties(
                            new P.NonVisualDrawingProperties() { Id = 1U, Name = "" },
                            new P.NonVisualGroupShapeDrawingProperties(),
                            new P.ApplicationNonVisualDrawingProperties()),
                        new P.GroupShapeProperties(
                            new D.TransformGroup(
                                new D.Offset() { X = 0L, Y = 0L },
                                new D.Extents() { Cx = 0L, Cy = 0L },
                                new D.ChildOffset() { X = 0L, Y = 0L },
                                new D.ChildExtents() { Cx = 0L, Cy = 0L })))),
                new P.ColorMap()
                {
                    Background1 = D.ColorSchemeIndexValues.Light1,
                    Text1 = D.ColorSchemeIndexValues.Dark1,
                    Background2 = D.ColorSchemeIndexValues.Light2,
                    Text2 = D.ColorSchemeIndexValues.Dark2,
                    Accent1 = D.ColorSchemeIndexValues.Accent1,
                    Accent2 = D.ColorSchemeIndexValues.Accent2,
                    Accent3 = D.ColorSchemeIndexValues.Accent3,
                    Accent4 = D.ColorSchemeIndexValues.Accent4,
                    Accent5 = D.ColorSchemeIndexValues.Accent5,
                    Accent6 = D.ColorSchemeIndexValues.Accent6,
                    Hyperlink = D.ColorSchemeIndexValues.Hyperlink,
                    FollowedHyperlink = D.ColorSchemeIndexValues.FollowedHyperlink
                },
                new P.SlideLayoutIdList(
                    new P.SlideLayoutId() { Id = 2147483649U }));

            slideMasterPart.SlideMaster = slideMaster;
        }

        private static void GenerateSlideLayoutPartContent(SlideLayoutPart slideLayoutPart)
        {
            var slideLayout = new P.SlideLayout(
                new P.CommonSlideData(
                    new P.ShapeTree(
                        new P.NonVisualGroupShapeProperties(
                            new P.NonVisualDrawingProperties() { Id = 1U, Name = "" },
                            new P.NonVisualGroupShapeDrawingProperties(),
                            new P.ApplicationNonVisualDrawingProperties()),
                        new P.GroupShapeProperties(
                            new D.TransformGroup(
                                new D.Offset() { X = 0L, Y = 0L },
                                new D.Extents() { Cx = 0L, Cy = 0L },
                                new D.ChildOffset() { X = 0L, Y = 0L },
                                new D.ChildExtents() { Cx = 0L, Cy = 0L })))),
                new P.ColorMapOverride(new D.MasterColorMapping()))
            {
                Type = P.SlideLayoutValues.Blank
            };

            slideLayoutPart.SlideLayout = slideLayout;
        }

        private static void GenerateThemePartContent(ThemePart themePart)
        {
            var theme = new D.Theme(
                new D.ThemeElements(
                    new D.ColorScheme(
                        new D.Dark1Color(new D.SystemColor() { Val = D.SystemColorValues.WindowText, LastColor = "000000" }),
                        new D.Light1Color(new D.SystemColor() { Val = D.SystemColorValues.Window, LastColor = "FFFFFF" }),
                        new D.Dark2Color(new D.RgbColorModelHex() { Val = "1F497D" }),
                        new D.Light2Color(new D.RgbColorModelHex() { Val = "EEECE1" }),
                        new D.Accent1Color(new D.RgbColorModelHex() { Val = "4F81BD" }),
                        new D.Accent2Color(new D.RgbColorModelHex() { Val = "C0504D" }),
                        new D.Accent3Color(new D.RgbColorModelHex() { Val = "9BBB59" }),
                        new D.Accent4Color(new D.RgbColorModelHex() { Val = "8064A2" }),
                        new D.Accent5Color(new D.RgbColorModelHex() { Val = "4BACC6" }),
                        new D.Accent6Color(new D.RgbColorModelHex() { Val = "F79646" }),
                        new D.Hyperlink(new D.RgbColorModelHex() { Val = "0000FF" }),
                        new D.FollowedHyperlinkColor(new D.RgbColorModelHex() { Val = "800080" }))
                    { Name = "Office" },
                    new D.FontScheme(
                        new D.MajorFont(
                            new D.LatinFont() { Typeface = "Calibri" }),
                        new D.MinorFont(
                            new D.LatinFont() { Typeface = "Calibri" }))
                    { Name = "Office" },
                    new D.FormatScheme(
                        new D.FillStyleList(
                            new D.NoFill(),
                            new D.SolidFill(new D.SchemeColor() { Val = D.SchemeColorValues.PhColor }),
                            new D.GradientFill(
                                new D.GradientStopList(
                                    new D.GradientStop(new D.SchemeColor() { Val = D.SchemeColorValues.PhColor }) { Position = 0 }),
                                new D.LinearGradientFill() { Angle = 16200000, Scaled = true })),
                        new D.LineStyleList(
                            new D.Outline(new D.NoFill(), new D.PresetDash() { Val = D.PresetLineDashValues.Solid }) { Width = 9525, CapType = D.LineCapValues.Flat, CompoundLineType = D.CompoundLineValues.Single, Alignment = D.PenAlignmentValues.Center },
                            new D.Outline(new D.SolidFill(new D.SchemeColor() { Val = D.SchemeColorValues.PhColor }), new D.PresetDash() { Val = D.PresetLineDashValues.Solid }) { Width = 9525, CapType = D.LineCapValues.Flat, CompoundLineType = D.CompoundLineValues.Single, Alignment = D.PenAlignmentValues.Center },
                            new D.Outline(new D.SolidFill(new D.SchemeColor() { Val = D.SchemeColorValues.PhColor }), new D.PresetDash() { Val = D.PresetLineDashValues.Solid }) { Width = 9525, CapType = D.LineCapValues.Flat, CompoundLineType = D.CompoundLineValues.Single, Alignment = D.PenAlignmentValues.Center }),
                        new D.EffectStyleList(
                            new D.EffectStyle(new D.EffectList()),
                            new D.EffectStyle(new D.EffectList()),
                            new D.EffectStyle(new D.EffectList())),
                        new D.BackgroundFillStyleList(
                            new D.NoFill(),
                            new D.SolidFill(new D.SchemeColor() { Val = D.SchemeColorValues.PhColor }),
                            new D.GradientFill(
                                new D.GradientStopList(
                                    new D.GradientStop(new D.SchemeColor() { Val = D.SchemeColorValues.PhColor }) { Position = 0 }),
                                new D.LinearGradientFill() { Angle = 16200000, Scaled = true })))
                    { Name = "Office" }))
            { Name = "Office Theme" };

            themePart.Theme = theme;
        }

        private static void GenerateSlidePartContent(SlidePart slidePart, int slideNumber)
        {
            var slide = new P.Slide(
                new P.CommonSlideData(
                    new P.ShapeTree(
                        new P.NonVisualGroupShapeProperties(
                            new P.NonVisualDrawingProperties() { Id = 1U, Name = "" },
                            new P.NonVisualGroupShapeDrawingProperties(),
                            new P.ApplicationNonVisualDrawingProperties()),
                        new P.GroupShapeProperties(
                            new D.TransformGroup(
                                new D.Offset() { X = 0L, Y = 0L },
                                new D.Extents() { Cx = 0L, Cy = 0L },
                                new D.ChildOffset() { X = 0L, Y = 0L },
                                new D.ChildExtents() { Cx = 0L, Cy = 0L })),
                        new P.Picture(
                            new P.NonVisualPictureProperties(
                                new P.NonVisualDrawingProperties() { Id = 2U, Name = $"Picture {slideNumber}" },
                                new P.NonVisualPictureDrawingProperties(new D.PictureLocks() { NoChangeAspect = true }),
                                new P.ApplicationNonVisualDrawingProperties()),
                            new P.BlipFill(
                                new D.Blip() { Embed = "rId2" },
                                new D.Stretch(new D.FillRectangle())),
                            new P.ShapeProperties(
                                new D.Transform2D(
                                    new D.Offset() { X = 0L, Y = 0L },
                                    new D.Extents() { Cx = 9144000L, Cy = 6858000L }),
                                new D.PresetGeometry(new D.AdjustValueList()) { Preset = D.ShapeTypeValues.Rectangle })))),
                new P.ColorMapOverride(new D.MasterColorMapping()));

            slidePart.Slide = slide;
        }

        private static void CreateZipEntry(System.IO.Compression.ZipArchive zip, string entryName, string content)
        {
            var entry = zip.CreateEntry(entryName);
            using (var stream = entry.Open())
            using (var writer = new StreamWriter(stream, System.Text.Encoding.UTF8))
            {
                writer.Write(content);
            }
        }

        private void SaveBookmarks_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(currentPdfPath))
            {
                MessageBox.Show("请先打开一个 PDF 文件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "PDF 文件 (*.pdf)|*.pdf";
            saveFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    using (PdfWrapper.PdfDocument pdfDoc = new PdfWrapper.PdfDocument(currentPdfPath))
                    {
                        pdfDoc.ClearBookmarks();
                        
                        foreach (var item in bookmarkItems)
                        {
                            PdfWrapper.PdfBookmark bookmark = ConvertToPdfBookmark(item);
                            if (bookmark != null)
                            {
                                pdfDoc.AddBookmark(bookmark);
                            }
                        }
                        
                        pdfDoc.Save(saveFileDialog.FileName);
                    }

                    MessageBox.Show($"目录已保存到: {saveFileDialog.FileName}", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    string debugInfo = BookmarkDebug.CheckPdfBookmarks(saveFileDialog.FileName);
                    string debugPath = Path.Combine(Path.GetDirectoryName(saveFileDialog.FileName), "bookmark_debug.txt");
                    File.WriteAllText(debugPath, debugInfo);
                    MessageBox.Show($"调试信息已保存到: {debugPath}", "调试信息", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private PdfWrapper.PdfBookmark ConvertToPdfBookmark(BookmarkItem item)
        {
            if (item == null) return null;
            
            PdfWrapper.PdfBookmark bookmark = new PdfWrapper.PdfBookmark();
            bookmark.Title = item.Title;
            bookmark.PageNumber = item.PageNumber;
            
            if (item.Children != null && item.Children.Count > 0)
            {
                foreach (var child in item.Children)
                {
                    PdfWrapper.PdfBookmark childBookmark = ConvertToPdfBookmark(child);
                    if (childBookmark != null)
                    {
                        bookmark.Children.Add(childBookmark);
                    }
                }
            }
            
            return bookmark;
        }

        

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("PDF 编辑工具 v1.1\n\n一款功能强大的 PDF 处理工具，支持合并、编辑、导出、可视化书签管理等功能。", "关于", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        private void ShowBookmarkMenu_Click(object sender, RoutedEventArgs e)
        {
            // 显示书签操作菜单
            ContextMenu menu = new ContextMenu();
            
            MenuItem addItem = new MenuItem { Header = "添加书签" };
            addItem.Click += AddBookmark_Click;
            menu.Items.Add(addItem);
            
            MenuItem editItem = new MenuItem { Header = "编辑书签" };
            editItem.Click += EditBookmark_Click;
            menu.Items.Add(editItem);
            
            MenuItem deleteItem = new MenuItem { Header = "删除书签" };
            deleteItem.Click += DeleteBookmark_Click;
            menu.Items.Add(deleteItem);
            
            menu.Items.Add(new Separator());
            
            MenuItem childItem = new MenuItem { Header = "添加子书签" };
            childItem.Click += AddChildBookmark_Click;
            menu.Items.Add(childItem);
            
            menu.Items.Add(new Separator());
            
            MenuItem saveItem = new MenuItem { Header = "保存书签到PDF" };
            saveItem.Click += SaveBookmarks_Click;
            menu.Items.Add(saveItem);
            
            menu.IsOpen = true;
        }

        private void BookmarkSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 实时搜索书签
            string searchText = BookmarkSearchBox.Text.Trim().ToLower();
            if (string.IsNullOrEmpty(searchText))
            {
                UpdateBookmarkTree();
                return;
            }

            var filtered = FilterBookmarks(bookmarkItems, searchText);
            BookmarkTree.ItemsSource = null;
            BookmarkTree.ItemsSource = filtered;
        }
    }
}