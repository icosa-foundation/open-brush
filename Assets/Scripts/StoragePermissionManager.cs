using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Android;

namespace HTC.OpenBrush.StoragePermission
{
	public enum FileDirectoryListOrder
    {
		Alphabet,
		RecentTime
    }

	public class StoragePermissionManager : MonoBehaviour
	{
#region Permission
		private static List<string> requestedPermissions = new List<string>
		{
            Permission.ExternalStorageWrite,
            Permission.ExternalStorageRead,
			"android.permission.ACCESS_MEDIA_LOCATION",
		};

		private Coroutine permissionCoroutine = null;

		public void RequestStoragePermission()
        {
            if (permissionCoroutine == null)
            {
                permissionCoroutine = StartCoroutine(AskForPermissions());
            }
        }

		public bool HasPermissionToStorage()
		{
			return Permission.HasUserAuthorizedPermission(requestedPermissions[0]);
		}

		public bool HasPermissionToSdCard()
        {
			return Permission.HasUserAuthorizedPermission("android.permission.ACCESS_MEDIA_LOCATION");
		}

		public bool HasPermissionToUsb()
        {
			return Permission.HasUserAuthorizedPermission("android.permission.WRITE_MEDIA_STORAGE");
		}

		public IEnumerator AskForPermissions()
		{
			Debug.Log($"[StoragePermissionManager] AskForPermissions");
			List<bool> permissions = new List<bool>(requestedPermissions.Count);
			List<bool> permissionsAsked = new List<bool>(requestedPermissions.Count);
			List<Action<int>> actions = new List<Action<int>>(requestedPermissions.Count);

			for (int i = 0; i < requestedPermissions.Count; i++)
			{
				permissions.Insert(i, false);
				permissionsAsked.Insert(i, false);
				var action = new Action<int>((int index) =>
				{
					permissions[index] = Permission.HasUserAuthorizedPermission(requestedPermissions[index]);
					if (!permissions[index] && !permissionsAsked[index])
					{
						Debug.Log($"[StoragePermissionManager] request {requestedPermissions[index]}");
						Permission.RequestUserPermission(requestedPermissions[index]);
						permissionsAsked[index] = true;
						return;
					}
				});
				actions.Insert(i, action);
			}
			
			for (int i = 0; i < permissionsAsked.Count;)
			{
				actions[i].Invoke(i);
				if (permissions[i])
				{
					++i;
				}
				yield return new WaitForEndOfFrame();
			}

			permissionCoroutine = null;
		}
#endregion

		private static StoragePermissionManager mInstance = null;
		public static StoragePermissionManager instance
		{
			get
			{
				if (!mInstance)
				{
					mInstance = FindObjectOfType(typeof(StoragePermissionManager)) as StoragePermissionManager;
					if (!mInstance)
					{
						Debug.LogError("You need to have one active StoragePermissionManager script on a GameObject in your scene.");
					}
				}
				return mInstance;
			}
		}
	}
}
