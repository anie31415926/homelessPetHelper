mergeInto(LibraryManager.library, {
    $HPHNameKeyboard: {
        nextTicket: 0,
        current: null,
        available: function () {
            return typeof wx !== 'undefined' && typeof wx.showKeyboard === 'function' &&
                typeof wx.hideKeyboard === 'function' && typeof wx.onKeyboardInput === 'function' &&
                typeof wx.offKeyboardInput === 'function' && typeof wx.onKeyboardConfirm === 'function' &&
                typeof wx.offKeyboardConfirm === 'function' && typeof wx.onKeyboardComplete === 'function' &&
                typeof wx.offKeyboardComplete === 'function';
        },
        detach: function (state) {
            wx.offKeyboardInput(state.input);
            wx.offKeyboardConfirm(state.confirm);
            wx.offKeyboardComplete(state.complete);
        },
        finish: function (state, event, status) {
            if (HPHNameKeyboard.current !== state || state.status !== 0) return;
            if (event && typeof event.value === 'string') state.value = event.value;
            state.status = status;
            HPHNameKeyboard.detach(state);
        },
        close: function (state) {
            HPHNameKeyboard.current = null;
            if (state.status !== 0) return;
            HPHNameKeyboard.detach(state);
            wx.hideKeyboard({});
        }
    },
    HPHHasNameKeyboard__deps: ['$HPHNameKeyboard'],
    HPHHasNameKeyboard: function () {
        return HPHNameKeyboard.available() ? 1 : 0;
    },
    HPHOpenNameKeyboard__deps: ['$HPHNameKeyboard'],
    HPHOpenNameKeyboard: function (initial, limit) {
        var bridge = HPHNameKeyboard;
        if (!bridge.available()) return 0;
        if (bridge.current) bridge.close(bridge.current);
        var state = { ticket: ++bridge.nextTicket, value: UTF8ToString(initial), status: 0, dirty: false };
        state.input = function (event) {
            if (bridge.current !== state || state.status !== 0 || typeof event.value !== 'string') return;
            state.value = event.value;
            state.dirty = true;
        };
        state.confirm = state.input; // Completion follows confirmation; the game Save button remains explicit.
        state.complete = function (event) { bridge.finish(state, event, 2); };
        bridge.current = state;
        wx.onKeyboardInput(state.input);
        wx.onKeyboardConfirm(state.confirm);
        wx.onKeyboardComplete(state.complete);
        try {
            wx.showKeyboard({
                defaultValue: state.value, maxLength: limit, multiple: false, confirmHold: false,
                confirmType: 'done', fail: function () { bridge.finish(state, null, -1); }
            });
        } catch (error) {
            bridge.finish(state, null, -1);
        }
        return state.ticket;
    },
    HPHPollNameKeyboard__deps: ['$HPHNameKeyboard'],
    HPHPollNameKeyboard: function (ticket, buffer, capacity) {
        var bridge = HPHNameKeyboard;
        var state = bridge.current;
        if (!state || state.ticket !== ticket) return -1;
        if (!state.dirty && state.status === 0) return 0;
        if (state.status !== 0) bridge.current = null;
        state.dirty = false;
        if (state.status < 0) return state.status;
        stringToUTF8(state.value, buffer, capacity);
        return state.status || 1;
    },
    HPHCloseNameKeyboard__deps: ['$HPHNameKeyboard'],
    HPHCloseNameKeyboard: function (ticket) {
        var state = HPHNameKeyboard.current;
        if (state && state.ticket === ticket) HPHNameKeyboard.close(state);
    }
});
