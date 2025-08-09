using UnityEngine;

[ExecuteInEditMode]
public class NoiseMapGenerator : MonoBehaviour
{
    // Singleton instance
    public static NoiseMapGenerator Instance { get; private set; }

    private void Awake()
    {
        // Ensure that there is only one instance of NoiseMapGenerator
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject); // Destroy duplicate instances
        }
    }

    public static float[] GenerateNoiseMap(int width, int height, float scale, Vector2 chunkOrigin, int octaves, float persistence, float lacunarity)
    {
        float minValue = float.MaxValue;
        float maxValue = float.MinValue;

        float[] noiseMap = new float[width * height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float amplitude = 1f;
                float frequency = 1f;
                float noiseValue = 0f;

                for (int octave = 0; octave < octaves; octave++)
                {
                    // Calculate the sample position based on frequency and amplitude
                    float sampleX = ((x + chunkOrigin.x)) / scale * frequency;
                    float sampleY = ((y + chunkOrigin.y)) / scale * frequency;

                    // Generate noise value using Perlin noise
                    noiseValue += (Mathf.PerlinNoise(sampleX, sampleY)) * amplitude;

                    // Update amplitude and frequency for the next octave
                    amplitude *= persistence;
                    frequency *= lacunarity;
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
                noiseMap[x + (y * height)] = noiseValue;
            }
        }

        // Normalize the noise values to the range [0, 1]
        for (int i = 0; i < noiseMap.Length; i++)
        {
            noiseMap[i] = Mathf.InverseLerp(-5f, 5f, noiseMap[i]);
        }

        Debug.Log($"Min Value: {minValue}, Max Value: {maxValue}");

        return noiseMap;
    }
}
