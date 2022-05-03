// This file was @generated with LibOVRPlatform/codegen/main. Do not modify it!

namespace Oculus.Platform.Models
{
  using System;
  using System.Collections;
  using Oculus.Platform.Models;
  using System.Collections.Generic;
  using UnityEngine;

  public class LeaderboardEntry
  {
    public readonly byte[] ExtraData;
    public readonly int Rank;
    public readonly long Score;
    public readonly DateTime Timestamp;
    public readonly User User;


    public LeaderboardEntry(IntPtr o)
    {
      ExtraData = CAPI.ovr_LeaderboardEntry_GetExtraData(o);
      Rank = CAPI.ovr_LeaderboardEntry_GetRank(o);
      Score = CAPI.ovr_LeaderboardEntry_GetScore(o);
      Timestamp = CAPI.ovr_LeaderboardEntry_GetTimestamp(o);
      User = new User(CAPI.ovr_LeaderboardEntry_GetUser(o));
    }
  }

  public class LeaderboardEntryList : DeserializableList<LeaderboardEntry> {
    public LeaderboardEntryList(IntPtr a) {
      var count = (int)CAPI.ovr_LeaderboardEntryArray_GetSize(a);
      _Data = new List<LeaderboardEntry>(count);
      for (int i = 0; i < count; i++) {
        _Data.Add(new LeaderboardEntry(CAPI.ovr_LeaderboardEntryArray_GetElement(a, (UIntPtr)i)));
      }

      TotalCount = CAPI.ovr_LeaderboardEntryArray_GetTotalCount(a);
      _PreviousUrl = CAPI.ovr_LeaderboardEntryArray_GetPreviousUrl(a);
      _NextUrl = CAPI.ovr_LeaderboardEntryArray_GetNextUrl(a);
    }

    public readonly ulong TotalCount;
  }
}
