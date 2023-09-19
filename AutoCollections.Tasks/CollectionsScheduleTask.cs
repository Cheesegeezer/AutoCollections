using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using AutoCollections.Config;

namespace AutoCollections.AutoCollections.Tasks
{
    public class CollectionsScheduleTask : IScheduledTask, IConfigurableScheduledTask
    {

        public static bool ScanTaskRunning;

        private readonly ILibraryManager _libraryManager;

        private static readonly object _scanLock = new object();
        public static CollectionsScheduleTask Instance { get; set; }

        private List<Tuple<string, BaseItem>> _itemsList = new List<Tuple<string, BaseItem>>();

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
            foreach (var fldr in libraryFolders)
            {
                if (fldr.Name == "Top Picks")
                {
                    continue;
                }
                var queryList = new InternalItemsQuery
                {
                    Recursive = false,
                    ParentIds = new[] { fldr.InternalId },
                    IncludeItemTypes = new[] { "Movie" },
                    IsVirtualItem = false,
                };

                var libraryItemsList = _libraryManager.GetItemList(queryList).ToList();
                foreach (var item in libraryItemsList)
                {
                    if (config.DoNotChangeLockedItems && item.IsLocked)
                    {
                        Log.Info("Locked Item: {0}", item.Name);
                        continue;
                    }
                    _itemsList.Add(Tuple.Create(config.MergeAcrossLibraries ? "" : fldr.InternalId.ToString(), item));
                }
            }

            stopWatch.Stop();
            Log.Info("GetItemsToProcess took {0} ms", stopWatch.ElapsedMilliseconds.ToString());
        }

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            PluginConfiguration config = Plugin.Instance.Configuration;
            await GetItemsToProcess();
            lock (_scanLock)
            {
                if (ScanTaskRunning)
                {
                    return;
                }
                ScanTaskRunning = true;
            }
            Log.Info("Executing Automatic Collections creation.  Searching distinct movies with separated versions," + 
                " DoNotChangeLockedMovies = '{0}', MergeAcrossLibraries = '{1}'", config.DoNotChangeLockedItems, config.MergeAcrossLibraries);

            IEnumerable<Tuple<string, BaseItem>> items = from i in _itemsList
                                            where i.Item2.LocationType == 0 && i.Item2.MediaType == "Video" && i.Item2.Path != null 
                                                && !i.Item2.IsVirtualItem && i.Item2.GetTopParent() != null && !(i.Item2.Parent.GetParent() is BoxSet)
                                            select i;
            int num = items.Count();
            int num2 = (from i in items
                        where i.Item2.ProviderIds != null && i.Item2.GetProviderId((MetadataProviders)3) != null
                        group i by i.Item1 + i.Item2.GetProviderId((MetadataProviders)3) into i
                        where i.Count() > 1
                        select i).ToList().Count + (from i in items
                                                    where i.Item2.ProviderIds != null && i.Item2.GetProviderId((MetadataProviders)2) != null 
                                                        && i.Item2.GetProviderId((MetadataProviders)3) == null
                                                    group i by i.Item1 + i.Item2.GetProviderId((MetadataProviders)2) into i
                                                    where i.Count() > 1
                                                    select i).ToList().Count;
            Log.Info("Found {0} of {1} movies that have multiple versions ...", num2, num);
            Log.Info("Searching distinct tmdb movies ...", null);
            List<IGrouping<string, BaseItem>> list = (from i in items
                                                      where i.Item2.ProviderIds != null && i.Item2.GetProviderId((MetadataProviders)3) != null
                                                      group i.Item2 by i.Item1 + i.Item2.GetProviderId((MetadataProviders)3).ToString() into j
                                                      where j.Count() != 1 + j.OfType<Video>().Sum(video => video.GetAlternateVersionIds().Count) / j.Count()
                                                      select j).ToList();
            Log.Info("Searching distinct imdb movies ...", null);
            List<IGrouping<string, BaseItem>> list2 = (from i in items
                                                        where i.Item2.ProviderIds != null && i.Item2.GetProviderId((MetadataProviders)3) == null 
                                                            && i.Item2.GetProviderId((MetadataProviders)2) != null
                                                        group i.Item2 by i.Item1 + i.Item2.GetProviderId((MetadataProviders)2) into j
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
