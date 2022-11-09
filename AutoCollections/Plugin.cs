using System;
using System.Collections.Generic;
using System.IO;
using AutoCollections.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace AutoCollections.AutoCollections;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages, IHasThumbImage
{
	public static string PluginName = "Auto Movie Version Collections";
	
	public override Guid Id => new Guid("1F4B97E2-B87F-4964-8F9F-3109DB54C334");

    public override string Name => PluginName;

	public override string Description => "Creates Collections for Movies with different versions by unique TMDB id.";

	public static Plugin Instance { get; private set; }

	public static ILogger Logger { get; set; }

	public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer, ILibraryManager libraryManager)
		: base(applicationPaths, xmlSerializer)
	{
		Instance = this;
	}
    public ImageFormat ThumbImageFormat => ImageFormat.Jpg;

    //Display Thumbnail image for Plugin Catalogue  - you will need to change build action for thumb.jpg to embedded Resource
    public Stream GetThumbImage()
    {
        Type type = GetType();
        return type.Assembly.GetManifestResourceStream(type.Namespace + ".thumb.jpg");
    }

    public IEnumerable<PluginPageInfo> GetPages() => new[]
    {
        new PluginPageInfo
        {
            //html File
            Name = "AGConfigurationPage",
            EmbeddedResourcePath = GetType().Namespace + ".Configuration.AGConfigurationPage.html",
            EnableInMainMenu = true,
            DisplayName = "Auto-Version-Grouping",
        },
        new PluginPageInfo
        {
            //JS File
            Name = "AGConfigurationPageJS",
            EmbeddedResourcePath = GetType().Namespace + ".Configuration.AGConfigurationPage.js",
        }
    };

	/*public override void UpdateConfiguration(BasePluginConfiguration configuration)
	{
		PluginConfiguration configuration2 = base.Configuration;
		base.UpdateConfiguration(configuration);
		ServerEntryPoint.Instance.OnConfigurationUpdated(configuration2, (PluginConfiguration)(object)configuration);
	}*/
	
}
