#include "PdfWrapper.h"

#define WIN32_LEAN_AND_MEAN
#include <podofo/podofo.h>
#include <string>

namespace PdfWrapper
{
    static std::string ToStdString(System::String^ str)
    {
        const char* chars = (const char*)(System::Runtime::InteropServices::Marshal::StringToHGlobalAnsi(str)).ToPointer();
        std::string result(chars);
        System::Runtime::InteropServices::Marshal::FreeHGlobal(System::IntPtr((void*)chars));
        return result;
    }

    static System::String^ ToManagedString(const std::string& str)
    {
        return gcnew System::String(str.c_str());
    }

    PdfDocument::PdfDocument(System::String^ filePath)
    {
        std::string path = ToStdString(filePath);
        PoDoFo::PdfMemDocument* doc = new PoDoFo::PdfMemDocument();
        doc->Load(path);
        m_NativeDoc = doc;
        m_Bookmarks = gcnew System::Collections::Generic::List<PdfBookmark^>();
        LoadBookmarks();
    }

    PdfDocument::~PdfDocument()
    {
        this->!PdfDocument();
    }

    PdfDocument::!PdfDocument()
    {
        if (m_NativeDoc != nullptr)
        {
            delete (PoDoFo::PdfMemDocument*)m_NativeDoc;
            m_NativeDoc = nullptr;
        }
    }

    int PdfDocument::GetPageCount()
    {
        PoDoFo::PdfMemDocument* doc = (PoDoFo::PdfMemDocument*)m_NativeDoc;
        return static_cast<int>(doc->GetPages().GetCount());
    }

    System::Collections::Generic::List<PdfBookmark^>^ PdfDocument::GetBookmarks()
    {
        return m_Bookmarks;
    }

    void PdfDocument::LoadBookmarks()
    {
        PoDoFo::PdfMemDocument* doc = (PoDoFo::PdfMemDocument*)m_NativeDoc;
        PoDoFo::PdfOutlines* outlines = doc->GetOutlines();
        if (outlines == nullptr)
            return;

        PoDoFo::PdfOutlineItem* child = outlines->First();
        while (child != nullptr)
        {
            LoadBookmarksFromItem(child, m_Bookmarks);
            child = child->Next();
        }
    }

    void PdfDocument::LoadBookmarksFromItem(void* itemPtr, System::Collections::Generic::List<PdfBookmark^>^ bookmarks)
    {
        PoDoFo::PdfOutlineItem* item = (PoDoFo::PdfOutlineItem*)itemPtr;
        PdfBookmark^ bookmark = gcnew PdfBookmark();
        bookmark->Title = ToManagedString(std::string(item->GetTitle().GetString()));

        auto dest = item->GetDestination();
        if (dest.has_value())
        {
            PoDoFo::PdfPage* page = dest.value().GetPage();
            if (page != nullptr)
                bookmark->PageNumber = static_cast<int>(page->GetPageNumber() - 1);
        }

        PoDoFo::PdfOutlineItem* child = item->First();
        while (child != nullptr)
        {
            LoadBookmarksFromItem(child, bookmark->Children);
            child = child->Next();
        }

        bookmarks->Add(bookmark);
    }

    void PdfDocument::AddBookmark(PdfBookmark^ bookmark)
    {
        m_Bookmarks->Add(bookmark);
    }

    void PdfDocument::Save(System::String^ outputPath)
    {
        PoDoFo::PdfMemDocument* doc = (PoDoFo::PdfMemDocument*)m_NativeDoc;
        PoDoFo::PdfOutlines* outlines = doc->GetOutlines();
        if (outlines == nullptr)
            return;

        for each (PdfBookmark^ bookmark in m_Bookmarks)
        {
            AddBookmarkToItem(outlines, bookmark);
        }

        std::string path = ToStdString(outputPath);
        doc->Save(path);
    }

    void PdfDocument::AddBookmarkToItem(void* parentPtr, PdfBookmark^ bookmark)
    {
        PoDoFo::PdfMemDocument* doc = (PoDoFo::PdfMemDocument*)m_NativeDoc;
        PoDoFo::PdfOutlineItem* parent = (PoDoFo::PdfOutlineItem*)parentPtr;

        std::string title = ToStdString(bookmark->Title);
        PoDoFo::PdfOutlineItem& item = parent->CreateChild(PoDoFo::PdfString(title));

        int pageIndex = bookmark->PageNumber;
        if (pageIndex >= 0 && pageIndex < static_cast<int>(doc->GetPages().GetCount()))
        {
            PoDoFo::PdfPage& page = doc->GetPages().GetPageAt(pageIndex);
            
            PoDoFo::PdfArray destArray;
            destArray.Add(page.GetObject().GetIndirectReference());
            destArray.Add(PoDoFo::PdfName("Fit"));
            
            PoDoFo::PdfObject& destObj = doc->GetObjects().CreateObject(destArray);
            std::unique_ptr<PoDoFo::PdfDestination> dest;
            if (PoDoFo::PdfDestination::TryCreateFromObject(destObj, dest))
            {
                item.SetDestination(PoDoFo::nullable<const PoDoFo::PdfDestination&>(*dest));
            }
        }

        if (bookmark->Children != nullptr && bookmark->Children->Count > 0)
        {
            for each (PdfBookmark^ child in bookmark->Children)
            {
                AddBookmarkToItem(&item, child);
            }
        }
    }

    void PdfDocument::Merge(cli::array<System::String^>^ pdfPaths, System::String^ outputPath)
    {
        throw gcnew System::NotSupportedException("Merge function not yet implemented");
    }
}
