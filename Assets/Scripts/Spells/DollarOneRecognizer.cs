using System.Collections.Generic;
using UnityEngine;

public class DollarOneRecognizer
{
    public class Result
    {
        public string Name;
        public float Score;
        public bool Success;
    }

    private class Template
    {
        public string Name;
        public List<Vector2> Points;

        public Template(string name, List<Vector2> points)
        {
            Name = name;
            Points = points;
        }
    }

    private readonly List<Template> templates = new List<Template>();

    private const int NumPoints = 64;
    private const float SquareSize = 1f;
    private const float HalfDiagonal = 0.70710678f; // sqrt(2) / 2 for 1x1 square
    private const float AngleRange = 45f;
    private const float AnglePrecision = 2f;
    private const float Phi = 0.61803398875f;

    public void AddTemplate(string name, List<Vector2> rawPoints)
    {
        if (rawPoints == null || rawPoints.Count < 2)
            return;

        List<Vector2> normalized = Normalize(rawPoints);
        templates.Add(new Template(name, normalized));
    }

    public Result Recognize(List<Vector2> rawPoints)
    {
        Result result = new Result
        {
            Name = "",
            Score = 0f,
            Success = false
        };

        if (rawPoints == null || rawPoints.Count < 2 || templates.Count == 0)
            return result;

        List<Vector2> points = Normalize(rawPoints);

        float bestDistance = float.MaxValue;
        string bestName = "";

        foreach (Template template in templates)
        {
            float distance = DistanceAtBestAngle(
                points,
                template.Points,
                -AngleRange * Mathf.Deg2Rad,
                AngleRange * Mathf.Deg2Rad,
                AnglePrecision * Mathf.Deg2Rad
            );

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestName = template.Name;
            }
        }

        float score = 1f - (bestDistance / HalfDiagonal);
        score = Mathf.Clamp01(score);

        result.Name = bestName;
        result.Score = score;
        result.Success = true;
        return result;
    }

    private List<Vector2> Normalize(List<Vector2> points)
    {
        List<Vector2> newPoints = Resample(points, NumPoints);
        float angle = IndicativeAngle(newPoints);
        newPoints = RotateBy(newPoints, -angle);
        newPoints = ScaleToSquare(newPoints, SquareSize);
        newPoints = TranslateToOrigin(newPoints);
        return newPoints;
    }

    private List<Vector2> Resample(List<Vector2> points, int n)
    {
        List<Vector2> working = new List<Vector2>(points);
        float pathLength = PathLength(working);
        float interval = pathLength / (n - 1);
        float distanceSoFar = 0f;

        List<Vector2> newPoints = new List<Vector2>();
        newPoints.Add(working[0]);

        for (int i = 1; i < working.Count; i++)
        {
            float d = Vector2.Distance(working[i - 1], working[i]);

            if (d <= Mathf.Epsilon)
                continue;

            while ((distanceSoFar + d) >= interval)
            {
                float t = (interval - distanceSoFar) / d;
                Vector2 q = Vector2.Lerp(working[i - 1], working[i], t);
                newPoints.Add(q);
                working.Insert(i, q);
                d = Vector2.Distance(working[i - 1], working[i]);
                distanceSoFar = 0f;

                if (newPoints.Count == n)
                    return newPoints;
            }

            distanceSoFar += d;
        }

        while (newPoints.Count < n)
            newPoints.Add(working[working.Count - 1]);

        return newPoints;
    }

    private float IndicativeAngle(List<Vector2> points)
    {
        Vector2 centroid = Centroid(points);
        return Mathf.Atan2(centroid.y - points[0].y, centroid.x - points[0].x);
    }

    private List<Vector2> RotateBy(List<Vector2> points, float radians)
    {
        Vector2 centroid = Centroid(points);
        List<Vector2> newPoints = new List<Vector2>(points.Count);

        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);

        foreach (Vector2 p in points)
        {
            float dx = p.x - centroid.x;
            float dy = p.y - centroid.y;

            float x = dx * cos - dy * sin + centroid.x;
            float y = dx * sin + dy * cos + centroid.y;

            newPoints.Add(new Vector2(x, y));
        }

        return newPoints;
    }

    private List<Vector2> ScaleToSquare(List<Vector2> points, float size)
    {
        Rect box = BoundingBox(points);
        List<Vector2> newPoints = new List<Vector2>(points.Count);

        float width = Mathf.Max(box.width, 0.0001f);
        float height = Mathf.Max(box.height, 0.0001f);

        foreach (Vector2 p in points)
        {
            float x = p.x * (size / width);
            float y = p.y * (size / height);
            newPoints.Add(new Vector2(x, y));
        }

        return newPoints;
    }

    private List<Vector2> TranslateToOrigin(List<Vector2> points)
    {
        Vector2 centroid = Centroid(points);
        List<Vector2> newPoints = new List<Vector2>(points.Count);

        foreach (Vector2 p in points)
            newPoints.Add(p - centroid);

        return newPoints;
    }

    private float DistanceAtBestAngle(List<Vector2> points, List<Vector2> templatePoints, float a, float b, float threshold)
    {
        float x1 = Phi * a + (1f - Phi) * b;
        float f1 = DistanceAtAngle(points, templatePoints, x1);
        float x2 = (1f - Phi) * a + Phi * b;
        float f2 = DistanceAtAngle(points, templatePoints, x2);

        while (Mathf.Abs(b - a) > threshold)
        {
            if (f1 < f2)
            {
                b = x2;
                x2 = x1;
                f2 = f1;
                x1 = Phi * a + (1f - Phi) * b;
                f1 = DistanceAtAngle(points, templatePoints, x1);
            }
            else
            {
                a = x1;
                x1 = x2;
                f1 = f2;
                x2 = (1f - Phi) * a + Phi * b;
                f2 = DistanceAtAngle(points, templatePoints, x2);
            }
        }

        return Mathf.Min(f1, f2);
    }

    private float DistanceAtAngle(List<Vector2> points, List<Vector2> templatePoints, float radians)
    {
        List<Vector2> rotated = RotateBy(points, radians);
        return PathDistance(rotated, templatePoints);
    }

    private float PathDistance(List<Vector2> a, List<Vector2> b)
    {
        float distance = 0f;

        for (int i = 0; i < a.Count; i++)
            distance += Vector2.Distance(a[i], b[i]);

        return distance / a.Count;
    }

    private float PathLength(List<Vector2> points)
    {
        float length = 0f;

        for (int i = 1; i < points.Count; i++)
            length += Vector2.Distance(points[i - 1], points[i]);

        return length;
    }

    private Vector2 Centroid(List<Vector2> points)
    {
        Vector2 c = Vector2.zero;

        foreach (Vector2 p in points)
            c += p;

        return c / points.Count;
    }

    private Rect BoundingBox(List<Vector2> points)
    {
        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;

        foreach (Vector2 p in points)
        {
            minX = Mathf.Min(minX, p.x);
            minY = Mathf.Min(minY, p.y);
            maxX = Mathf.Max(maxX, p.x);
            maxY = Mathf.Max(maxY, p.y);
        }

        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }
    public Result RecognizeOnly(string templateName, List<Vector2> rawPoints)
    {
        Result result = new Result
        {
            Name = templateName,
            Score = 0f,
            Success = false
        };

        if (rawPoints == null || rawPoints.Count < 2 || templates.Count == 0)
            return result;

        List<Vector2> points = Normalize(rawPoints);

        float bestDistance = float.MaxValue;
        bool foundTemplate = false;

        foreach (Template template in templates)
        {
            if (template.Name != templateName)
                continue;

            float distance = DistanceAtBestAngle(
                points,
                template.Points,
                -AngleRange * Mathf.Deg2Rad,
                AngleRange * Mathf.Deg2Rad,
                AnglePrecision * Mathf.Deg2Rad
            );

            if (distance < bestDistance)
            {
                bestDistance = distance;
                foundTemplate = true;
            }
        }

        if (!foundTemplate)
            return result;

        float score = 1f - (bestDistance / HalfDiagonal);
        score = Mathf.Clamp01(score);

        result.Score = score;
        result.Success = true;
        return result;
    }
}