define(["loading", "dialogHelper", "formDialogStyle", "emby-checkbox", "emby-select", "emby-toggle", "emby-collapse"],
    function (loading, dialogHelper) {

        
        var CollectionsConfigurationPage =
        {
            var pluginId = "1F4B97E2-B87F-4964-8F9F-3109DB54C334";
            refreshCollections: function () {
                var url = ApiClient.getUrl("AutoCollections/Refresh");
                console.log(url);
                ApiClient.ajax({
                    type: 'post',
                    url: url
                });
                Dashboard.alert("Movie Version Collections Refresh Started");

            }

        };
        
        return function (view) {
            view.addEventListener('viewshow', async () => {

                Dashboard.showLoadingMsg();

                var page = view;

                ApiClient.getPluginConfiguration(CollectionsConfigurationPage.pluginId).then(function (config) {
                    view.querySelector('#chkDoNotChangeLockedItems', page).checked(config.DoNotChangeLockedItems || false).checkboxradio("refresh");

                    Dashboard.hideLoadingMsg();
                });
                
                loading.show();
                
                view.querySelector('.collectionsConfigurationPage').on('pageinit', function (event) {
                    console.log("pageinit");
                    var page = view;

                    view.querySelector('#collectionsConfigurationForm', page).on('submit', function (e) {

                        Dashboard.showLoadingMsg();

                        var form = this;

                        ApiClient.getPluginConfiguration(CollectionsConfigurationPage.pluginId).then(function (config) {
                            config.DoNotChangeLockedItems = view.querySelector('#chkDoNotChangeLockedItems', form).checked();
                            ApiClient.updatePluginConfiguration(CollectionsConfigurationPage.pluginId, config).then(Dashboard.processPluginConfigurationUpdateResult);
                        });
                        // Disable default form submission
                        return false;
                    });
                });
                

                loading.hide();

                

                

                document.querySelector('.pageTitle').innerHTML = "AutoCollections";

            });
        }
    });