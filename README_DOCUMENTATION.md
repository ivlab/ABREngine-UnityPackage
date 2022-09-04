# Generating a Documentation Website

This package uses currently recommended documentation guidelines from the [IVLab Template Project](https://github.umn.edu/ivlab-cs/Template-UnityPackage). Please see this project and its' [README_DOCUMENTATION.md](https://github.umn.edu/ivlab-cs/Template-UnityPackage/blob/main/README_DOCUMENTATION.md) for more information and reasoning behind the documentation structure.

## Prereqs: Installing DocFx

Step 1 is to install a recent stable release of DocFx by following [the instructions on their "getting started" page](https://dotnet.github.io/docfx/tutorial/docfx_getting_started.html).  The instructions differ a bit depending upon your platform.  

*Tip: As of summer 2022, the Apple M1 chipset is not yet supported, so M1 users be careful to follow the instructions to install with Rosetta support.*

## Actually Running DocFx to Generate the Documentation!

The important script: [build-docs](DocumentationSrc~/build-docs).

1. Make sure you already have docfx installed, if not, go back to the top of this document to find the link to download it.
2. Open your usual unix-style shell (use Terminal on OSX, use Git Bash or similar on Windows).
3. Check to make sure that the docfx executable is in your PATH by running ```which docfx```.  If it prints a path to docfx, then proceed.  If not, figure out which directory you installed docfx in and add it to your path OR open up the [build-docs](DocumentationSrc~/build-docs) script and edit it to hardcode a path in the docfxExe variable.  
4. Build the docs by running the script.  You can call it from any directory, or you could cd into the `DocumentationSrc~` directory first, like this:

```
cd DocumentationSrc\~
./build-docs
```


## Building ABR-specific pages and generating fancy .gifs for data impression inputs

So, you may have seen the gifs that are included in every data impression page. If not, take a look over at @IVLab.ABREngine.SimpleSurfaceDataImpression for a quick example. Generating the gifs on those pages isn't a hard task but it does take some effort. The following script is included in case someone else ever wants to go though and generate gifs for ABR features.


```cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IVLab.ABREngine;
using System.Linq;

public class VisDriver : MonoBehaviour
{
    public float oscRate = 5.0f;
    public float multiplier = 0.05f;
    public float min = 0.01f;
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

    // Start is called before the first frame update
    // SimpleGlyphDataImpression surf;
    // SimpleSurfaceDataImpression surf;
    // SimpleLineDataImpression surf;
    SimpleVolumeDataImpression surf;
    InstancedSurfaceDataImpression instSurf;
    Mesh sphereMesh;
    Mesh capsuleMesh;
    void Start()
    {
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


        // Load the dataset from disk (see ABREngine-UnityPackage/Runtime/Resources/media/datasets) for the raw data files
        // string contourDataPath = "Demo/Wavelet/KeyData/Contour";
        string contourDataPath = "Demo/Wavelet/KeyData/Volume";
        // string contourDataPath = "Demo/Wavelet/KeyData/Points";
        // string contourDataPath = "Demo/Wavelet/KeyData/Flow";
        RawDataset contourRaw = ABREngine.Instance.Data.LoadRawDataset<ResourcesDataLoader>(contourDataPath);

        // Import the contour surface dataset into ABR
        KeyData contour = ABREngine.Instance.Data.ImportRawDataset(contourDataPath, contourRaw);
        var1 = contour.GetScalarVariable("XAxis");
        // var2 = contour.GetScalarVariable("YAxis");
        var2 = contour.GetScalarVariable("RTData");

        // Import a Colormap VisAsset
        cmap1 = ABREngine.Instance.VisAssets.LoadVisAsset<ColormapVisAsset>(new System.Guid("5a761a72-8bcb-11ea-9265-005056bae6d8"));
        cmap2 = ABREngine.Instance.VisAssets.GetDefault<ColormapVisAsset>() as ColormapVisAsset;
        glyph1 = ABREngine.Instance.VisAssets.LoadVisAsset<GlyphVisAsset>(new System.Guid("d7659fc0-bc3f-11ec-bb4c-005056bae6d8"));
        tex1 = ABREngine.Instance.VisAssets.LoadVisAsset<SurfaceTextureVisAsset>(new System.Guid("35a12d70-72cd-11ea-8d65-005056bae6d8"));
        tex2 = ABREngine.Instance.VisAssets.LoadVisAsset<SurfaceTextureVisAsset>(new System.Guid("91095a14-72c5-11ea-bfdd-005056bae6d8"));
        line1 = ABREngine.Instance.VisAssets.LoadVisAsset<LineTextureVisAsset>(new System.Guid("1165e2a0-8805-11ea-9265-005056bae6d8"));
        // line2 = ABREngine.Instance.VisAssets.LoadVisAsset<LineTextureVisAsset>(new System.Guid("6b66ac76-7dd3-11ea-813e-005056bae6d8"));
        line2 = ABREngine.Instance.VisAssets.LoadVisAsset<LineTextureVisAsset>(new System.Guid("02e35728-7dd6-11ea-9d7c-005056bae6d8"));

        instSurf = new InstancedSurfaceDataImpression();
        instSurf.keyData = kd;
        instSurf.instanceMesh = capsuleMesh;
        instSurf.colormap = cmap1;
        instSurf.colorVariable = instvar1;
        // instSurf.outlineColor = new Color(0.3f, 0.3f, 0.85f);
        instSurf.outlineWidth = 0.05f;
        instSurf.forceOutlineColor = true;
        instSurf.showOutline = true;


        // surf = new SimpleLineDataImpression();
        // surf.keyData = contour;

        // surf.colorVariable = var1;
        // // surf.lineTexture = line2;
        // surf.colormap = cmap1;
        // surf.lineWidth = 0.1f;

        surf = new SimpleVolumeDataImpression();
        surf.keyData = contour;

        surf.colorVariable = var2;
        surf.colormap = cmap1;
        // surf.opacitymap = PrimitiveGradient.Default();
        // var x = (1.0f + Mathf.Sin(oscRate * Time.time)) * multiplier + min;
        var x = 0.8f;
        float[] points = new float[] { 0.0f, x - 0.1f, x, x + 0.1f, 1.0f };
        string b = "5%";
        string[] values = new string[] { b, b, "100%", b, b };
        PrimitiveGradient pg = new PrimitiveGradient(System.Guid.NewGuid(), points, values);
        surf.opacitymap = pg;

        // surf = new SimpleSurfaceDataImpression();
        // surf.keyData = contour;

        // surf.colorVariable = var1;
        // surf.colormap = cmap1;
        // surf.pattern = tex1;
        // surf.patternSize = 0.5f;
        // surf.outlineWidth = 0.05f;
        // surf.outlineColor = Color.white;
        // surf.showOutline = true;


        // // Create surface data impression and assign key data
        // surf = new SimpleSurfaceDataImpression();
        // // SimpleLineDataImpression surf = new SimpleLineDataImpression();
        // // surf.lineWidth = 0.10f;
        // surf.colorVariable = var1;
        // // surf.colormap = cmap1;
        // // surf.glyph = glyph1;
        // surf.keyData = contour;
        // // surf.outlineColor = new Color(0.3f, 0.3f, 0.85f);
        // surf.glyphSize = 0.1f;
        // surf.outlineWidth = 0.05f;
        // surf.showOutline = true;
        // surf.forceOutlineColor = true;
        // surf.colormap = ABREngine.Instance.VisAssets.GetDefault<ColormapVisAsset>() as ColormapVisAsset;
        // surf.colorVariable = contour.GetScalarVariable("RTData");
        // surf.opacitymap = PrimitiveGradient.Default();
        // surf.volumeOpacityMultiplier = 0.05f;

        // 2.b. Assign colormap to data impression
        // surf.colormap = cmap;

        // 2.c. Assign color variable to data impression
        // surf.colorVariable = contour.GetScalarVariable("XAxis");

        // 3.a. Register the data impression so ABR knows about it
        // ABREngine.Instance.RegisterDataImpression(surf);
        ABREngine.Instance.RegisterDataImpression(instSurf);
        // instSurf.RenderHints.DataChanged = true;

        // 3.b. Render the visualization
        ABREngine.Instance.Render();
    }

    void Update()
    {
        // surf.glyphSize = (1.0f + Mathf.Sin(oscRate * Time.time)) * multiplier + min;
        // surf.glyphDensity = (1.0f + Mathf.Sin(oscRate * Time.time)) * multiplier + min;
        // surf.outlineWidth = (1.0f + Mathf.Sin(oscRate * Time.time)) * multiplier + min;
        // surf.outlineColor = new Color(Time.time % 1.0f, (Time.time + 0.5f) % 1.0f, (Time.time + 0.7f) % 1.0f);
        // surf.outlineColor = Color.HSVToRGB(Time.time % 1.0f, Time.time % 1.0f, Time.time % 1.0f);
        // surf.patternSize = (1.0f + Mathf.Sin(oscRate * Time.time)) * multiplier + min;
        // surf.patternSeamBlend = (1.0f + Mathf.Sin(oscRate * Time.time)) * multiplier + min;
        // surf.patternSaturation = (1.0f + Mathf.Sin(oscRate * Time.time)) * multiplier + min;
        // surf.patternIntensity = (1.0f + Mathf.Sin(oscRate * Time.time)) * multiplier + min;
        // surf.opacity = (1.0f + Mathf.Sin(oscRate * Time.time)) * multiplier + min;
        // surf.outlineWidth = (1.0f + Mathf.Sin(oscRate * Time.time)) * multiplier + min;
        // surf.outlineColor = Color.HSVToRGB(Time.time % 1.0f, Time.time % 1.0f, Time.time % 1.0f);
        // surf.textureCutoff = (1.0f + Mathf.Sin(oscRate * Time.time)) * multiplier + min;
        // surf.averageCount = Mathf.RoundToInt((1.0f + Mathf.Sin(oscRate * Time.time)) * multiplier + min);
        // surf.lineWidth = (1.0f + Mathf.Sin(oscRate * Time.time)) * multiplier + min;
        // surf.ribbonRotationAngle = (1.0f + Mathf.Sin(oscRate * Time.time)) * multiplier + min;
        // surf.ribbonBrightness = (1.0f + Mathf.Sin(oscRate * Time.time)) * multiplier + min;
        // surf.ribbonCurveAngle = (1.0f + Mathf.Sin(oscRate * Time.time)) * multiplier + min;
        // var w = (1.0f + Mathf.Cos(oscRate * Time.time)) * multiplier + min;
        // surf.defaultCurveDirection = (new Vector3(v, 1.0f, w)).normalized;
        // surf.volumeOpacityMultiplier = (1.0f + Mathf.Sin(oscRate * Time.time)) * multiplier + min;

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
        // instSurf.outlineWidth = (1.0f + Mathf.Sin(oscRate * Time.time)) * multiplier + min;
        instSurf.outlineColor = Color.HSVToRGB(Time.time % 1.0f, Time.time % 1.0f, Time.time % 1.0f);

        if (Time.time % 2.0f > 1.00f)
        // instSurf.forceOutlineColor = false;
        // instSurf.showOutline = false;
        // instSurf.colormap = cmap1;
        // instSurf.colorVariable = instvar1;
        // instSurf.instanceMesh = sphereMesh;
        // surf.volumeLighting = true;
        // surf.lineTexture = line1;
        // surf.onlyOutline = true;
        // // surf.forceOutlineColor = false;
        // // surf.showOutline = true;
        // //     surf.useRandomOrientation = false;
        // surf.colorVariable = var1;
            // surf.colormap = cmap1;
        // //     surf.glyph = glyph1;
        //     surf.pattern = tex1;
        // surf.showOutline = false;
        // else
        // instSurf.forceOutlineColor = true;
        // instSurf.showOutline = true;
        // instSurf.colormap = cmap2;
        // instSurf.colorVariable = instvar2;
        // instSurf.instanceMesh = capsuleMesh;
        // surf.volumeLighting = false;
        // surf.lineTexture = line2;
        // surf.onlyOutline = false;
        // // surf.forceOutlineColor = true;
        // // surf.showOutline = false;
        // //     surf.useRandomOrientation = true;
        // surf.colorVariable = var2;
            // surf.colormap = cmap2;
        // //     surf.glyph = null;
        //     surf.pattern = tex2;
        // surf.showOutline = true;

        // if (Time.time % 2.0f > 1.50f)
        //     surf.glyphDensity = 1.0f;
        // else if (Time.time % 2.0f > 0.75f)
        //     surf.glyphDensity = 0.5f;
        // else
        //     surf.glyphDensity = 0.1f;


        instSurf.RenderHints.StyleChanged = true;
        // instSurf.RenderHints.DataChanged = true;
        // surf.RenderHints.StyleChanged = true;
        // surf.RenderHints.DataChanged = true;
        ABREngine.Instance.Render();
    }
}
```