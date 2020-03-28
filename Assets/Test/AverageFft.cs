using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Assets.Test
{
    public class AverageFft : MonoBehaviour
    {
        private NativeArray<float> Input { get; }
        private readonly int _numBins;
        private const float SampleRate = 48000;

        public AverageFft(NativeArray<float> input, int numBins)
        {
            Input = input;
            _numBins = numBins;
        }

        public NativeArray<float> CalculateAverages([ReadOnly] NativeArray<float> input)
        {
            Input.CopyFrom(input);
            // Log based averaging, which closely resembles how humans perceive sound
            const int minBandwidth = 60;
            var nyq = SampleRate / 2.0f;
            var octaves = 1;
            //// Log averaging algorithm returns one less band
            //numBins++;

            while ((nyq /= 2) > minBandwidth)
            {
                octaves++;
            }

            var bandsPerOctave = _numBins / octaves;
            var averages = new NativeArray<float>(_numBins, Allocator.Persistent);

            new AverageFftJob()
                {Input = input, Averages = averages, Octaves = octaves, BandsPerOctaves = bandsPerOctave}.Run(_numBins);

            return averages;
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
            var bandWidth = (2.0f / input.Length) * ((float) SampleRate / 2.0f);

            // Special case: freq is lower than the bandwidth of spectrum[0]
            if (freq < bandWidth / 2) return 0;
            // Special case: freq is within the bandwidth of spectrum[spectrum.length - 1]
            if (freq > SampleRate / 2 - bandWidth / 2) return input.Length - 1;

            var fraction = freq / (float) SampleRate;
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
                        lowFreq = (SampleRate / 2) / (float) math.pow(2, Octaves - i);
                    }

                    var hiFreq = (SampleRate / 2) / (float) math.pow(2, Octaves - i - 1);
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