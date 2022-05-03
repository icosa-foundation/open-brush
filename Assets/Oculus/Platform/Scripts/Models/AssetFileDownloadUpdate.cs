// This file was @generated with LibOVRPlatform/codegen/main. Do not modify it!

namespace Oculus.Platform.Models
{
  using System;
  using System.Collections;
  using Oculus.Platform.Models;
  using System.Collections.Generic;
  using UnityEngine;

  public class AssetFileDownloadUpdate
  {
    public readonly UInt64 AssetFileId;
    public readonly UInt64 AssetId;
    public readonly uint BytesTotal;
    public readonly int BytesTransferred;
    public readonly bool Completed;


    public AssetFileDownloadUpdate(IntPtr o)
    {
      AssetFileId = CAPI.ovr_AssetFileDownloadUpdate_GetAssetFileId(o);
      AssetId = CAPI.ovr_AssetFileDownloadUpdate_GetAssetId(o);
      BytesTotal = CAPI.ovr_AssetFileDownloadUpdate_GetBytesTotal(o);
      BytesTransferred = CAPI.ovr_AssetFileDownloadUpdate_GetBytesTransferred(o);
      Completed = CAPI.ovr_AssetFileDownloadUpdate_GetCompleted(o);
    }
  }

}
