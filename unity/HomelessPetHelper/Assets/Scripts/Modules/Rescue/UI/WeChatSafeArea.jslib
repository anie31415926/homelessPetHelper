mergeInto(LibraryManager.library, {
    HPHReadWeChatSafeArea: function (pointer) {
        if (typeof wx === 'undefined' || typeof wx.getWindowInfo !== 'function') return 0;

        var windowInfo;
        try {
            windowInfo = wx.getWindowInfo();
        } catch (error) {
            return 0;
        }
        if (!windowInfo) return 0;
        var width = windowInfo.windowWidth;
        var height = windowInfo.windowHeight;
        if (!Number.isFinite(width) || !Number.isFinite(height) || width <= 0 || height <= 0) return 0;

        var safe = windowInfo.safeArea;
        var left = 0, top = 0, right = width, bottom = height;
        if (safe && [safe.left, safe.top, safe.right, safe.bottom].every(Number.isFinite) &&
            safe.left < safe.right && safe.top < safe.bottom && safe.right > 0 && safe.bottom > 0 &&
            safe.left < width && safe.top < height) {
            left = Math.max(0, safe.left);
            top = Math.max(0, safe.top);
            right = Math.min(width, safe.right);
            bottom = Math.min(height, safe.bottom);
        }

        if (typeof wx.getMenuButtonBoundingClientRect === 'function') {
            var menu = null;
            try {
                menu = wx.getMenuButtonBoundingClientRect();
            } catch (error) {
                menu = null;
            }
            if (menu && [menu.left, menu.top, menu.right, menu.bottom].every(Number.isFinite) &&
                menu.left >= 0 && menu.right <= width && menu.left < menu.right &&
                menu.top >= 0 && menu.top < menu.bottom && menu.bottom < bottom) {
                top = Math.max(top, menu.bottom);
            }
        }

        var index = pointer >> 2;
        HEAPF32[index] = left / width;
        HEAPF32[index + 1] = 1 - bottom / height;
        HEAPF32[index + 2] = right / width;
        HEAPF32[index + 3] = 1 - top / height;
        return 1;
    }
});
