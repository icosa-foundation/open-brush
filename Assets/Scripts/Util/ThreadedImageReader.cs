// Copyright 2020 The Tilt Brush Authors
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
using System.IO;
using UnityEngine;

namespace TiltBrush
{
    public class RawImage
    {
        public Color32[] ColorData;
        public int ColorWidth;
        public int ColorHeight;
        /// width / height of the original image data
        public float ColorAspect;
    }

    /// On construction, starts a thread that reads file.
    /// Usage:
    /// - Poll .Done until it returns true.
    /// - .Result will be non-null if the file read and parsed properly.
    ///   If it's non-null, all the fields are guaranteed to be filled in.
    ///
    class ThreadedImageReader : Future<RawImage>
    {
        public bool Finished
        {
            get
            {
                return m_state == State.Done || m_state == State.Error;
            }
        }

        /// It's an error to call this when Done=false
        public RawImage Result
        {
            get
            {
                RawImage value;
                if (!TryGetResult(out value))
                {
                    throw new InvalidOperationException("Result not ready");
                }
                return value;
            }
        }

        /// maxDimension and abortDimension are in texels.
        /// If an image dimension exceeds maxDimension, a (slow) cpu-based downsample will be done.
        /// If an image dimension exceeds abortDimension, an ImageTooLargeError exception will be thrown.
        public ThreadedImageReader(string file, int maxDimension = -1, int abortDimension = -1)
            : base(computation: () => ThreadProc(file, maxDimension, abortDimension),
                cleanupFunction: null,
                longRunning: true)
        {
        }

        public ThreadedImageReader(
            Func<Stream> openRead,
            string identifier = "",
            int maxDimension = -1,
            int abortDimension = -1,
            long maxFileSize = -1)
            : base(
                computation: () => ThreadProc(
                    openRead, identifier ?? "", maxDimension, abortDimension, maxFileSize),
                cleanupFunction: null,
                longRunning: true)
        {
        }

        /// identifier is optional, and only used to report back the source of the image
        /// if there is an error. Can be null.
        /// maxDimension and abortDimension are in texels.
        /// If an image dimension exceeds maxDimension, a (slow) cpu-based downsample will be done.
        /// If an image dimension exceeds abortDimension, an ImageTooLargeError exception will be thrown.
        public ThreadedImageReader(byte[] rawData, string identifier = "",
                                   int maxDimension = -1, int abortDimension = -1)
            : base(computation: () =>
                    ThreadProc(rawData, identifier == null ? "" : identifier,
                        maxDimension, abortDimension),
                cleanupFunction: null,
                longRunning: true)
        {
        }

        /// Returns null if the file can't be read, or does not parse.
        static private RawImage ThreadProc(string filename,
                                           int maxDimension = -1, int abortDimension = -1)
        {
            byte[] rawData;
            try
            {
                rawData = File.ReadAllBytes(filename);
            }
            catch (IOException e)
            {
                // Will occur if file is deleted while thread is in flight.
                Debug.LogErrorFormat("Load {0}: {1}", filename, e);
                return null;
            }

            return ThreadProc(rawData, filename, maxDimension, abortDimension);
        }

        static private RawImage ThreadProc(
            Func<Stream> openRead,
            string identifier,
            int maxDimension = -1,
            int abortDimension = -1,
            long maxFileSize = -1)
        {
            byte[] rawData;
            try
            {
                using (Stream input = openRead())
                using (var bytes = new MemoryStream())
                {
                    var buffer = new byte[81920];
                    while (true)
                    {
                        int read = input.Read(buffer, 0, buffer.Length);
                        if (read == 0)
                        {
                            break;
                        }
                        if (maxFileSize >= 0 && bytes.Length + read > maxFileSize)
                        {
                            throw new ImageLoadError(
                                $"{identifier} is too large and could not be loaded.");
                        }
                        bytes.Write(buffer, 0, read);
                    }
                    rawData = bytes.ToArray();
                }
            }
            catch (IOException e)
            {
                Debug.LogError($"Load {identifier}: {e}");
                return null;
            }

            return ThreadProc(rawData, identifier, maxDimension, abortDimension);
        }

        static private RawImage ThreadProc(byte[] rawData, string identifier,
                                           int maxDimension = -1, int abortDimension = -1)
        {
            // Throws exception on error.
            var orig = ImageUtils.FromImageData(
                rawData, identifier, abortDimension);

            if (orig == null)
            {
                // This is unexpected... FromImageData should have raised an error.
                Debug.LogErrorFormat("Load {0}: silently failed?", identifier);
                return null;
            }

            if (maxDimension > 0)
            {
                int desiredW = Mathf.Min(orig.ColorWidth, maxDimension);
                int desiredH = Mathf.Min(orig.ColorHeight, maxDimension);
                if (desiredW != orig.ColorWidth ||
                    desiredH != orig.ColorHeight)
                {
                    return new RawImage
                    {
                        ColorData = TextureScale.BlockingBilinear(
                            orig.ColorData, orig.ColorWidth, orig.ColorHeight,
                            null, desiredW, desiredH),
                        ColorWidth = desiredW,
                        ColorHeight = desiredH,
                        ColorAspect = orig.ColorAspect
                    };
                }
            }
            return orig;
        }
    }

} // namespace TiltBrush
