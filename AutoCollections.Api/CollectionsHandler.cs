using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Services;

namespace AutoCollections.Api;

internal class CollectionsHandler : IService
{
	public void Post(RefreshRequest request)
	{
		Task.WhenAll(Plugin.Instance.CreateAllCollections(new Progress<double>(), CancellationToken.None));
	}
}
