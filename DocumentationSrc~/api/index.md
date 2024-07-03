# API Overview

Here are the most important classes start out making visualizations with C# and
ABR. Check out the
[Creating a C# ABR visualization tutorial](../manual/examples/abr-cs.md)
for more examples.

## ABREngine

| Class | Importance |
|-------|----------|
| @IVLab.ABREngine.ABREngine | This is the main ABR Engine class. ABREngine is a singleton, meaning that you will most often access an instance of this class with `ABREngine.Instance`. |
| @IVLab.ABREngine.ABRConfig | Main configuration class for ABR. Create an ABRConfig using *Assets > Create > ABR > ABR Configuration* and modify as necessary. |
| @IVLab.ABREngine.ABRStateParser | Main class for serializing and deserializing ABR states |


## Data

### Data Loading

| Class | Importance |
| ----- | ---------- |
| @IVLab.ABREngine.DataManager | Main "manager" object where data are stored. Single instance of this class is [ABREngine.Instance.Data](xref:IVLab.ABREngine.ABREngine#IVLab_ABREngine_ABREngine_Data). |
| @IVLab.ABREngine.KeyData | High-level representation of geometric data. See [Key Data](../manual/concepts/key-data.md). |
| @IVLab.ABREngine.RawDataset | Low-level representation of geometric data. See [Basic Terminology](../manual/concepts/terminology-starter.md) |
| @IVLab.ABREngine.RawDatasetAdapter | Factory used to create ABR-compatible raw datasets from existing data you have (e.g., creating a surface out of a bunch of @UnityEngine.Vector3 s) |
| @IVLab.ABREngine.MediaDataLoader | Load data from the [media folder](../manual/concepts/media-folder.md) |
| @IVLab.ABREngine.ResourcesDataLoader | Load data from any [Unity Resources folder](https://docs.unity3d.com/Manual/BestPracticeUnderstandingPerformanceInUnity6.html) - useful for bundling data with a project. |
| @IVLab.ABREngine.HttpDataLoader | Load data from a network (http) source (must be set up with ABR data server) |

### Data Variables and Ranges
| Class | Importance |
| ----- | ---------- |
| @IVLab.ABREngine.ScalarDataVariable | Scalar variables (single value at each point in the dataset) |
| @IVLab.ABREngine.VectorDataVariable | Vector variables (3 values at each point in the dataset) |
| @IVLab.ABREngine.DataRange | Representation of a data range (min/max, only meaningful for scalar variables |

## VisAssets

| Class | Importance |
| ----- | ---------- |
| @IVLab.ABREngine.VisAssetManager | Main "manager" object where VisAssets are stored. Single instance of this class is [ABREngine.Instance.VisAssets](xref:IVLab.ABREngine.ABREngine#IVLab_ABREngine_ABREngine_VisAssets). |
| @IVLab.ABREngine.VisAssetLoader | Responsible for loading visassets, deciding which VisAsset fetcher to use, and error handling |
| @IVLab.ABREngine.VisAsset | Every VisAsset is one of these |
| @IVLab.ABREngine.ColormapVisAsset | Imported from a `colormap.xml` and have a `GetGradient()` method to get a @UnityEngine.Texture2D |
| @IVLab.ABREngine.LineTextureVisAsset | Imported from a `horizontal.png` texture, have a `GetTexture()` method to get a @UnityEngine.Texture2D |
| @IVLab.ABREngine.SurfaceTextureVisAsset | Imported from a `texture.png` texture, have a `GetTexture()` method to get a @UnityEngine.Texture2D |
| @IVLab.ABREngine.GlyphVisAsset | LOD-Separated, imported from a `LOD1.obj` and normal-mapped with a `LOD1.png`. |
| @IVLab.ABREngine.VisAssetGradient | Gradients of multiple VisAssets (applies to every VisAsset type except Colormaps) |

## Data Impressions

| Class | Importance |
| ----- | ---------- |
| @IVLab.ABREngine.DataImpression | Main class that all data impressions inherit from |
| @IVLab.ABREngine.SimpleSurfaceDataImpression | Data impression for surfaces. Takes key data type "Surface" |
| @IVLab.ABREngine.SimpleLineDataImpression | Data impression for ribbon-formed lines. Takes key data type "Lines" |
| @IVLab.ABREngine.SimpleGlyphDataImpression | Data impression for glyphs. Takes key data type "Points" |
| @IVLab.ABREngine.SimpleVolumeDataImpression | Data impression for volume data. Takes key data type "Volume" (a structured grid) |
| @IVLab.ABREngine.InstancedSurfaceDataImpression | Data impression for a series of instanced surfaces (same geometry repeated over many different transforms) |