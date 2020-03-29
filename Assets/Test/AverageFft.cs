using System;
using Lasp;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Assets.Test
{
    public class AverageFft : IDisposable
    {
        public NativeArray<float> Averages => _output;
        private NativeArray<float> _output;
        private readonly FftBuffer _fftBuffer;
        private static float _sampleRate;

        public AverageFft(int numBins, int sampleRate)
        {
            _fftBuffer = new FftBuffer(1024);
            _output = new NativeArray<float>(numBins, Allocator.Persistent);
            _sampleRate = sampleRate;
        }

        public void CalculateAverages(NativeSlice<float> audioSlice, int numBins)
        {
            _fftBuffer.Push(audioSlice);
            _fftBuffer.Analyze();

            // Log based averaging, which closely resembles how humans perceive sound
            const int minBandwidth = 60;
            var nyq = _sampleRate / 2.0f;
            var octaves = 1;

            while ((nyq /= 2) > minBandwidth)
            {
                octaves++;
            }

            var bandsPerOctave = _output.Length / octaves;
            //var averages = new NativeArray<float>(_numBins, Allocator.Persistent);

            new AverageFftJob()
                {
                    Input = _fftBuffer.Spectrum, Averages = _output, Octaves = octaves, BandsPerOctaves = bandsPerOctave
                }
                .Run(_output.Length);
        }

        public void Dispose()
        {
            _output.Dispose();
            _fftBuffer.Dispose();
        }

        private static float CalculateAvg([ReadOnly] NativeArray<float> input, float lowFreq, float hiFreq)
        {
            var lowBound = FreqToIndex(input, lowFreq);
            var hiBound = FreqToIndex(input, hiFreq);
            float avg = 0;
            for (var i = lowBound; i <= hiBound; i++)
            {
                avg += input[i];
            }

            avg /= (float) hiBound - (float) lowBound + 1;
            return avg;
        }

        private static int FreqToIndex([ReadOnly] NativeArray<float> input, float freq)
        {
            var bandWidth = (2.0f / input.Length) * ((float) _sampleRate / 2.0f);

            // Special case: freq is lower than the bandwidth of spectrum[0]
            if (freq < bandWidth / 2) return 0;
            // Special case: freq is within the bandwidth of spectrum[spectrum.length - 1]
            if (freq > _sampleRate / 2 - bandWidth / 2) return input.Length - 1;

            var fraction = freq / (float) _sampleRate;
            var i = (int) (input.Length * fraction);
            return i;
        }

        private struct AverageFftJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float> Input;
            [ReadOnly] public int Octaves, BandsPerOctaves;
            [WriteOnly] public NativeArray<float> Averages;

            public void Execute(int index)
            {
                for (var i = 0; i < Octaves; i++)
                {
                    float lowFreq;
                    if (i == 0)
                    {
                        lowFreq = 0;
                    }
                    else
                    {
                        lowFreq = (_sampleRate / 2) / (float) math.pow(2, Octaves - i);
                    }

                    var hiFreq = (_sampleRate / 2) / (float) math.pow(2, Octaves - i - 1);
                    var freqStep = (hiFreq - lowFreq) / BandsPerOctaves;
                    var f = lowFreq;
                    for (var j = 0; j < BandsPerOctaves; j++)
                    {
                        var offset = (int) (j + i * BandsPerOctaves);
                        Averages[offset] = CalculateAvg(Input, f, f + freqStep);
                        f += freqStep;
                    }
                }
            }
        }
    }
}