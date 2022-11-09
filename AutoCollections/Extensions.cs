using System.Collections.Generic;

namespace AutoCollections.AutoCollections;

public static class Extensions
{
	public static TU GetValueOrDefault<T, TU>(this Dictionary<T, TU> dictionary, T key, TU defaultValue)
	{
		if (!dictionary.TryGetValue(key, out var value))
		{
			return defaultValue;
		}
		return value;
	}
}
