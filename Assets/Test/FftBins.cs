using System;
using System.Linq;
using Lasp.Vfx;
using UnityEngine;
using UnityEngine.Video;

namespace Assets.Test
{
    public class FftBins : MonoBehaviour

    {
        [SerializeField] [Range(1, 256)] int _fftBins = 11;
        public Lasp.AudioLevelTracker Target = null;

        private int _bands;
        private float[] _normalizedLevel;
        private GameObject[] _cubes;
        private int _sampleRate;
        private const int Resolution = 1024;

        private FftBuffer _fft;

        void Start()
        {
            Initialize();
        }

        void Initialize()
        {
            _sampleRate = Lasp.AudioSystem.InputDevices.FirstOrDefault().SampleRate;
            _bands = _fftBins;
            _normalizedLevel = new float[_fftBins];
            _cubes = new GameObject[_fftBins];
            CreateFftCubes();
        }

        void Update()
        {
            if (_fft == null)
                _fft = new FftBuffer(Resolution);
            _fft.Push(Target.AudioDataSlice);
            _fft.Analyze();
            UpdateFftCubes();
        }

        void CreateFftCubes()
        {
            var scale = 2.0f / _fftBins;
            for (var i = 0; i < _fftBins; i++)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var cubeWidth = 1 / (float) _fftBins;
                cube.transform.position = new Vector3(scale * i - 1 + cubeWidth, 0, 0);
                cube.transform.localScale = new Vector3(cubeWidth, 0, 0);
                _cubes[i] = cube;
            }
        }

        void UpdateFftCubes()
        {
            // Reset if number of bins has changed
            if (_bands != _fftBins)
            {
                OnDestroy();
                Initialize();
                return;
            }

            var dt = Time.deltaTime;
            var fall = 0f;

            for (var i = 0; i < _fftBins; i++)
            {
                var input = float.IsNaN(_fft.Spectrum[i]) ? 0 : _fft.Spectrum[i];
                var normalizedInput = Mathf.Clamp01(Target.currentGain * input * (i + 1));
                _normalizedLevel[i] = normalizedInput - fall * dt;

                if (Target.smoothFall)
                {
                    // Hold-and-fall-down animation.
                    fall += Mathf.Pow(10, 1 + Target.fallSpeed * 2) * dt;
                    _normalizedLevel[i] -= fall * dt;

                    // Pull up by input.
                    if (_normalizedLevel[i] < normalizedInput)
                    {
                        _normalizedLevel[i] = normalizedInput;
                        fall = 0;
                    }
                }
                else
                {
                    _normalizedLevel[i] = normalizedInput;
                }

                var height = Mathf.Clamp(_normalizedLevel[i], 0, 1);
                var scale = _cubes[i].transform.localScale;
                scale.y = height;
                _cubes[i].transform.localScale = scale;
            }
        }

        void OnDestroy()
        {
            foreach (var obj in _cubes)
            {
                Destroy(obj);
            }

            _fft?.Dispose();
            _fft = null;
        }
    }
}