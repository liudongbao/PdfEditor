#define PDFNATIVE_EXPORTS
#include "PdfNative.h"
#include <podofo/podofo.h>
#include <string>

struct PdfHandle {
    PoDoFo::PdfMemDocument* doc;
};

extern "C" {
    PDFNATIVE_API void* OpenPdf(const char* filePath) {
        try {
            PdfHandle* handle = new PdfHandle();
            handle->doc = new PoDoFo::PdfMemDocument(filePath);
            return handle;
        }
        catch (...) {
            return nullptr;
        }
    }

    PDFNATIVE_API void ClosePdf(void* handle) {
        if (!handle) return;
        PdfHandle* h = static_cast<PdfHandle*>(handle);
        if (h->doc) delete h->doc;
        delete h;
    }

    PDFNATIVE_API int GetPageCount(void* handle) {
        if (!handle) return 0;
        PdfHandle* h = static_cast<PdfHandle*>(handle);
        return h->doc->GetPageCount();
    }

    PDFNATIVE_API void GetBookmarks(void* handle, void* callback, void* userData) {
        if (!handle || !callback) return;
        
        PdfHandle* h = static_cast<PdfHandle*>(handle);
        BookmarkCallback cb = reinterpret_cast<BookmarkCallback>(callback);
        
        try {
            PoDoFo::PdfOutline* outline = h->doc->GetOutline();
            if (!outline) return;

            PoDoFo::PdfOutlineItem root = outline->GetRoot();
            for (int i = 0; i < root.GetChildCount(); i++) {
                PoDoFo::PdfOutlineItem child = root.GetChild(i);
                std::string title = child.GetTitle();
                
                PoDoFo::PdfDestination dest = child.GetDestination();
                int pageNum = 0;
                if (dest.IsValid()) {
                    PoDoFo::PdfPage* page = dest.GetPage();
                    if (page) {
                        pageNum = page->GetPageNumber();
                    }
                }
                
                cb(title.c_str(), pageNum, userData);
            }
        }
        catch (...) {
        }
    }

    PDFNATIVE_API void AddBookmark(void* handle, const char* title, int pageNumber) {
        if (!handle || !title) return;
        
        PdfHandle* h = static_cast<PdfHandle*>(handle);
        
        try {
            PoDoFo::PdfOutline* outline = h->doc->GetOutline();
            if (!outline) {
                outline = h->doc->CreateOutline();
            }

            PoDoFo::PdfOutlineItem root = outline->GetRoot();
            PoDoFo::PdfPage* page = h->doc->GetPage(pageNumber);
            if (!page) return;

            PoDoFo::PdfDestination dest(page, PoDoFo::EPdfDestination_Fit);
            root.CreateChild(title, dest);
        }
        catch (...) {
        }
    }

    PDFNATIVE_API void SavePdf(void* handle, const char* outputPath) {
        if (!handle || !outputPath) return;
        
        PdfHandle* h = static_cast<PdfHandle*>(handle);
        
        try {
            h->doc->Write(outputPath);
        }
        catch (...) {
        }
    }

    PDFNATIVE_API void MergePdfs(const char** pdfPaths, int count, const char* outputPath) {
        if (!pdfPaths || count <= 0 || !outputPath) return;
        
        try {
            PoDoFo::PdfMemDocument mergedDoc;

            for (int i = 0; i < count; i++) {
                PoDoFo::PdfMemDocument srcDoc(pdfPaths[i]);
                for (int j = 0; j < srcDoc.GetPageCount(); j++) {
                    PoDoFo::PdfPage* page = srcDoc.GetPage(j);
                    mergedDoc.InsertPage(page);
                }
            }

            mergedDoc.Write(outputPath);
        }
        catch (...) {
        }
    }
}