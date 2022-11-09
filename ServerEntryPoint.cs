using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Security;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;

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
    private ILogger Log { get; }

    private ISecurityManager PluginSecurityManager { get; set; }

    private IApplicationPaths ApplicationPaths { get; set; }

    private readonly ITaskManager TaskManager;

    public ServerEntryPoint(ILibraryManager libraryManager, IProviderManager providerManager, ILogManager logManager,
        ISecurityManager securityManager, ILibraryMonitor libraryMonitor, ICollectionManager collectionManager,
        IApplicationPaths applicationPaths, ITaskManager taskManager)
    {
        LibraryManager = libraryManager;
        LibraryMonitor = libraryMonitor;
        PluginSecurityManager = securityManager;
        CollectionManager = collectionManager;
        ApplicationPaths = applicationPaths;
        TaskManager = taskManager;
        ProviderManager = providerManager;
        Log = logManager.GetLogger(Plugin.Instance.Name);
        Instance = this;
    }

    public void Run()
    {
        LibraryManager.ItemAdded += libraryManager_ItemAdded;
    }

    public void Dispose()
    {
        NewItemTimer?.Dispose();
        LibraryMonitor?.Dispose();
    }

    private async void libraryManager_ItemAdded(object sender, ItemChangeEventArgs e)
    {
        var item = e.Item;
        if (item.GetType().Name == nameof(Movie))
        {
            Log.Info("Library Event Detected for Auto Version Grouping but will wait 1 min", null);
            await Task.Delay(TimeSpan.FromSeconds(60)).ConfigureAwait(false);

            try
            {
                Log.Info("New Library Event --- Running AutoGroup Task for {0}", item.Name);
                await TaskManager.Execute(
                    TaskManager.ScheduledTasks.FirstOrDefault(t => t.Name == "Auto Version Grouping"),
                    new TaskOptions()).ConfigureAwait(false);

            }
            catch (Exception ex)
            {
                //Catch so we can continue
            }
        }
    }
}


