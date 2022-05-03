// This file was @generated with LibOVRPlatform/codegen/main. Do not modify it!

namespace Oculus.Platform
{
  using System;
  using System.Collections;
  using Oculus.Platform.Models;
  using System.Collections.Generic;
  using UnityEngine;

  public class RichPresenceOptions {

    public RichPresenceOptions() {
      Handle = CAPI.ovr_RichPresenceOptions_Create();
    }

    public void SetApiName(string value) {
      CAPI.ovr_RichPresenceOptions_SetApiName(Handle, value);
    }

    public void SetArgs(string key, string value) {
      CAPI.ovr_RichPresenceOptions_SetArgsString(Handle, key, value);
    }

    public void ClearArgs() {
      CAPI.ovr_RichPresenceOptions_ClearArgs(Handle);
    }

    public void SetCurrentCapacity(uint value) {
      CAPI.ovr_RichPresenceOptions_SetCurrentCapacity(Handle, value);
    }

    public void SetDeeplinkMessageOverride(string value) {
      CAPI.ovr_RichPresenceOptions_SetDeeplinkMessageOverride(Handle, value);
    }

    public void SetEndTime(DateTime value) {
      CAPI.ovr_RichPresenceOptions_SetEndTime(Handle, value);
    }

    public void SetExtraContext(RichPresenceExtraContext value) {
      CAPI.ovr_RichPresenceOptions_SetExtraContext(Handle, value);
    }

    public void SetIsIdle(bool value) {
      CAPI.ovr_RichPresenceOptions_SetIsIdle(Handle, value);
    }

    public void SetIsJoinable(bool value) {
      CAPI.ovr_RichPresenceOptions_SetIsJoinable(Handle, value);
    }

    public void SetJoinableId(string value) {
      CAPI.ovr_RichPresenceOptions_SetJoinableId(Handle, value);
    }

    public void SetMaxCapacity(uint value) {
      CAPI.ovr_RichPresenceOptions_SetMaxCapacity(Handle, value);
    }

    public void SetStartTime(DateTime value) {
      CAPI.ovr_RichPresenceOptions_SetStartTime(Handle, value);
    }


    // For passing to native C
    public static explicit operator IntPtr(RichPresenceOptions options) {
      return options != null ? options.Handle : IntPtr.Zero;
    }

    ~RichPresenceOptions() {
      CAPI.ovr_RichPresenceOptions_Destroy(Handle);
    }

    IntPtr Handle;
  }
}
