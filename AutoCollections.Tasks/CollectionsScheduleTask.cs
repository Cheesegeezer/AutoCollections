﻿using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Configuration;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;

namespace AutoCollections.Tasks
{
    public class CollectionsScheduleTask : IScheduledTask, IConfigurableScheduledTask
    {

        public static bool ScanTaskRunning;

        private readonly ILibraryManager _libraryManager;

        private static readonly object _scanLock = new object();
        public static CollectionsScheduleTask Instance { get; private set; }
        
        private MetadataConfiguration _metadataConfig;

        private ILogger Logger { get; }

        public CollectionsScheduleTask(ILibraryManager libraryManager, ILogManager logManager, MetadataConfiguration metadataConfig)
        {
            _libraryManager = libraryManager;
            _metadataConfig = metadataConfig;
            Logger = logManager.GetLogger(Plugin.Instance.Name);
        }

        private bool IgnoreLockedItems(BaseItem item)
        {
            PluginConfiguration config = Plugin.Instance.Configuration;
            if(!config.DoNotChangeLockedItems)
            {
                return false;
            }
            return !item.IsLocked;
        }
        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {

            
            //Plugin plugin = this;
            lock (_scanLock)
            {
                if (ScanTaskRunning)
                {
                    return;
                }
                ScanTaskRunning = true;
            }
            Logger.Info("Executing Automatic Collections creation.  Searching distinct movies with separated versions, DoNotChangeLockedMovies = '{0}'", Plugin.Instance.Configuration.DoNotChangeLockedItems);
            
            IEnumerable<BaseItem> items = from i in GetAllItems(typeof(Movie))
                                          where (int)i.LocationType == 0 && i.MediaType == "Video" && i.Path != null && !i.IsVirtualItem && i.GetTopParent() != null && !(i.Parent.GetParent() is BoxSet) && !i.Path.EndsWith(".strm") && IgnoreLockedItems(i)
                                          select i;
            int num = items.Count();
            int num2 = (from i in items
                        where i.ProviderIds != null && i.GetProviderId((MetadataProviders)3) != null
                        group i by i.GetProviderId((MetadataProviders)3) into i
                        where i.Count() > 1
                        select i).ToList().Count + (from i in items
                                                    where i.ProviderIds != null && i.GetProviderId((MetadataProviders)2) != null && i.GetProviderId((MetadataProviders)3) == null
                                                    group i by ((IHasProviderIds)i).GetProviderId((MetadataProviders)2) into i
                                                    where i.Count() > 1
                                                    select i).ToList().Count;
            Logger.Info("Found {0} of {1} movies that have multiple versions ...", num2, num);
            Logger.Info("Searching distinct tmdb movies ...", null);
            List<IGrouping<string, BaseItem>> list = (from i in items
                                                      where i.ProviderIds != null && i.GetProviderId((MetadataProviders)3) != null
                                                      group i by i.GetProviderId((MetadataProviders)3) into j
                                                      where j.Count() != 1 + j.OfType<Video>().Sum(video => video.GetAlternateVersionIds().Count) / j.Count()
                                                      select j).ToList();
            Logger.Info("Searching distinct imdb movies ...", null);
            List<IGrouping<string, BaseItem>> list2 = (from i in items
                                                       where i.ProviderIds != null && i.GetProviderId((MetadataProviders)3) == null && i.GetProviderId((MetadataProviders)2) != null
                                                       group i by i.GetProviderId((MetadataProviders)2) into j
                                                       where j.Count() != 1 + j.OfType<Video>().Sum(video => video.GetAlternateVersionIds().Count) / j.Count()
                                                       select j).ToList();
            Logger.Info("Found {0} tmdb and {1} imdb version movie lists, merging them to process ...", list.Count, list2.Count);
            list.AddRange(list2);

            int actionrequiredmoviecount = list.Count;
            if (actionrequiredmoviecount == 0)
            {
                Logger.Info("No movies with multiple ungrouped versions found, we're done!", null);
            }
            else
            {
                Logger.Info("Executing Movie version grouping. Found {0} movies require regrouping and update.", actionrequiredmoviecount);
                double current = 1.0;
                List<IGrouping<string, BaseItem>>.Enumerator enumerator = list.GetEnumerator();
                try
                {

                    while (enumerator.MoveNext())
                    {
                        foreach (BaseItem item in enumerator.Current)
                        {
                            var libraryOptions = _libraryManager.GetLibraryOptions(item); //we need to pass in lib options for the item in the GetMediaSources method.
                            var mediaSources = item.GetMediaSources(true, false, libraryOptions); //There are all the media sources attached to this item.
                            
                            Logger.Debug("Checking Item File {0}", item.Path);

                            bool isStrm = Path.GetExtension(item.Path).Equals(".strm", StringComparison.InvariantCultureIgnoreCase);

                            if (isStrm)
                            {
                                Logger.Info("AutoCollections found virtual item and will not process Virtual items for {0} - skipping auto grouping for this item", item.Path);
                                Logger.Debug("isSTRM file = {0}", isStrm.ToString());

                            }
                            else
                            {
                                Logger.Info("AutoCollections is happy to continue processing {0}", item.Path);
                                Logger.Debug("isSTRM file = {0}", isStrm.ToString());

                                await UpdateCollection(enumerator.Current).ConfigureAwait(continueOnCapturedContext: false);
                                progress.Report(current / actionrequiredmoviecount);
                                current += 1.0;
                                cancellationToken.ThrowIfCancellationRequested();

                            }



                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.ErrorException("Automatic Movie version grouping failed - ex:", ex, Array.Empty<object>());
                }
                finally
                {
                    Logger.Info("Automatic Movie version grouping completed.", null);
                    enumerator.Dispose();
                    ScanTaskRunning = false;
                }
            }
            ScanTaskRunning = false;
        }

        private async Task<bool> UpdateCollection(IGrouping<string, BaseItem> collection)
        {
            Plugin plugin = Plugin.Instance;
            int num = collection.Count();
            Logger.Debug("Found movie {0} ({1}) with {2} separate versions - (re)grouping them.", collection.Key, collection.First().Name, num);

            bool result = false;
            if (num > 1)
            {
                _libraryManager.MergeItems(collection.ToArray());
            }
            else
            {
                Logger.Debug("single item version - resetting linked items.", null);
                BaseItem[] array = collection.ToArray();
                foreach (BaseItem val in array)
                {
                    _libraryManager.SplitItems(val);
                }
            }
            if (plugin.Configuration.NeedsUpdate)
            {
                plugin.Configuration.NeedsUpdate = false;
                plugin.SaveConfiguration();
            }
            return result;
        }

        private IEnumerable<BaseItem> GetAllItems(Type type)
        {
            InternalItemsQuery itemsList = new InternalItemsQuery();
            itemsList.IncludeItemTypes = new[] { type.Name };
            itemsList.IsVirtualItem = false;
            InternalItemsQuery namedList = itemsList;
            return _libraryManager.GetItemList(namedList);
        }

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            throw new NotImplementedException();
        }


        public string Name => "Auto Version Grouping";
        public string Key => nameof(CollectionsScheduleTask);
        public string Description => "Run Auto Group Task to combine versions for same Movie";
        public string Category => "MikePlanet Plugins";
        public bool IsHidden => false;
        public bool IsEnabled => true;
        public bool IsLogged => true;
    }
}