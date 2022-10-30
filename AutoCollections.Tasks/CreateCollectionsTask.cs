using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;

namespace AutoCollections.Tasks;

public class CreateCollectionsTask : ILibraryPostScanTask
{
	public Task Run(IProgress<double> progress, CancellationToken cancellationToken)
	{
		return Plugin.Instance.CreateAllCollections(progress, cancellationToken);
	}
}
