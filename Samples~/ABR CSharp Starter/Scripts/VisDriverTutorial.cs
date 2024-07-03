using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IVLab.ABREngine;

/// <summary>
/// This is the completed version of the VisDriver class shown in the @abr-cs.md
/// tutorial.
/// </summary>
public class VisDriverTutorial : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        // 1. Load the dataset from disk (see ABREngine-UnityPackage/Runtime/Resources/media/datasets) for the raw data files
        // (this is the data that was loaded when you clicked *ABR > Copy Example Data to Media Folder* earlier!)
        string contourDataPath = "Demo/Wavelet/KeyData/RTData230";
        KeyData contour = ABREngine.Instance.Data.LoadData(contourDataPath);

        // 1.b. Import a Colormap VisAsset
        ColormapVisAsset cmap = ABREngine.Instance.VisAssets.LoadVisAsset<ColormapVisAsset>(new System.Guid("5a761a72-8bcb-11ea-9265-005056bae6d8"));




        // 2. Create surface data impression and assign key data
        SimpleSurfaceDataImpression surf = DataImpression.Create<SimpleSurfaceDataImpression>("Contour Impression");
        surf.keyData = contour;

        // 2.b. Assign colormap to data impression
        surf.colormap = cmap;

        // 2.c. Assign color variable to data impression
        surf.colorVariable = contour.GetScalarVariable("XAxis");




        // 3.a. Register the data impression so ABR knows about it
        ABREngine.Instance.RegisterDataImpression(surf);

        // 3.b. Render the visualization
        ABREngine.Instance.Render();
    }
}
