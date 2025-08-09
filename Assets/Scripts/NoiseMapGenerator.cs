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

    public static float[] GenerateNoiseMap(int width, int height, float numPointsPerAxis, float scale, Vector2 offset)
    {
        float minValue = float.MaxValue;
        float maxValue = float.MinValue;

        float[] noiseMap = new float[width * height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float sampleX = (x + offset.x) / scale;
                float sampleY = (y + offset.y) / scale;

                // Generate noise value using Perlin noise
                float noiseValue = Mathf.PerlinNoise(sampleX, sampleY);
                noiseMap[x + (y * height)] = noiseValue;

                // Track min and max values for normalization
                if (noiseValue < minValue)
                {
                    minValue = noiseValue;
                }
                if (noiseValue > maxValue)
                {
                    maxValue = noiseValue;
                }
            }
        }

        Debug.Log($"Min Value: {minValue}, Max Value: {maxValue}");

        return noiseMap;
    }
}
