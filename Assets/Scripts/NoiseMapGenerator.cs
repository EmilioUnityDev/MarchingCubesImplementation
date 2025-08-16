using UnityEngine;

[ExecuteAlways]
public class NoiseMapGenerator : MonoBehaviour
{
    // MeshGenerator reference to access the mesh generation system
    [SerializeField, Tooltip("Reference to the MeshGenerator component")]
    private MeshGenerator _meshGenerator;

    // Variables for noise generation
    [Space(10), Header("Noise Generation Variables")]
    [SerializeField, Tooltip("Number of octaves for the noise generation"), Range(1, 10)]
    private int _numOctaves = 4;
    [SerializeField, Tooltip("Persistence of the noise generation"), Range(0.0f, 1.0f)]
    private float _persistance = 0.5f;
    [SerializeField, Tooltip("Lacunarity of the noise generation"), Range(1.0f, 4.0f)]
    private float _lacunarity = 2.0f;
    [SerializeField, Tooltip("Zoom factor for the noise generation"), Range(0.1f, 2.0f)]
    private float _zoom = 1.0f;
    [SerializeField, Tooltip("Seed for the noise generation"), Range(0, 100)]
    private int _seed = 0;
    private int previousSeed = -1;

    private float _offsetX;
    private float _offsetY;

    void Init()
    {
        // Initialize offsets based on the seed
        Random.InitState(_seed);
        _offsetX = Random.Range(-10000f, 10000f);
        _offsetY = Random.Range(-10000f, 10000f);
        Debug.Log($"NoiseMapGenerator initialized with seed: {_seed}, offsets: ({_offsetX}, {_offsetY})");
    }

    void OnValidate()
    {
        if (previousSeed != _seed)
        {
            previousSeed = _seed;
            Init();
        }
        _meshGenerator.OnValidateNoiseGenerator();
    }

    public float[] GenerateNoiseMap(int numPointsWidth, int numPointsHeight, 
                                    float chunkSize, Vector2 chunkOrigin)
    {
        float minValue = float.MaxValue;
        float maxValue = float.MinValue;

        // Place the chunk origin correctly
        chunkOrigin = new Vector2(
            chunkOrigin.x / chunkSize,
            chunkOrigin.y / chunkSize
        );

        float[] noiseMap = new float[numPointsWidth * numPointsHeight];
        for (int x = 0; x < numPointsWidth; x++)
        {
            for (int y = 0; y < numPointsHeight; y++)
            {
                float amplitude = 1f;
                float frequency = 1f;
                float noiseValue = 0f;

                for (int octave = 0; octave < _numOctaves; octave++)
                {
                    // Calculate the sample position based on frequency and amplitude
                    float pointInChunkX = (float)x / (numPointsWidth - 1);
                    float pointInChunkY = (float)y / (numPointsHeight - 1);

                    // Calculate the sample coordinates in the noise space
                    float sampleX = (pointInChunkX + chunkOrigin.x + _offsetX) / _zoom * frequency;
                    float sampleY = (pointInChunkY + chunkOrigin.y + _offsetY) / _zoom * frequency;

                    // Generate noise value using Perlin noise
                    noiseValue += (Mathf.PerlinNoise(sampleX, sampleY)) * amplitude;

                    // Update amplitude and frequency for the next octave
                    amplitude *= _persistance;
                    frequency *= _lacunarity;
                }

                // Track min and max values for normalization
                if (noiseValue < minValue)
                {
                    minValue = noiseValue;
                }
                if (noiseValue > maxValue)
                {
                    maxValue = noiseValue;
                }

                // Store the noise value in the map
                noiseMap[x + (y * numPointsHeight)] = noiseValue;
            }
        }

        // Normalize the noise values to the range [0, 1]
        for (int i = 0; i < noiseMap.Length; i++)
        {
            noiseMap[i] = Mathf.InverseLerp(-5f, 5f, noiseMap[i]);
        }

        // Debug.Log($"Min Value: {minValue}, Max Value: {maxValue}");

        return noiseMap;
    }
}
