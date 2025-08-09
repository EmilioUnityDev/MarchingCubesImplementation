using System;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

[ExecuteAlways]
public class MeshGenerator : MonoBehaviour
{
    public struct Triangle
    {
        #pragma warning disable 649 // disable unassigned variable warning
        public Vector3 a;
        public Vector3 b;
        public Vector3 c;

        public Vector3 this[int i]
        {
            get
            {
                switch (i)
                {
                    case 0:
                        return a;
                    case 1:
                        return b;
                    default:
                        return c;
                }
            }
        }
    }

    public class Chunk
    {
        public Vector3Int id;

        public Mesh mesh;
        public MeshRenderer meshRenderer;
        public MeshFilter meshFilter;
        public MeshCollider meshCollider;

        public GameObject _chunkObject;

        // Array of points in the chunk
        public Vector4[] points;

        public Chunk(GameObject chunkCollection, Vector3Int id, float chunkSize)
        {
            this.id = id;
            _chunkObject = new GameObject($"Chunk_{id.x}_{id.y}_{id.z}");
            _chunkObject.layer = LayerMask.NameToLayer("Terrain"); // Set the layer for the chunk

            // Set the parent of the chunk object to the chunk collection
            _chunkObject.transform.parent = chunkCollection.transform;
            Debug.Log($"Chunk created with ID: {id} at position: {_chunkObject.transform.position}");
            // Set the position of the chunk based on its ID and the chunk size
            _chunkObject.transform.localPosition = new Vector3(id.x * chunkSize, id.y * chunkSize, id.z * chunkSize);
            Debug.Log($"Chunk {id} LocalPosition set to: {_chunkObject.transform.localPosition} and WorldPosition: {_chunkObject.transform.position}");

            meshFilter = _chunkObject.AddComponent<MeshFilter>();
            meshRenderer = _chunkObject.AddComponent<MeshRenderer>();

            // Add a mesh collider to the chunk object
            meshCollider = _chunkObject.AddComponent<MeshCollider>();
            meshCollider.providesContacts = true; // Enable contact generation for the mesh collider

            // Add rigidbody for physics interactions and freeze position and rotation
            Rigidbody rigidbody = _chunkObject.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true; // Prevent physics interactions
            rigidbody.constraints = RigidbodyConstraints.FreezePosition | RigidbodyConstraints.FreezeRotation;
            rigidbody.useGravity = false; // Disable gravity for the chunk
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // Use continuous collision detection

            mesh = new Mesh();
            meshFilter.mesh = mesh;
        }

        public void PlaceChunkInWorld(float chunkSize)
        {
            // Set the position of the chunk in the world based on its ID and the chunk size
            _chunkObject.transform.localPosition = new Vector3(id.x * chunkSize, id.y * chunkSize, id.z * chunkSize);
        }

        public void SetPoints(Vector4[] points)
        {
            this.points = points;
        }

        public Vector4[] GetPoints()
        {
            return points;
        }
    }

    // Variables for mesh generation
    [Space(10), Header("Mesh Generation Variables")]
    [SerializeField, Tooltip("Size of the mesh in chunks")]
    public Vector3Int _numChunks;
    [SerializeField, Tooltip("Size of each chunk")]
    public float _chunkSize;
    [SerializeField, Tooltip("Number of points of each axis"), Range(2, 50)]
    private int _numPointsPerAxis;
    [SerializeField, Tooltip("Collection of chunks")]
    private GameObject _chunkCollection;
    [SerializeField, Tooltip("Isolevel for the isosurface"), Range(0f, 1f)]
    private float _isoLevel = 0.5f;
    [SerializeField, Tooltip("Terrain material")]
    private Material _terrainMaterial;

    // Variables for terraforming
    [Space(10), Header("Terraforming Variables")]
    [SerializeField, Tooltip("Radius of the terraforming effect"), Range(0.1f, 1.0f)]
    private float _radiusTerraform = 0.3f;

    // Array of chunks
    private Chunk[,,] _chunks;

    // Variables for compute shaders
    [Space(10), Header("Compute Shaders")]
    [SerializeField, Tooltip("Marching Cubes Shader")]
    private ComputeShader _marchingCubesCS;
    [SerializeField, Tooltip("Random Noise Shader")]
    private ComputeShader _randomNoiseCS;
    [SerializeField, Tooltip("Smooth Top Noise Shader")]
    private ComputeShader _smoothTopNoiseCS;
    [SerializeField, Tooltip("Terraform Shader")]
    private ComputeShader _terraformCS;

    // Compute shader variables
    private ComputeBuffer _pointsBuffer;
    private ComputeBuffer _trianglesBuffer;

    // Debug options
    [SerializeField, Tooltip("Apply changes to the mesh immediately"), Header("Debug Options"), Space()]
    private bool _autoApplyChanges;
    private bool _changesApplied = true;
    [SerializeField, Tooltip("Render points as spheres in the scene view")]
    private bool _renderPoints = false;

    // Singleton instance
    private static MeshGenerator _instance;
    public static MeshGenerator Instance
    {
        get
        {
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Debug.LogWarning("MeshGenerator: Another instance already exists. Destroying this instance.");
            Destroy(gameObject);
            return;
        }
    }

    void OnValidate()
    {
        _changesApplied = false;
        Debug.Log("MeshGenerator: OnValidate called, asking for update.");
    }

    void Update()
    {
        if (_autoApplyChanges && !_changesApplied)
        {
            _changesApplied = true;
            DestroyAllSpheres(); // Clear existing spheres if any
            GenerateMesh();
        }
    }

    public void GenerateMesh()
    {
        if (!Application.isPlaying)
        {
            // Clear existing chunks if in edit mode
            foreach (var child in _chunkCollection.GetComponentsInChildren<Transform>())
            {
                if (child != _chunkCollection.transform) // Avoid destroying the parent object
                    DestroyImmediate(child.gameObject, false);
            }
        }
        else
        {
            // Clear existing chunks if in play mode
            foreach (var child in _chunkCollection.GetComponentsInChildren<Transform>())
            {
                if (child != _chunkCollection.transform) // Avoid destroying the parent object
                    Destroy(child.gameObject);
            }
        }

        _chunks = new Chunk[_numChunks.x, _numChunks.y, _numChunks.z];

        for (int x = 0; x < _numChunks.x; x++)
        {
            for (int y = 0; y < _numChunks.y; y++)
            {
                for (int z = 0; z < _numChunks.z; z++)
                {
                    float time = Time.realtimeSinceStartup;
                    Chunk chunk = new(_chunkCollection, new Vector3Int(x, y, z), _chunkSize);
                    GenerateChunk(chunk);
                    _chunks[x, y, z] = chunk;
                    //chunk.PlaceChunkInWorld(_chunkSize);

                    Debug.Log($"Chunk {chunk.id} generated in {Time.realtimeSinceStartup - time} seconds.");
                }
            }
        }
    }

    public void GenerateChunk(Chunk chunk)
    {
        // CHUNK GENERATION PART (CREATION OF POINTS)

        // Calculate the size of the chunk in local space
        Vector3 chunkPosition = new(chunk.id.x * _chunkSize, chunk.id.y * _chunkSize, chunk.id.z * _chunkSize);

        // Create a grid of points within the chunk
        Vector4[] points = new Vector4[_numPointsPerAxis * _numPointsPerAxis * _numPointsPerAxis];
        for (int x = 0; x < _numPointsPerAxis; x++)
        {
            for (int y = 0; y < _numPointsPerAxis; y++)
            {
                for (int z = 0; z < _numPointsPerAxis; z++)
                {
                    int index = x + (y * _numPointsPerAxis) + (z * _numPointsPerAxis * _numPointsPerAxis);
                    points[index] = new Vector4(
                        ((float)x / (_numPointsPerAxis - 1)) * _chunkSize,
                        ((float)y / (_numPointsPerAxis - 1)) * _chunkSize,
                        ((float)z / (_numPointsPerAxis - 1)) * _chunkSize,
                        0.0f // Initialize the 4th component to 0, it will be modified later by the noise shader
                    );
                }
            }
        }

        // Create a compute buffer for the points
        if (_pointsBuffer != null)
            _pointsBuffer.Release();

        // Initialize the compute buffer with the points data
        _pointsBuffer = new ComputeBuffer(points.Length, sizeof(float) * 4);
        _pointsBuffer.SetData(points);

        // Dispatch the random noise compute shader to modify the points
        //int kernelHandle = _randomNoiseCS.FindKernel("Noise");
        //_randomNoiseCS.SetBuffer(kernelHandle, "points", _pointsBuffer);
        //_randomNoiseCS.SetInt("numPointsPerAxis", _numPointsPerAxis);
        //_randomNoiseCS.SetFloat("chunkSize", _chunkSize);
        //_randomNoiseCS.SetFloats("chunkOrigin", chunk._chunkObject.transform.position.x,
        //                                        chunk._chunkObject.transform.position.y,
        //                                        chunk._chunkObject.transform.position.z);
        //int numGroupThreads = Mathf.CeilToInt(_numPointsPerAxis / 8f);
        //_randomNoiseCS.Dispatch(kernelHandle, numGroupThreads, numGroupThreads, numGroupThreads);

        // Create a point noise buffer
        float[] pointsNoiseData = NoiseMapGenerator.GenerateNoiseMap(_numPointsPerAxis, _numPointsPerAxis, _numPointsPerAxis, _chunkSize, new Vector2(chunk.id.x * (_numPointsPerAxis - 1), chunk.id.z * (_numPointsPerAxis - 1)));
        ComputeBuffer pointsNoise = new ComputeBuffer(_numPointsPerAxis * _numPointsPerAxis, sizeof(float));
        pointsNoise.SetData(pointsNoiseData);

        int kernelHandle = _smoothTopNoiseCS.FindKernel("SmoothTopNoise");
        _smoothTopNoiseCS.SetBuffer(kernelHandle, "points", _pointsBuffer);
        _smoothTopNoiseCS.SetBuffer(kernelHandle, "pointsNoise", pointsNoise);
        _smoothTopNoiseCS.SetInt("numPointsPerAxis", _numPointsPerAxis);
        _smoothTopNoiseCS.SetFloat("chunkSize", _chunkSize);
        _smoothTopNoiseCS.SetBool("isTopLayer", chunk.id.y == _numChunks.y - 1); // Check if it's the top layer
        int numGroupThreads = Mathf.CeilToInt(_numPointsPerAxis / 8f);
        _smoothTopNoiseCS.Dispatch(kernelHandle, numGroupThreads, numGroupThreads, numGroupThreads);

        // Obtain the processed points from the compute buffer and release the buffer
        Vector4[] processedPoints = new Vector4[points.Length];
        _pointsBuffer.GetData(processedPoints);

        chunk.SetPoints(processedPoints);

        CreateSpheres(chunk); // Create spheres for visualization if needed

        // POLYGONIZE PART (MARCHING CUBES ALGORITHM)
        Polygonize(chunk);

        // Clean up the points buffer
        if (_pointsBuffer != null)
        {
            _pointsBuffer.Release();
            _pointsBuffer = null;
        }
    }

    private void Polygonize(Chunk chunk)
    {
        // POLYGONIZE PART (MARCHING CUBES ALGORITHM)

        if (_trianglesBuffer != null)
            _trianglesBuffer.Release();

        // Create a compute buffer for the triangles
        int numVoxelsPerAxis = _numPointsPerAxis - 1;
        int numVoxels = numVoxelsPerAxis * numVoxelsPerAxis * numVoxelsPerAxis;
        int maxTriangleCount = numVoxels * 5;
        _trianglesBuffer = new ComputeBuffer(maxTriangleCount, sizeof(float) * 3 * 3, ComputeBufferType.Append); // 3 vertices per triangle, 3 floats per vertex
        _trianglesBuffer.SetCounterValue(0);

        // Set the compute shader parameters
        int kernelHandle = _marchingCubesCS.FindKernel("MarchingCubes");
        _marchingCubesCS.SetBuffer(kernelHandle, "_pointCloudData", _pointsBuffer);
        _marchingCubesCS.SetBuffer(kernelHandle, "_triangles", _trianglesBuffer);
        _marchingCubesCS.SetInt("_numPointsPerAxis", _numPointsPerAxis);
        _marchingCubesCS.SetFloat("_isoLevel", _isoLevel); // Threshold value for the isosurface

        // Dispatch the compute shader
        int threadGroups = Mathf.CeilToInt(numVoxelsPerAxis / 8f);
        Debug.Log($"Dispatching Marching Cubes with {threadGroups} thread groups.");
        _marchingCubesCS.Dispatch(kernelHandle, threadGroups, threadGroups, threadGroups);

        // Retrieve the triangles from the compute buffer
        Triangle[] triangles = new Triangle[maxTriangleCount];

        // Antes de GetData:
        ComputeBuffer countBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
        ComputeBuffer.CopyCount(_trianglesBuffer, countBuffer, 0);

        int[] triCountArray = { 0 };
        countBuffer.GetData(triCountArray);

        int triangleCount = triCountArray[0];
        countBuffer.Release();

        _trianglesBuffer.GetData(triangles, 0, 0, triangleCount);

        // Create the mesh from the triangles
        Vector3[] vertices = new Vector3[triangleCount * 3];
        int[] indices = new int[triangleCount * 3];
        for (int i = 0; i < triangleCount; i++)
        {
            vertices[i * 3] = triangles[i][0];
            vertices[i * 3 + 1] = triangles[i][1];
            vertices[i * 3 + 2] = triangles[i][2];
            indices[i * 3] = i * 3;
            indices[i * 3 + 1] = i * 3 + 1;
            indices[i * 3 + 2] = i * 3 + 2;
        }

        // Assign the vertices and indices to the mesh
        chunk.mesh.Clear();
        chunk._chunkObject.transform.position = new Vector3(0,0,0); // Reset position to avoid offset issues

        chunk.mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        chunk.mesh.vertices = vertices;
        chunk.mesh.triangles = indices;
        chunk.mesh.RecalculateNormals();
        chunk.meshFilter.mesh = chunk.mesh;
        chunk.meshCollider.sharedMesh = chunk.mesh; // Update the mesh collider with the new mesh

        // Place the chunk in the world
        chunk.PlaceChunkInWorld(_chunkSize);

        Renderer _renderer = chunk.meshRenderer;
        _renderer.material = _terrainMaterial;

        // Clean up the triangles buffer
        if (_trianglesBuffer != null)
        {
            _trianglesBuffer.Release();
            _trianglesBuffer = null;
        }
    }

    public void Terraform(float type, Vector3 hit)
    {
        float time = Time.realtimeSinceStartup;

        DestroyAllSpheres(); // Clear existing spheres if any

        // Obtain the chunk ID based on the hit position
        Vector3Int chunkId = new Vector3Int(
            Mathf.FloorToInt((hit.x - _chunkCollection.transform.position.x) / _chunkSize),
            Mathf.FloorToInt((hit.y - _chunkCollection.transform.position.y) / _chunkSize),
            Mathf.FloorToInt((hit.z - _chunkCollection.transform.position.z) / _chunkSize)
        );

        //Vector3Int chunkId = new Vector3Int(
        //    Mathf.FloorToInt((hit.x) / _chunkSize),
        //    Mathf.FloorToInt((hit.y) / _chunkSize),
        //    Mathf.FloorToInt((hit.z) / _chunkSize)
        //);

        Debug.Log($"Terraforming chunk {chunkId} with type {type} at position {hit}.");
        // Create a sphere at the hit position
        //GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        //sphere.transform.position = hit;
        //sphere.transform.localScale = Vector3.one * _radiusTerraform * 2; // Scale the sphere based on the terraform radius

        // Check if the chunk exists
        if (_chunks[chunkId.x, chunkId.y, chunkId.z] == null)
        {
            Debug.LogWarning($"Chunk {chunkId} does not exist. Cannot terraform.");
            return;
        }

        Chunk chunk = _chunks[chunkId.x, chunkId.y, chunkId.z];

        if (_pointsBuffer != null)
            _pointsBuffer.Release();

        // Initialize the compute buffer with the points data
        _pointsBuffer = new ComputeBuffer(_numPointsPerAxis * _numPointsPerAxis * _numPointsPerAxis, sizeof(float) * 4);
        _pointsBuffer.SetData(chunk.GetPoints());

        // Set the compute shader parameters for terraforming
        int kernelHandle = _terraformCS.FindKernel("Terraform");
        _terraformCS.SetBuffer(kernelHandle, "points", _pointsBuffer);
        _terraformCS.SetVector("hit", hit);
        _terraformCS.SetFloats("chunkOrigin", chunk._chunkObject.transform.position.x,
                                              chunk._chunkObject.transform.position.y,
                                              chunk._chunkObject.transform.position.z);
        _terraformCS.SetInt("numPointsPerAxis", _numPointsPerAxis);
        _terraformCS.SetFloat("chunkSize", _chunkSize);
        _terraformCS.SetFloat("radiusTerraform", _radiusTerraform);
        _terraformCS.SetBool("actionType", type == 1.0f); // If type is 1 -> create terrain, else destroy terrain

        // Dispatch the compute shader for terraforming
        int numGroupThreads = Mathf.CeilToInt(_numPointsPerAxis / 8f);
        _terraformCS.Dispatch(kernelHandle, numGroupThreads, numGroupThreads, numGroupThreads);

        // Obtain the processed points from the compute buffer
        Vector4[] processedPoints = new Vector4[_numPointsPerAxis * _numPointsPerAxis * _numPointsPerAxis];
        _pointsBuffer.GetData(processedPoints);
        chunk.SetPoints(processedPoints);

        // Create spheres for visualization if needed
        CreateSpheres(chunk);

        // Recreate the mesh with the updated points
        Polygonize(chunk);

        // Clean up the points buffer
        if (_pointsBuffer != null)
        {
            _pointsBuffer.Release();
            _pointsBuffer = null;
        }

        Debug.Log($"Terraforming completed in {Time.realtimeSinceStartup - time} seconds for chunk {chunk.id}.");
    }

    private void CreateSpheres(Chunk chunk)
    {
        // Obtain the processed points from the chunk
        Vector4[] processedPoints = chunk.GetPoints();

        // Move the chunk object to the world origin
        chunk._chunkObject.transform.position = Vector3.zero;

        if (_renderPoints)
        {
            // Show the points as spheres in the scene view, assigning a gradient color based on the point's 4th value
            for (int i = 0; i < processedPoints.Length; i++)
            {
                // Obtain point data
                Vector3 pointPosition = new Vector3(processedPoints[i].x, processedPoints[i].y, processedPoints[i].z);
                float value = processedPoints[i].w;

                // Create a sphere at the point position
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.tag = "TerraformSphere"; // Tag the sphere for easy identification
                sphere.transform.position = pointPosition;
                sphere.transform.localScale = Vector3.one * 0.1f; // Scale down the sphere
                sphere.transform.parent = chunk.meshFilter.transform; // Set the sphere as a child of the chunk object

                // Assign a color to the sphere based on the value
                Renderer renderer = sphere.GetComponent<Renderer>();
                Color color = Color.Lerp(Color.blue, Color.red, value);
                renderer.material.color = color;
            }
        }

        chunk.PlaceChunkInWorld(_chunkSize); // Place the chunk in the world after creating spheres
    }

    private void DestroyAllSpheres()
    {
        // Destroy all spheres in the scene
        GameObject[] spheres = GameObject.FindGameObjectsWithTag("TerraformSphere");
        if (!Application.isPlaying)
        {
            // Clear existing chunks if in edit mode
            foreach (GameObject sphere in spheres)
            {
                DestroyImmediate(sphere);
            }
        }
        else
        {
            // Clear existing chunks if in play mode
            foreach (GameObject sphere in spheres)
            {
                Destroy(sphere);
            }
        }
    }
}
