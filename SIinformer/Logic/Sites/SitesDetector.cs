using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SIinformer.Logic.Sites
{
    public static class SitesDetector
    {
        public static ISite GetSite(string url)
        {
            url = url.ToLower().Trim();
            if (url.StartsWith("http://samlib.ru")) return new Samlib();
            if (url.StartsWith("http://zhurnal.lib.ru")) return new Samlib();
            if (url.StartsWith("http://budclub.ru")) return new Samlib();
            if (url.StartsWith("http://www.fanfiction.net/")) return new FanFiction();

            return null;
        }
    }
}
