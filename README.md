# PDF 编辑工具

一款面向 Windows 办公用户的 PDF 桌面工具，提供 PDF 合并、页面编辑、目录维护和格式导出功能。

## 快速下载

| 文件 | 大小 | 说明 |
|------|------|------|
| [PdfEditor_v1.0.0.zip](PdfEditor_v1.0.0.zip) | ~86 MB | ⭐ **推荐**：完整发布包，解压即用，无需安装任何依赖 |

## 运行时需求

| 发布包 | .NET 9.0 运行时 | 说明 |
|--------|------------------|------|
| `PdfEditor_v1.0.0.zip` | ❌ 不需要 | 自包含版，已内置 .NET 9.0 运行时 |
| `publish/` 目录 | ❌ 不需要 | 开发发布包，已内置 .NET 9.0 运行时 |

**所有发布包均为自包含版本，无需用户安装 .NET 9.0 运行时。**

## 功能特性

| 功能 | 说明 |
|------|------|
| PDF 合并 | 将多个 PDF 文件合并成一个完整文档 |
| 页面编辑 | 删除多余页面、调整页面顺序 |
| 目录维护 | 添加、编辑、删除 PDF 书签/目录 |
| 格式导出 | 导出为 Word (.docx)、Excel (.xlsx)、PowerPoint (.pptx) |

## 技术栈

- **框架**: .NET 9.0 Windows (WPF)
- **PDF 处理**: PoDoFo (C++ PDF 库，通过 C++/CLI 包装)、iTextSharp (文本提取、合并)
- **Word 导出**: DocumentFormat.OpenXml (OpenXML SDK)
- **Excel 导出**: EPPlus 5.8.10
- **PowerPoint 导出**: DocumentFormat.OpenXml 3.0.2
- **PDF 预览**: WebView2 + PDF.js

## 使用方法

### 运行方式

1. **直接运行**（推荐）:
   ```
   打开 publish/PdfEditor.exe
   ```

2. **开发环境运行**:
   ```bash
   cd PdfEditor
   dotnet run
   ```

### 发布项目

```bash
dotnet publish PdfEditor/PdfEditor.csproj -c Release -r win-x64 --self-contained true -o publish
```

## 项目结构

```
pdftool/
├── PdfEditor/           # WPF 项目源代码
│   ├── App.xaml / .cs   # 应用程序入口
│   ├── MainWindow.xaml / .cs  # 主窗口
│   ├── BookmarkDebug.cs # 书签调试工具
│   └── PdfEditor.csproj # 项目配置
├── PdfWrapper/          # C++/CLI 包装层（PoDoFo 封装）
│   ├── PdfWrapper.h / .cpp  # 包装层实现
│   └── PdfWrapper.vcxproj   # C++ 项目配置
├── publish/             # 自包含发布包
├── README.md            # 项目说明
├── prd.md               # 产品需求文档
├── session.md           # 开发会话记录
└── .gitignore           # Git 忽略规则
```

## 核心功能说明

### 1. PDF 合并
- 支持选择多个 PDF 文件
- 支持调整合并顺序
- 进度条显示合并进度

### 2. 页面编辑
- 打开单个 PDF 文件
- 多选删除页面
- 上下调整页面顺序
- 保存修改后的 PDF

### 3. 目录管理
- 添加书签（标题 + 页码）
- 编辑现有书签
- 删除书签
- 保存目录到 PDF 文件

### 4. 格式导出
- **Word**: 提取 PDF 文本内容生成 .docx 文件
- **Excel**: 将文本按行导入 Excel 表格
- **PowerPoint**: 将文本内容生成演示文稿

## 许可证

MIT License

## 注意事项

- 本工具为自包含发布，无需安装 .NET 运行时
- 支持 Windows 10/11 64 位系统
- 导出功能依赖第三方库，请确保遵守相关许可协议