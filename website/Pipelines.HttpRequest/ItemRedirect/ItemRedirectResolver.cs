using Sitecore.Data.Fields;
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
    public class ItemRedirectResolver : RedirectResolverBase
    {
        public ItemRedirectResolver()
        {
        }

        public override void ProcessRequest(HttpRequestArgs args)
        {
            //if the source request url isn't associated with an item, skip this processor
            if (Context.Item == null)
                return;

            string itemId = Context.Item.ID.ToString();
            ItemRedirect resolvedMapping = this.GetResolvedMapping(itemId);

            if (resolvedMapping == null)
            {
                resolvedMapping = this.FindMapping(itemId);

                if (resolvedMapping != null)
                {
                    var dictionaryitem = GetCache<Dictionary<string, ItemRedirect>>(ResolvedItemRedirectPrefix)
                                      ?? new Dictionary<string, ItemRedirect>();

                    dictionaryitem[itemId] = resolvedMapping;

                    SetCache(ResolvedItemRedirectPrefix, dictionaryitem);
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

        protected virtual ItemRedirect FindMapping(string itemId)
        {
            ItemRedirect redirectMapping = null;
            List<ItemRedirect>.Enumerator enumerator = this.MappingsMap.GetEnumerator();
            try
            {
                while (enumerator.MoveNext())
                {

                    ItemRedirect current = enumerator.Current;
                    if (itemId == current.Source)
                    {
                        redirectMapping = current;
                        return redirectMapping;
                    }
                }
            }
            finally
            {
                ((IDisposable)enumerator).Dispose();
            }
            return redirectMapping;
        }

        /// <summary>
        /// Gets the list of redirect items associated with the website (bound through the redirectSettingsID attribute)
        /// </summary>
        protected virtual List<ItemRedirect> MappingsMap
        {
            get
            {
                var itemRedirects = GetCache<List<ItemRedirect>>(AllItemRedirectMappingsPrefix);

                if (itemRedirects == null)
                {
                    itemRedirects = new List<ItemRedirect>();

                    //get the settings item
                    Item redirectSettingsItem = GetItem(GetRedirectSettingsId);

                    List<Item> redirectItems = new List<Item>();

                    if (redirectSettingsItem != null)
                    {
                        //get the descendants that inherit the Redirect template's ID
                        using (new SecurityDisabler())
                        {
                            redirectItems.AddRange((
                            from i in (IEnumerable<Item>)redirectSettingsItem.Axes.GetDescendants()
                            where i.IsDerived(Templates.ItemRedirect.ID)
                            select i).ToList<Item>());
                        }
                    }

                    redirectItems.Sort(new TreeComparer());

                    //foreach redirect item
                    foreach (var redirectItem in redirectItems)
                    {
                        RedirectType redirectType;
                        Enum.TryParse<RedirectType>(redirectItem[Templates.RedirectSettings.Fields.RedirectType], out redirectType);

                        string sourceItemId, targetUrl;
                        using (new SecurityDisabler())
                        {
                            sourceItemId = this.GetSourceItemID(redirectItem);

                            // Get the resolved target URL (this already includes any target query string) 
                            targetUrl = this.GetRedirectUrl(redirectItem);
                        }
                        
                        if (!string.IsNullOrWhiteSpace(targetUrl) && !string.IsNullOrWhiteSpace(sourceItemId))
                        {
                            var redirect = new ItemRedirect
                            {
                                RedirectItem = redirectItem,
                                RedirectType = redirectType,
                                PreserveQueryString = MainUtil.GetBool(redirectItem[Templates.RedirectSettings.Fields.PreserveQueryString], false),
                                IgnoreCase = MainUtil.GetBool(redirectItem[Templates.RedirectSettings.Fields.IgnoreCase], false),
                                Source = sourceItemId,
                                Target = targetUrl
                            };

                            itemRedirects.Add(redirect);
                        }
                    }

                    //if the config is set to use cache, cache the redirects for this site
                    if (this.CacheExpiration > 0)
                    {
                        SetCache(AllItemRedirectMappingsPrefix, itemRedirects);
                    }
                }
                return itemRedirects;
            }
        }

        protected virtual ItemRedirect GetResolvedMapping(string ItemId)
        {
            var item = GetCache<Dictionary<string, ItemRedirect>>(ResolvedItemRedirectPrefix);
            if (item == null || !item.ContainsKey(ItemId))
            {
                return null;
            }
            return item[ItemId];
        }

        protected virtual string GetSourceItemID(Item sourceItem)
        {
            LinkField sourceLinkField = sourceItem.Fields[Templates.ItemRedirect.Fields.SourceItem];
            string str = string.Empty;
            if (sourceLinkField != null)
            {
                if (sourceLinkField.IsInternal && !string.IsNullOrWhiteSpace(sourceLinkField.Value))
                {
                    str = GetItemIdByPath(sourceLinkField.Value);
                }
            }
            return str;
        }

        public static string GetItemIdByPath(string path)
        {
            try
            {
                var item = Context.Database.SelectSingleItem(path);
                if (item != null)
                {
                    return item.ID.ToString();
                }
            }
            catch
            {
                return string.Empty;
            }

            return string.Empty;
        }
    }
}