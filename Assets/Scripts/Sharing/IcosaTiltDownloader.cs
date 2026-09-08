// Copyright 2026 The Open Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace TiltBrush
{
    public enum IcosaTiltDownloadStatus
    {
        Success,
        MissingUrl,
        InvalidUrl,
        CreateFailed,
        RequestFailed,
        InvalidTilt,
        ReplaceFailed,
        Canceled
    }

    public class IcosaTiltDownloadResult
    {
        public IcosaTiltDownloadStatus Status { get; }
        public string UserMessage { get; }
        public string Details { get; }
        public Exception Exception { get; }
        public bool Succeeded => Status == IcosaTiltDownloadStatus.Success;

        public IcosaTiltDownloadResult(
            IcosaTiltDownloadStatus status, string userMessage, string details = null,
            Exception exception = null)
        {
            Status = status;
            UserMessage = userMessage;
            Details = details;
            Exception = exception;
        }
    }

    public static class IcosaTiltDownloader
    {
        private static readonly HashSet<string> s_InvalidTiltDownloadsThisSession =
            new HashSet<string>();

        public static IEnumerator DownloadTiltCoroutine(
            IcosaSceneFileInfo info, string targetTiltPath, byte[] buffer,
            Func<bool> isCanceled, Action<UnityWebRequest> onRequestChanged,
            Action<IcosaTiltDownloadResult> onComplete)
        {
            IcosaTiltDownloadResult Complete(
                IcosaTiltDownloadStatus status, string userMessage, string details = null,
                Exception exception = null)
            {
                var result = new IcosaTiltDownloadResult(status, userMessage, details, exception);
                onComplete?.Invoke(result);
                return result;
            }

            if (info == null)
            {
                Complete(IcosaTiltDownloadStatus.MissingUrl, "Sketch metadata is missing.");
                yield break;
            }

            if (string.IsNullOrEmpty(info.TiltFileUrl))
            {
                Complete(IcosaTiltDownloadStatus.MissingUrl,
                    $"No downloadable sketch file was found for {info.HumanName}.");
                yield break;
            }

            if (!Uri.TryCreate(info.TiltFileUrl, UriKind.Absolute, out _))
            {
                Complete(IcosaTiltDownloadStatus.InvalidUrl,
                    $"The sketch download URL is invalid for {info.HumanName}.",
                    info.TiltFileUrl);
                yield break;
            }

            string invalidDownloadKey = GetInvalidDownloadKey(info);
            if (s_InvalidTiltDownloadsThisSession.Contains(invalidDownloadKey))
            {
                Complete(IcosaTiltDownloadStatus.InvalidTilt,
                    $"Downloaded sketch file for {info.HumanName} was invalid.",
                    $"{info.HumanName} {targetTiltPath}");
                yield break;
            }

            if (string.IsNullOrEmpty(targetTiltPath))
            {
                Complete(IcosaTiltDownloadStatus.CreateFailed,
                    $"No local download path was available for {info.HumanName}.");
                yield break;
            }

            string tempTiltPath = targetTiltPath + ".download";
            UnityWebRequest request = null;
            try
            {
                File.Delete(tempTiltPath);
                request = UnityWebRequest.Get(info.TiltFileUrl);
                request.downloadHandler = new DownloadHandlerFastFile(tempTiltPath, buffer);
                onRequestChanged?.Invoke(request);
            }
            catch (Exception ex)
            {
                request?.Dispose();
                onRequestChanged?.Invoke(null);
                TryDeleteTempFile(tempTiltPath);
                Complete(IcosaTiltDownloadStatus.CreateFailed,
                    $"Error downloading sketch file for {info.HumanName}.",
                    $"{info.HumanName} {targetTiltPath}", ex);
                yield break;
            }

            using (request)
            {
                var op = request.SendWebRequest();
                while (!op.isDone)
                {
                    yield return null;
                    if (isCanceled != null && isCanceled())
                    {
                        TryDeleteTempFile(tempTiltPath);
                        onRequestChanged?.Invoke(null);
                        Complete(IcosaTiltDownloadStatus.Canceled, "Sketch download canceled.");
                        yield break;
                    }
                }

                if (request.isNetworkError || request.responseCode >= 400 ||
                    !string.IsNullOrEmpty(request.error))
                {
                    TryDeleteTempFile(tempTiltPath);
                    onRequestChanged?.Invoke(null);
                    Complete(IcosaTiltDownloadStatus.RequestFailed,
                        $"Error downloading sketch file for {info.HumanName}.\nOut of disk space?",
                        $"{request.error} {info.HumanName} {targetTiltPath}");
                    yield break;
                }
            }
            onRequestChanged?.Invoke(null);

            if (!new TiltFile(tempTiltPath).IsLoadable())
            {
                TryDeleteTempFile(tempTiltPath);
                s_InvalidTiltDownloadsThisSession.Add(invalidDownloadKey);
                Complete(IcosaTiltDownloadStatus.InvalidTilt,
                    $"Downloaded sketch file for {info.HumanName} was invalid.",
                    $"{info.HumanName} {targetTiltPath}");
                yield break;
            }

            try
            {
                if (File.Exists(targetTiltPath))
                {
                    File.Replace(tempTiltPath, targetTiltPath, null);
                }
                else
                {
                    File.Move(tempTiltPath, targetTiltPath);
                }
                info.TiltPath = targetTiltPath;
                info.TiltDownloaded = true;
                s_InvalidTiltDownloadsThisSession.Remove(invalidDownloadKey);
                Complete(IcosaTiltDownloadStatus.Success, null);
            }
            catch (Exception ex)
            {
                TryDeleteTempFile(tempTiltPath);
                Complete(IcosaTiltDownloadStatus.ReplaceFailed,
                    $"Error downloading sketch file for {info.HumanName}.\nCould not update the cached file.",
                    $"{info.HumanName} {targetTiltPath}", ex);
            }
        }

        private static string GetInvalidDownloadKey(IcosaSceneFileInfo info)
        {
            if (!string.IsNullOrEmpty(info.AssetId))
            {
                return info.AssetId;
            }
            return info.TiltFileUrl;
        }

        private static void TryDeleteTempFile(string tempTiltPath)
        {
            try
            {
                if (!string.IsNullOrEmpty(tempTiltPath) && File.Exists(tempTiltPath))
                {
                    File.Delete(tempTiltPath);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"ICOSATILT_LOAD Could not clean up failed download: {ex}");
            }
        }
    }
}
