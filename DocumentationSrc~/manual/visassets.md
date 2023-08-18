# VisAssets (Visualization Assets)

VisAssets are visual building blocks or elements of a visualization sourced from real-world artifacts and materials. ABR currently supports four types of VisAssets:

| **Name** | Colormap | Line | Texture | Glyph |
| --- | --- | --- | --- | --- |
| **Example** | ![](resources/colormap.png) | ![](resources/lineva.png) | ![](resources/tex.png) | ![](resources/glyph.png) |
| **C# Class** | @IVLab.ABREngine.ColormapVisAsset | @IVLab.ABREngine.LineTextureVisAsset | @IVLab.ABREngine.SurfaceTextureVisAsset | @IVLab.ABREngine.GlyphVisAsset |

## Using VisAssets in code:

Each VisAsset has a unique identifier, known as a UUID (or a Guid in C# (Links to an external site.)) - this is the long hex number you may have seen in your ABR media folder. When programming with ABR, you'll need to use these UUIDs to load and reference VisAssets, since there's not an easy way to tell the computer "I want that blue linear colormap over there".

The general process for loading VisAssets in C# scripts is:

1.  Find the UUID of the VisAsset you want. Usually this can be accomplished by
right/two-finger clicking the VisAsset in question in a browser (either from the
[ABR Design Interface](http://localhost:8000) or the [Sculpting Vis Library](https://sculptingvis.tacc.utexas.edu)), then copy/pasting the long hex UUID from your browser's URL bar (e.g.
`5a761a72-8bcb-11ea-9265-005056bae6d8`).
2.  Create a @System.Guid object to store this UUID.
3.  Load the VisAsset using @IVLab.ABREngine.VisAssetManager#IVLab_ABREngine_VisAssetManager_LoadVisAsset__1_System_Guid_System_Boolean_ LoadVisAsset method.

For example, in code, you might load a colormap like this (note the use of `LoadVisAsset` and Guid):

```cs
ColormapVisAsset cmap = ABREngine.Instance.VisAssets.LoadVisAsset<ColormapVisAsset>(new Guid("5a761a72-8bcb-11ea-9265-005056bae6d8"));
```

See the @IVLab.ABREngine.VisAssetManager for more details and example loading code.