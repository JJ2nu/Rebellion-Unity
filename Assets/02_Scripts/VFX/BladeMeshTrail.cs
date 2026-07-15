using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class BladeMeshTrail : MonoBehaviour
{
    [SerializeField] private Transform bladeBase;
    [SerializeField] private Transform bladeTip;
    [SerializeField] private Material trailMaterial;
    [SerializeField] private bool emitting;
    [SerializeField, Min(0.01f)] private float lifetime = 0.22f;
    [SerializeField, Min(0.001f)] private float minVertexDistance = 0.015f;
    [SerializeField, Range(2, 96)] private int maxSegments = 32;
    [SerializeField] private AnimationCurve alphaOverLifetime = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    private readonly List<Sample> _samples = new();
    private Mesh _mesh;
    private MeshRenderer _meshRenderer;

    public Transform BladeBase
    {
        get => bladeBase;
        set => bladeBase = value;
    }

    public Transform BladeTip
    {
        get => bladeTip;
        set => bladeTip = value;
    }

    public bool Emitting
    {
        get => emitting;
        set => emitting = value;
    }

    private void Awake()
    {
        EnsureMesh();
    }

    private void OnEnable()
    {
        EnsureMesh();
        ResetTrail();
        SetVisible(emitting);
    }

    private void LateUpdate()
    {
        if (bladeBase == null || bladeTip == null)
        {
            ClearMesh();
            return;
        }

        var deltaTime = Time.deltaTime;
        for (var i = _samples.Count - 1; i >= 0; i--)
        {
            var sample = _samples[i];
            sample.age += deltaTime;
            if (sample.age >= lifetime)
            {
                _samples.RemoveAt(i);
            }
            else
            {
                _samples[i] = sample;
            }
        }

        if (emitting)
        {
            AddSampleIfNeeded(bladeBase.position, bladeTip.position);
        }

        RebuildMesh();
    }

    private void OnDisable()
    {
        ResetTrail();
    }

    public void ResetTrail()
    {
        _samples.Clear();
        ClearMesh();
    }

    public void SetVisible(bool isVisible)
    {
        EnsureMesh();
        _meshRenderer.enabled = isVisible;
        if (!isVisible)
        {
            ResetTrail();
        }
    }

    private void EnsureMesh()
    {
        if (_mesh == null)
        {
            _mesh = new Mesh { name = "Blade Mesh Trail" };
            _mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }

        _meshRenderer = GetComponent<MeshRenderer>();
        if (trailMaterial != null && _meshRenderer.sharedMaterial == null)
        {
            _meshRenderer.sharedMaterial = trailMaterial;
        }
    }

    private void AddSampleIfNeeded(Vector3 basePosition, Vector3 tipPosition)
    {
        if (_samples.Count > 0)
        {
            var last = _samples[_samples.Count - 1];
            var baseDistance = Vector3.Distance(last.basePosition, basePosition);
            var tipDistance = Vector3.Distance(last.tipPosition, tipPosition);
            if (Mathf.Max(baseDistance, tipDistance) < minVertexDistance)
            {
                return;
            }
        }

        _samples.Add(new Sample(basePosition, tipPosition, 0f));

        while (_samples.Count > maxSegments)
        {
            _samples.RemoveAt(0);
        }
    }

    private void RebuildMesh()
    {
        if (_mesh == null || _samples.Count < 2)
        {
            ClearMesh();
            return;
        }

        var vertexCount = _samples.Count * 2;
        var vertices = new Vector3[vertexCount];
        var uvs = new Vector2[vertexCount];
        var colors = new Color[vertexCount];
        var triangles = new int[(_samples.Count - 1) * 6];

        for (var i = 0; i < _samples.Count; i++)
        {
            var sample = _samples[i];
            var vertexIndex = i * 2;
            var normalizedAge = Mathf.Clamp01(sample.age / lifetime);
            var alpha = alphaOverLifetime.Evaluate(normalizedAge);
            var v = _samples.Count <= 1 ? 0f : i / (float)(_samples.Count - 1);

            vertices[vertexIndex] = transform.InverseTransformPoint(sample.basePosition);
            vertices[vertexIndex + 1] = transform.InverseTransformPoint(sample.tipPosition);
            uvs[vertexIndex] = new Vector2(0f, v);
            uvs[vertexIndex + 1] = new Vector2(1f, v);
            colors[vertexIndex] = new Color(1f, 1f, 1f, alpha);
            colors[vertexIndex + 1] = new Color(1f, 1f, 1f, alpha);
        }

        for (var i = 0; i < _samples.Count - 1; i++)
        {
            var vertexIndex = i * 2;
            var triangleIndex = i * 6;
            triangles[triangleIndex] = vertexIndex;
            triangles[triangleIndex + 1] = vertexIndex + 1;
            triangles[triangleIndex + 2] = vertexIndex + 2;
            triangles[triangleIndex + 3] = vertexIndex + 2;
            triangles[triangleIndex + 4] = vertexIndex + 1;
            triangles[triangleIndex + 5] = vertexIndex + 3;
        }

        _mesh.Clear();
        _mesh.vertices = vertices;
        _mesh.uv = uvs;
        _mesh.colors = colors;
        _mesh.triangles = triangles;
        _mesh.RecalculateBounds();
    }

    private void ClearMesh()
    {
        if (_mesh != null)
        {
            _mesh.Clear();
        }
    }

    private struct Sample
    {
        public Vector3 basePosition;
        public Vector3 tipPosition;
        public float age;

        public Sample(Vector3 basePosition, Vector3 tipPosition, float age)
        {
            this.basePosition = basePosition;
            this.tipPosition = tipPosition;
            this.age = age;
        }
    }
}
