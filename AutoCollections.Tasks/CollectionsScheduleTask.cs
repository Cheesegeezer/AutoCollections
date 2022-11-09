using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoCollections.Configuration;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;

namespace AutoCollections.AutoCollections.Tasks
{
    public class CollectionsScheduleTask : IScheduledTask, IConfigurableScheduledTask
    {

        public static bool ScanTaskRunning;

        private readonly ILibraryManager _libraryManager;
        private readonly IUserViewManager _userview;

        private static readonly object _scanLock = new object();
        public static CollectionsScheduleTask Instance { get; set; }

        private List<BaseItem> _itemsList;

        private ILogger Log { get; }

        public CollectionsScheduleTask(ILibraryManager libraryManager, ILogManager logManager)
        {
            _libraryManager = libraryManager;
            Log = logManager.GetLogger(Plugin.Instance.Name);
        }

        private async Task GetItemsToProcess()
        {
            PluginConfiguration config = Plugin.Instance.Configuration;
            Log.Info("Getting items to process", null);
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            InternalItemsQuery libraries = new InternalItemsQuery
            {
                IncludeItemTypes = new[] { "CollectionFolder" },
                Recursive = false,
            };
            var libraryFolders = _libraryManager.GetItemList(libraries).ToList();
            var internalIds = new List<long>();
            foreach (var fldr in libraryFolders)
            {
                if (fldr.Name != "Top Picks")
                {
                    internalIds.Add(fldr.InternalId);
                }
            }
            var libraryIds = internalIds.ToArray();
            var queryList = new InternalItemsQuery
            {
                Recursive = false,
                ParentIds = libraryIds,
                IncludeItemTypes = new[] { "Movie" },
                IsVirtualItem = false,
            };

            _itemsList = _libraryManager.GetItemList(queryList).ToList();
            var itemsToRemove = new List<BaseItem>();
            foreach (var item in _itemsList)
            {
                if (config.DoNotChangeLockedItems)
                {
                    if (item.IsLocked)
                    {
                        itemsToRemove.Add(item);
                        Log.Info("Locked Item: {0}", item.Name);
                    }
                }
            }

            _itemsList.RemoveAll(x => itemsToRemove.Contains(x));
            stopWatch.Stop();
            Log.Info("GetItemsToProcess took {0} ms", stopWatch.ElapsedMilliseconds.ToString());
        }

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            await GetItemsToProcess();
            lock (_scanLock)
            {
                if (ScanTaskRunning)
                {
                    return;
                }
                ScanTaskRunning = true;
            }
            Log.Info("Executing Automatic Collections creation.  Searching distinct movies with separated versions, DoNotChangeLockedMovies = '{0}'", Plugin.Instance.Configuration.DoNotChangeLockedItems);

            IEnumerable<BaseItem> items = from i in _itemsList
                                          where i.LocationType == 0 && i.MediaType == "Video" && i.Path != null && !i.IsVirtualItem && i.GetTopParent() != null && !(i.Parent.GetParent() is BoxSet)
                                          select i;
            int num = items.Count();
            int num2 = (from i in items
                        where i.ProviderIds != null && i.GetProviderId((MetadataProviders)3) != null
                        group i by i.GetProviderId((MetadataProviders)3) into i
                        where i.Count() > 1
                        select i).ToList().Count + (from i in items
                                                    where i.ProviderIds != null && i.GetProviderId((MetadataProviders)2) != null && i.GetProviderId((MetadataProviders)3) == null
                                                    group i by i.GetProviderId((MetadataProviders)2) into i
                                                    where i.Count() > 1
                                                    select i).ToList().Count;
            Log.Info("Found {0} of {1} movies that have multiple versions ...", num2, num);
            Log.Info("Searching distinct tmdb movies ...", null);
            List<IGrouping<string, BaseItem>> list = (from i in items
                                                      where i.ProviderIds != null && i.GetProviderId((MetadataProviders)3) != null
                                                      group i by i.GetProviderId((MetadataProviders)3) into j
                                                      where j.Count() != 1 + j.OfType<Video>().Sum(video => video.GetAlternateVersionIds().Count) / j.Count()
                                                      select j).ToList();
            Log.Info("Searching distinct imdb movies ...", null);
            List<IGrouping<string, BaseItem>> list2 = (from i in items
                                                       where i.ProviderIds != null && i.GetProviderId((MetadataProviders)3) == null && i.GetProviderId((MetadataProviders)2) != null
                                                       group i by i.GetProviderId((MetadataProviders)2) into j
                                                       where j.Count() != 1 + j.OfType<Video>().Sum(video => video.GetAlternateVersionIds().Count) / j.Count()
                                                       select j).ToList();
            Log.Info("Found {0} tmdb and {1} imdb version movie lists, merging them to process ...", list.Count, list2.Count);
            list.AddRange(list2);

            int actionrequiredmoviecount = list.Count;
            if (actionrequiredmoviecount == 0)
            {
                Log.Info("No movies with multiple ungrouped versions found, we're done!", null);
            }
            else
            {
                Log.Info("Executing Movie version grouping. Found {0} movies require regrouping and update.", actionrequiredmoviecount);
                double current = 1.0;
                List<IGrouping<string, BaseItem>>.Enumerator enumerator = list.GetEnumerator();
                try
                {

                    while (enumerator.MoveNext())
                    {
                        await UpdateCollection(enumerator.Current).ConfigureAwait(continueOnCapturedContext: false);
                        progress.Report(current / actionrequiredmoviecount);
                        current += 1.0;
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }
                catch (Exception ex)
                {
                    Log.ErrorException("Automatic Movie version grouping failed - ex:", ex, Array.Empty<object>());
                }
                finally
                {
                    Log.Info("Automatic Movie version grouping completed.", null);
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
            Log.Debug("Found movie {0} ({1}) with {2} separate versions - (re)grouping them.", collection.Key, collection.First().Name, num);

            bool result = false;
            if (num > 1)
            {
                _libraryManager.MergeItems(collection.ToArray());
            }
            else
            {
                Log.Debug("single item version - resetting linked items.", null);
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
