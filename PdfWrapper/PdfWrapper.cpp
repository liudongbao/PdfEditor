#include "PdfWrapper.h"

#define WIN32_LEAN_AND_MEAN
#include <podofo/podofo.h>
#include <string>
#include <iostream>
#include <fstream>
#include <ctime>

static void WriteLog(const char* message)
{
    try
    {
        std::string logPath = "C:\\Users\\Public\\Documents\\PdfWrapper.log";
        std::ofstream logFile(logPath, std::ios::app);
        if (logFile.is_open())
        {
            time_t now = time(nullptr);
            char buf[32];
            strftime(buf, sizeof(buf), "%Y-%m-%d %H:%M:%S", localtime(&now));
            logFile << "[" << buf << "] " << message << std::endl;
            logFile.close();
        }
    }
    catch (...) {}
}

static void WriteLog(const std::string& message)
{
    WriteLog(message.c_str());
}

static void ClearLog()
{
    try
    {
        std::string logPath = "C:\\Users\\Public\\Documents\\PdfWrapper.log";
        std::ofstream logFile(logPath, std::ios::trunc);
        logFile.close();
    }
    catch (...) {}
}

namespace PdfWrapper
{
    static std::string ToUtf8String(System::String^ str)
    {
        array<unsigned char>^ bytes = System::Text::Encoding::UTF8->GetBytes(str);
        pin_ptr<unsigned char> pinned = &bytes[0];
        char* chars = reinterpret_cast<char*>(pinned);
        return std::string(chars, bytes->Length);
    }

    static PoDoFo::PdfString ToPdfString(System::String^ str)
    {
        array<unsigned char>^ utf16Bytes = System::Text::Encoding::BigEndianUnicode->GetBytes(str);
        
        std::vector<unsigned char> bytes;
        bytes.push_back(0xFE);
        bytes.push_back(0xFF);
        
        for (int i = 0; i < utf16Bytes->Length; i++)
        {
            bytes.push_back(utf16Bytes[i]);
        }
        
        PoDoFo::charbuff buff(bytes.size());
        memcpy(buff.data(), bytes.data(), bytes.size());
        
        return PoDoFo::PdfString::FromRaw(buff, true);
    }

    static System::String^ ToManagedString(const std::string& str)
    {
        array<unsigned char>^ bytes = gcnew array<unsigned char>(str.length());
        for (int i = 0; i < str.length(); i++)
            bytes[i] = reinterpret_cast<const unsigned char*>(str.c_str())[i];
        return System::Text::Encoding::UTF8->GetString(bytes);
    }

    PdfDocument::PdfDocument(System::String^ filePath)
    {
        m_NativeDoc = nullptr;
        m_Bookmarks = gcnew System::Collections::Generic::List<PdfBookmark^>();

        try
        {
            std::string path = ToUtf8String(filePath);
            WriteLog("Loading PDF: " + path);
            
            PoDoFo::PdfMemDocument* doc = new PoDoFo::PdfMemDocument();
            WriteLog("Created PdfMemDocument");
            
            doc->Load(path);
            WriteLog("Loaded PDF successfully");
            
            m_NativeDoc = doc;
            LoadBookmarks();
            WriteLog("Loaded bookmarks");
        }
        catch (const PoDoFo::PdfError& e)
        {
            std::string error = "PoDoFo error: " + std::string(e.what());
            WriteLog(error);
            throw gcnew System::Exception(gcnew System::String(error.c_str()));
        }
        catch (const std::exception& e)
        {
            std::string error = "std::exception: " + std::string(e.what());
            WriteLog(error);
            throw gcnew System::Exception(gcnew System::String(error.c_str()));
        }
        catch (...)
        {
            WriteLog("Unknown error occurred while loading PDF");
            throw gcnew System::Exception("Unknown error occurred while loading PDF");
        }
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
        try
        {
            PoDoFo::PdfMemDocument* doc = (PoDoFo::PdfMemDocument*)m_NativeDoc;
            if (!doc) return 0;
            return static_cast<int>(doc->GetPages().GetCount());
        }
        catch (...)
        {
            return 0;
        }
    }

    System::Collections::Generic::List<PdfBookmark^>^ PdfDocument::GetBookmarks()
    {
        return m_Bookmarks;
    }

    void PdfDocument::LoadBookmarks()
    {
        try
        {
            PoDoFo::PdfMemDocument* doc = (PoDoFo::PdfMemDocument*)m_NativeDoc;
            if (!doc) return;

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
        catch (...)
        {
        }
    }

    void PdfDocument::LoadBookmarksFromItem(void* itemPtr, System::Collections::Generic::List<PdfBookmark^>^ bookmarks)
    {
        try
        {
            PoDoFo::PdfOutlineItem* item = (PoDoFo::PdfOutlineItem*)itemPtr;
            if (!item) return;

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
        catch (...)
        {
        }
    }

    void PdfDocument::AddBookmark(PdfBookmark^ bookmark)
    {
        m_Bookmarks->Add(bookmark);
    }

    void PdfDocument::ClearBookmarks()
    {
        m_Bookmarks->Clear();
    }

    static void CreateBookmarkItem(PoDoFo::PdfOutlineItem& parent, PdfBookmark^ bookmark, PoDoFo::PdfMemDocument* doc)
    {
        PoDoFo::PdfOutlineItem& item = parent.CreateChild(ToPdfString(bookmark->Title));
        
        int pageIndex = bookmark->PageNumber;
        if (pageIndex >= 0 && pageIndex < static_cast<int>(doc->GetPages().GetCount()))
        {
            PoDoFo::PdfPage& page = doc->GetPages().GetPageAt(pageIndex);
            auto dest = doc->CreateDestination();
            dest->SetDestination(page, PoDoFo::PdfDestinationFit::Fit);
            item.SetDestination(*dest);
        }
        
        if (bookmark->Children != nullptr && bookmark->Children->Count > 0)
        {
            for each (PdfBookmark^ child in bookmark->Children)
            {
                CreateBookmarkItem(item, child, doc);
            }
        }
    }

    void PdfDocument::Save(System::String^ outputPath)
    {
        try
        {
            PoDoFo::PdfMemDocument* doc = (PoDoFo::PdfMemDocument*)m_NativeDoc;
            if (!doc) return;

            WriteLog("Save started, bookmark count: " + std::to_string(m_Bookmarks->Count));

            PoDoFo::PdfOutlines* outlines = doc->GetOutlines();
            if (outlines != nullptr)
            {
                WriteLog("Removing existing outlines");
                while (outlines->First() != nullptr)
                {
                    outlines->First()->Erase();
                }
            }
            
            PoDoFo::PdfOutlines& newOutlines = doc->GetOrCreateOutlines();
            
            WriteLog("Outlines object created");
            
            int count = 0;
            for each (PdfBookmark^ bookmark in m_Bookmarks)
            {
                CreateBookmarkItem(newOutlines, bookmark, doc);
                count++;
            }

            WriteLog("Bookmark tree built, count: " + std::to_string(count));

            std::string path = ToUtf8String(outputPath);
            WriteLog("Saving to: " + path);
            doc->Save(path);
            
            WriteLog("Save completed: " + path);
        }
        catch (const std::exception& e)
        {
            WriteLog(std::string("Save error: ") + e.what());
            throw gcnew System::Exception(gcnew System::String(e.what()));
        }
        catch (...)
        {
            WriteLog("Save error: unknown exception");
            throw gcnew System::Exception("Unknown error occurred while saving PDF");
        }
    }

    void PdfDocument::AddBookmarkToOutlines(void* outlinesPtr, PdfBookmark^ bookmark)
    {
        PoDoFo::PdfOutlines* outlines = (PoDoFo::PdfOutlines*)outlinesPtr;
        PoDoFo::PdfMemDocument* doc = (PoDoFo::PdfMemDocument*)m_NativeDoc;
        if (!doc || !outlines) return;

        PoDoFo::PdfOutlineItem& item = outlines->CreateChild(
            ToPdfString(bookmark->Title)
        );

        item.GetObject().GetDictionary().RemoveKey("Type");

        int pageIndex = bookmark->PageNumber;
        if (pageIndex >= 0 && pageIndex < static_cast<int>(doc->GetPages().GetCount()))
        {
            PoDoFo::PdfPage& page = doc->GetPages().GetPageAt(pageIndex);
            std::unique_ptr<PoDoFo::PdfDestination> dest = doc->CreateDestination();
            if (dest)
            {
                dest->SetDestination(page, PoDoFo::PdfDestinationFit::Fit);
                item.SetDestination(*dest);
            }
        }

        if (bookmark->Children != nullptr && bookmark->Children->Count > 0)
        {
            for each (PdfBookmark^ child in bookmark->Children)
            {
                AddBookmarkToOutlineItem(&item, child);
            }
        }
    }

    void PdfDocument::AddBookmarkToOutlineItem(void* parentPtr, PdfBookmark^ bookmark)
    {
        PoDoFo::PdfOutlineItem* parent = (PoDoFo::PdfOutlineItem*)parentPtr;
        PoDoFo::PdfMemDocument* doc = (PoDoFo::PdfMemDocument*)m_NativeDoc;
        if (!doc || !parent) return;

        PoDoFo::PdfOutlineItem& item = parent->CreateChild(
            ToPdfString(bookmark->Title)
        );

        item.GetObject().GetDictionary().RemoveKey("Type");

        int pageIndex = bookmark->PageNumber;
        if (pageIndex >= 0 && pageIndex < static_cast<int>(doc->GetPages().GetCount()))
        {
            PoDoFo::PdfPage& page = doc->GetPages().GetPageAt(pageIndex);
            std::unique_ptr<PoDoFo::PdfDestination> dest = doc->CreateDestination();
            if (dest)
            {
                dest->SetDestination(page, PoDoFo::PdfDestinationFit::Fit);
                item.SetDestination(*dest);
            }
        }

        if (bookmark->Children != nullptr && bookmark->Children->Count > 0)
        {
            for each (PdfBookmark^ child in bookmark->Children)
            {
                AddBookmarkToOutlineItem(&item, child);
            }
        }
    }

    void PdfDocument::Merge(cli::array<System::String^>^ pdfPaths, System::String^ outputPath)
    {
        throw gcnew System::NotSupportedException("Merge function not yet implemented");
    }
}
