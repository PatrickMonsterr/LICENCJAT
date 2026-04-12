using System.Collections.Generic;
using UnityEngine;

public class SpellDrawingSurface : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MeshRenderer visualPlaneRenderer;
    [SerializeField] private LineRenderer lineRenderer;

    [Header("Placement")]
    [SerializeField] private float distanceFromCamera = 1.1f;
    [SerializeField] private float verticalOffset = -0.1f;
    [SerializeField] private Vector2 planeSize = new Vector2(0.6f, 0.6f);

    [Header("Stroke")]
    [SerializeField] private float minPointDistance = 0.015f;
    [SerializeField] private int maxPoints = 256;
    [SerializeField] private float lineOffset = 0.002f;
    [SerializeField][Range(0f, 1f)] private float smoothing = 0.35f;
    [SerializeField][Range(0, 10)] private int visualSmoothingSubsteps = 4;

    private readonly List<Vector2> localStrokePoints = new List<Vector2>();
    private readonly List<Vector3> worldStrokePoints = new List<Vector3>();
    private bool visible;

    private void Awake()
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        if (visualPlaneRenderer == null)
            visualPlaneRenderer = GetComponent<MeshRenderer>();

        if (lineRenderer != null)
        {
            lineRenderer.useWorldSpace = true;
            lineRenderer.positionCount = 0;
            lineRenderer.widthMultiplier = 0.01f;

            if (lineRenderer.material == null)
                lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        }

        HideSurface();
    }

    public void ShowForCamera(Camera cam)
    {
        Vector3 targetPosition =
            cam.transform.position +
            cam.transform.forward * distanceFromCamera +
            cam.transform.up * verticalOffset;

        transform.position = targetPosition;
        transform.rotation = Quaternion.LookRotation(cam.transform.position - targetPosition, Vector3.up);

        visible = true;

        if (visualPlaneRenderer != null)
            visualPlaneRenderer.enabled = true;

        if (lineRenderer != null)
            lineRenderer.enabled = true;

        ClearStroke();
        Debug.Log("[SpellDrawingSurface] Surface shown");
    }

    public void HideSurface()
    {
        visible = false;

        if (visualPlaneRenderer != null)
            visualPlaneRenderer.enabled = false;

        if (lineRenderer != null)
            lineRenderer.enabled = false;

        ClearStroke();
        Debug.Log("[SpellDrawingSurface] Surface hidden");
    }

    public bool IsVisible()
    {
        return visible;
    }

    public void BeginStroke()
    {
        ClearStroke();

        if (lineRenderer != null)
            lineRenderer.enabled = true;
    }

    public bool TryAddPointFromRay(Vector3 rayOrigin, Vector3 rayDirection)
    {
        if (!visible)
            return false;

        Plane plane = new Plane(transform.forward, transform.position);
        Ray ray = new Ray(rayOrigin, rayDirection);

        if (!plane.Raycast(ray, out float enter))
            return false;

        Vector3 hitPoint = ray.GetPoint(enter);
        Vector3 local3D = transform.InverseTransformPoint(hitPoint);

        float halfWidth = planeSize.x * 0.5f;
        float halfHeight = planeSize.y * 0.5f;

        if (Mathf.Abs(local3D.x) > halfWidth || Mathf.Abs(local3D.y) > halfHeight)
            return false;

        Vector2 local2D = new Vector2(local3D.x, local3D.y);

        if (localStrokePoints.Count > 0)
        {
            Vector2 previous = localStrokePoints[localStrokePoints.Count - 1];
            local2D = Vector2.Lerp(local2D, previous, smoothing);

            float dist = Vector2.Distance(previous, local2D);
            if (dist < minPointDistance)
                return false;
        }

        if (localStrokePoints.Count >= maxPoints)
            return false;

        localStrokePoints.Add(local2D);

        Vector3 drawWorldPoint = transform.TransformPoint(new Vector3(local2D.x, local2D.y, 0f));
        drawWorldPoint += transform.forward * lineOffset;
        worldStrokePoints.Add(drawWorldPoint);

        RefreshLine();
        return true;
    }

    public List<Vector2> GetStrokePoints2D()
    {
        return new List<Vector2>(localStrokePoints);
    }

    private void ClearStroke()
    {
        localStrokePoints.Clear();
        worldStrokePoints.Clear();

        if (lineRenderer != null)
            lineRenderer.positionCount = 0;
    }

    private void RefreshLine()
    {
        if (lineRenderer == null)
            return;

        if (worldStrokePoints.Count < 2)
        {
            lineRenderer.positionCount = worldStrokePoints.Count;
            lineRenderer.SetPositions(worldStrokePoints.ToArray());
            return;
        }

        List<Vector3> smoothPoints = new List<Vector3>();

        for (int i = 0; i < worldStrokePoints.Count - 1; i++)
        {
            Vector3 a = worldStrokePoints[i];
            Vector3 b = worldStrokePoints[i + 1];

            smoothPoints.Add(a);

            for (int s = 1; s <= visualSmoothingSubsteps; s++)
            {
                float t = s / (float)(visualSmoothingSubsteps + 1);
                Vector3 p = Vector3.Lerp(a, b, t);
                smoothPoints.Add(p);
            }
        }

        smoothPoints.Add(worldStrokePoints[worldStrokePoints.Count - 1]);

        lineRenderer.positionCount = smoothPoints.Count;
        lineRenderer.SetPositions(smoothPoints.ToArray());
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Matrix4x4 old = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(planeSize.x, planeSize.y, 0.001f));
        Gizmos.matrix = old;
    }
}