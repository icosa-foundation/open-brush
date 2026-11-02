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
using NUnit.Framework;

namespace TiltBrush
{
    internal class TestVisualizerManagedAnalysis
    {
        private const int kFftSize = 512;
        private const int kSampleRate = 48000;

        [Test]
        public void ManagedFftDetectsSineNearExpectedBin()
        {
            const int expectedBin = 8;
            float[] samples = GenerateSine(expectedBin * kSampleRate / kFftSize);
            float[] fft = new float[kFftSize];
            var analyzer = new VisualizerManagedFft(1, kFftSize);

            analyzer.Add(samples, samples.Length);
            analyzer.GetFftData(fft);

            int peakBin = FindPeakBin(fft, 1, kFftSize / 2);
            Assert.That(peakBin, Is.InRange(expectedBin - 1, expectedBin + 1));
            Assert.That(fft[peakBin], Is.GreaterThan(fft[expectedBin + 8] * 5.0f));
        }

        [Test]
        public void ManagedFftKeepsSilenceAtZero()
        {
            float[] samples = new float[kFftSize];
            float[] fft = new float[kFftSize];
            var analyzer = new VisualizerManagedFft(1, kFftSize);

            analyzer.Add(samples, samples.Length);
            analyzer.GetFftData(fft);

            for (int i = 0; i < fft.Length; ++i)
            {
                Assert.That(fft[i], Is.EqualTo(0.0f).Within(1e-6f));
            }
        }

        [Test]
        public void ManagedFftUsesFirstChannelFromInterleavedSamples()
        {
            const int expectedBin = 8;
            float[] mono = GenerateSine(expectedBin * kSampleRate / kFftSize);
            float[] interleaved = new float[kFftSize * 2];
            for (int i = 0; i < mono.Length; ++i)
            {
                interleaved[i * 2] = mono[i];
                interleaved[i * 2 + 1] = 0.0f;
            }
            float[] fft = new float[kFftSize];
            var analyzer = new VisualizerManagedFft(2, kFftSize);

            analyzer.Add(interleaved, interleaved.Length);
            analyzer.GetFftData(fft);

            int peakBin = FindPeakBin(fft, 1, kFftSize / 2);
            Assert.That(peakBin, Is.InRange(expectedBin - 1, expectedBin + 1));
        }

        [Test]
        public void ManagedLowPassAttenuatesHighFrequencies()
        {
            float[] low = GenerateSine(120);
            float[] high = GenerateSine(4000);
            var lowPass = new VisualizerManagedFilter(VisualizerManagedFilter.FilterType.Low, kSampleRate, 500);

            lowPass.Process(low);
            lowPass = new VisualizerManagedFilter(VisualizerManagedFilter.FilterType.Low, kSampleRate, 500);
            lowPass.Process(high);

            Assert.That(Rms(low), Is.GreaterThan(Rms(high) * 2.0f));
        }

        [Test]
        public void ManagedHighPassAttenuatesLowFrequencies()
        {
            float[] low = GenerateSine(120);
            float[] high = GenerateSine(4000);
            var highPass = new VisualizerManagedFilter(VisualizerManagedFilter.FilterType.High, kSampleRate, 1000);

            highPass.Process(low);
            highPass = new VisualizerManagedFilter(VisualizerManagedFilter.FilterType.High, kSampleRate, 1000);
            highPass.Process(high);

            Assert.That(Rms(high), Is.GreaterThan(Rms(low) * 2.0f));
        }

        private static float[] GenerateSine(float frequency)
        {
            float[] samples = new float[kFftSize];
            for (int i = 0; i < samples.Length; ++i)
            {
                samples[i] = (float)Math.Sin(2.0 * Math.PI * frequency * i / kSampleRate);
            }
            return samples;
        }

        private static int FindPeakBin(float[] fft, int start, int end)
        {
            int peakIndex = start;
            float peak = fft[start];
            for (int i = start + 1; i < end; ++i)
            {
                if (fft[i] > peak)
                {
                    peak = fft[i];
                    peakIndex = i;
                }
            }
            return peakIndex;
        }

        private static float Rms(float[] samples)
        {
            double sumSquares = 0.0;
            for (int i = 0; i < samples.Length; ++i)
            {
                sumSquares += samples[i] * samples[i];
            }
            return (float)Math.Sqrt(sumSquares / samples.Length);
        }
    }
}
