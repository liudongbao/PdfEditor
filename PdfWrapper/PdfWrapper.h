#pragma once

#include <vcclr.h>

namespace PdfWrapper
{
    public ref class PdfBookmark
    {
    public:
        System::String^ Title;
        int PageNumber;
        System::Collections::Generic::List<PdfBookmark^>^ Children;

        PdfBookmark()
        {
            PageNumber = 0;
            Children = gcnew System::Collections::Generic::List<PdfBookmark^>();
        }
    };

    public ref class PdfDocument
    {
    public:
        PdfDocument(System::String^ filePath);
        ~PdfDocument();
        !PdfDocument();

        int GetPageCount();
        System::Collections::Generic::List<PdfBookmark^>^ GetBookmarks();
        void AddBookmark(PdfBookmark^ bookmark);
        void Save(System::String^ outputPath);
        static void Merge(cli::array<System::String^>^ pdfPaths, System::String^ outputPath);

    private:
        void* m_NativeDoc;
        System::Collections::Generic::List<PdfBookmark^>^ m_Bookmarks;
        
        void LoadBookmarks();
        void LoadBookmarksFromItem(void* item, System::Collections::Generic::List<PdfBookmark^>^ bookmarks);
        void AddBookmarkToItem(void* parent, PdfBookmark^ bookmark);
    };
}
