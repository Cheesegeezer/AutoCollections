using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AutoCollections.Tasks;
using Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Security;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;

namespace AutoCollections;

public class ServerEntryPoint : IServerEntryPoint, IDisposable
{
    private readonly List<BaseItem> _newlyAddedItems = new List<BaseItem>();

    private const int NewItemDelay = 15000;

    private Timer NewItemTimer { get; set; }

    public static ServerEntryPoint Instance { get; private set; }

    public ILibraryManager LibraryManager { get; private set; }

    public ILibraryMonitor LibraryMonitor { get; private set; }

    public ICollectionManager CollectionManager { get; private set; }

    public IProviderManager ProviderManager { get; private set; }

    private ISecurityManager PluginSecurityManager { get; set; }

    private IApplicationPaths ApplicationPaths { get; set; }

    public ServerEntryPoint(ILibraryManager libraryManager, IProviderManager providerManager, ILogManager logManager,
        ISecurityManager securityManager, ILibraryMonitor libraryMonitor, ICollectionManager collectionManager,
        IApplicationPaths applicationPaths)
    {
        LibraryManager = libraryManager;
        LibraryMonitor = libraryMonitor;
        PluginSecurityManager = securityManager;
        CollectionManager = collectionManager;
        ApplicationPaths = applicationPaths;
        ProviderManager = providerManager;
        Plugin.Logger = logManager.GetLogger(Plugin.Instance.Name);
        Instance = this;
    }

    public void Run()
    {
        LibraryManager.ItemAdded += libraryManager_ItemAdded;
        LibraryManager.ItemUpdated += libraryManager_ItemAdded;
        if (Plugin.Instance.Configuration.NeedsUpdate)
        {
            CollectionsScheduleTask.Instance.Execute(CancellationToken.None, new Progress<double>()).ConfigureAwait(continueOnCapturedContext: false);
        }
    }

    public void Dispose()
    {
        NewItemTimer?.Dispose();
        LibraryMonitor?.Dispose();
    }


    public async void OnConfigurationUpdated(PluginConfiguration oldConfig, PluginConfiguration newConfig)
    {
        if (oldConfig.DoNotChangeLockedItems != newConfig.DoNotChangeLockedItems)
        {
            await CollectionsScheduleTask.Instance.Execute(CancellationToken.None, new Progress<double>())
                .ConfigureAwait(continueOnCapturedContext: false);
        }
    }

    private void libraryManager_ItemAdded(object sender, ItemChangeEventArgs e)
    {
        lock (_newlyAddedItems)
        {
            if (((e != null) ? e.Item : null) != null)
            {
                _newlyAddedItems.Add(e.Item);
                if (NewItemTimer == null)
                {
                    NewItemTimer = new Timer(NewItemTimerCallback, null, 15000, -1);
                }
                else
                {
                    NewItemTimer.Change(15000, -1);
                }
            }
        }
    }

    private async void NewItemTimerCallback(object state)
    {
        List<BaseItem> list;
        lock (_newlyAddedItems)
        {
            list = _newlyAddedItems.Distinct().ToList();
            _newlyAddedItems.Clear();
            NewItemTimer.Dispose();
            NewItemTimer = null;
        }
        try
        {
            if (list.Count == 0)
            {
                Plugin.Logger.Info("AutoCollections: NewItemTimerCallback - no new items registered!", null);
            }
            else if ((from i in list.OfType<Movie>()
                where i.GetProviderId((MetadataProviders)3) != null && (int)i.LocationType == 0 && i.MediaType == "Video" && !(i.Parent.GetParent() is BoxSet)
                select i).Take(5).ToList().Count != 0)
            {
                await CollectionsScheduleTask.Instance.Execute(CancellationToken.None, new Progress<double>()).ConfigureAwait(continueOnCapturedContext: false);
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.Error("AutoCollections: NewItemTimerCallback - fatal error: {0}", ex.Message);
        }
    }
}
	

