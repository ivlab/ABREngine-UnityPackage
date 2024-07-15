#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;

using UnityEngine;
using UnityEditor;

using IVLab.ABREngine.ExtensionMethods;

namespace IVLab.ABREngine
{
    public static class ABREngineHelpers
    {
        // Open the ABR Server folder in a file explorer/finder window
        [MenuItem("ABR/Open ABRServer~ Folder")]
        private static void OpenServerFolderMenu() => ABRServer.OpenServerFolder();

        // Copy the example data and VisAssets to the media folder
        [MenuItem("ABR/Import Example Data and VisAssets")]
        private static void CopyExampleData()
        {
            if (GameObject.FindAnyObjectByType<ABREngine>() == null)
            {
                Debug.LogError("Unable to copy example data and visassets. ABREngine does not exist in scene.");
                return;
            }

            // These are all the datasets and visassets provided in the
            // ABREngine-UnityPackage/Runtime/Resources/media folder
            var dataToCopy = new List<string>()
            {
                "Demo/Wavelet/KeyData/DensityRadius^5",
                "Demo/Wavelet/KeyData/FullVolume",
                "Demo/Wavelet/KeyData/InputFlow",
                "Demo/Wavelet/KeyData/OutputFlow",
                "Demo/Wavelet/KeyData/RTData100",
                "Demo/Wavelet/KeyData/RTData230",
            };

            var visAssetsToCopy = new List<string>()
            {
                "1af025aa-f1ed-11e9-a623-8c85900fd4af",
                "1af02820-f1ed-11e9-a623-8c85900fd4af",
                "3bbc2064-6a0a-11ea-9014-005056bae6d8",
                "5a761a72-8bcb-11ea-9265-005056bae6d8",
                "5ff84202-027e-11eb-8fb0-005056bae6d8",
                "6e2f8662-7905-11ea-9472-005056bae6d8",
                "66b3cde4-034d-11eb-a7e6-005056bae6d8",
                "26975e70-8bce-11ea-85c7-005056bae6d8",
                "91095a14-72c5-11ea-bfdd-005056bae6d8",
            };

            // import datasets
            var dsPath = Path.Combine(ABREngine.ConfigPrototype.mediaPath, ABRConfig.Consts.DatasetFolder);
            var tmpDataManager = new DataManager(dsPath);

            foreach (string keyDataPath in dataToCopy)
            {
                // Load the data
                RawDataset rds = tmpDataManager.LoadRawDataset(keyDataPath);

                // Then save it back to disk in the media folder
                tmpDataManager.CacheRawDataset(keyDataPath, rds);
            }

            // this makes assumptions about ABREngine's Resources folder
            // location and is not guaranteed to work.
            var vaSrcPath = Path.Combine(Application.dataPath, "../", ABREngine.PackagePath, "Runtime", "Resources", "media", ABRConfig.Consts.VisAssetFolder);
            var vaDestPath = Path.Combine(ABREngine.ConfigPrototype.mediaPath, ABRConfig.Consts.VisAssetFolder);

            // import visassets
            foreach (string uuid in visAssetsToCopy)
            {
                DirectoryInfo sourceInfo = new DirectoryInfo(Path.Combine(vaSrcPath, uuid));
                DirectoryInfo destInfo = new DirectoryInfo(Path.Combine(vaDestPath, uuid));
                if (!sourceInfo.Exists)
                {
                    Debug.LogWarning("Example VisAsset does not exist (" + sourceInfo + ")");
                    continue;
                }
                else
                {
                    sourceInfo.CopyAll(destInfo);
                    Debug.Log("Copied " + sourceInfo + " to " + destInfo);
                }
            }
        }
    }
}

#endif