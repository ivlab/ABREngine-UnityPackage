---
uid: terminology-starter.md
name: Basic ABREngine Terminology
---

# Basic ABREngine Terminology

Beyond the key data, visasset, and data impression metaphors, there are also
some new concepts and terminology that you'll need to work with the ABR Engine
via C# scripting in Unity.

## ABREngine

The @IVLab.ABREngine.ABREngine is the main class with which visualizations are
constructed. ABREngine exists as a singleton object in the Unity scene, that is,
there is only ONE instance of ABREngine the entire time Unity is running. You
can access the running instance easily in code with ABREngine.Instance; for
example, you can use any of the following important methods and objects in this
manner:

- [ABREngine.Instance.VisAssets](xref:IVLab.ABREngine.VisAssetManager) - single instanceSingle instance of the VisAssetManager (you can load or get visassets with this object) 
- [ABREngine.Instance.Data](xref:IVLab.ABREngine.DataManager) - Single instance of the DataManager (you can load or get data with this object)
- [ABREngine.Instance.RegisterDataImpression()](xref:IVLab.ABREngine.ABREngine#IVLab_ABREngine_ABREngine_RegisterDataImpression_IVLab_ABREngine_DataImpression_IVLab_ABREngine_DataImpressionGroup_System_Boolean_) - Connect the data and visuals together in the engine
- [ABRengine.Instance.Render()](xref:IVLab.ABREngine.ABREngine#IVLab_ABREngine_ABREngine_Render) - Display the visualization


## RawDataset

A @IVLab.ABREngine.RawDataset is a standardized geometric dataset (points, lines, surface, volume) formatted in a way that ABR can import it. RawDatasets MUST be imported to be used in ABR. RawDataset objects can be obtained from from the @IVLab.ABREngine.RawDatasetAdapter class and import it immediately after.

The following example shows how to construct and import a simple dataset of 3 points:

```cs
// Build a simple list of 3D points
List<Vector3> points = new List<Vector3> { Vector3.zero, Vector3.one, 2*Vector3.one };

// We need to provide a bounding box for the data so ABR knows where it can safely place the visualization in space.
// The data should NEVER go outside these bounds.
Bounds b = new Bounds(Vector3.zero, 2*Vector3.one);

// Standardize these points into a format ABR can understand
RawDataset abrPoints = RawDatasetAdapter.PointsToPoints(points, b, null, null);

// Then, import the data so we can use it in a visualization
KeyData pointsKD = ABREngine.Instance.Data.ImportRawDataset(abrPoints);
```

Read the docs on [ABREngine.Instance.Data.ImportRawDataset()](xref:IVLab.ABREngine.DataManager#IVLab_ABREngine_DataManager_ImportRawDataset_System_String_IVLab_ABREngine_RawDataset_) and @IVLab.ABREngine.RawDatasetAdapter for more information.


## DataImpressionGroup

Data impressions can be "grouped" in your ABR scene, which makes it easier to move data impressions around (e.g., for a side-by-side visualization). Check out the DataImpressionGroup example in the @IVLab.ABREngine.ABREngine for more information.