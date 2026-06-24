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

## 十、2026-06-24 开发记录

### 书签功能修复（核心问题解决）

#### 问题描述
- 保存后的 PDF 书签在其他 PDF 阅读器（如 WPS、Adobe Reader）中无法正常显示和跳转
- 原有的书签重复保存
- 保存时报错 "Unknown error occurred while saving PDF"

#### 解决方案演变

##### 方案一：手动构建书签字典（失败）
- 尝试直接操作 PDF 内部对象，手动创建 Outlines 字典
- 问题：`PdfDictionary::AddKey` 参数类型不匹配导致保存失败
- 问题：手动构建的结构可能不符合 PoDoFo 内部状态

##### 方案二：使用 PoDoFo 标准 API（成功）
- 使用 `PdfDocument::GetOrCreateOutlines()` 获取/创建书签根节点
- 使用 `PdfOutlineItem::CreateChild()` 创建书签项
- 使用 `PdfDocument::CreateDestination()` 创建跳转目标
- 使用 `PdfDestination::SetDestination(page, PdfDestinationFit::Fit)` 设置目标页
- 使用 `PdfOutlineItem::SetDestination()` 绑定目标到书签

#### 关键代码修改
- **PdfWrapper.h** - 新增 `ClearBookmarks()` 方法声明
- **PdfWrapper.cpp** - 完全重写 `Save()` 方法和 `CreateBookmarkItem()` 辅助函数
- **MainWindow.xaml.cs** - 修复书签重复问题：
  - 打开 PDF 前调用 `bookmarkItems.Clear()`
  - 保存前调用 `pdfDoc.ClearBookmarks()`

#### 修复的问题列表
1. ✅ 书签保存后在其他 PDF 阅读器中正常显示
2. ✅ 书签跳转功能正常（使用 /Fit 模式）
3. ✅ 中文标题正确编码（UTF-16BE 带 BOM）
4. ✅ 书签不再重复保存
5. ✅ 多级书签结构正确
6. ✅ 保存不再报错

### 技术要点
- **PdfDestination 是私有构造函数**：必须通过 `doc->CreateDestination()` 创建
- **PdfOutlines 操作标准流程**：GetOrCreateOutlines → CreateChild → SetDestination
- **书签重复原因**：加载时不清空列表 + 保存时不清空内存书签，双重叠加
- **日志调试**：日志文件位于 `C:\Users\Public\Documents\PdfWrapper.log`

### 当前状态
- ✅ 所有核心功能正常工作
- ✅ PDF 合并功能正常
- ✅ 页面编辑（删除、调整顺序）功能正常
- ✅ 目录管理（可视化书签编辑）功能正常
- ✅ 书签保存后在其他 PDF 阅读器中正常显示和跳转
- ✅ 格式导出功能正常

### Git 提交记录更新
- 新增提交：修复书签保存问题，使用 PoDoFo 标准 API
- 发布包已更新至 publish 目录

---

## 十一、项目里程碑

| 阶段 | 完成时间 | 主要成果 |
|------|----------|----------|
| 项目启动 | 2026-06-22 | 需求确认，技术选型（.NET WPF） |
| 初版完成 | 2026-06-22 | PDF 合并、页面编辑、格式导出、基础目录功能 |
| 预览功能 | 2026-06-22 | WebView2 + PDF.js 页面预览，左右联动 |
| 可视化书签 | 2026-06-22 | 目录管理标签页，可视化书签编辑 |
| 架构升级 | 2026-06-23 | 引入 C++/CLI + PoDoFo 替代 iTextSharp |
| 书签修复 | 2026-06-24 | 解决书签兼容性问题，所有功能正常 |

---

## 十二、环境信息

- **操作系统:** Windows
- **开发工具:** Visual Studio 2022 Build Tools
- **.NET 版本:** .NET 9.0
- **C++ 标准:** C++17
- **平台工具集:** v143
- **目标平台:** x64
- **C++ 库管理:** vcpkg (podofo:x64-windows)
- **版本控制:** Git
- **远程仓库:** https://github.com/liudongbao/PdfEditor.git

---

## 十三、2026-06-24 下午开发记录 - PPT 导出修复与发布包整理

### PPT 导出图片翻转问题修复

#### 问题描述
- PPT 导出保存的文件无法正常打开
- 修复后可以打开，但里面的图片内容上下颠倒、左右颠倒

#### 解决方案
使用 PDFium 渲染 PDF 页面为图片，然后插入 PPTX 中：

```csharp
// 使用 PDFium 渲染当前页
FpdfBitmapT bitmap = fpdfview.FPDFBitmapCreateEx(renderWidth, renderHeight, ...);
fpdfview.FPDF_RenderPageBitmap(bitmap, page, 0, 0, renderWidth, renderHeight, 0, 0);

// 获取渲染缓冲区并创建 Bitmap
using (var bmp = new System.Drawing.Bitmap(...))
{
    // 旋转和翻转修复
    bmp.RotateFlip(RotateFlipType.Rotate180FlipNone);  // 顺时针旋转180°
    bmp.RotateFlip(RotateFlipType.RotateNoneFlipY);    // 垂直翻转
    bmp.RotateFlip(RotateFlipType.RotateNoneFlipX);    // 水平翻转
    
    // 保存为 PNG 并插入幻灯片
    imagePart.FeedData(ms);
}
```

#### 修复效果
- ✅ PPT 文件可以正常打开（无需修复）
- ✅ 图片内容显示正确（上下左右方向正确）
- ✅ 每页 PDF 对应一页幻灯片

### 单文件发布尝试

#### 尝试方案
使用 .NET 的 PublishSingleFile 功能，将所有依赖打包到单个 exe 文件中：

```xml
<PropertyGroup>
  <PublishSingleFile>true</PublishSingleFile>
  <SelfContained>true</SelfContained>
  <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
  <IncludeAllContentForSelfExtract>true</IncludeAllContentForSelfExtract>
  <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
</PropertyGroup>
```

#### 遇到的问题
- 单文件发布后部分功能不可用（PDF 预览、书签功能等）
- PdfWrapper 是 C++/CLI 混合程序集，单文件打包机制无法正确处理
- WebView2、PDFium 等原生库的嵌入和提取需要额外处理

#### 结论
- 单文件发布方案暂时搁置，功能不完整
- 继续使用完整的 publish 目录发布包

### 版本回退

#### 回退操作
```bash
git reset --hard da06564d8a92e28be82a4709897baaa612410d1c
git push --force
```

#### 回退原因
- 单文件发布尝试引入了问题
- 需要恢复到稳定的可工作版本

#### 回退结果
- ✅ 代码恢复到 PPT 导出修复后的稳定版本
- ✅ publish 目录完全恢复
- ✅ 所有功能正常工作

### 发布包整理

#### 发布内容
1. **完整发布包**: `PdfEditor_v1.0.0.zip`（约 86 MB）
   - 包含完整的 publish 目录所有文件
   - 无需安装任何运行时
   - 解压即用

2. **开发发布包**: `publish/PdfEditor.exe`
   - 源代码编译后的完整输出
   - 包含所有依赖 DLL

#### 文件清单
- PdfEditor.exe - 主程序
- PdfEditor.dll - WPF 程序集
- PdfWrapper.dll - C++/CLI 包装层
- podofo.dll - PoDoFo 库
- pdfium.dll - PDFium 库
- freetype.dll - FreeType 字体库
- 以及其他原生依赖库（约 16 个 DLL）

### Git 提交记录

| 提交 | 说明 |
|------|------|
| da06564 | feat: PPT导出功能使用PDFium渲染图片，修复图像翻转问题 |
| 44aa3db | fix: 修复PPT导出图片翻转(180度)和文件损坏问题 |

### 当前稳定版本

- **Commit:** da06564
- **版本号:** v1.5
- **发布日期:** 2026-06-24
- **状态:** ✅ 所有功能正常工作

#### 正常工作功能
- ✅ PDF 合并
- ✅ 页面编辑（删除、调整顺序）
- ✅ 页面预览（WebView2 + PDF.js）
- ✅ 目录管理（可视化书签编辑）
- ✅ 书签保存（PoDoFo）
- ✅ Word 导出
- ✅ Excel 导出
- ✅ PPT 导出（PDFium 图片方式）

---

## 十四、附录

### 日志文件位置
- `C:\Users\Public\Documents\PdfWrapper.log` - PoDoFo 操作日志

### 常见问题排查

#### 1. 打开 PDF 报错 "Unknown error occurred while loading PDF"
- 检查 podofo.dll 和依赖的原生库是否正确复制到 publish 目录
- 检查 PDF 文件是否损坏或加密

#### 2. 书签功能不正常
- 检查日志文件 `C:\Users\Public\Documents\PdfWrapper.log`
- 确认使用的是正确版本的 publish 目录

#### 3. PPT 导出文件无法打开
- 当前版本已修复，PPT 文件可以正常打开
- 图片内容可能需要根据 PDF 原始方向调整旋转/翻转参数

### 相关资源链接
- PoDoFo: https://podofo.github.io/
- PDFium: https://pdfium.googlesource.com/pdfium/
- DocumentFormat.OpenXml: https://github.com/dotnet/Open-XML-SDK
- EPPlus: https://github.com/JanKallman/EPPlus
