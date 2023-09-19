using MediaBrowser.Model.Plugins;

namespace AutoCollections.Config;

public class PluginConfiguration : BasePluginConfiguration
{
    public int MinimumMembers { get; set; }

    public bool NeedsUpdate { get; set; }

    public bool DoNotChangeLockedItems { get; set; }

    public bool MergeAcrossLibraries { get; set; }

    public PluginConfiguration()
    {
        DoNotChangeLockedItems = false;
        MergeAcrossLibraries = true;
        MinimumMembers = 2;
        NeedsUpdate = true;
    }
}
