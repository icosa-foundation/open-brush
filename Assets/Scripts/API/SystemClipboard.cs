using System;
using System.IO;
using UnityEngine;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System.Runtime.InteropServices;
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

        [DllImport("gdi32.dll", EntryPoint = "GetObject", SetLastError = true)]
        private static extern int GetObjectBitmap(IntPtr hObject, int nCount, ref BITMAP lpObject);

        [DllImport("gdi32.dll", EntryPoint = "GetDIBits", SetLastError = true)]
        private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan,
            uint cScanLines, byte[] lpvBits, ref BITMAPINFO lpbi, uint uUsage);

        [DllImport("user32.dll", EntryPoint = "GetDC", SetLastError = true)]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll", EntryPoint = "ReleaseDC", SetLastError = true)]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAP
        {
            public int bmType;
            public int bmWidth;
            public int bmHeight;
            public int bmWidthBytes;
            public ushort bmPlanes;
            public ushort bmBitsPixel;
            public IntPtr bmBits;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        /// Header plus room for the three BI_BITFIELDS masks. 32bpp BI_RGB needs no colour
        /// table, but GDI expects a BITMAPINFO rather than a bare header.
        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
            public uint bmiColors0;
            public uint bmiColors1;
            public uint bmiColors2;
        }

        private const uint BI_RGB = 0;
        private const uint DIB_RGB_COLORS = 0;

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

        /// Reads the clipboard HBITMAP through GDI rather than System.Drawing, which is not
        /// available under the .NET Standard profile. Requesting 32bpp BI_RGB with a negative
        /// biHeight yields top-down BGRA rows, the same bytes the previous
        /// Image.FromHbitmap/LockBits(Format32bppArgb) path produced, so the resulting texture
        /// is unchanged.
        private static Texture2D TextureFromClipboardData(IntPtr hBitmap)
        {
            BITMAP bmp = new BITMAP();
            if (GetObjectBitmap(hBitmap, Marshal.SizeOf(typeof(BITMAP)), ref bmp) == 0)
            {
                return null;
            }

            int width = bmp.bmWidth;
            int height = Math.Abs(bmp.bmHeight);
            if (width <= 0 || height <= 0)
            {
                return null;
            }

            BITMAPINFO info = new BITMAPINFO();
            info.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
            info.bmiHeader.biWidth = width;
            info.bmiHeader.biHeight = -height;
            info.bmiHeader.biPlanes = 1;
            info.bmiHeader.biBitCount = 32;
            info.bmiHeader.biCompression = BI_RGB;

            byte[] pixels = new byte[width * height * 4];
            IntPtr hdc = GetDC(IntPtr.Zero);
            try
            {
                if (GetDIBits(hdc, hBitmap, 0, (uint)height, pixels, ref info, DIB_RGB_COLORS) == 0)
                {
                    return null;
                }
            }
            finally
            {
                if (hdc != IntPtr.Zero)
                {
                    ReleaseDC(IntPtr.Zero, hdc);
                }
            }

            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.LoadRawTextureData(pixels);
            texture.Apply();
            return texture;
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
