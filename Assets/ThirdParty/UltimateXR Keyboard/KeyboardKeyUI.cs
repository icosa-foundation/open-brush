// --------------------------------------------------------------------------------------------------------------------
// <copyright file="UxrKeyboardKeyUI.cs" company="VRMADA">
//   Copyright (c) VRMADA, All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.UI;


namespace TiltBrush
{
    public enum KeyboardKeyType
    {
        Printable,
        Tab,
        Shift,
        CapsLock,
        Control,
        Alt,
        AltGr,
        Enter,
        Backspace,
        Del,
        ToggleSymbols,
        ToggleViewPassword,
        Escape
    }

    public enum KeyboardKeyLayoutType
    {
        /// <summary>
        ///     Single character label.
        /// </summary>
        SingleChar,

        /// <summary>
        ///     Key supports multiple outputs depending on shift/alt gr and has multiple labels because of that.
        /// </summary>
        MultipleChar
    }

    /// <summary>
    ///     Key press/release event parameters.
    /// </summary>
    public class KeyboardKeyEventArgs
    {
        #region Public Types & Data

        /// <summary>
        ///     Gets the key that was pressed/released.
        /// </summary>
        public KeyboardKeyUI Key { get; }

        /// <summary>
        ///     Gets whether it was a press (true) or release (false).
        /// </summary>
        public bool IsPress { get; }

        /// <summary>
        ///     Gets the current line content. If it was a keypress event and the the keypress was the ENTER key then the line
        ///     before pressing ENTER is passed.
        /// </summary>
        public string Line { get; }

        #endregion

        #region Constructors & Finalizer

        /// <summary>
        ///     Constructor.
        /// </summary>
        /// <param name="key">Key that was pressed</param>
        /// <param name="isPress">Is it a press or a release?</param>
        /// <param name="line">Current line</param>
        public KeyboardKeyEventArgs(KeyboardKeyUI key, bool isPress, string line)
        {
            Key     = key;
            IsPress = isPress;
            Line    = line;
        }

        #endregion
    }

    /// <summary>
    ///     UI component for a keyboard key.
    /// </summary>
    [ExecuteInEditMode]
    public class KeyboardKeyUI : MonoBehaviour
    {
        #region Inspector Properties/Serialized Fields

        [SerializeField] private KeyboardKeyType                 _keyType;
        [SerializeField] private KeyboardKeyLayoutType           _layout;
        [SerializeField] private string                     _printShift;
        [SerializeField] private string                     _printNoShift;
        [SerializeField] private string                     _printAltGr;
        [SerializeField] private string                     _forceLabel;
        [SerializeField] private LocalizedString            _LocalizedLabel;
        [SerializeField] private TMPro.TMP_Text             _singleLayoutValue;
        [SerializeField] private TMPro.TMP_Text             _multipleLayoutValueTopLeft;
        [SerializeField] private TMPro.TMP_Text             _multipleLayoutValueBottomLeft;
        [SerializeField] private TMPro.TMP_Text             _multipleLayoutValueBottomRight;
        [SerializeField] private List<KeyboardToggleSymbolsPage> _toggleSymbols;

        // Hidden in the custom inspector
        [SerializeField] private bool _nameDirty;

        #endregion

        #region Public Types & Data

        /// <summary>
        ///     Gets the key type.
        /// </summary>
        public KeyboardKeyType KeyType => _keyType;

        /// <summary>
        ///     Gets the layout use for the labels on the key.
        /// </summary>
        public KeyboardKeyLayoutType KeyLayoutType => _layout;

        /// <summary>
        ///     Gets the character used when the key has a single label.
        /// </summary>
        public char SingleLayoutValue => _singleLayoutValue != null && _singleLayoutValue.text.Length > 0 ? _singleLayoutValue.text[0] : '?';

        /// <summary>
        ///     Gets the character used in the top left corner when the key has multiple labels, because it supports combination
        ///     with shift and alt gr.
        /// </summary>
        public char MultipleLayoutValueTopLeft => _multipleLayoutValueTopLeft != null && _multipleLayoutValueTopLeft.text.Length > 0 ? _multipleLayoutValueTopLeft.text[0] : '?';

        /// <summary>
        ///     Gets the character used in the bottom left corner when the key has multiple labels, because it supports combination
        ///     with shift and alt gr.
        /// </summary>
        public char MultipleLayoutValueBottomLeft => _multipleLayoutValueBottomLeft != null && _multipleLayoutValueBottomLeft.text.Length > 0 ? _multipleLayoutValueBottomLeft.text[0] : '?';

        /// <summary>
        ///     Gets the character used in the bottom right corner when the key has multiple labels, because it supports
        ///     combination with shift and alt gr.
        /// </summary>
        public char MultipleLayoutValueBottomRight => _multipleLayoutValueBottomRight != null ? _multipleLayoutValueBottomRight.text[0] : '?';

        /// <summary>
        ///     Gets whether the key supports combination with shift and alt gr, and has a character specified for the bottom
        ///     right.
        /// </summary>
        public bool HasMultipleLayoutValueBottomRight => _multipleLayoutValueBottomRight != null && _multipleLayoutValueBottomRight.text.Length > 0;

        /// <summary>
        ///     Gets whether the key is a letter.
        /// </summary>
        public bool IsLetterKey => KeyType == KeyboardKeyType.Printable && char.IsLetter(SingleLayoutValue);

        /// <summary>
        ///     Gets the current symbols group selected, for a key that has a <see cref="KeyType" /> role of
        ///     <see cref="KeyboardKeyType.ToggleSymbols" />.
        /// </summary>
        public KeyboardToggleSymbolsPage CurrentToggleSymbolsPage => KeyType == KeyboardKeyType.ToggleSymbols && _toggleSymbols != null && _currentSymbolsIndex < _toggleSymbols.Count ? _toggleSymbols[_currentSymbolsIndex] : null;

        /// <summary>
        ///     Gets the next symbols group, for a key that has a <see cref="KeyType" /> role of
        ///     <see cref="KeyboardKeyType.ToggleSymbols" />, that would be selected if pressed.
        /// </summary>
        public KeyboardToggleSymbolsPage NextToggleSymbolsPage => KeyType == KeyboardKeyType.ToggleSymbols && _toggleSymbols != null && _toggleSymbols.Count > 0 ? _toggleSymbols[(_currentSymbolsIndex + 1) % _toggleSymbols.Count] : null;

        /// <summary>
        ///     Gets the <see cref="KeyboardKeyUI" /> component the key belongs to.
        /// </summary>
        public KeyboardUI Keyboard
        {
            get
            {
                if (_keyboard == null)
                {
                    _keyboard = GetComponentInParent<KeyboardUI>();
                }

                return _keyboard;
            }
        }

        public void KeyDown()
        {
            _keyboard.KeyButton_KeyDown(this);
        }

        public void KeyUp()
        {
            _keyboard.KeyButton_KeyUp(this);
        }

        /// <summary>
        ///     Gets the <see cref="UxrControlInput" /> component for the key.
        /// </summary>
        // public UxrControlInput ControlInput { get; private set; }
        //
        // /// <summary>
        // ///     Gets or sets whether the key can be interacted with.
        // /// </summary>
        // public bool Enabled
        // {
        //     get => ControlInput.Enabled;
        //     set => ControlInput.Enabled = value;
        // }

        /// <summary>
        ///     Gets or sets the string that, if non-empty, will override the label content on the key.
        /// </summary>
        public string ForceLabel
        {
            get => _forceLabel;
            set
            {
                _forceLabel = value;
                SetupKeyLabels();
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        ///     Gets the character that would be printed if the key was pressed.
        /// </summary>
        /// <param name="shift">Whether shift is pressed</param>
        /// <param name="altGr">Whether alt gr is pressed</param>
        /// <returns>Character that would be printed</returns>
        public char GetSingleLayoutValueNoForceLabel(bool shift, bool altGr)
        {
            if (shift && !string.IsNullOrEmpty(_printShift))
            {
                return _printShift[0];
            }
            if (altGr && !string.IsNullOrEmpty(_printAltGr))
            {
                return _printAltGr[0];
            }

            return !string.IsNullOrEmpty(_printNoShift) ? _printNoShift[0] : ' ';
        }

        /// <summary>
        ///     Updates the label on the key.
        /// </summary>
        /// <param name="shiftEnabled">Whether shift is enabled</param>
        public void UpdateLetterKeyLabel(bool shiftEnabled)
        {
            if (KeyType == KeyboardKeyType.Printable && _singleLayoutValue)
            {
                _singleLayoutValue.text = shiftEnabled ? _printShift : _printNoShift;
            }
        }

        /// <summary>
        ///     Sets up the toggle symbol entries.
        /// </summary>
        /// <param name="entries">Entries</param>
        public void SetupToggleSymbolsPages(List<KeyboardToggleSymbolsPage> entries)
        {
            if (_keyType == KeyboardKeyType.ToggleSymbols)
            {
                _toggleSymbols       = entries;
                _currentSymbolsIndex = 0;

                if (entries != null)
                {
                    for (int i = 0; i < entries.Count; ++i)
                    {
                        entries[i].KeysRoot.SetActive(i == 0);
                    }
                }

                SetupKeyLabels();
            }
        }

        /// <summary>
        ///     Sets the default symbols as the ones currently active.
        /// </summary>
        public void SetDefaultSymbols()
        {
            if (_keyType == KeyboardKeyType.ToggleSymbols && _toggleSymbols != null && _toggleSymbols.Count > 0)
            {
                _currentSymbolsIndex = 0;

                for (int i = 0; i < _toggleSymbols.Count; ++i)
                {
                    _toggleSymbols[i].KeysRoot.SetActive(i == _currentSymbolsIndex);
                }

                SetupKeyLabels();
            }
        }

        /// <summary>
        ///     Toggles to the next symbols.
        /// </summary>
        public void ToggleSymbols()
        {
            if (_keyType == KeyboardKeyType.ToggleSymbols && _toggleSymbols != null && _toggleSymbols.Count > 0)
            {
                _currentSymbolsIndex = (_currentSymbolsIndex + 1) % _toggleSymbols.Count;

                for (int i = 0; i < _toggleSymbols.Count; ++i)
                {
                    _toggleSymbols[i].KeysRoot.SetActive(i == _currentSymbolsIndex);
                }

                SetupKeyLabels();
            }
        }

        #endregion

        #region Unity

        /// <summary>
        ///     Initializes the component.
        /// </summary>
        protected void Awake()
        {
            // base.Awake();

            if (_keyboard == null)
            {
                _keyboard = GetComponentInParent<KeyboardUI>();
            }

            if (_keyboard == null && !Application.isEditor)
            {
                Debug.LogWarning($"{nameof(KeyboardUI)} component not found in parent hierarchy of key " + name);
            }

            SetupKeyLabels();

            if (_keyboard && Application.isPlaying)
            {
                _keyboard.RegisterKey(this);
            }
        }

        /// <summary>
        ///     Called when the component is destroyed.
        /// </summary>
        protected void OnDestroy()
        {
            // base.OnDestroy();

            if (_keyboard && Application.isPlaying)
            {
                _keyboard.UnregisterKey(this);
            }
        }

#if UNITY_EDITOR

        /// <summary>
        ///     Updates the labels and the GameObject's name in editor-mode depending on the labels that are set in the inspector.
        /// </summary>
        private void Update()
        {
            if (Application.isEditor)
            {
                if (!Application.isPlaying)
                {
                    SetupKeyLabels();
                }

                if (_nameDirty)
                {
                    UpdateName();
                }
            }
        }

#endif

        #endregion

        #region Private Methods

        /// <summary>
        ///     Sets up the labels on the key based on the current values in the inspector.
        /// </summary>
        private void SetupKeyLabels()
        {
            if (_singleLayoutValue)
            {
                _singleLayoutValue.text = "";
            }

            if (_multipleLayoutValueTopLeft)
            {
                _multipleLayoutValueTopLeft.text = "";
            }
            if (_multipleLayoutValueBottomLeft)
            {
                _multipleLayoutValueBottomLeft.text = "";
            }
            if (_multipleLayoutValueBottomRight)
            {
                _multipleLayoutValueBottomRight.text = "";
            }

            if (!string.IsNullOrEmpty(_forceLabel) && _singleLayoutValue)
            {
                _singleLayoutValue.text = _forceLabel;
                return;
            }

            switch (_keyType)
            {
                case KeyboardKeyType.Printable when _layout == KeyboardKeyLayoutType.SingleChar:
                    {
                        if (_singleLayoutValue)
                        {
                            _singleLayoutValue.text = _printShift.Length > 0 && (_keyboard.ShiftEnabled || _keyboard.CapsLockEnabled) ? _printShift : _printNoShift;
                        }
                        break;
                    }
                case KeyboardKeyType.Printable:
                    {
                        if (_multipleLayoutValueTopLeft)
                        {
                            _multipleLayoutValueTopLeft.text = _printShift;
                        }
                        if (_multipleLayoutValueBottomLeft)
                        {
                            _multipleLayoutValueBottomLeft.text = _printNoShift;
                        }
                        if (_multipleLayoutValueBottomRight)
                        {
                            _multipleLayoutValueBottomRight.text = _printAltGr;
                        }
                        break;
                    }
                case KeyboardKeyType.Tab:
                case KeyboardKeyType.Shift:
                case KeyboardKeyType.CapsLock:
                case KeyboardKeyType.Control:
                case KeyboardKeyType.Alt:
                case KeyboardKeyType.AltGr:
                case KeyboardKeyType.Enter:
                case KeyboardKeyType.Backspace:
                case KeyboardKeyType.Del:
                case KeyboardKeyType.Escape:
                    if (_singleLayoutValue)
                    {
                        _singleLayoutValue.text = GetLocalizedKeyName(_keyType);
                    }
                    break;
                case KeyboardKeyType.ToggleSymbols:
                    _singleLayoutValue.text = NextToggleSymbolsPage != null    ? NextToggleSymbolsPage.Label :
                        CurrentToggleSymbolsPage != null ? CurrentToggleSymbolsPage.Label : string.Empty;
                    break;
            }
        }
        private string GetLocalizedKeyName(KeyboardKeyType fallbackText)
        {
            try
            {
                var locString = _LocalizedLabel.GetLocalizedStringAsync().Result;
                return locString;
            }
            catch
            {
                return fallbackText.ToString();
            }
        }

        /// <summary>
        ///     Updates the GameObject name based on the labels set up in the inspector.
        /// </summary>
        private void UpdateName()
        {
            if (_keyType == KeyboardKeyType.Printable)
            {
                if (_singleLayoutValue && _layout == KeyboardKeyLayoutType.SingleChar)
                {
                    if (_singleLayoutValue.text == " ")
                    {
                        name = "Key Space";
                    }
                    else
                    {
                        name = "Key " + _singleLayoutValue.text;
                    }
                }
                else
                {
                    name = "Key" + (!string.IsNullOrEmpty(_printShift) ? " " + _printShift : "") + (!string.IsNullOrEmpty(_printNoShift) ? " " + _printNoShift : "") + (!string.IsNullOrEmpty(_printAltGr) ? " " + _printAltGr : "");
                }
            }
            else if (_keyType == KeyboardKeyType.ToggleSymbols)
            {
                name = "Key Toggle Symbols";
            }
            else if (_keyType == KeyboardKeyType.ToggleViewPassword)
            {
                name = "Key Toggle View Password";
            }
            else if (_singleLayoutValue)
            {
                name = $"Key {_singleLayoutValue.text}";
            }

            _nameDirty = false;
        }

        #endregion

        #region Private Types & Data

        private KeyboardUI _keyboard;
        private int _currentSymbolsIndex;

        #endregion
    }
}

#pragma warning restore 0414