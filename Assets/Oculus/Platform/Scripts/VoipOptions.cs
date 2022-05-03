// This file was @generated with LibOVRPlatform/codegen/main. Do not modify it!

namespace Oculus.Platform
{
  using System;
  using System.Collections;
  using Oculus.Platform.Models;
  using System.Collections.Generic;
  using UnityEngine;

  public class VoipOptions {

    public VoipOptions() {
      Handle = CAPI.ovr_VoipOptions_Create();
    }

    public void SetBitrateForNewConnections(VoipBitrate value) {
      CAPI.ovr_VoipOptions_SetBitrateForNewConnections(Handle, value);
    }

    public void SetCreateNewConnectionUseDtx(VoipDtxState value) {
      CAPI.ovr_VoipOptions_SetCreateNewConnectionUseDtx(Handle, value);
    }


    // For passing to native C
    public static explicit operator IntPtr(VoipOptions options) {
      return options != null ? options.Handle : IntPtr.Zero;
    }

    ~VoipOptions() {
      CAPI.ovr_VoipOptions_Destroy(Handle);
    }

    IntPtr Handle;
  }
}
