mergeInto(LibraryManager.library, {
    $HPHSaveResponse: function (response) {
        var json = JSON.stringify(response);
        var pointer = _malloc(lengthBytesUTF8(json) + 1);
        stringToUTF8(json, pointer, lengthBytesUTF8(json) + 1);
        return pointer;
    },
    HPHSaveRead__deps: ['$HPHSaveResponse'],
    HPHSaveRead: function (keyPointer) {
        if (typeof wx === 'undefined' || !wx.getStorageInfoSync || !wx.getStorageSync) {
            return HPHSaveResponse({ ok: false, code: 'unsupported_runtime' });
        }
        try {
            var key = UTF8ToString(keyPointer);
            var info = wx.getStorageInfoSync();
            if (!info || !Array.isArray(info.keys)) {
                return HPHSaveResponse({ ok: false, code: 'invalid_storage_info' });
            }
            if (info.keys.indexOf(key) < 0) return HPHSaveResponse({ ok: true, value: null });
            var value = wx.getStorageSync(key);
            if (typeof value !== 'string') {
                return HPHSaveResponse({ ok: false, code: 'invalid_value_type' });
            }
            return HPHSaveResponse({ ok: true, value: value });
        } catch (error) {
            return HPHSaveResponse({ ok: false, code: 'read_failed' });
        }
    },
    HPHSaveDelete__deps: ['$HPHSaveResponse'],
    HPHSaveDelete: function (keyPointer) {
        if (typeof wx === 'undefined' || !wx.removeStorageSync) {
            return HPHSaveResponse({ ok: false, code: 'unsupported_runtime' });
        }
        try {
            wx.removeStorageSync(UTF8ToString(keyPointer));
            return HPHSaveResponse({ ok: true });
        } catch (error) {
            return HPHSaveResponse({ ok: false, code: 'delete_failed' });
        }
    },
    HPHSaveWrite__deps: ['$HPHSaveResponse'],
    HPHSaveWrite: function (keyPointer, contentPointer) {
        if (typeof wx === 'undefined' || !wx.setStorageSync) {
            return HPHSaveResponse({ ok: false, code: 'unsupported_runtime' });
        }
        try {
            wx.setStorageSync(UTF8ToString(keyPointer), UTF8ToString(contentPointer));
            return HPHSaveResponse({ ok: true });
        } catch (error) {
            return HPHSaveResponse({ ok: false, code: 'write_failed' });
        }
    }
});
