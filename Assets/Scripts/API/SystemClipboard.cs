using System;
using System.IO;
using UnityEngine;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;
#endif

namespace TiltBrush
{
    public static class SystemClipboard
    {
        public static string GetClipboardText()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return GetClipboardManager().Call<string>("getText");
#else
            return GUIUtility.systemCopyBuffer;
#endif
        }

        public static void SetClipboardText(string text)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            GetClipboardManager().Call("setText", text);
#else
            GUIUtility.systemCopyBuffer = text;
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject GetClipboardManager()
        {
            var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject _currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            var staticContext = new AndroidJavaClass("android.content.Context");
            AndroidJavaObject _clipboardService = staticContext.GetStatic<AndroidJavaObject>("CLIPBOARD_SERVICE");
            return _currentActivity.Call<AndroidJavaObject>("getSystemService", _clipboardService);
        }
#endif

        public static Texture2D GetClipboardImage()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
        return GetClipboardImageAndroid();
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            return GetClipboardImageWindows();
#else
            Debug.LogError("GetClipboardImage is not supported on this platform.");
            return null;
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [DllImport("user32.dll", EntryPoint = "OpenClipboard", SetLastError = true)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", EntryPoint = "CloseClipboard", SetLastError = true)]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll", EntryPoint = "GetClipboardData", SetLastError = true)]
        private static extern IntPtr GetClipboardData(uint uFormat);

        [DllImport("user32.dll", EntryPoint = "IsClipboardFormatAvailable", SetLastError = true)]
        private static extern bool IsClipboardFormatAvailable(uint format);

        private const uint CF_BITMAP = 2;

        private static Texture2D GetClipboardImageWindows()
        {
            Texture2D clipboardImage = null;

            if (IsClipboardFormatAvailable(CF_BITMAP) && OpenClipboard(IntPtr.Zero))
            {
                try
                {
                    IntPtr hBitmap = GetClipboardData(CF_BITMAP);
                    if (hBitmap != IntPtr.Zero)
                    {
                        clipboardImage = TextureFromClipboardData(hBitmap);
                    }
                }
                finally
                {
                    CloseClipboard();
                }
            }

            return clipboardImage;
        }

        private static Texture2D TextureFromClipboardData(IntPtr hBitmap)
        {
            Bitmap bitmap = null;
            try
            {
                bitmap = Image.FromHbitmap(hBitmap);
                Texture2D texture = new Texture2D(bitmap.Width, bitmap.Height, TextureFormat.RGBA32, false);
                BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

                IntPtr pixelsPtr = bitmapData.Scan0;
                int size = bitmapData.Stride * bitmapData.Height;
                byte[] pixels = new byte[size];
                Marshal.Copy(pixelsPtr, pixels, 0, size);

                texture.LoadRawTextureData(pixels);
                texture.Apply();

                bitmap.UnlockBits(bitmapData);

                return texture;
            }
            finally
            {
                if (bitmap != null)
                {
                    bitmap.Dispose();
                }
            }
        }


#endif


#if UNITY_ANDROID && !UNITY_EDITOR
    private static Texture2D GetClipboardImageAndroid()
    {
        Texture2D clipboardImage = null;

        AndroidJavaClass UnityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        AndroidJavaObject currentActivity = UnityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        currentActivity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
        {
            AndroidJavaObject clipboardManager = currentActivity.Call<AndroidJavaObject>("getSystemService", "clipboard");
            if (clipboardManager.Call<bool>("hasPrimaryClip"))
            {
                AndroidJavaObject clipData = clipboardManager.Call<AndroidJavaObject>("getPrimaryClip");
                if (clipData.Call<int>("getItemCount") > 0)
                {
                    AndroidJavaObject clipItem = clipData.Call<AndroidJavaObject>("getItemAt", 0);
                    AndroidJavaObject clipUri = clipItem.Call<AndroidJavaObject>("getUri");
                    if (clipUri != null)
                    {
                        string imagePath = GetImagePathFromUri(currentActivity, clipUri);
                        if (!string.IsNullOrEmpty(imagePath))
                        {
                            clipboardImage = LoadTexture2DFromPath(imagePath);
                        }
                    }
                }
            }
        }));

        return clipboardImage;
    }

    private static string GetImagePathFromUri(AndroidJavaObject activity, AndroidJavaObject uri)
    {
        string imagePath = "";

        AndroidJavaClass contentResolverClass = new AndroidJavaClass("android.content.ContentResolver");
        string columnData = contentResolverClass.GetStatic<string>("DATA");

        AndroidJavaObject contentResolver = activity.Call<AndroidJavaObject>("getContentResolver");
        AndroidJavaClass cursorLoaderClass = new AndroidJavaClass("android.content.CursorLoader");

        AndroidJavaObject cursorLoader = new AndroidJavaObject("android.content.CursorLoader", activity, uri, null, null, null);
        AndroidJavaObject cursor = cursorLoader.Call<AndroidJavaObject>("loadInBackground");

        int columnIndex = cursor.Call<int>("getColumnIndexOrThrow", columnData);
        if (cursor.Call<bool>("moveToFirst"))
        {
            imagePath = cursor.Call<string>("getString", columnIndex);
        }

        cursor.Call("close");

        return imagePath;
    }

    private static Texture2D LoadTexture2DFromPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        Texture2D texture = null;
        byte[] fileData;

        if (File.Exists(path))
        {
            fileData = File.ReadAllBytes(path);
            texture = new Texture2D(2, 2);
            texture.LoadImage(fileData); // This will auto-resize the texture dimensions
        }

        return texture;
    }

#endif
    }
}
