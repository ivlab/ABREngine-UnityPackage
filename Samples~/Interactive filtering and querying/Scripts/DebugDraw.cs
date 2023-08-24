using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using IVLab.Utilities;

// (copied from https://github.umn.edu/ivlab-cs/MinVR3-UnityPackage/blob/main/Runtime/Scripts/Debug/DebugDraw.cs)

/// <summary>
/// Additions to Unity's useful debugging utililies like Debug.DrawRay and
/// Debug.DrawLine. These are particularly useful for AR/VR applications
/// where the debug output needs to be seen in the Game View (not just scene
/// view.)
///
/// All parameters `duration` will default to a single frame when left at 0,
/// otherwise duration is in seconds.
/// </summary>
public static class DebugDraw
{
    /// <summary>
    /// Draw a ray as an in-game-rendered cylinder
    /// </summary>
    public static void Ray(Vector3 start, Vector3 direction, Color color, float duration = 0.0f, float thickness = 0.001f)
    {
        DebugDrawing.Instance.DrawRay(start, direction, color, duration, thickness);
    }

    /// <summary>
    /// Draw a line as an in-game-rendered cylinder
    /// </summary>
    public static void Line(Vector3 start, Vector3 end, Color color, float duration = 0.0f, float thickness = 0.001f)
    {
        Vector3 direction = end - start;
        DebugDrawing.Instance.DrawRay(start, direction, color, duration, thickness);
    }

    /// <summary>
    /// Draw a circular mesh
    /// </summary>
    public static void Circle(Vector3 center, float radius, Vector3 normal, Color color, float duration = 0.0f)
    {
        DebugDrawing.Instance.DrawCircle(center, radius, normal, color, duration);
    }

    /// <summary>
    /// Draw a sphere/point mesh
    /// </summary>
    public static void Sphere(Vector3 center, float radius, Color color, float duration = 0.0f)
    {
        DebugDrawing.Instance.DrawSphere(center, radius, color, duration);
    }
    public static void Point(Vector3 center, float radius, Color color, float duration = 0.0f)
    {
        DebugDrawing.Instance.DrawSphere(center, radius, color, duration);
    }

    /// <summary>
    /// Draw a bounds outline mesh
    /// </summary>
    public static void Bounds(Bounds bounds, Color color, float duration = 0.0f, float thickness = 0.001f)
    {
        DebugDrawing.Instance.DrawBounds(bounds, color, thickness, Matrix4x4.identity, duration);
    }

    public static void Bounds(Bounds bounds, Color color, Matrix4x4 boundsTransform, float duration = 0.0f, float thickness = 0.001f)
    {
        DebugDrawing.Instance.DrawBounds(bounds, color, thickness, boundsTransform, duration);
    }

    /// <summary>
    /// Draw a set of axes corresponding to a Matrix4x4 basis. Uses standard RGB->XYZ coloring.
    /// </summary>
    public static void Axes(Matrix4x4 basis, float size = 0.1f, float duration = 0.0f, float thickness = 0.001f)
    {
        DebugDrawing.Instance.DrawRay(basis.ExtractPosition(), basis.GetColumn(0) * size, Color.red, duration, thickness);
        DebugDrawing.Instance.DrawRay(basis.ExtractPosition(), basis.GetColumn(1) * size, Color.green, duration, thickness);
        DebugDrawing.Instance.DrawRay(basis.ExtractPosition(), basis.GetColumn(2) * size, Color.blue, duration, thickness);
    }

    /// <summary>
    /// Draw some text to the screen at a specified position
    /// </summary>
    /// <remarks>
    /// > [!NOTE]
    /// > The <see cref="Text"/> method only works in the Unity Editor and only displays in scene view.
    /// </remarks>
    public static void Text(Vector3 position, string text, Color color, float duration = 0.0f)
    {
        DebugDrawing.Instance.DrawText(position, text, color, duration);
    }
}

internal class DebugDrawing : Singleton<DebugDrawing>
{
    private List<RayWithMagnitude> rays = new List<RayWithMagnitude>();
    private List<Circle> circles = new List<Circle>();
    private List<Sphere> spheres = new List<Sphere>();
    private List<BoundsWithColor> boundsList = new List<BoundsWithColor>();
    private List<Debug2String> textList = new List<Debug2String>();

    private struct RayWithMagnitude
    {
        public Vector3 start;
        public Vector3 directionWithMagnitude;
        public Color color;
        public float duration;
        public float radius;
    }

    private struct Circle
    {
        public Vector3 center;
        public Vector3 normal;
        public float radius;
        public Color color;
        public float duration;
    }

    private struct Sphere
    {
        public Vector3 center;
        public float radius;
        public Color color;
        public float duration;
    }

    private struct BoundsWithColor
    {
        public Bounds bounds;
        public Color color;
        public float thickness;
        public Matrix4x4 transform;
        public float duration;
    }

    // Source: http://answers.unity.com/answers/1357176/view.html
    private class Debug2String
    {
        public Vector3 pos;
        public string text;
        public Color color;
        public float duration;
    }

    private Mesh cylinderMesh;
    private Mesh sphereMesh;
    private Material debugMaterial;

    void Start()
    {
        GameObject cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinderMesh = cyl.GetComponent<MeshFilter>().mesh;
        Destroy(cyl);
        GameObject sph = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphereMesh = sph.GetComponent<MeshFilter>().mesh;
        Destroy(sph);

        Shader s = Shader.Find("Unlit/Color");
        debugMaterial = new Material(s);
    }

    void Update()
    {
        // Draw rays
        for (int r = 0; r < rays.Count; r++)
        {
            RayWithMagnitude ray = rays[r];
            var localScale = new Vector3(ray.radius, (ray.directionWithMagnitude).magnitude * 0.5f, ray.radius);
            var position = (ray.start + ray.directionWithMagnitude * 0.5f);
            var up = ray.directionWithMagnitude.normalized;
            Matrix4x4 m = Matrix4x4.TRS(position, Quaternion.FromToRotation(Vector3.up, up), localScale);

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetColor("_Color", rays[r].color);
            Graphics.DrawMesh(cylinderMesh, m, debugMaterial, 0, null, 0, properties: block);

            ray.duration -= Time.deltaTime;
            rays[r] = ray;
        }


        // Draw circles
        for (int i = 0; i < circles.Count; i++)
        {
            Circle c = circles[i];
            var localScale = new Vector3(c.radius * 2.0f, 0.00001f, c.radius * 2.0f);
            var position = c.center;
            var up = c.normal;
            Matrix4x4 m = Matrix4x4.TRS(position, Quaternion.FromToRotation(Vector3.up, up), localScale);

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetColor("_Color", c.color);
            Graphics.DrawMesh(cylinderMesh, m, debugMaterial, 0, null, 0, properties: block);

            c.duration -= Time.deltaTime;
            circles[i] = c;
        }

        // Draw spheres
        for (int i = 0; i < spheres.Count; i++)
        {
            Sphere s = spheres[i];
            var localScale = Vector3.one * s.radius;
            var position = s.center;
            Matrix4x4 m = Matrix4x4.TRS(position, Quaternion.identity, localScale);

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetColor("_Color", s.color);
            Graphics.DrawMesh(sphereMesh, m, debugMaterial, 0, null, 0, properties: block);

            s.duration -= Time.deltaTime;
            spheres[i] = s;
        }

        // Draw bounds
        for (int i = 0; i < boundsList.Count; i++)
        {
            BoundsWithColor b = boundsList[i];
            var lineWidth = b.thickness;
            var bT = Matrix4x4.Translate(b.bounds.center);
            var tf = b.transform;

            // 0 = along y axis
            // 1 = along z axis
            // 2 = along x axis
            var s0 = Matrix4x4.Scale(new Vector3(lineWidth, b.bounds.extents.y, lineWidth));
            var s1 = Matrix4x4.Scale(new Vector3(lineWidth, lineWidth, b.bounds.extents.z));
            var s2 = Matrix4x4.Scale(new Vector3(b.bounds.extents.x, lineWidth, lineWidth));

            var r0 = Matrix4x4.Rotate(Quaternion.identity);
            var r1 = Matrix4x4.Rotate(Quaternion.FromToRotation(Vector3.up, Vector3.forward));
            var r2 = Matrix4x4.Rotate(Quaternion.FromToRotation(Vector3.up, Vector3.right));

            var t00 = Matrix4x4.Translate(new Vector3(b.bounds.extents.x, 0.0f, b.bounds.extents.z));
            var t01 = Matrix4x4.Translate(new Vector3(-b.bounds.extents.x, 0.0f, b.bounds.extents.z));
            var t02 = Matrix4x4.Translate(new Vector3(b.bounds.extents.x, 0.0f, -b.bounds.extents.z));
            var t03 = Matrix4x4.Translate(new Vector3(-b.bounds.extents.x, 0.0f, -b.bounds.extents.z));

            var t10 = Matrix4x4.Translate(new Vector3(b.bounds.extents.x, b.bounds.extents.y, 0.0f));
            var t11 = Matrix4x4.Translate(new Vector3(-b.bounds.extents.x, b.bounds.extents.y, 0.0f));
            var t12 = Matrix4x4.Translate(new Vector3(b.bounds.extents.x, -b.bounds.extents.y, 0.0f));
            var t13 = Matrix4x4.Translate(new Vector3(-b.bounds.extents.x, -b.bounds.extents.y, 0.0f));

            var t20 = Matrix4x4.Translate(new Vector3(0.0f, b.bounds.extents.y, b.bounds.extents.z));
            var t21 = Matrix4x4.Translate(new Vector3(0.0f, -b.bounds.extents.y, b.bounds.extents.z));
            var t22 = Matrix4x4.Translate(new Vector3(0.0f, b.bounds.extents.y, -b.bounds.extents.z));
            var t23 = Matrix4x4.Translate(new Vector3(0.0f, -b.bounds.extents.y, -b.bounds.extents.z));

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetColor("_Color", b.color);
            Graphics.DrawMesh(cylinderMesh, tf * bT * t00 * s0 * r0, debugMaterial, 0, null, 0, properties: block);
            Graphics.DrawMesh(cylinderMesh, tf * bT * t01 * s0 * r0, debugMaterial, 0, null, 0, properties: block);
            Graphics.DrawMesh(cylinderMesh, tf * bT * t02 * s0 * r0, debugMaterial, 0, null, 0, properties: block);
            Graphics.DrawMesh(cylinderMesh, tf * bT * t03 * s0 * r0, debugMaterial, 0, null, 0, properties: block);

            Graphics.DrawMesh(cylinderMesh, tf * bT * t10 * s1 * r1, debugMaterial, 0, null, 0, properties: block);
            Graphics.DrawMesh(cylinderMesh, tf * bT * t11 * s1 * r1, debugMaterial, 0, null, 0, properties: block);
            Graphics.DrawMesh(cylinderMesh, tf * bT * t12 * s1 * r1, debugMaterial, 0, null, 0, properties: block);
            Graphics.DrawMesh(cylinderMesh, tf * bT * t13 * s1 * r1, debugMaterial, 0, null, 0, properties: block);

            Graphics.DrawMesh(cylinderMesh, tf * bT * t20 * s2 * r2, debugMaterial, 0, null, 0, properties: block);
            Graphics.DrawMesh(cylinderMesh, tf * bT * t21 * s2 * r2, debugMaterial, 0, null, 0, properties: block);
            Graphics.DrawMesh(cylinderMesh, tf * bT * t22 * s2 * r2, debugMaterial, 0, null, 0, properties: block);
            Graphics.DrawMesh(cylinderMesh, tf * bT * t23 * s2 * r2, debugMaterial, 0, null, 0, properties: block);

            b.duration -= Time.deltaTime;
            boundsList[i] = b;
        }

        boundsList = boundsList.Where(b => b.duration > float.Epsilon).ToList();
        rays = rays.Where(b => b.duration > float.Epsilon).ToList();
        circles = circles.Where(b => b.duration > float.Epsilon).ToList();
        spheres = spheres.Where(s => s.duration > float.Epsilon).ToList();

    }

    public void OnDrawGizmos()
    {
        foreach (var stringpair in textList)
        {
            GUIStyle style = new GUIStyle();
            Color color = stringpair.color;
            style.normal.textColor = color;
            style.alignment = TextAnchor.MiddleCenter;
#if UNITY_EDITOR
            UnityEditor.Handles.color = color;
            UnityEditor.Handles.Label(stringpair.pos, stringpair.text, style);
#endif
            stringpair.duration -= Time.deltaTime;
        }
        textList = textList.Where(s => s.duration > float.Epsilon).ToList();
    }

    public void DrawRay(Vector3 start, Vector3 direction, Color color, float duration, float radius)
    {
        rays.Add(new RayWithMagnitude()
        {
            start = start,
            directionWithMagnitude = direction,
            color = color,
            duration = duration,
            radius = radius
        });
    }

    public void DrawCircle(Vector3 center, float radius, Vector3 normal, Color color, float duration)
    {
        circles.Add(new Circle()
        {
            center = center,
            radius = radius,
            normal = normal,
            color = color,
            duration = duration
        });
    }

    public void DrawSphere(Vector3 center, float radius, Color color, float duration)
    {
        spheres.Add(new Sphere()
        {
            center = center,
            radius = radius,
            color = color,
            duration = duration
        });
    }

    public void DrawBounds(Bounds bounds, Color color, float thickness, Matrix4x4 boundsTransform, float duration)
    {
        boundsList.Add(new BoundsWithColor()
        {
            bounds = bounds,
            color = color,
            thickness = thickness,
            transform = boundsTransform,
            duration = duration
        });
    }

    public void DrawText(Vector3 pos, string text, Color color, float duration)
    {
        textList.Add(new Debug2String() { text = text, color = color, pos = pos, duration = duration, });
    }
}