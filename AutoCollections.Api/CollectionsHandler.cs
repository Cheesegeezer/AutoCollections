using System;
using System.Threading;
using System.Threading.Tasks;
using AutoCollections.Tasks;
using MediaBrowser.Model.Services;

namespace AutoCollections.Api;

internal class CollectionsHandler : IService
{
    /*public void Post(RefreshRequest request)
	{
		Task.WhenAll(CollectionsScheduleTask.Instance.Execute(CancellationToken.None, new Progress<double>()));
	}*/
}
