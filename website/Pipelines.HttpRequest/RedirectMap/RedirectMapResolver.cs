using LiquidSC.Foundation.RedirectManager.Extensions;
using LiquidSC.Foundation.RedirectManager.Pipelines.Base;
using Sitecore;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Pipelines.HttpRequest;
using Sitecore.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LiquidSC.Foundation.RedirectManager.Pipelines.HttpRequest
{
    public class RedirectMapResolver : RedirectResolverBase
    {
        public RedirectMapResolver()
        {
        }

        public override void ProcessRequest(HttpRequestArgs args)
        {
            //there are no specific conditions to skip this processor, as we are redirecting custom paths to new destinations

            //ensure request contains a trailing in order to give request a common foundation for cache/comparison logic
            string requestedPath = this.EnsureSlashes(Context.Request.FilePath.ToLower());

            //check cache for previously resolved redirect
            RedirectMapping resolvedMapping = this.GetResolvedMapping(requestedPath);

            //if it wasn't found via cache, generate the mapping now
            if (resolvedMapping == null)
            {
                resolvedMapping = FindRedirectMatch(requestedPath, this.MappingsMap);

                //if the map was found, cache it
                if (resolvedMapping != null)
                {
                    var item = GetCache<Dictionary<string, RedirectMapping>>(ResolvedMappingsPrefix)
                                         ?? new Dictionary<string, RedirectMapping>();

                    item[requestedPath] = resolvedMapping;

                    SetCache(ResolvedMappingsPrefix, item);
                }
            }

            if (resolvedMapping != null && HttpContext.Current != null)
            {       
                string targetUrl = this.GetTargetUrl(resolvedMapping, requestedPath);
                if (resolvedMapping.RedirectType == RedirectType.Redirect301)
                {
                    this.Redirect301(HttpContext.Current, targetUrl);
                }
                else if (resolvedMapping.RedirectType == RedirectType.Redirect302)
                {
                    this.Redirect302(HttpContext.Current, targetUrl);
                }
                else if (resolvedMapping.RedirectType == RedirectType.ServerTransfer)
                {
                    HttpContext.Current.Server.TransferRequest(this.GetPathAndQuery(targetUrl));
                }
                else
                {
                    this.Redirect302(HttpContext.Current, targetUrl);
                }
            }
        }

        protected virtual RedirectMapping GetResolvedMapping(string requestedPath)
        {
            var item = GetCache<Dictionary<string, RedirectMapping>>(ResolvedMappingsPrefix);

            if (item == null || !item.ContainsKey(requestedPath))
            {
                return null;
            }
            return item[requestedPath];
        }

        protected virtual List<RedirectMapping> MappingsMap
        {
            get
            {
                var redirectMappings = GetCache<List<RedirectMapping>>(AllRedirectMappingsPrefix);
                if (redirectMappings == null)
                {
                    redirectMappings = new List<RedirectMapping>();

                    List<Item> redirectItems = new List<Item>();

                    Item globalRedirectFolderItem = GetItem(Constants.GlobalRedirectsFolderId);

                    
                    if (globalRedirectFolderItem != null)
                    {
                        redirectItems.AddRange((
                            from i in (IEnumerable<Item>)globalRedirectFolderItem.Axes.GetDescendants()
                            where i.IsDerived(Templates.RedirectMap.ID)
                            select i).ToList<Item>());
                    }

                    Item redirectSettingsItem = GetItem(GetRedirectSettingsId);

                    if (redirectSettingsItem != null)
                    {
                        //to prevent cnnflicts, only add entries that don't already exist in the global redirects (I.E global takes precendence)
                        redirectItems.AddRangeIfNew((
                            from i in (IEnumerable<Item>)redirectSettingsItem.Axes.GetDescendants()
                            where i.IsDerived(Templates.RedirectMap.ID)
                            select i).ToList<Item>());
                    }

                    redirectItems.Sort(new TreeComparer());

                    foreach (var redirectItem in redirectItems)
                    {
                        RedirectType redirectType;

                        if (Enum.TryParse<RedirectType>(redirectItem[Templates.RedirectSettings.Fields.RedirectType], out redirectType))
                        {
                            bool flag = MainUtil.GetBool(redirectItem[Templates.RedirectSettings.Fields.PreserveQueryString], false);

                            //gets all the map keys as url
                            UrlString urlString = new UrlString()
                            {
                                Query = redirectItem[Templates.RedirectMap.Fields.UrlMapping]
                            };

                            //loop through map collection key value pairs
                            foreach (string key in urlString.Parameters.Keys)
                            {
                                //ensure key exists
                                if (string.IsNullOrEmpty(key))
                                {
                                    continue;
                                }

                                //ensure value exists
                                string target = urlString.Parameters[key];
                                if (string.IsNullOrEmpty(target))
                                {
                                    continue;
                                }

                                //decode and ToLower key
                                string source = HttpUtility.UrlDecode(key.ToLower(), System.Text.Encoding.UTF8);

                                //append trailing slash if not regex
                                if (!IsValidRegex(source))
                                {
                                    source = this.EnsureSlashes(source);
                                }

                                target = HttpUtility.UrlDecode(target.ToLower(), System.Text.Encoding.UTF8);
                                target = target.ToLower() ?? string.Empty;
                                target = target.TrimStart(new char[] { '\u005E' }).TrimEnd(new char[] { '$' });
                                target = !flag ? target.TrimStart(("%2f").ToCharArray()) : target;

                                var redirect = new RedirectMapping
                                {
                                    RedirectItem = redirectItem,
                                    RedirectType = redirectType,
                                    PreserveQueryString = MainUtil.GetBool(redirectItem[Templates.RedirectSettings.Fields.PreserveQueryString], false),
                                    IgnoreCase = MainUtil.GetBool(redirectItem[Templates.RedirectSettings.Fields.IgnoreCase], false),
                                    Source = source,
                                    Target = target
                                };

                                redirectMappings.Add(redirect);
                            }
                        }
                        else
                        {
                            Log.Info(string.Format("Redirect map {0} does not specify redirect type.", redirectItem.Paths.FullPath), this);
                        }
                    }
 
                    if (this.CacheExpiration > 0)
                    {
                        SetCache(AllRedirectMappingsPrefix, redirectMappings);
                    }
                }
                return redirectMappings;
            }
        }

        protected virtual string GetTargetUrl(RedirectMapping mapping, string input)
        {
            string target = mapping.Target;

            if (IsValidRegex(mapping.Source))
            {
                target = mapping.Source.Replace(input.TrimEnd(new char[] { '/' }), target);
            }

            //if its fully qualified url target, use that
            if (Uri.IsWellFormedUriString(HttpUtility.UrlDecode(target), UriKind.Absolute))
            {
                var targetUri = new UriBuilder(HttpUtility.UrlDecode(target));

                if (mapping.PreserveQueryString)
                {
                    MergeUriQueryStringWithIncomingQueryString(targetUri);
                }

                target = targetUri.Uri.AbsoluteUri;
            }
            //if its not a fully qualified url, prepend with the virtual folder path of the current site if available to produce the final relative target url
            else if (!string.IsNullOrEmpty(Context.Site.VirtualFolder))
            {
                //fake uri with the current url just so we can access useful uribuilder functions (which are not available to relative uris)
                var targetUri = new UriBuilder(new Uri(HttpContext.Current.Request.Url, string.Concat(StringUtil.EnsurePostfix('/', Context.Site.VirtualFolder), HttpUtility.UrlDecode(target).TrimStart(new char[] { '/' }))));

                if (mapping.PreserveQueryString)
                {
                    MergeUriQueryStringWithIncomingQueryString(targetUri);
                }

                target = targetUri.Uri.PathAndQuery;
            }

            return target;
        }
        
        public void MergeUriQueryStringWithIncomingQueryString(UriBuilder uri)
        {
            if (!String.IsNullOrWhiteSpace(uri.Query))
            {
                //append the preserved query string to the new query string
                uri.Query = uri.Query.TrimStart('?') + "&" + HttpContext.Current.Request.Url.Query.TrimStart('?');
            }
            else
            {
                //append the preserved query string onto the new query string
                uri.Query = HttpContext.Current.Request.Url.Query.TrimStart('?');
            }
        }
     }
}