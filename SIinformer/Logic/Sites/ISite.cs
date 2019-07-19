using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using SIinformer.Utils;

namespace SIinformer.Logic.Sites
{
    public interface ISite
    {
        string GetAuthorPage(string url);
        bool UpdateAuthorInfo(string page, Author author, SynchronizationContext context, bool skipBookDescriptionChecking = false);
        void GetAuthorCredentials(string page, out string AuthorName, out DateTime AuthorUpdateDate);
        string PrepareAuthorUrlBeforeOppening(string url, Setting setting);
        string PrepareAuthorUrlOnAdding(string url);
        string PrepareTextUrlBeforeOpenning(string authorUrl, string textUrl);

        string GetFileExtention(AuthorText authorText);
        string GetFileName(AuthorText authorText);

        string RootBooksFolder { get; }

        string GetUserBooksFolder(Author author,AuthorText authorText);

        int GetSupportedReaderNumber(int suggestedNumber);

        List<string> GetKnownDomens();
        List<string> GetUrlVariants(string url);
    }
}
