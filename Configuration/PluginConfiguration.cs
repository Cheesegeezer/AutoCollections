using MediaBrowser.Model.Plugins;

namespace AutoCollections.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    public int MinimumMembers { get; set; }

    public bool NeedsUpdate { get; set; }

    public bool DoNotChangeLockedItems { get; set; }

    public PluginConfiguration()
    {
        DoNotChangeLockedItems = false;
        MinimumMembers = 2;
        NeedsUpdate = true;
    }
}
