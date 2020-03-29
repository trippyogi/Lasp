using System.Linq;
using Lasp.Vfx;
using UnityEngine;

namespace Assets.Test
{
    public class FftBins : MonoBehaviour

    {
        [SerializeField] [Range(1, 256)] int _fftBins = 11;
        public Material CubeMaterial;
        public Lasp.AudioLevelTracker Target = null;

        private int _bands;
        private float[] _normalizedLevel;
        private float _fall;
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
                var mesh = cube.GetComponent<MeshRenderer>();
                mesh.material = CubeMaterial;
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

            for (var i = 0; i < _fftBins; i++)
            {
                var input = float.IsNaN(_fft.Spectrum[i]) ? 0 : _fft.Spectrum[i];
                var normalizedInput = Mathf.Clamp01(5 * input * Target.currentGain * (i + 1) / Target.dynamicRange);

                if (Target.smoothFall)
                {
                    // Hold-and-fall-down animation.
                    _fall += Mathf.Pow(10, 1 + Target.fallSpeed * 2) * dt;
                    _normalizedLevel[i] -= _fall * dt;

                    // Pull up by input.
                    if (_normalizedLevel[i] < normalizedInput)
                    {
                        _normalizedLevel[i] = normalizedInput;
                        _fall = 0;
                    }
                }
                else
                {
                    _normalizedLevel[i] = normalizedInput;
                }

                var scale = _cubes[i].transform.localScale;
                scale.y = _normalizedLevel[i];
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