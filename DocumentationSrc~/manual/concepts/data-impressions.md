# Data Impressions

Every "layer" in the visualization is represented by a data impression. In the ABR design interface, each impression is shown by a "tower"; for example this data impression represents the ground's surface in the simulation, and we've applied some scalar data variables and styling to it.

![](../resources/groundTower.png)

ABR has four types of data impressions:

| @IVLab.ABREngine.SimpleSurfaceDataImpression | @IVLab.ABREngine.SimpleLineDataImpression | @IVLab.ABREngine.SimpleGlyphDataImpression | @IVLab.ABREngine.SimpleVolumeDataImpression |
| --- | --- | --- | --- |
| ![](../resources/data-impression-surface.png) | ![](../resources/data-impression-lines.png) | ![](../resources/data-impression-points.png) | ![](../resources/data-impression-volume.png) |


## Using Data Impressions in Code

To construct a data impression, use the KeyData, Variables, and VisAssets that you have loaded in in the previous examples and link them into the data impression by assigning to instance variables like `keyData`, `colormap`, `colorVariable` etc. See the documentation links above for the values that can be changed for each Data Impression. For example, to create the same effect as the "tower" in the previous image, we might use code like this:

```cs
// Import ground data
KeyData groundData = ABREngine.Instance.Data.ImportRawDataset(...);

// Import the colormap
ColormapVisAsset cmap = ABREngine.Instance.VisAssets.LoadVisAsset<ColormapVisAsset>(...);

// Create a new data impression for the ground
SimpleSurfaceDataImpression ground = DataImpression.Create<SimpleSurfaceDataImpression>("Ground");
ground.keyData = groundData;
ground.colormap = cmap;
ground.colorVariable = groundData.GetScalarVariable(...);

// Register the data impression with the engine
ABREngine.Instance.RegisterDataImpression(ground);
```



## Data Impression Groups

Data impression groups are a structure to keep data impressions and key data from the same dataset organized. By default, ABR automatically puts data impressions with the same key data dataset in the same data impression group. For example,

```
TACC/Ronne/KeyData/Terrain
TACC/Ronne/KeyData/Bathymetry
```

would automatically be placed in the same group with an automatic name `TACC/Ronne`. This behavior is to ensure that datasets with the same coordinate space remain registered together, while enabling developers to control position, rotation, scale, etc. of the whole group of data impressions.


### Data Impression Groups in the Editor


If you want ABR to put a data impression group at a specific position or scale in your scene, you may add a Data Impression Group to your scene in the editor with *GameObject > ABR > ABR Data Impression Group*. Then, rename the GameObject. For example, if you want all DataImpressions in the `TACC/Ronne` dataset to be contained within this data impression group, name the group `TACC/Ronne`. You can also add data impresison groups that don't have any specific dataset semantics.

If you wish the data to be "squished" inside a container bounding box, change the "ABR Data Container" bounds to suit your fancy. We recommend leaving the bounds center at (0, 0, 0) and using the GameObject transform to change the bounds position, rotation, and scale.

If you want to define a group but *retain* the data's original coordinate system, delete the "ABR Data Container" script that was automatically added when you created a Data Impression Group.


### Data Impression Groups in C# code

You may also want to create custom data impression groups and manually add data impressions to them. In the ABR C# API, you can use the following code to accomplish this:


```cs
// DI Groups must be created with "CreateDataImpressionGroup"
DataImpressionGroup exampleGroup = ABREngine.Instance.CreateDataImpressionGroup("Example");

// Add the `ground` object from above into the new example group
ABREngine.Instance.RegisterDataImpression(ground, exampleGroup);
```


You may optionally provide a unique ID:

```cs
System.Guid uuid = new System.Guid("80d9db78-8539-4c5b-b882-ff5b7328477a");
DataImpressionGroup exampleGroupWithID = ABREngine.Instance.CreateDataImpressionGroup("Example", uuid);
```


And, you may optionally provide container bounds and an initial transformation for the data impression group:

```cs
// 20m x 20m x 20m cube bounds, centered at (0, 0, 0)
Bounds container = new Bounds(Vector3.zero, Vector3.one * 10);

// Matrix transformation to move to (2, 0, 0) and rotate by 45 degrees on the x axis
// Matrix multiplication is right-to-left so the Rotate is executed first.
Matrix4x4 xform = Matrix4x4.identity;
xform *= Matrix4x4.Translate(new Vector3(2, 0, 0));
xform *= Matrix4x4.Rotate(Quaternion.Euler(45, 0, 0));

DataImpressionGroup exampleGroupWithIDAndXform = ABREngine.Instance.CreateDataImpressionGroup("Example", Guid.NewGuid());
```


> [!TIP]
> The container bounds center should almost always remain at (0, 0, 0). Handle any transformations with the Transform matrix. Or, even better, use the Transform the DataImpressionGroup GameObject is attached to.