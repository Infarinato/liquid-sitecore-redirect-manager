using Sitecore.Data.Items;
using LiquidSC.Foundation.RedirectManager.Extensions;
using LiquidSC.Foundation.RedirectManager.Pipelines.Base;
using Sitecore.Pipelines.HttpRequest;
using Sitecore.SecurityModel;
using Sitecore.StringExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Sitecore;

namespace LiquidSC.Foundation.RedirectManager.Pipelines.HttpRequest
{
    public class PathRedirectResolver : RedirectResolverBase
    {
        public PathRedirectResolver()
        {
        }

        public override void ProcessRequest(HttpRequestArgs args)
        {
            //there are no specific conditions to skip this processor, as we are redirecting custom paths to new destinations

            //ensure request contains a trailing in order to give request a common foundation for cache/comparison logic
            string requestedPath = this.EnsureSlashes(Context.Request.FilePath.ToLower());

            //check cache for previously resolved redirect
            PathRedirect resolvedMapping = this.GetResolvedMapping(requestedPath);

            //if it wasn't found via cache, generate the mapping now
            if (resolvedMapping == null)
            {
                resolvedMapping = FindRedirectMatch(requestedPath, this.MappingsMap);

                //if its found, cache it
                if (resolvedMapping != null)
                {
                    var dictionaryitem = GetCache<Dictionary<string, PathRedirect>>(ResolvedPathRedirectPrefix)
                                      ?? new Dictionary<string, PathRedirect>();

                    dictionaryitem[requestedPath] = resolvedMapping;

                    SetCache(ResolvedPathRedirectPrefix, dictionaryitem);
                }
            }

            if (resolvedMapping != null && HttpContext.Current != null)
            {
                if (!resolvedMapping.Target.IsNullOrEmpty())
                {
                    // If we are preserving the incoming query string, append it now
                    var targetUrl = this.GetTargetUrlWithPreservedQueryString(resolvedMapping);
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
                        // Default to 302
                        this.Redirect302(HttpContext.Current, targetUrl);
                    }

                    args.AbortPipeline();
                }
            }
        }

        /// <summary>
        /// Gets the list of redirect items associated with the website (bound through the redirectSettingsID attribute)
        /// </summary>
        protected virtual List<PathRedirect> MappingsMap
        {
            get
            {
                var pathRedirects = GetCache<List<PathRedirect>>(AllPathRedirectMappingsPrefix);

                if (pathRedirects == null)
                {
                    pathRedirects = new List<PathRedirect>();

                    //get the settings item
                    Item redirectSettingsItem = GetItem(GetRedirectSettingsId);

                    List<Item> redirectItems = new List<Item>();

                    Item globalRedirectFolderItem = GetItem(Constants.GlobalRedirectsFolderId);

                    if (globalRedirectFolderItem != null)
                    {
                        using (new SecurityDisabler())
                        {
                            redirectItems.AddRange((
                            from i in (IEnumerable<Item>)globalRedirectFolderItem.Axes.GetDescendants()
                            where i.IsDerived(Templates.PathRedirect.ID)
                            select i).ToList<Item>());
                        }
                    }

                    if (redirectSettingsItem != null)
                    {
                        //get the descendants that inherit the Redirect template's ID
                        using (new SecurityDisabler())
                        {
                            redirectItems.AddRangeIfNew((
                            from i in (IEnumerable<Item>)redirectSettingsItem.Axes.GetDescendants()
                            where i.IsDerived(Templates.PathRedirect.ID)
                            select i).ToList<Item>());
                        }
                    }

                    redirectItems.Sort(new TreeComparer());

                    //foreach redirect item
                    foreach (var redirectItem in redirectItems)
                    {
                        //get the type
                        RedirectType redirectType;

                        Enum.TryParse<RedirectType>(redirectItem[Templates.RedirectSettings.Fields.RedirectType], out redirectType);

                        //get the source raw value
                        var source = this.GetSourceFieldValue(redirectItem);

                        string targetUrl;
                        using (new SecurityDisabler())
                        {
                            // Get the resolved target URL
                            targetUrl = this.GetRedirectUrl(redirectItem);
                        }

                        if (!string.IsNullOrWhiteSpace(targetUrl)
                            && !string.IsNullOrWhiteSpace(source)
                            )
                        {
                            var redirect = new PathRedirect
                            {
                                RedirectItem = redirectItem,
                                RedirectType = redirectType,
                                PreserveQueryString = MainUtil.GetBool(redirectItem[Templates.RedirectSettings.Fields.PreserveQueryString], false),
                                IgnoreCase = MainUtil.GetBool(redirectItem[Templates.RedirectSettings.Fields.IgnoreCase], false),
                                Source = source,
                                Target = targetUrl
                            };

                            pathRedirects.Add(redirect);
                        }
                    }


                    //if the config is set to use cache, cache the redirects for this site
                    if (this.CacheExpiration > 0)
                    {
                        SetCache(AllPathRedirectMappingsPrefix, pathRedirects);
                    }
                }
                return pathRedirects;
            }
        }

        protected virtual PathRedirect GetResolvedMapping(string ItemId)
        {
            var item = GetCache<Dictionary<string, PathRedirect>>(ResolvedPathRedirectPrefix);
            if (item == null || !item.ContainsKey(ItemId))
            {
                return null;
            }
            return item[ItemId];
        }

        protected virtual string GetSourceFieldValue(Item sourceItem)
        {
            string value = string.Empty;

            if (sourceItem.Fields[Templates.PathRedirect.Fields.Source] != null)
            {
                value = sourceItem.Fields[Templates.PathRedirect.Fields.Source].Value;
            }

            return value;
        }
    }
}