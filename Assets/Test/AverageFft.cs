using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class AverageFft : MonoBehaviour
{
    public NativeArray<float> Averages => _averages;
    private NativeArray<float> _averages { get; }
    private NativeArray<float> _input { get; }
    private readonly int _numBins;

    private const float SampleRate = 48000;

    public AverageFft(int numBins)
    {
        _numBins = numBins;
        _averages = new NativeArray<float>(numBins, Allocator.Persistent);
    }

    public float[] CalculateAverages(NativeArray<float> input, int numBins)
    {
        _input.CopyFrom(input);
        // Log based averaging, which closely resembles how humans perceive sound
        const int minBandwidth = 60;
        var nyq = SampleRate / 2.0f;
        var octaves = 1;
        // Log averaging algorithm returns one less band
        //numBins++;

        while ((nyq /= 2) > minBandwidth)
        {
            octaves++;
        }

        var bandsPerOctave = (float) numBins / octaves;
        var averages = new float[numBins];
        for (var i = 0; i < octaves; i++)
        {
            float lowFreq;
            if (i == 0)
            {
                lowFreq = 0;
            }
            else
            {
                lowFreq = (SampleRate / 2) / (float) math.pow(2, octaves - i);
            }

            var hiFreq = (SampleRate / 2) / (float) math.pow(2, octaves - i - 1);
            var freqStep = (hiFreq - lowFreq) / bandsPerOctave;
            var f = lowFreq;
            for (var j = 0; j < bandsPerOctave; j++)
            {
                var offset = (int) (j + i * bandsPerOctave);
                averages[offset] = CalculateAvg(f, f + freqStep);
                f += freqStep;
            }
        }

        return averages;
    }

    private float CalculateAvg(float lowFreq, float hiFreq)
    {
        var lowBound = FreqToIndex(lowFreq);
        var hiBound = FreqToIndex(hiFreq);
        float avg = 0;
        for (var i = lowBound; i <= hiBound; i++)
        {
            avg += _input[i];
        }

        avg /= (float) hiBound - (float) lowBound + 1;
        return avg;
    }

    private int FreqToIndex(float freq)
    {
        var bandWidth = (2.0f / _input.Length) * ((float) SampleRate / 2.0f);

        // Special case: freq is lower than the bandwidth of spectrum[0]
        if (freq < bandWidth / 2) return 0;
        // Special case: freq is within the bandwidth of spectrum[spectrum.length - 1]
        if (freq > SampleRate / 2 - bandWidth / 2) return _input.Length - 1;

        var fraction = freq / (float) SampleRate;
        var i = (int)(_input.Length * fraction);
        return i;
    }

    private struct AverageFftJob : IJobParallelFor
    {
        public void Execute(int index)
        {
        }
    }
}