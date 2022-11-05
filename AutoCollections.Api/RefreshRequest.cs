using System.Linq;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Services;
using MediaBrowser.Model.Tasks;

namespace AutoCollections.AutoCollections.Api;
public class AutoGroupRefreshRequest : IService 

{
    [Route("/AutoCollections/Refresh", "POST", Summary = "Run update AutoCollections scheduled task")]
    public class ExecuteSpotlightUpdateRequest : IReturnVoid
    {

    }

    private ITaskManager TaskManager { get; set; }
    private ILibraryManager LibraryManager { get; set; }
    private IJsonSerializer JsonSerializer { get; set; }
    private IRequest Request { get; set; }
    private IUserManager UserManager { get; set; }
    private ILogger Log { get; set; }
    public AutoGroupRefreshRequest(ITaskManager taskManager, ILibraryManager libraryManager, IJsonSerializer json, IUserManager userManager, ILogManager logManager)
    {
        TaskManager = taskManager;
        LibraryManager = libraryManager;
        JsonSerializer = json;
        UserManager = userManager;
        Log = logManager.GetLogger(Plugin.Instance.Name);
    }

    public async void Post(ExecuteSpotlightUpdateRequest request)
    {
        await TaskManager.Execute(TaskManager.ScheduledTasks.FirstOrDefault(task => task.Name == "Auto Version Grouping"), new TaskOptions()).ConfigureAwait(false);
    }
}
