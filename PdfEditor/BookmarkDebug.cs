using System;
using System.IO;
using System.Text;

namespace PdfEditor
{
    public class BookmarkDebug
    {
        public static string CheckPdfBookmarks(string pdfPath)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== PDF 书签检查 ===");
            sb.AppendLine($"文件: {pdfPath}");
            sb.AppendLine($"文件大小: {new FileInfo(pdfPath).Length} 字节");
            sb.AppendLine();

            try
            {
                byte[] data = File.ReadAllBytes(pdfPath);
                string content = Encoding.ASCII.GetString(data);

                int outlinesIndex = content.IndexOf("/Outlines");
                if (outlinesIndex >= 0)
                {
                    sb.AppendLine("找到 /Outlines 条目");
                    
                    int start = Math.Max(0, outlinesIndex - 50);
                    int length = Math.Min(500, content.Length - start);
                    sb.AppendLine("上下文:");
                    sb.AppendLine(content.Substring(start, length));
                    sb.AppendLine();
                }
                else
                {
                    sb.AppendLine("未找到 /Outlines 条目");
                }

                int titleCount = 0;
                int pos = 0;
                while ((pos = content.IndexOf("/Title", pos)) >= 0)
                {
                    titleCount++;
                    int start = Math.Max(0, pos - 10);
                    int length = Math.Min(200, content.Length - start);
                    sb.AppendLine($"\n--- 第 {titleCount} 个 /Title ---");
                    sb.AppendLine(content.Substring(start, length));
                    pos += 6;
                    if (titleCount > 20)
                    {
                        sb.AppendLine("... 更多 Title 省略");
                        break;
                    }
                }
                sb.AppendLine($"\n共找到 {titleCount} 个 /Title 条目");

                int destCount = 0;
                pos = 0;
                while ((pos = content.IndexOf("/Dest", pos)) >= 0)
                {
                    destCount++;
                    pos += 5;
                    if (destCount > 20) break;
                }
                sb.AppendLine($"共找到 {destCount} 个 /Dest 条目");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"错误: {ex.Message}");
            }

            return sb.ToString();
        }
    }
}
