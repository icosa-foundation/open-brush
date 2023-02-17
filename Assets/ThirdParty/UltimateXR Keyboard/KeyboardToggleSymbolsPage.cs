// --------------------------------------------------------------------------------------------------------------------
// <copyright file="UxrToggleSymbolsPage.cs" company="VRMADA">
//   Copyright (c) VRMADA, All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------
using System;
using UnityEngine;

namespace TiltBrush
{
    /// <summary>
    ///     Symbols page for keyboard symbols. A keyboard may have multiple symbol pages.
    /// </summary>
    [Serializable]
    public class KeyboardToggleSymbolsPage
    {
        #region Inspector Properties/Serialized Fields

        [SerializeField] private GameObject _keysRoot;
        [SerializeField] private string     _label;

        #endregion

        #region Public Types & Data

        /// <summary>
        ///     Gets the root <see cref="GameObject" /> where all the keys in the page hang from.
        /// </summary>
        public GameObject KeysRoot => _keysRoot;

        /// <summary>
        ///     Gets the label that describes the symbols in the page.
        /// </summary>
        public string Label => _label;

        #endregion

        #region Constructors & Finalizer

        /// <summary>
        ///     Constructor.
        /// </summary>
        /// <param name="keysRoot">Root where are symbols in the page hang from</param>
        /// <param name="label">Label that describes the symbols in the page</param>
        public KeyboardToggleSymbolsPage(GameObject keysRoot, string label)
        {
            _keysRoot = keysRoot;
            _label    = label;
        }

        #endregion
    }
}