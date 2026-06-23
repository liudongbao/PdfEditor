#pragma once

#ifdef PDFNATIVE_EXPORTS
#define PDFNATIVE_API __declspec(dllexport)
#else
#define PDFNATIVE_API __declspec(dllimport)
#endif

extern "C" {
    PDFNATIVE_API void* OpenPdf(const char* filePath);
    PDFNATIVE_API void ClosePdf(void* handle);
    PDFNATIVE_API int GetPageCount(void* handle);
    PDFNATIVE_API void GetBookmarks(void* handle, void* callback, void* userData);
    PDFNATIVE_API void AddBookmark(void* handle, const char* title, int pageNumber);
    PDFNATIVE_API void SavePdf(void* handle, const char* outputPath);
    PDFNATIVE_API void MergePdfs(const char** pdfPaths, int count, const char* outputPath);
}

typedef void (*BookmarkCallback)(const char* title, int pageNumber, void* userData);