using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using iTextSharp.text;
using iTextSharp.text.pdf;
using PdfTextExtractor = iTextSharp.text.pdf.parser.PdfTextExtractor;
using OfficeOpenXml;
using Xceed.Words.NET;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using P = DocumentFormat.OpenXml.Presentation;
using D = DocumentFormat.OpenXml.Drawing;

namespace PdfEditor
{
    public partial class MainWindow : Window
    {
        private string currentPdfPath;

        public MainWindow()
        {
            InitializeComponent();
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

            try
            {
                using (PdfReader reader = new PdfReader(filePath))
                {
                    for (int i = 0; i < reader.NumberOfPages; i++)
                    {
                        PageList.Items.Add($"第 {i + 1} 页");
                    }
                }
                StatusText.Text = $"已打开: {Path.GetFileName(filePath)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开 PDF 失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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
                        Document document = new Document();
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
                    using (PdfReader reader = new PdfReader(currentPdfPath))
                    {
                        using (FileStream fs = new FileStream(saveFileDialog.FileName, FileMode.Create))
                        {
                            Document document = new Document();
                            PdfCopy copy = new PdfCopy(document, fs);
                            document.Open();

                            foreach (var item in PageList.Items)
                            {
                                string pageText = item.ToString();
                                int pageNum = int.Parse(pageText.Split('第', '页')[1].Trim()) - 1;
                                copy.AddPage(copy.GetImportedPage(reader, pageNum + 1));
                            }

                            document.Close();
                        }
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
                using (var doc = DocX.Create(outputPath))
                {
                    doc.InsertParagraph(text);
                    doc.Save();
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
                    text += PdfTextExtractor.GetTextFromPage(reader, i);
                }
                return text;
            }
        }

        private void ExportToPpt(string outputPath)
        {
            try
            {
                string text = ExtractTextFromPdf(ExportPdfPath.Text);
                using (PresentationDocument presentationDocument = PresentationDocument.Create(outputPath, PresentationDocumentType.Presentation))
                {
                    PresentationPart presentationPart = presentationDocument.AddPresentationPart();
                    presentationPart.Presentation = new Presentation();

                    SlideMasterPart slideMasterPart = presentationPart.AddNewPart<SlideMasterPart>("rId1");
                    slideMasterPart.SlideMaster = new SlideMaster();
                    slideMasterPart.SlideMaster.CommonSlideData = new CommonSlideData();
                    slideMasterPart.SlideMaster.CommonSlideData.ShapeTree = new ShapeTree();

                    Shape shape = new Shape();
                    shape.NonVisualShapeProperties = new NonVisualShapeProperties();
                    shape.NonVisualShapeProperties.NonVisualDrawingProperties = new NonVisualDrawingProperties { Id = 1, Name = "Title" };
                    shape.NonVisualShapeProperties.NonVisualShapeDrawingProperties = new NonVisualShapeDrawingProperties();
                    shape.NonVisualShapeProperties.ApplicationNonVisualDrawingProperties = new ApplicationNonVisualDrawingProperties();
                    shape.ShapeProperties = new ShapeProperties();
                    slideMasterPart.SlideMaster.CommonSlideData.ShapeTree.Append(shape);

                    SlideLayoutPart slideLayoutPart = slideMasterPart.AddNewPart<SlideLayoutPart>("rId1");
                    slideLayoutPart.SlideLayout = new SlideLayout();
                    slideLayoutPart.SlideLayout.CommonSlideData = new CommonSlideData();
                    slideLayoutPart.SlideLayout.CommonSlideData.ShapeTree = new ShapeTree();

                    SlidePart slidePart = presentationPart.AddNewPart<SlidePart>("rId2");
                    slidePart.Slide = new Slide();
                    slidePart.Slide.CommonSlideData = new CommonSlideData();
                    slidePart.Slide.CommonSlideData.ShapeTree = new ShapeTree();

                    string[] paragraphs = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    double top = 50;
                    foreach (string paragraph in paragraphs.Take(50))
                    {
                        Shape textShape = new Shape();
                        textShape.NonVisualShapeProperties = new NonVisualShapeProperties();
                        textShape.NonVisualShapeProperties.NonVisualDrawingProperties = new NonVisualDrawingProperties { Id = (uint)(slidePart.Slide.CommonSlideData.ShapeTree.ChildElements.Count + 1), Name = "Text" };
                        textShape.NonVisualShapeProperties.NonVisualShapeDrawingProperties = new NonVisualShapeDrawingProperties();
                        textShape.NonVisualShapeProperties.ApplicationNonVisualDrawingProperties = new ApplicationNonVisualDrawingProperties();
                        textShape.ShapeProperties = new P.ShapeProperties();
                        textShape.ShapeProperties.Transform2D = new D.Transform2D();
                        textShape.ShapeProperties.Transform2D.Offset = new D.Offset { X = 500000, Y = (long)(top * 9525) };
                        textShape.ShapeProperties.Transform2D.Extents = new D.Extents { Cx = 8000000, Cy = 500000 };
                        textShape.TextBody = new P.TextBody();
                        textShape.TextBody.BodyProperties = new D.BodyProperties();
                        textShape.TextBody.ListStyle = new D.ListStyle();
                        textShape.TextBody.Append(new D.Paragraph(new D.Run(new D.Text(paragraph))));
                        slidePart.Slide.CommonSlideData.ShapeTree.Append(textShape);
                        top += 25;
                        if (top > 600)
                        {
                            slidePart = presentationPart.AddNewPart<SlidePart>("rId" + (presentationPart.SlideParts.Count() + 1));
                            slidePart.Slide = new Slide();
                            slidePart.Slide.CommonSlideData = new CommonSlideData();
                            slidePart.Slide.CommonSlideData.ShapeTree = new ShapeTree();
                            top = 50;
                        }
                    }

                    presentationPart.Presentation.SlideIdList = new SlideIdList();
                    int slideIndex = 1;
                    foreach (var slide in presentationPart.SlideParts)
                    {
                        SlideId slideId = new SlideId();
                        slideId.Id = (uint)(256 + slideIndex);
                        slideId.RelationshipId = presentationPart.GetIdOfPart(slide);
                        presentationPart.Presentation.SlideIdList.Append(slideId);
                        slideIndex++;
                    }

                    presentationPart.Presentation.Save();
                }
                MessageBox.Show($"PowerPoint 导出完成！已保存到: {outputPath}", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"PowerPoint 导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddBookmark_Click(object sender, RoutedEventArgs e)
        {
            BookmarkWindow window = new BookmarkWindow();
            if (window.ShowDialog() == true)
            {
                TreeViewItem item = new TreeViewItem();
                item.Header = window.TitleText;
                item.Tag = window.PageNumber;
                BookmarkTree.Items.Add(item);
            }
        }

        private void EditBookmark_Click(object sender, RoutedEventArgs e)
        {
            if (BookmarkTree.SelectedItem is TreeViewItem selectedItem)
            {
                BookmarkWindow window = new BookmarkWindow((string)selectedItem.Header, (int)selectedItem.Tag);
                if (window.ShowDialog() == true)
                {
                    selectedItem.Header = window.TitleText;
                    selectedItem.Tag = window.PageNumber;
                }
            }
            else
            {
                MessageBox.Show("请选择要编辑的书签", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void DeleteBookmark_Click(object sender, RoutedEventArgs e)
        {
            if (BookmarkTree.SelectedItem is TreeViewItem selectedItem)
            {
                BookmarkTree.Items.Remove(selectedItem);
            }
            else
            {
                MessageBox.Show("请选择要删除的书签", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
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
            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    using (PdfReader reader = new PdfReader(currentPdfPath))
                    {
                        using (FileStream fs = new FileStream(saveFileDialog.FileName, FileMode.Create))
                        {
                            PdfStamper stamper = new PdfStamper(reader, fs);
                            AddBookmarksToPdf(stamper, BookmarkTree.Items, null);
                            stamper.Close();
                        }
                    }

                    MessageBox.Show($"目录已保存到: {saveFileDialog.FileName}", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void AddBookmarksToPdf(PdfStamper stamper, ItemCollection items, PdfOutline parent)
        {
            foreach (TreeViewItem item in items)
            {
                string title = (string)item.Header;
                int pageNum = (int)item.Tag;
                PdfDestination dest = new PdfDestination(PdfDestination.FIT);
                dest.AddPage(stamper.Writer.GetImportedPage(new PdfReader(currentPdfPath), pageNum + 1).IndirectReference);
                PdfOutline outline = new PdfOutline(parent, dest, title);
                if (item.Items.Count > 0)
                {
                    AddBookmarksToPdf(stamper, item.Items, outline);
                }
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("PDF 编辑工具 v1.0\n\n一款功能强大的 PDF 处理工具，支持合并、编辑、导出等功能。", "关于", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }
    }
}