using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static class BulletHoleDecal
{
    private const int SegmentCount = 32;
    private const int InitialPoolSize = 32;
    private const int MaxPoolSize = 96;

    private static Mesh roundHoleMesh;
    private static Material roundHoleMaterial;
    private static Transform poolRoot;
    private static int nextReuseIndex;
    private static readonly List<SurfaceFollower> pooledRoundHoles = new List<SurfaceFollower>(MaxPoolSize);

    public static void Prewarm(int count = InitialPoolSize)
    {
        EnsurePoolRoot();

        int targetCount = Mathf.Clamp(count, 0, MaxPoolSize);
        while (pooledRoundHoles.Count < targetCount)
        {
            CreatePooledRoundHole();
        }
    }

    public static GameObject CreateRound(Vector3 hitPoint, Vector3 hitNormal, float diameter, float surfaceOffset, Transform parent, float lifetime)
    {
        Vector3 normal = hitNormal.sqrMagnitude > 0.0001f ? hitNormal.normalized : Vector3.forward;
        float safeDiameter = Mathf.Max(0.01f, diameter);
        float rollDegrees = Random.Range(0f, 360f);

        SurfaceFollower pooledHole = GetPooledRoundHole();
        pooledHole.InitializePooled(parent, hitPoint, normal, safeDiameter, surfaceOffset, rollDegrees, lifetime, poolRoot);

        return pooledHole.gameObject;
    }

    private static SurfaceFollower GetPooledRoundHole()
    {
        EnsurePoolRoot();

        for (int i = 0; i < pooledRoundHoles.Count; i++)
        {
            SurfaceFollower candidate = pooledRoundHoles[i];
            if (candidate != null && !candidate.gameObject.activeSelf)
            {
                return candidate;
            }
        }

        if (pooledRoundHoles.Count < MaxPoolSize)
        {
            return CreatePooledRoundHole();
        }

        SurfaceFollower reused = pooledRoundHoles[nextReuseIndex % pooledRoundHoles.Count];
        nextReuseIndex++;
        return reused;
    }

    private static void EnsurePoolRoot()
    {
        if (poolRoot != null)
        {
            return;
        }

        GameObject root = new GameObject("Bullet Hole Pool");
        root.hideFlags = HideFlags.HideInHierarchy;
        Object.DontDestroyOnLoad(root);
        poolRoot = root.transform;
    }

    private static SurfaceFollower CreatePooledRoundHole()
    {
        GameObject bulletHole = new GameObject("Round Bullet Hole");
        bulletHole.hideFlags = HideFlags.HideInHierarchy;
        bulletHole.transform.SetParent(poolRoot, false);

        MeshFilter meshFilter = bulletHole.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = GetRoundHoleMesh();

        MeshRenderer meshRenderer = bulletHole.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = GetRoundHoleMaterial();
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        SurfaceFollower follower = bulletHole.AddComponent<SurfaceFollower>();
        bulletHole.SetActive(false);
        pooledRoundHoles.Add(follower);
        return follower;
    }

    public static void AttachToSurface(GameObject decal, Transform surface, Vector3 hitPoint, Vector3 hitNormal, float diameter, float surfaceOffset, float rollDegrees)
    {
        if (decal == null)
        {
            return;
        }

        Vector3 normal = hitNormal.sqrMagnitude > 0.0001f ? hitNormal.normalized : Vector3.forward;
        float safeDiameter = Mathf.Max(0.01f, diameter);
        float safeOffset = Mathf.Max(0.001f, surfaceOffset);

        SetWorldTransform(decal.transform, hitPoint, normal, safeDiameter, safeOffset, rollDegrees);

        if (surface == null)
        {
            return;
        }

        SurfaceFollower follower = decal.GetComponent<SurfaceFollower>();
        if (follower == null)
        {
            follower = decal.AddComponent<SurfaceFollower>();
        }

        follower.Initialize(surface, hitPoint, normal, safeDiameter, safeOffset, rollDegrees);
    }

    private static void SetWorldTransform(Transform target, Vector3 hitPoint, Vector3 normal, float diameter, float surfaceOffset, float rollDegrees)
    {
        Quaternion rotation = Quaternion.LookRotation(normal) * Quaternion.AngleAxis(rollDegrees, Vector3.forward);
        target.SetPositionAndRotation(hitPoint + normal * surfaceOffset, rotation);
        target.localScale = Vector3.one * diameter;
    }

    private sealed class SurfaceFollower : MonoBehaviour
    {
        private Transform surface;
        private Transform poolRoot;
        private Vector3 localPoint;
        private Vector3 localNormal;
        private Vector3 worldPoint;
        private Vector3 worldNormal;
        private float diameter;
        private float surfaceOffset;
        private float rollDegrees;
        private float expireTime;
        private bool followsSurface;
        private bool hasLifetime;
        private bool isPooled;

        public void Initialize(Transform targetSurface, Vector3 hitPoint, Vector3 hitNormal, float decalDiameter, float decalSurfaceOffset, float decalRollDegrees)
        {
            isPooled = false;
            hasLifetime = false;
            poolRoot = null;
            ApplyState(targetSurface, hitPoint, hitNormal, decalDiameter, decalSurfaceOffset, decalRollDegrees);
        }

        public void InitializePooled(Transform targetSurface, Vector3 hitPoint, Vector3 hitNormal, float decalDiameter, float decalSurfaceOffset, float decalRollDegrees, float lifetime, Transform targetPoolRoot)
        {
            isPooled = true;
            poolRoot = targetPoolRoot;
            hasLifetime = lifetime > 0f;
            expireTime = hasLifetime ? Time.time + lifetime : 0f;
            gameObject.SetActive(true);
            ApplyState(targetSurface, hitPoint, hitNormal, decalDiameter, decalSurfaceOffset, decalRollDegrees);
        }

        private void ApplyState(Transform targetSurface, Vector3 hitPoint, Vector3 hitNormal, float decalDiameter, float decalSurfaceOffset, float decalRollDegrees)
        {
            surface = targetSurface;
            followsSurface = surface != null;
            worldPoint = hitPoint;
            worldNormal = hitNormal.sqrMagnitude > 0.0001f ? hitNormal.normalized : Vector3.forward;
            diameter = decalDiameter;
            surfaceOffset = decalSurfaceOffset;
            rollDegrees = decalRollDegrees;

            if (followsSurface)
            {
                localPoint = surface.InverseTransformPoint(hitPoint);
                localNormal = surface.InverseTransformDirection(worldNormal).normalized;
                if (localNormal.sqrMagnitude <= 0.0001f)
                {
                    localNormal = Vector3.forward;
                }
            }

            LateUpdate();
        }

        private void LateUpdate()
        {
            if (hasLifetime && Time.time >= expireTime)
            {
                Release();
                return;
            }

            if (followsSurface && surface == null)
            {
                Release();
                return;
            }

            Vector3 currentWorldPoint = worldPoint;
            Vector3 currentWorldNormal = worldNormal;
            if (followsSurface)
            {
                currentWorldPoint = surface.TransformPoint(localPoint);
                currentWorldNormal = surface.TransformDirection(localNormal);
                if (currentWorldNormal.sqrMagnitude <= 0.0001f)
                {
                    currentWorldNormal = Vector3.forward;
                }
            }

            SetWorldTransform(transform, currentWorldPoint, currentWorldNormal.normalized, diameter, surfaceOffset, rollDegrees);
        }

        private void Release()
        {
            surface = null;
            followsSurface = false;
            hasLifetime = false;

            if (!isPooled)
            {
                enabled = false;
                return;
            }

            transform.SetParent(poolRoot, false);
            gameObject.SetActive(false);
        }
    }

    private static Mesh GetRoundHoleMesh()
    {
        if (roundHoleMesh != null)
        {
            return roundHoleMesh;
        }

        Vector3[] vertices = new Vector3[SegmentCount + 1];
        Vector2[] uvs = new Vector2[SegmentCount + 1];
        int[] triangles = new int[SegmentCount * 3];

        vertices[0] = Vector3.zero;
        uvs[0] = new Vector2(0.5f, 0.5f);

        for (int i = 0; i < SegmentCount; i++)
        {
            float angle = Mathf.PI * 2f * i / SegmentCount;
            float radius = 0.5f;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
            uvs[i + 1] = new Vector2(vertices[i + 1].x + 0.5f, vertices[i + 1].y + 0.5f);
        }

        for (int i = 0; i < SegmentCount; i++)
        {
            int triangleIndex = i * 3;
            triangles[triangleIndex] = 0;
            triangles[triangleIndex + 1] = i + 1;
            triangles[triangleIndex + 2] = i == SegmentCount - 1 ? 1 : i + 2;
        }

        roundHoleMesh = new Mesh
        {
            name = "Round Bullet Hole Mesh",
            vertices = vertices,
            uv = uvs,
            triangles = triangles
        };
        roundHoleMesh.RecalculateNormals();
        roundHoleMesh.RecalculateBounds();

        return roundHoleMesh;
    }

    private static Material GetRoundHoleMaterial()
    {
        if (roundHoleMaterial != null)
        {
            return roundHoleMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        roundHoleMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave,
            color = new Color(0.015f, 0.012f, 0.01f, 1f)
        };

        if (roundHoleMaterial.HasProperty("_BaseColor"))
        {
            roundHoleMaterial.SetColor("_BaseColor", new Color(0.015f, 0.012f, 0.01f, 1f));
        }

        if (roundHoleMaterial.HasProperty("_Color"))
        {
            roundHoleMaterial.SetColor("_Color", new Color(0.015f, 0.012f, 0.01f, 1f));
        }

        if (roundHoleMaterial.HasProperty("_Cull"))
        {
            roundHoleMaterial.SetFloat("_Cull", 0f);
        }

        return roundHoleMaterial;
    }
}
