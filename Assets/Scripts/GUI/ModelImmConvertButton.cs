// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

namespace TiltBrush
{
    public class ModelImmConvertButton : BaseButton
    {
        protected override void OnButtonPressed()
        {
            base.OnButtonPressed();
            GetComponentInParent<ReferencePanelModelTab>()?.ConvertSelectedImmToEditable();
        }
    }
}
