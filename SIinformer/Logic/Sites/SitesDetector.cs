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
            ISite site=null;
            site = new Samlib();
            if (IsSiteOfThisDomain(url, site)) return site;
            site = new FanFiction();
            if (IsSiteOfThisDomain(url, site)) return site;
            
            return null;
        }

        private static bool IsSiteOfThisDomain(string url, ISite site)
        {
            return site.GetKnownDomens().Any(url.StartsWith);
        }
    }
}
