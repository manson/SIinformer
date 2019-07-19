using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SIinformer.Logic
{
    public static class TextCleaner
    {
        static Regex regex = new Regex("[\\x00-\\x08\\x0B-\\x1F\\x7F]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.IgnorePatternWhitespace | RegexOptions.Compiled);//"\\&\\#x\\d+;"
        static Regex regHtml = new Regex("<[^>]+>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        public static string Clean(string text)
        {            
            var result = string.IsNullOrWhiteSpace(text) ? "" : regex.Replace(text, "");
            result = result.Replace("&quot;", "\"");
            return result;
        }
        public static string Html2Text(string html)
        {
            html = html.Replace("<hr", "<br><hr");
            html = html.Replace("<br>", "\r\n");
            html = html.Replace(" ,", "");

            return regHtml.Replace(html, "");
        }

        public static string MakeFileAcceptableName(string fileName)
        {
            string delims = "~`#$%^&*?:;№\"'\\|/.,";
            foreach (var delim in delims)
            {
                fileName = fileName.Replace(delim.ToString(), "-");
            }
            return fileName;
        }
    }
}
