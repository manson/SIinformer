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
        public static string Clean(string text)
        {            
            var result = string.IsNullOrWhiteSpace(text) ? text : regex.Replace(text, "");
            result = result.Replace("&quot;", "\"");
            return result;
        }
    }
}
