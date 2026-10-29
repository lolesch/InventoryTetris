// Unity's persistentDataPath on WebGL is an in-memory filesystem that only reaches IndexedDB when it is
// synced, and nothing syncs it after a File.Write: a refresh loses every save. Writes that land while a
// sync runs share one follow-up sync, since Emscripten warns about overlapping ones.
mergeInto(LibraryManager.library, {
    IdbfsSync_Flush: function () {
        var state = window.__idbfsSync || (window.__idbfsSync = { running: false, again: false });

        if (state.running) {
            state.again = true;
            return;
        }

        state.running = true;
        FS.syncfs(false, function (error) {
            if (error)
                console.error('IDBFS sync failed: ' + error);

            state.running = false;

            if (state.again) {
                state.again = false;
                _IdbfsSync_Flush();
            }
        });
    }
});
