using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IVLab.ABREngine;
using System.Linq;

/// <summary>
/// Example of setting up a scene to record interactively changing EVERY
/// variable and value in ABR, for every data impression type.
///
/// This is currently super manual (see the script). Probably could be automated
/// with reflection or something, but I figured since we're only doing this
/// rarely it's not worth it. Instead, see the "regions" laid out below.
///
/// Comment/uncomments bits of this file to record gifs of each parameter.
///
/// Oh yeah, you might want to install the Unity Recorder and add a "GIF"
/// recorder for this. Once installed, access via Window > General > Recorder.
/// My setup for this was:
/// Recorder Settings:
/// - Recording Mode: Frame Interval
/// - Start: 0
/// - End: 60
/// - Frame Rate: Constant / 30 FPS / Cap FPS
/// GIF Animation:
/// - Source: Targeted Camera
/// - Camera: MainCamera
/// - Output Resolution: 240p
/// - Aspect Ratio: 1:1
/// - Flip Vertical (checked)
///
/// Then, set the output file to the needed DocumentationSrc~/resources/api/ folder.
/// </summary>
public class VisDriver : MonoBehaviour
{
#region Control the rate of interpolation between various endpoints
    public float oscRate = 5.0f;
    public float multiplier = 0.05f;
    public float min = 0.01f;
#endregion


#region Design elements to interpolate between (later in Update())
    GlyphVisAsset glyph1;

    ColormapVisAsset cmap1;
    ColormapVisAsset cmap2;

    SurfaceTextureVisAsset tex1;
    SurfaceTextureVisAsset tex2;

    LineTextureVisAsset line1;
    LineTextureVisAsset line2;

    ScalarDataVariable var1;
    ScalarDataVariable var2;
    ScalarDataVariable instvar1;
    ScalarDataVariable instvar2;

    Mesh sphereMesh;
    Mesh capsuleMesh;
#endregion

#region Data impression declarations
    // SimpleGlyphDataImpression di;
    // SimpleSurfaceDataImpression di;
    // SimpleLineDataImpression di;
    // SimpleVolumeDataImpression di;
    InstancedSurfaceDataImpression di;
#endregion

    void Start()
    {
#region Specific setup for InstancedSurfaceDataImpression
        // build some xform data for instanced xform renderer
        int numPoints = 30;
        Bounds bds = new Bounds(Vector3.zero, Vector3.one * 2.0f);
        List<Vector3> pts = new List<Vector3>();
        List<Vector3> dirs = new List<Vector3>();
        List<float> xs = new List<float>();
        List<float> ys = new List<float>();
        for (int p = 0; p < numPoints; p++)
        {
            var t = (p / (float) numPoints);
            var xVal = t * bds.size.x - bds.extents.x;
            var yVal = 1.0f * Mathf.Sin(7.0f * t);
            pts.Add(new Vector3( xVal, yVal, 0.0f));
            dirs.Add(new Vector3(Mathf.Sin(t), Mathf.Cos(t), Mathf.Sin(t)).normalized);
            xs.Add(xVal);
            ys.Add(yVal);
        }
        Matrix4x4[] xforms = pts.Select((_p, i) => Matrix4x4.TRS(pts[i], Quaternion.LookRotation(Vector3.forward, dirs[i]), Vector3.one * 0.1f)).ToArray();
        Matrix4x4[][] matrixArrays = new Matrix4x4[][] { xforms };
        string[] matrixArrayNames = new string[] { "transforms" };
        string[] scalarArrayNames = new string[] { "XAxis", "YAxis" };

        RawDataset rds = new RawDataset();
        rds.bounds = bds;
        rds.scalarMins = new float[] { xs.Min(), ys.Min() };
        rds.scalarMaxes = new float[] { xs.Max(), ys.Max() };
        rds.matrixArrays = matrixArrays;
        rds.matrixArrayNames = matrixArrayNames;
        rds.scalarArrayNames = scalarArrayNames;
        rds.scalarArrays = new SerializableFloatArray[] {
            new SerializableFloatArray() { array = xs.ToArray() },
            new SerializableFloatArray() { array = ys.ToArray() }
        };
        rds.vectorArrayNames = new string[0];
        KeyData kd = ABREngine.Instance.Data.ImportRawDataset("Demo/Demo/KeyData/Transforms", rds);
        instvar1 = kd.GetScalarVariable("XAxis");
        instvar2 = kd.GetScalarVariable("YAxis");

        // Gather a mesh for instanced mesh
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphereMesh = sphere.GetComponent<MeshFilter>().sharedMesh;
        Destroy(sphere);
        GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        capsuleMesh = capsule.GetComponent<MeshFilter>().sharedMesh;
        Destroy(capsule);
#endregion


#region Import various datasets for use (should already exist in ABREngine-UnityPackage/Runtime/Resources/media/datasets)
        // comment/uncomment the one you need

        // string dataPath = "Demo/Wavelet/KeyData/Contour";
        string dataPath = "Demo/Wavelet/KeyData/Volume";
        // string dataPath = "Demo/Wavelet/KeyData/Points";
        // string dataPath = "Demo/Wavelet/KeyData/Flow";

        // Import the contour surface dataset into ABR
        KeyData volume = ABREngine.Instance.Data.LoadData(dataPath);
        var1 = volume.GetScalarVariable("XAxis");
        // var2 = contour.GetScalarVariable("YAxis");
        var2 = volume.GetScalarVariable("RTData");
#endregion

#region Import VisAssets (should all exist in Resources already)
        // Import VisAssets
        cmap1 = ABREngine.Instance.VisAssets.LoadVisAsset<ColormapVisAsset>(new System.Guid("5a761a72-8bcb-11ea-9265-005056bae6d8"));
        cmap2 = ABREngine.Instance.VisAssets.GetDefault<ColormapVisAsset>() as ColormapVisAsset;
        glyph1 = ABREngine.Instance.VisAssets.LoadVisAsset<GlyphVisAsset>(new System.Guid("d7659fc0-bc3f-11ec-bb4c-005056bae6d8"));
        tex1 = ABREngine.Instance.VisAssets.LoadVisAsset<SurfaceTextureVisAsset>(new System.Guid("35a12d70-72cd-11ea-8d65-005056bae6d8"));
        tex2 = ABREngine.Instance.VisAssets.LoadVisAsset<SurfaceTextureVisAsset>(new System.Guid("91095a14-72c5-11ea-bfdd-005056bae6d8"));
        line1 = ABREngine.Instance.VisAssets.LoadVisAsset<LineTextureVisAsset>(new System.Guid("1165e2a0-8805-11ea-9265-005056bae6d8"));
        // line2 = ABREngine.Instance.VisAssets.LoadVisAsset<LineTextureVisAsset>(new System.Guid("6b66ac76-7dd3-11ea-813e-005056bae6d8"));
        line2 = ABREngine.Instance.VisAssets.LoadVisAsset<LineTextureVisAsset>(new System.Guid("02e35728-7dd6-11ea-9d7c-005056bae6d8"));
#endregion

#region Create InstancedSurfaceDataImpression
        di = new InstancedSurfaceDataImpression();
        di.keyData = kd;
        di.instanceMesh = capsuleMesh;
        di.colormap = cmap1;
        di.colorVariable = instvar1;
        // di.outlineColor = new Color(0.3f, 0.3f, 0.85f);
        di.outlineWidth = 0.05f;
        di.forceOutlineColor = true;
        di.showOutline = true;
#endregion


#region Create SimpleLineDataImpression
        // di = new SimpleLineDataImpression();
        // di.keyData = contour;

        // di.colorVariable = var1;
        // // di.lineTexture = line2;
        // di.colormap = cmap1;
        // di.lineWidth = 0.1f;
#endregion

#region Create SimpleVolumeDataImpression
        // di = new SimpleVolumeDataImpression();
        // di.keyData = volume;

        // di.colorVariable = var2;
        // di.colormap = cmap1;
        // // di.opacitymap = PrimitiveGradient.Default();
        // // var x = (1.0f + Mathf.Sin(oscRate * Time.time)) * multiplier + min;
        // var x = 0.8f;
        // float[] points = new float[] { 0.0f, x - 0.1f, x, x + 0.1f, 1.0f };
        // string b = "5%";
        // string[] values = new string[] { b, b, "100%", b, b };
        // PrimitiveGradient pg = new PrimitiveGradient(System.Guid.NewGuid(), points, values);
        // di.opacitymap = pg;
#endregion

#region Create SimpleSurfaceDataImpression
        // di = new SimpleSurfaceDataImpression();
        // di.keyData = contour;

        // di.colorVariable = var1;
        // di.colormap = cmap1;
        // di.pattern = tex1;
        // di.patternSize = 0.5f;
        // di.outlineWidth = 0.05f;
        // di.outlineColor = Color.white;
        // di.showOutline = true;
#endregion


#region Register Data Impression so ABR knows about it
        di.RenderHints.DataChanged = true;
        ABREngine.Instance.RegisterDataImpression(di);

        // 3.b. Render the visualization
        ABREngine.Instance.Render();
#endregion
    }

    void Update()
    {
#region Animated parameters for most data impressions
        // di.glyphSize = (1.0f + Mathf.Sin(oscRate * Time.time)) * multiplier + min;
        // di.glyphDensity = (1.0f + Mathf.Sin(oscRate * Time.time)) * multiplier + min;
        // di.outlineWidth = (1.0f + Mathf.Sin(oscRate * Time.time)) * multiplier + min;
        // di.outlineColor = new Color(Time.time % 1.0f, (Time.time + 0.5f) % 1.0f, (Time.time + 0.7f) % 1.0f);
        // di.outlineColor = Color.HSVToRGB(Time.time % 1.0f, Time.time % 1.0f, Time.time % 1.0f);
        // di.patternSize = (1.0f + Mathf.Sin(oscRate * Time.time)) * multiplier + min;
        // di.patternSeamBlend = (1.0f + Mathf.Sin(oscRate * Time.time)) * multiplier + min;
        // di.patternSaturation = (1.0f + Mathf.Sin(oscRate * Time.time)) * multiplier + min;
        // di.patternIntensity = (1.0f + Mathf.Sin(oscRate * Time.time)) * multiplier + min;
        // di.opacity = (1.0f + Mathf.Sin(oscRate * Time.time)) * multiplier + min;
        // di.outlineWidth = (1.0f + Mathf.Sin(oscRate * Time.time)) * multiplier + min;
        // di.outlineColor = Color.HSVToRGB(Time.time % 1.0f, Time.time % 1.0f, Time.time % 1.0f);
        // di.textureCutoff = (1.0f + Mathf.Sin(oscRate * Time.time)) * multiplier + min;
        // di.averageCount = Mathf.RoundToInt((1.0f + Mathf.Sin(oscRate * Time.time)) * multiplier + min);
        // di.lineWidth = (1.0f + Mathf.Sin(oscRate * Time.time)) * multiplier + min;
        // di.ribbonRotationAngle = (1.0f + Mathf.Sin(oscRate * Time.time)) * multiplier + min;
        // di.ribbonBrightness = (1.0f + Mathf.Sin(oscRate * Time.time)) * multiplier + min;
        // di.ribbonCurveAngle = (1.0f + Mathf.Sin(oscRate * Time.time)) * multiplier + min;
        // var w = (1.0f + Mathf.Cos(oscRate * Time.time)) * multiplier + min;
        // di.defaultCurveDirection = (new Vector3(v, 1.0f, w)).normalized;
        // di.volumeOpacityMultiplier = (1.0f + Mathf.Sin(oscRate * Time.time)) * multiplier + min;
#endregion

#region Animated parameters for Instanced Surfaces
        // Capture key data changing for instanced surfaces
        // RawDataset rds;
        // if (ABREngine.Instance.Data.TryGetRawDataset("Demo/Demo/KeyData/Transforms", out rds))
        // {
        //     Matrix4x4[] xforms = rds.matrixArrays[0];
        //     for (int i = 0; i < xforms.Length; i++)
        //     {
        //         Quaternion rotation = xforms[i].ExtractRotation();
        //         // var rot = Quaternion.RotateTowards(rotation, Quaternion.Euler(Vector3.up), 10.0f);
        //         var rot =  Quaternion.AngleAxis(Time.deltaTime * 100.0f, Vector3.up) * rotation;
        //         xforms[i].SetTRS(xforms[i].ExtractPosition(), rot, xforms[i].ExtractScale());
        //     }
        //     rds.matrixArrays[0] = xforms;
        // }
        // di.outlineWidth = (1.0f + Mathf.Sin(oscRate * Time.time)) * multiplier + min;
        di.outlineColor = Color.HSVToRGB(Time.time % 1.0f, Time.time % 1.0f, Time.time % 1.0f);
#endregion

#region Handle discrete-valued animations
        if (Time.time % 2.0f > 1.00f)
        // di.forceOutlineColor = false;
        // di.showOutline = false;
        // di.colormap = cmap1;
        // di.colorVariable = instvar1;
        // di.instanceMesh = sphereMesh;
        // di.volumeLighting = true;
        // di.lineTexture = line1;
        // di.onlyOutline = true;
        // // di.forceOutlineColor = false;
        // // di.showOutline = true;
        // //     di.useRandomOrientation = false;
        // di.colorVariable = var1;
            // di.colormap = cmap1;
        // //     di.glyph = glyph1;
        //     di.pattern = tex1;
        // di.showOutline = false;
        // else
        // di.forceOutlineColor = true;
        // di.showOutline = true;
        // di.colormap = cmap2;
        // di.colorVariable = instvar2;
        // di.instanceMesh = capsuleMesh;
        // di.volumeLighting = false;
        // di.lineTexture = line2;
        // di.onlyOutline = false;
        // // di.forceOutlineColor = true;
        // // di.showOutline = false;
        // //     di.useRandomOrientation = true;
        // di.colorVariable = var2;
            // di.colormap = cmap2;
        // //     di.glyph = null;
        //     di.pattern = tex2;
        // di.showOutline = true;

        // if (Time.time % 2.0f > 1.50f)
        //     di.glyphDensity = 1.0f;
        // else if (Time.time % 2.0f > 0.75f)
        //     di.glyphDensity = 0.5f;
        // else
        //     di.glyphDensity = 0.1f;
#endregion


        di.RenderHints.StyleChanged = true;
        // di.RenderHints.DataChanged = true;
        ABREngine.Instance.Render();
    }
}
