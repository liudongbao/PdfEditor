# PdfTool 项目开发会话记录

## 日期：2026-06-22 ~ 2026-06-23

---

## 一、项目概述

开发一款 Windows 桌面程序，支持以下功能：
1. PDF 合并（多个 PDF 合并成一个）
2. 删除单个 PDF 多余页面
3. 调整页面顺序
4. 维护文档目录（书签）
5. PDF 内容导出为 Word、PPT、Excel 等常用格式

---

## 二、技术架构演变

### 初始方案：Python
- 最初计划使用 Python + PyMuPDF (fitz) 实现
- 因用户要求改为 .NET 技术栈

### 第二阶段：.NET WPF + iTextSharp
- UI 框架：WPF
- PDF 处理：iTextSharp
- 导出功能：EPPlus (Excel)、Xceed.Words.NET (Word)、DocumentFormat.OpenXml (PPT)
- PDF 预览：WebView2 + PDF.js

### 第三阶段：.NET WPF + iText7
- 因书签兼容性问题，从 iTextSharp 切换到 iText7
- 书签问题仍未解决

### 当前方案：.NET WPF + C++/CLI + PoDoFo
- UI 框架：WPF (.NET 9.0-windows)
- PDF 处理：PoDoFo (C++ 库)
- 互操作层：C++/CLI 包装层（PdfWrapper 项目）
- 导出功能：EPPlus、Xceed.Words.NET、DocumentFormat.OpenXml
- PDF 预览：WebView2 + PDF.js

---

## 三、关键文件说明

### PdfEditor 项目（WPF 主程序）
- **PdfEditor.csproj** - 项目配置文件，目标框架 net9.0-windows
- **MainWindow.xaml / MainWindow.xaml.cs** - 主窗口 UI 和逻辑
  - PDF 合并
  - 页面编辑（删除、调整顺序）
  - 目录管理（书签可视化编辑）
  - 导出功能（Word、Excel、PPT）
  - PDF 预览（WebView2 + PDF.js）
- **pdfviewer.html** - PDF.js 预览页面

### PdfWrapper 项目（C++/CLI 包装层）
- **PdfWrapper.vcxproj** - C++/CLI 项目配置
- **PdfWrapper.h** - 托管类定义（PdfBookmark、PdfDocument）
- **PdfWrapper.cpp** - 托管类实现，封装 PoDoFo 调用
- **PdfNative.h / PdfNative.cpp** - 原生 C++ 封装（PIMPL 模式）
- **PdfNative.vcxproj** - 原生 C++ 项目配置

---

## 四、核心功能实现

### 1. PDF 预览
- 使用 WebView2 控件加载 PDF.js
- 通过 Base64 注入 PDF 内容，避免 file:// 协议限制
- 支持页码联动（左侧选择页面，右侧预览跳转）

### 2. 书签管理
- 左侧显示书签树，右侧显示 PDF 预览
- 支持添加、编辑、删除书签
- 滚动页面时可添加当前页书签
- 使用 PoDoFo 库确保书签在其他 PDF 阅读器中兼容

### 3. PDF 合并
- 选择多个 PDF 文件
- 按顺序合并为一个 PDF

### 4. 页面编辑
- 删除选中页面
- 调整页面顺序（拖拽排序）

### 5. 导出功能
- 导出为 Word (Xceed.Words.NET)
- 导出为 Excel (EPPlus)
- 导出为 PPT (DocumentFormat.OpenXml)

---

## 五、遇到的问题与解决方案

### 编译环境问题

1. **MSBuild 路径问题**
   - 问题：无法识别 msbuild 命令
   - 解决：使用 Visual Studio 2022 Developer Command Prompt

2. **平台工具集版本问题**
   - 问题：找不到 v143/v144/v145/ClangCL 工具集
   - 解决：安装 MSVC v143 生成工具，使用 v143 工具集

3. **.NET SDK 版本问题**
   - 问题：不支持 .NET 10.0
   - 解决：改为 .NET 9.0

4. **C++/CLI 运行库冲突**
   - 问题：`/clr:netcore` 和 `/MT` 不兼容
   - 解决：改为 MultiThreadedDLL (/MD)

5. **C++17 标准支持**
   - 问题：`string_view` 不是 std 成员
   - 解决：启用 C++17 标准

### 代码实现问题

6. **命名空间冲突（IServiceProvider）**
   - 问题：Windows.h 与 PoDoFo 类型冲突
   - 解决：使用 PIMPL 模式隔离 C++ 实现细节

7. **string_view 转换问题**
   - 问题：无法将 string_view 转换为 const string&
   - 解决：显式转换为 string

8. **PdfDestination 私有构造函数**
   - 问题：无法直接创建 PdfDestination 对象
   - 解决：使用 PdfDestination::TryCreateFromObject

### 功能问题

9. **PDF 预览不显示内容**
   - 问题：file:// 协议安全限制
   - 解决：C# 读取 PDF 为 Base64，通过 JavaScript 注入

10. **书签在其他阅读器不兼容**
    - 问题：iTextSharp/iText7 创建的书签在其他阅读器中无法跳转
    - 解决：切换到 PoDoFo 库（正在验证中）

11. **所有书签指向第一页**
    - 问题：页码处理逻辑错误
    - 解决：修正 PageNumber 设置逻辑

---

## 六、当前状态

### 已完成
- ✅ PdfWrapper (C++/CLI) 项目编译成功
- ✅ PdfEditor (WPF) 项目编译成功
- ✅ 程序可以启动运行
- ✅ 所有依赖 DLL 正确复制
- ✅ Git 提交（commit: 6162a68）

### 待解决
- ⚠️ PoDoFo 书签功能需要调试（功能不正常）
- ⚠️ 验证书签在其他 PDF 阅读器中的兼容性

---

## 七、Git 提交记录

### 最新提交
- **commit: 6162a68**
- **message:** feat: 引入PoDoFo库，通过C++/CLI包装层实现PDF书签操作
- **变更:** 8 files changed, 585 insertions(+), 40 deletions(-)

---

## 八、下一步计划

1. 调试 PdfWrapper 中 PoDoFo 书签功能
2. 验证书签读取和保存的正确性
3. 测试书签在其他 PDF 阅读器中的兼容性
4. 完善导出功能
5. 优化用户界面和交互体验

---

## 九、环境信息

- **操作系统:** Windows
- **开发工具:** Visual Studio 2022 Build Tools
- **.NET 版本:** .NET 9.0
- **C++ 标准:** C++17
- **平台工具集:** v143
- **目标平台:** x64
- **C++ 库管理:** vcpkg (podofo:x64-windows)
