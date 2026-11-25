using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HomelessPetHelper.Game.UI
{
    public sealed class StationNameInput : InputField
    {
        internal event Action KeyboardUnavailable;

#if UNITY_WEBGL && !UNITY_EDITOR
        private readonly byte[] _nativeText = new byte[257];
        private int _ticket;
#endif

        internal void OpenKeyboard()
        {
            if (!IsActive() || !IsInteractable()) return;
            if (!UsesWeChatKeyboard())
            {
                ActivateInputField();
                return;
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            ReadKeyboard();
            if (_ticket != 0) return;
            _ticket = HPHOpenNameKeyboard(text, characterLimit);
            if (_ticket == 0) ReportKeyboardUnavailable();
#endif
        }

        public override void OnPointerClick(PointerEventData eventData)
        {
            if (!UsesWeChatKeyboard()) base.OnPointerClick(eventData);
            else if (eventData.button == PointerEventData.InputButton.Left) OpenKeyboard();
        }

        internal void ReadKeyboard()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (_ticket == 0) return;
            int result = HPHPollNameKeyboard(_ticket, _nativeText, _nativeText.Length);
            if (result == 0) return;
            if (result != 1) _ticket = 0;
            if (result < 0) { ReportKeyboardUnavailable(); return; }
            int length = Array.IndexOf(_nativeText, (byte)0);
            if (length >= 0) text = Encoding.UTF8.GetString(_nativeText, 0, length);
#endif
        }

        public override bool shouldActivateOnSelect
        {
            get => !UsesWeChatKeyboard() && base.shouldActivateOnSelect;
            set => base.shouldActivateOnSelect = value;
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            ReadKeyboard();
        }

        internal void CloseKeyboard()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (_ticket != 0) HPHCloseNameKeyboard(_ticket);
            _ticket = 0;
#endif
            DeactivateInputField();
        }

        private void ReportKeyboardUnavailable() => KeyboardUnavailable?.Invoke();

        protected override void OnDisable()
        {
            CloseKeyboard();
            base.OnDisable();
        }

        private static bool UsesWeChatKeyboard()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return HPHHasNameKeyboard() != 0;
#else
            return false;
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int HPHHasNameKeyboard();
        [DllImport("__Internal")] private static extern int HPHOpenNameKeyboard(string initial, int limit);
        [DllImport("__Internal")] private static extern int HPHPollNameKeyboard(int ticket, [Out] byte[] buffer,
            int capacity);
        [DllImport("__Internal")] private static extern void HPHCloseNameKeyboard(int ticket);
#endif
    }
}
