using LiquidSC.Foundation.RedirectManager.Pipelines.HttpRequest;
using Sitecore;
using Sitecore.Data;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Links;
#if SC93 || SC103
using Sitecore.Links.UrlBuilders;
#endif
using Sitecore.Pipelines.HttpRequest;
using Sitecore.Resources.Media;
using Sitecore.Sites;
using Sitecore.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Caching;

namespace LiquidSC.Foundation.RedirectManager.Pipelines.Base
{
    public class RedirectResolverBase : HttpRequestProcessor
    {
        public override void Process(HttpRequestArgs args)
        {
            if (args == null || args.HttpContext == null)
                return;

            //off limits context conditions for redirects
            if (Context.Database == null 
                || Context.Site == null 
                || Context.PageMode.IsExperienceEditor 
                || Context.PageMode.IsExperienceEditorEditing 
                || Context.PageMode.IsPreview)
                return;

            //off limits databases
            if (Sitecore.Context.Database.Name == "core")
                return;

            //off limits paths (not allowing domain level redirects at this time)
            if (args.Url.FilePath == "/" || args.Url.FilePath == "")
                return;

            //configurable off limits paths
            foreach (string startPath in this.IgnoreStartPaths)
            {
                if (args.Url.FilePath.ToLower().StartsWith(StringUtil.EnsurePrefix('/', startPath.ToLower())))
                {
                    return;
                }
            }

            //get the site level redirect settings and apply to the current requested path
#if DEBUG
            //SetSiteLevelRedirectSettings(GetSiteLevelConfigMapping());
#else

            try
            {
                SetSiteLevelRedirectSettings(GetSiteLevelConfigMapping());
            }
            catch (Exception ex)
            {
                Sitecore.Diagnostics.Log.Error("An error has occured while the Sitecore Redirect Manager was processing the Site Level Redirect Settings", ex, this);
            }
#endif


#if DEBUG
            ProcessRequest(args);
#else

            try
            {
                ProcessRequest(args);
            }
            catch (Exception ex)
            {
                Sitecore.Diagnostics.Log.Error("An error has occured while the Sitecore Redirect Manager was processing a resolver", ex, this);
            }
#endif
        }

        public virtual void ProcessRequest(HttpRequestArgs args)
        {

        }

        public int CacheExpiration
        {
            get; set;
        }

        protected string GetRedirectSettingsId
        {
            get
            {
                return Context.Site.Properties["redirectSettingsId"];
            }
        }

        protected string DatabaseName
        {
            get { return Context.Database.Name; }
        }


        protected string SiteName
        {
            get { return Context.Site.Name; }
        }

        protected string SiteLanguage
        {
            get { return Context.Language.Name; }
        }

        protected Item GetItem(string itemId)
        {
            //var redirectSettingsId = ;
            if (!string.IsNullOrEmpty(itemId) && ID.TryParse(itemId, out ID iD))
            {
                return Context.Database.GetItem(iD);
            }
            return null;
        }

        protected string ResolvedItemRedirectPrefix
        {
            get
            {
                return string.Format("{0}ResolvedItemRedirect-{1}-{2}-{3}", "Sitecore-Redirect-", DatabaseName, SiteName, SiteLanguage);
            }
        }

        protected string ResolvedPathRedirectPrefix
        {
            get
            {
                return string.Format("{0}ResolvedPathRedirect-{1}-{2}-{3}", "Sitecore-Redirect-", DatabaseName, SiteName, SiteLanguage);
            }
        }

        protected string ResolvedAdvancedRedirectPrefix
        {
            get
            {
                return string.Format("{0}ResolvedAdvancedRedirect-{1}-{2}-{3}", "Sitecore-Redirect-", DatabaseName, SiteName, SiteLanguage);
            }
        }

        protected string AllItemRedirectMappingsPrefix
        {
            get
            {
                return string.Format("{0}AllItemRedirectMappings-{1}-{2}-{3}", "Sitecore-Redirect-", DatabaseName, SiteName, SiteLanguage);
            }
        }

        protected string AllAdvancedRedirectMappingsPrefix
        {
            get
            {
                return string.Format("{0}AllAdvancedRedirectMappings-{1}-{2}-{3}", "Sitecore-Redirect-", DatabaseName, SiteName, SiteLanguage);
            }
        }

        protected string AllPathRedirectMappingsPrefix
        {
            get
            {
                return string.Format("{0}AllPathRedirectMappings-{1}-{2}-{3}", "Sitecore-Redirect-", DatabaseName, SiteName, SiteLanguage);
            }
        }

        protected string AllRedirectMappingsPrefix
        {
            get
            {
                return string.Format("{0}AllRedirectMappings-{1}-{2}-{3}", "Sitecore-Redirect-", DatabaseName, SiteName, SiteLanguage);
            }
        }

        protected string ResolvedMappingsPrefix
        {
            get
            {
                return string.Format("{0}ResolvedMappings-{1}-{2}-{3}", "Sitecore-Redirect-", DatabaseName, SiteName, SiteLanguage);
            }
        }

        protected string SiteLevelMappingPrefix
        {
            get
            {
                return string.Format("{0}SiteLevel-{1}-{2}-{3}", "Sitecore-Redirect-", DatabaseName, SiteName, SiteLanguage);
            }
        }

        protected string SiteLevelAllMappingPrefix
        {
            get
            {
                return string.Format("{0}SiteLevelAllMappings-{1}-{2}-{3}", "Sitecore-Redirect-", DatabaseName, SiteName, SiteLanguage);
            }
        }

        protected List<string> CacheDatabases
        {
            get
            {
                var cacheDatabasesSetting = Sitecore.Configuration.Settings.GetSetting("RedirectManagerCacheDatabases");

                List<string> cacheDatabases = new List<string>();

                if (!string.IsNullOrWhiteSpace(cacheDatabasesSetting))
                {
                    cacheDatabases = cacheDatabasesSetting.Split(new char[] { ',', ';', '|' }).ToList();
                }

                return cacheDatabases;
            }
        }

        protected List<string> IgnoreStartPaths
        {
            get
            {
                var startPathsSetting = Sitecore.Configuration.Settings.GetSetting("RedirectManagerIgnoreStartPaths");

                List<string> paths = new List<string>();

                if (!string.IsNullOrWhiteSpace(startPathsSetting))
                {
                    paths = startPathsSetting.Split(new char[] { ',', ';', '|', }).ToList();
                }

                return paths;
            }
        }

        /// <summary>
        /// ensures a there is a pre and post slash on a string
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        protected string EnsureSlashes(string text)
        {
            return StringUtil.EnsurePostfix('/', StringUtil.EnsurePrefix('/', text));
        }

        protected T GetCache<T>(string key)
        {
            foreach (var cacheDatabase in this.CacheDatabases)
            {
                if (cacheDatabase.ToLower() == Sitecore.Context.Database.Name.ToLower())
                {
                    return (T)HttpRuntime.Cache[key];
                }
            }

            return default;
        }

        protected void SetCache(string key, object value)
        {
            if (this.CacheExpiration > 0)
            {
                Cache cache = HttpRuntime.Cache;
                DateTime utcNow = DateTime.UtcNow;
                cache.Add(key, value, null, utcNow.AddMinutes((double)this.CacheExpiration), TimeSpan.Zero, CacheItemPriority.Normal, null);
            }
        }

        protected virtual string GetTargetQueryString(Item redirectItem)
        {
            LinkField redirectLinkField = redirectItem.Fields[Constants.TargetFieldName];
            string redirectQueryString = String.Empty;
            if (redirectLinkField != null)
            {
                redirectQueryString = redirectLinkField.QueryString.TrimStart('?');
            }

            return redirectQueryString;
        }

        protected virtual string GetTargetUrlWithPreservedQueryString(Redirect resolvedMapping)
        {
            var targetUrl = resolvedMapping.Target;
            if (!resolvedMapping.PreserveQueryString || Context.Request.QueryString.Count <= 0)
            {
                return targetUrl;
            }

            var sourceQueryStringList = new List<string>();

            // Append all source parameters to list
            foreach (string key in Context.Request.QueryString)
            {
                var parameter = key;
                if (!string.IsNullOrWhiteSpace(Context.Request.QueryString[key]))
                {
                    parameter = key + "=" + HttpUtility.UrlEncode(Context.Request.QueryString[key]);
                }

                sourceQueryStringList.Add(parameter);
            }

            var targetUrlParts = targetUrl.Split('#');
            var targetUrlNoAnchor = targetUrlParts[0];
            targetUrl = string.Concat(targetUrlNoAnchor, string.Concat(targetUrlNoAnchor.IndexOf('?') >= 0 ? "&" : "?", string.Join("&", sourceQueryStringList)));
            if (targetUrlParts.Length > 1)
            {
                targetUrl = $"{targetUrl}#{targetUrlParts[1]}";
            }

            return targetUrl;
        }

        protected virtual string GetRedirectUrl(Item redirectItem)
        {
            LinkField redirectLinkField = redirectItem.Fields[Constants.TargetFieldName];
            string redirectUrl = null;
            if (redirectLinkField != null && !String.IsNullOrWhiteSpace(redirectLinkField.Value))
            {
                //implcitly an internal or media link if its targeting an item
                if (redirectLinkField.TargetItem != null)
                {
                    if (redirectLinkField.IsInternal)
                    {
#if SC93 || SC103
                        var urlOptions = (ItemUrlBuilderOptions)new DefaultItemUrlBuilderOptions();
#else
                        var urlOptions = LinkManager.GetDefaultUrlOptions();
#endif
                 
                        //potential alternative approach reflection based approach kept for study and reference
                        //to avoid having to create multiple configurations and use preprocessor directives to determine which class type to use, instead we check if the assembly exists as an all-in-one solution
                        //performance impact of this is unknown
                        //9.3
                        //var urlOptionsClass = Type.GetType("Sitecore.Links.UrlBuilders.ItemUrlBuilderOptions,Sitecore.Kernel");
                        //dynamic linkOptionsClassInstance = urlOptionsClass == null ? null : Activator.CreateInstance(urlOptionsClass); //Check if exists, instantiate if so.

                        //9.2
                        //if (urlOptionsClass == null)
                        //{
                        //    urlOptionsClass = Type.GetType("Sitecore.Links.UrlOptions,Sitecore.Kernel");
                        //    linkOptionsClassInstance = urlOptionsClass == null ? null : Activator.CreateInstance(urlOptionsClass); //Check if exists, instantiate if so.
                        //}

                        urlOptions.AlwaysIncludeServerUrl = true;
                        urlOptions.SiteResolving = true;

                        // Switch to relevant site to resolve target link, if needed
                        if (IsFromCurrentSite(redirectLinkField.TargetItem))
                        {
                            redirectUrl = LinkManager.GetItemUrl(redirectLinkField.TargetItem, urlOptions);
                        }
                        else
                        {
                            var website = GetSiteContext(redirectLinkField.TargetItem);
                            using (new SiteContextSwitcher(website))
                            {
                                redirectUrl = LinkManager.GetItemUrl(redirectLinkField.TargetItem, urlOptions);
                            }
                        }

                        // GetItemUrl appears to produce double trailing slashes for cross-site links: clean that up
                        redirectUrl = new UriBuilder(redirectUrl)
                        {
                            Path = this.GetPathAndQuery(redirectUrl).Replace("//", "/"),
                            Query = redirectLinkField.QueryString.TrimStart('?'),
                            Fragment = redirectLinkField.Anchor.TrimStart('#')
                        }.Uri.AbsoluteUri;
                    }
                    else if (redirectLinkField.IsMediaLink)
                    {
#if SC93 || SC103
                        var urlOptions = new MediaUrlBuilderOptions();
#else
                        var urlOptions = new MediaUrlOptions();
#endif
                        //note that this will always redirect to the current context site's domain 
                        urlOptions.AlwaysIncludeServerUrl = true;

                        redirectUrl = MediaManager.GetMediaUrl((MediaItem)redirectLinkField.TargetItem, urlOptions);
                    }
                }
                else
                {
                    redirectUrl = redirectLinkField.Url;
                }
            }

            return redirectUrl;
        }

        protected string GetPathAndQuery(string redirectUrl)
        {
            return new Uri(redirectUrl).PathAndQuery;
        }

        private static bool IsFromCurrentSite(Item item)
        {
            return item.Paths.FullPath.StartsWith(Context.Site.StartPath, StringComparison.OrdinalIgnoreCase);
        }

        private static SiteContext GetSiteContext(Item item)
        {
            var site = GetSites().LastOrDefault(s => item.Paths.FullPath.ToLower().StartsWith(s.Key.ToLower()));
            return site.Value;
        }

        private static IEnumerable<KeyValuePair<string, SiteContext>> GetSites()
        {
            return SiteManager.GetSites()
                .Where(
                    s =>
                        !string.IsNullOrEmpty(s.Properties["rootPath"]) &&
                        !string.IsNullOrEmpty(s.Properties["startItem"])).Select(
                    d => new KeyValuePair<string, SiteContext>(
                        $"{d.Properties["rootPath"]}{d.Properties["startItem"]}",
                        new SiteContext(new SiteInfo(d.Properties)))).ToList();
        }

        protected virtual void Redirect301(HttpContext context, string url)
        {
            HttpCookieCollection httpCookieCollection = new HttpCookieCollection();
            for (int i = 0; i < context.Response.Cookies.Count; i++)
            {
                HttpCookie item = context.Response.Cookies[i];
                if (item != null)
                {
                    httpCookieCollection.Add(item);
                }
            }
            context.Response.Clear();
            for (int j = 0; j < httpCookieCollection.Count; j++)
            {
                HttpCookie httpCookie = httpCookieCollection[j];
                if (httpCookie != null)
                {
                    context.Response.Cookies.Add(httpCookie);
                }
            }

            context.Response.Status = "301 Moved Permanently";
            context.Response.StatusCode = (int)HttpStatusCode.MovedPermanently;
            context.Response.AppendHeader("Location", url);
            context.Response.Flush();

            // Complete the request rather than ending the response to avoid System.Threading.ThreadAbortException on redirecting
            context.ApplicationInstance.CompleteRequest();
        }
        
        protected virtual void Redirect302(HttpContext context, string url)
        {
            context.Response.Redirect(url, false);

            // Complete the request rather than ending the response to avoid System.Threading.ThreadAbortException on redirecting
            context.ApplicationInstance.CompleteRequest();
        }

        protected static bool IsValidRegex(string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) return false;

            //supporting only "starts with" regex at this time, as the only other alternative is to generate a "is regex" checkbox on the cms side for the user to determine if their own is regex (since regex cannot be intelligiently determined if all non regex paths start with "/")
            if (!pattern.StartsWith("^")) return false;

            //test the pattern to make sure its valid
            try
            {
                pattern = HttpUtility.UrlDecode(pattern.ToLower(), System.Text.Encoding.UTF8);
                Regex.Match("", pattern);
            }
            catch (ArgumentException)
            {
                return false;
            }

            return true;
        }

        protected virtual SiteLevelRedirectMapping GetSiteLevelConfigMapping()
        {
            var siteLevelAllMapping = GetCache<Dictionary<string, SiteLevelRedirectMapping>>(SiteLevelAllMappingPrefix);

            if (siteLevelAllMapping == null || siteLevelAllMapping[SiteLevelMappingPrefix] == null)
            {
                var siteLevelMapping = new SiteLevelRedirectMapping();

                Item siteRedirectSettingsItem = GetItem(GetRedirectSettingsId);

                if (siteRedirectSettingsItem != null && siteRedirectSettingsItem.Fields != null && siteRedirectSettingsItem.TemplateID == Templates.SiteRedirectsSettings.ID)
                {
                    CheckboxField wwwCheckbox = siteRedirectSettingsItem.Fields[Templates.SiteRedirectsSettings.Fields.RedirectToWWW];
                    CheckboxField httpsCheckbox = siteRedirectSettingsItem.Fields[Templates.SiteRedirectsSettings.Fields.RedirectToHttps];
                    CheckboxField slashCheckbox = siteRedirectSettingsItem.Fields[Templates.SiteRedirectsSettings.Fields.RedirectWithTrailingSlash];
                    CheckboxField lowerCheckbox = siteRedirectSettingsItem.Fields[Templates.SiteRedirectsSettings.Fields.RedirectToLowercase];

                    bool www = wwwCheckbox != null ? wwwCheckbox.Checked : false;
                    bool https = httpsCheckbox != null ? httpsCheckbox.Checked : false;
                    bool slash = slashCheckbox != null ? slashCheckbox.Checked : false;
                    bool lower = lowerCheckbox != null ? lowerCheckbox.Checked : false;

                    siteLevelMapping = new SiteLevelRedirectMapping()
                    {
                        IncludeWWW = www,
                        ForceHttps = https,
                        ForceTrailingSlash = slash,
                        ForceLowerCase = lower
                    };

                    if (siteLevelAllMapping == null)
                        siteLevelAllMapping = new Dictionary<string, SiteLevelRedirectMapping>();
                    siteLevelAllMapping.Add(SiteLevelMappingPrefix, siteLevelMapping);
                }

                //cache the site settings
                if (this.CacheExpiration > 0 && siteLevelAllMapping != null)
                {
                    SetCache(SiteLevelAllMappingPrefix, siteLevelAllMapping);
                }

                return siteLevelMapping;
            }

            return siteLevelAllMapping[SiteLevelMappingPrefix];
        }

        protected virtual void SetSiteLevelRedirectSettings(SiteLevelRedirectMapping siteLevel)
        {
            if (siteLevel == null || HttpContext.Current == null)
            {
                return;
            }

            var current = HttpContext.Current;

            var finalUrl = current.Request.Url;

            //run through all options and construct a new url, before producing a 301 redirect, to simulate 'StopProcessing="false"' and avoid running the url back through several times
            if (siteLevel.ForceHttps)
            {
                if (!current.Request.IsSecureConnection)
                {
                    finalUrl = new UriBuilder(finalUrl)
                    {
                        Scheme = Uri.UriSchemeHttps
                    }.Uri;
                }
            }

            if (siteLevel.IncludeWWW)
            {
                var absoluteUri = finalUrl.AbsoluteUri;

                var absoluteUriVal = absoluteUri.Substring(absoluteUri.IndexOf("//") + 2);
                if (!absoluteUriVal.StartsWith("www."))
                {
                    var wwwUri = (absoluteUri.Substring(0, absoluteUri.IndexOf("//") + 2)) + "www." + absoluteUriVal;

                    finalUrl = new Uri(wwwUri);
                }
            }

            if (siteLevel.ForceLowerCase)
            {
                if (finalUrl.AbsoluteUri != finalUrl.AbsoluteUri.ToLower())
                {
                    finalUrl = new Uri(finalUrl.AbsoluteUri.ToLower());
                }
            }

            if (siteLevel.ForceTrailingSlash)
            {
                if (!finalUrl.AbsolutePath.EndsWith("/"))
                {
                    finalUrl = new UriBuilder(finalUrl)
                    {
                        Path = finalUrl.AbsolutePath + "/"
                    }.Uri;
                }
            }

            var portlessFinalUrl = new UriBuilder(finalUrl) { Port = -1 }.Uri.ToString();

            //compare the original incoming url to the (portless) modified one to determine if a redirect is neccesary
            if (current.Request.Url.ToString() != new UriBuilder(finalUrl) { Port = -1 }.Uri.ToString())
            {
                //redirect the request to a request that applies all site level redirect transformations
                this.Redirect301(current, portlessFinalUrl);
            }
        }

        protected virtual T FindRedirectMatch<T>(string requestedPath, List<T> mappingsMap) where T : Redirect
        {
            Redirect redirectMapping = null;

            foreach (var current in mappingsMap)
            {
                if (IsValidRegex(current.Source))
                {
                    //if the current regex does not match the requested path, skip
                    if (!Regex.IsMatch(requestedPath, current.Source, RegexOptions.IgnoreCase))
                    {
                        continue;
                    }
                }
                //if the map entry key path does not equal the request path (agnostic of trailing slash), skip
                else if (!StringUtil.EnsurePostfix('/', current.Source).Equals(StringUtil.EnsurePostfix('/', requestedPath), StringComparison.OrdinalIgnoreCase))
                {                    
                    continue;
                }

                redirectMapping = current;
                return (T)redirectMapping;
            }

            return null;
        }

        //TODO: placeholder for future state
        protected virtual bool IsRedirectLoop(Redirect redirect)
        {
            if (redirect.RedirectItem.TemplateID == Templates.ItemRedirect.ID)
            {
                InternalLinkField sourceField = redirect.RedirectItem.Fields[Constants.SourceFieldName];
                LinkField targetField = redirect.RedirectItem.Fields[Constants.TargetFieldName];

                if (targetField.IsMediaLink || targetField.IsInternal)
                {
                    if (sourceField.TargetID == targetField.TargetID)
                    {
                        return true;
                    }
                }
            }
            else if (redirect.RedirectItem.TemplateID == Templates.PathRedirect.ID)
            {
                var sourceField = redirect.RedirectItem.Fields[Constants.SourceFieldName];
                LinkField targetField = redirect.RedirectItem.Fields[Constants.TargetFieldName];

                if (targetField.IsMediaLink || targetField.IsInternal || targetField.TargetItem != null)
                {
                    var targetItem = targetField.TargetItem;

                    if (IsValidRegex(sourceField.Value))
                    {
                        if (Regex.IsMatch(targetItem.Paths.Path.Replace(Sitecore.Context.Site.StartPath, "").ToLower(), sourceField.Value))
                        {
                            return true;
                        }
                    }

                    if (targetItem.Paths.Path.Replace(Sitecore.Context.Site.StartPath, "").ToLower() == sourceField.Value.ToLower())
                    {
                        return true;
                    }
                }
            }
            else if (redirect.RedirectItem.TemplateID == Templates.RedirectMap.ID)
            {
                //TODO
            }

            return false;
        }
    }
}