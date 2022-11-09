using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;

namespace AutoCollections.AutoCollections.Tasks;

public class CreateCollectionsTask : ILibraryPostScanTask
{
    private readonly CollectionsScheduleTask _task;

    public CreateCollectionsTask(CollectionsScheduleTask task)
    {
        _task = task;
    }
    public Task Run(IProgress<double> progress, CancellationToken cancellationToken)
	{
		return _task.Execute(cancellationToken, progress);
	}
}
