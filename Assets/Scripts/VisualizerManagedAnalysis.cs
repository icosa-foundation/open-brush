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
using UnityEngine;

namespace TiltBrush
{
    /// Managed FFT implementation suitable for IL2CPP/AOT platforms.
    public class VisualizerManagedFft : VisualizerManager.Fft
    {
        private readonly int m_Size;
        private readonly int m_Channels;
        private readonly int[] m_BitReverse;
        private readonly double[] m_Real;
        private readonly double[] m_Imaginary;
        private readonly float[] m_Samples;

        public VisualizerManagedFft(int channels, int fftSize)
        {
            Debug.Assert(channels > 0);
            Debug.Assert(fftSize > 0 && (fftSize & (fftSize - 1)) == 0);

            m_Size = fftSize;
            m_Channels = channels;
            m_BitReverse = new int[m_Size];
            m_Real = new double[m_Size];
            m_Imaginary = new double[m_Size];
            m_Samples = new float[m_Size];

            int bits = 0;
            for (int n = m_Size; n > 1; n >>= 1)
            {
                ++bits;
            }

            for (int i = 0; i < m_Size; ++i)
            {
                int reversed = 0;
                int value = i;
                for (int bit = 0; bit < bits; ++bit)
                {
                    reversed = (reversed << 1) | (value & 1);
                    value >>= 1;
                }
                m_BitReverse[i] = reversed;
            }
        }

        public override void Add(float[] samples, int count)
        {
            int sampleCount = Mathf.Min(count / m_Channels, m_Size);
            int input = 0;
            for (int i = 0; i < sampleCount; ++i)
            {
                m_Samples[i] = samples[input];
                input += m_Channels;
            }
            for (int i = sampleCount; i < m_Size; ++i)
            {
                m_Samples[i] = 0.0f;
            }
        }

        public override void GetFftData(float[] resultBuffer)
        {
            Debug.Assert(resultBuffer.Length >= m_Size);

            for (int i = 0; i < m_Size; ++i)
            {
                int source = m_BitReverse[i];
                double window = 0.5 - 0.5 * Math.Cos(2.0 * Math.PI * source / (m_Size - 1));
                m_Real[i] = m_Samples[source] * window;
                m_Imaginary[i] = 0.0;
            }

            for (int length = 2; length <= m_Size; length <<= 1)
            {
                double angle = -2.0 * Math.PI / length;
                double wLengthReal = Math.Cos(angle);
                double wLengthImaginary = Math.Sin(angle);
                int halfLength = length >> 1;

                for (int i = 0; i < m_Size; i += length)
                {
                    double wReal = 1.0;
                    double wImaginary = 0.0;

                    for (int j = 0; j < halfLength; ++j)
                    {
                        int even = i + j;
                        int odd = even + halfLength;
                        double oddReal = m_Real[odd] * wReal - m_Imaginary[odd] * wImaginary;
                        double oddImaginary = m_Real[odd] * wImaginary + m_Imaginary[odd] * wReal;
                        double evenReal = m_Real[even];
                        double evenImaginary = m_Imaginary[even];

                        m_Real[even] = evenReal + oddReal;
                        m_Imaginary[even] = evenImaginary + oddImaginary;
                        m_Real[odd] = evenReal - oddReal;
                        m_Imaginary[odd] = evenImaginary - oddImaginary;

                        double nextReal = wReal * wLengthReal - wImaginary * wLengthImaginary;
                        wImaginary = wReal * wLengthImaginary + wImaginary * wLengthReal;
                        wReal = nextReal;
                    }
                }
            }

            int usefulBins = m_Size / 2;
            for (int i = 0; i < usefulBins; ++i)
            {
                double real = m_Real[i];
                double imaginary = m_Imaginary[i];
                resultBuffer[i] = (float)(Math.Sqrt(real * real + imaginary * imaginary) / usefulBins);
            }
            for (int i = usefulBins; i < m_Size; ++i)
            {
                resultBuffer[i] = 0.0f;
            }
        }
    }

    /// Managed biquad low/high-pass filter for platforms without CSCore.
    public class VisualizerManagedFilter : VisualizerManager.Filter
    {
        public enum FilterType
        {
            Low,
            High,
        }

        private readonly FilterType m_Type;
        private readonly int m_SampleRate;
        private const double kFrequencyChangeEpsilon = 1e-6;
        private double m_Frequency;
        private double m_B0;
        private double m_B1;
        private double m_B2;
        private double m_A1;
        private double m_A2;
        private double m_X1;
        private double m_X2;
        private double m_Y1;
        private double m_Y2;

        public VisualizerManagedFilter(FilterType type, int sampleRate, double frequency)
        {
            m_Type = type;
            m_SampleRate = Mathf.Max(2, sampleRate);
            Frequency = frequency;
        }

        public override void Process(float[] samples)
        {
            for (int i = 0; i < samples.Length; ++i)
            {
                double input = samples[i];
                double output = m_B0 * input + m_B1 * m_X1 + m_B2 * m_X2
                    - m_A1 * m_Y1 - m_A2 * m_Y2;
                m_X2 = m_X1;
                m_X1 = input;
                m_Y2 = m_Y1;
                m_Y1 = output;
                samples[i] = (float)output;
            }
        }

        public override double Frequency
        {
            get { return m_Frequency; }
            set
            {
                double nyquist = m_SampleRate * 0.5;
                double clampedFrequency = Math.Max(1.0, Math.Min(value, Math.Max(1.0, nyquist - 1.0)));
                if (Math.Abs(clampedFrequency - m_Frequency) <= kFrequencyChangeEpsilon)
                {
                    return;
                }

                m_Frequency = clampedFrequency;
                UpdateCoefficients();
            }
        }

        private void UpdateCoefficients()
        {
            const double q = 0.7071067811865476;
            double omega = 2.0 * Math.PI * m_Frequency / m_SampleRate;
            double sin = Math.Sin(omega);
            double cos = Math.Cos(omega);
            double alpha = sin / (2.0 * q);
            double a0 = 1.0 + alpha;

            if (m_Type == FilterType.Low)
            {
                m_B0 = (1.0 - cos) * 0.5 / a0;
                m_B1 = (1.0 - cos) / a0;
                m_B2 = m_B0;
            }
            else
            {
                m_B0 = (1.0 + cos) * 0.5 / a0;
                m_B1 = -(1.0 + cos) / a0;
                m_B2 = m_B0;
            }

            m_A1 = -2.0 * cos / a0;
            m_A2 = (1.0 - alpha) / a0;
        }
    }
}
