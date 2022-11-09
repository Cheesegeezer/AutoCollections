define(["loading", "dialogHelper", "formDialogStyle", "emby-checkbox", "emby-select", "emby-toggle", "emby-collapse"],
    function (loading, dialogHelper) {

        const pluginId = "1F4B97E2-B87F-4964-8F9F-3109DB54C334";

        function refreshCollections() {
            var url = ApiClient.getUrl("AutoCollections/Refresh");
            console.log(url);
            ApiClient.ajax({
                type: 'post',
                url: url
            });
            Dashboard.alert("Movie Version Collections Refresh Started");
        }

        return function (view) {
            view.addEventListener('viewshow', async () => {

                loading.show();

                var config = await ApiClient.getPluginConfiguration(pluginId);

                ApiClient.getPluginConfiguration(pluginId).then(function (config) {
                    view.querySelector('.chkDoNotChangeLockedItems').checked = config.DoNotChangeLockedItems || false;

                });

                loading.hide();

                var dontChange = view.querySelector(".chkDoNotChangeLockedItems");
                dontChange.addEventListener('change',
                    (e) => {
                        e.preventDefault();
                        ApiClient.getPluginConfiguration(pluginId).then((config) => {
                            config.DoNotChangeLockedItems = dontChange.checked;
                            ApiClient.updatePluginConfiguration(pluginId, config).then((r) => {
                                Dashboard.processPluginConfigurationUpdateResult(r);
                            });
                        });
                    });

                var update = view.querySelector(".btnUpdate");
                update.addEventListener('click', e => {
                    e.preventDefault();
                    refreshCollections();
                });

                document.querySelector('.pageTitle').innerHTML = "Auto Version Grouping";

            });
        }
    });