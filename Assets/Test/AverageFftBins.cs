using System.Linq;
using Lasp.Vfx;
using UnityEngine;

namespace Assets.Test
{
    public class AverageFftBins : MonoBehaviour

    {
        [SerializeField] [Range(0, 32)] float _inputGain = 1.0f;
        [SerializeField] [Range(1, 512)] int _fftBins = 11;
        [SerializeField] bool _holdAndFallDown = true;
        [SerializeField, Range(0, 1)] float _fallDownSpeed = 0.1f;

        public Lasp.AudioLevelTracker Target = null;

        private int _bands;
        private float _fall = 0;
        private float[] _fftOut;
        private GameObject[] _cubes;
        private int _sampleRate;
        private const int Resolution = 1024;

        private FftBuffer _fft;

        void Start()
        {
            Initialize();
        }

        void OnDestroy()
        {
            foreach(var obj in _cubes)
            {
                Destroy(obj);
            }

            _fft?.Dispose();
            _fft = null;
        }

        void Update()
        {
            if(_fft == null)
                _fft = new FftBuffer(Resolution);
        
            UpdateFftCubes();

            _fft.Push(Target.AudioDataSlice);
            _fft.Analyze();
        }

        void Initialize()
        {
            _sampleRate = Lasp.AudioSystem.InputDevices.FirstOrDefault().SampleRate;
            _bands = _fftBins;
            _fftOut = new float[_fftBins];
            _cubes = new GameObject[_fftBins];
            CreateFftCubes();
        }

        void CreateFftCubes()
        {
            var scale = 2.0f / _fftBins;
            for(var i = 0; i < _fftBins; i++)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var cubeWidth = 1 / (float)_fftBins;
                cube.transform.position = new Vector3(scale * i - 1 + cubeWidth, 0, 0);
                cube.transform.localScale = new Vector3(cubeWidth, 1, 1);
                _cubes[i] = cube;
            }
        }

        void UpdateFftCubes()
        {
            if(_bands != _fftBins)
            {
                OnDestroy();
                Initialize();
            }

            var gain = _inputGain / 10.0f;
            for(var i = 0; i < _fftBins; i++)
            {
                Debug.Log(_fft.Spectrum[i]);
                var input = Mathf.Clamp01(gain * _fft.Spectrum[i] * (3 * i + 1));
                var dt = Time.deltaTime;
                _fftOut[i] = input - _fall * dt;

                if(_holdAndFallDown)
                {
                    // Hold-and-fall-down animation.
                    _fall += Mathf.Pow(10, 1 + _fallDownSpeed * 2) * dt;
                    _fftOut[i] -= _fall * dt;

                    // Pull up by input.
                    if(_fftOut[i] < input)
                    {
                        _fftOut[i] = input;
                        _fall = 0;
                    }
                }
                else
                {
                    _fftOut[i] = input;
                }

                var height = Mathf.Clamp(_fftOut[i], 0, 1);
                var scale = _cubes[i].transform.localScale;
                scale.y = height;
                _cubes[i].transform.localScale = scale;
            }
        }
    }
}