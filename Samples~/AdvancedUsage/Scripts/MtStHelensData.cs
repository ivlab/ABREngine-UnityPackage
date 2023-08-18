using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;

using IVLab.Utilities;

namespace IVLab.ABREngine.Examples
{
    public class MtStHelensData : MonoBehaviour
    {
        // Constants for data files
        [SerializeField, Tooltip("Source data path where before/after grid CSVs are located")]
        private string dataFilePath = "./Assets/Samples/ABR Engine/2022.9.0/Advanced Usage/StreamingAssets";

        public string beforeFileName = "beforeGrid240x346.csv";
        public string afterFileName = "afterGrid240x346.csv";

        public const int gridX = 240;
        public const int gridY = 346;
        public bool Loaded { get; private set; } = false;

        // Lists of 3D points for you to access the point data for before/after the eruption
        [HideInInspector]
        public List<Vector3> beforePointList;
        [HideInInspector]
        public List<Vector3> afterPointList;
        [HideInInspector]
        public List<float> differences;

        // Bounding box of the above lists
        [HideInInspector]
        public Bounds pointsBounds;

        void Awake()
        {
            // Set the data file path to StreamingAssets
            // dataFilePath = Application.streamingAssetsPath;

            // Load in the data
            LoadData();
        }

        // Load Data: Same as LoadPointData from previous A5
        private void LoadData()
        {
            // Load in the raw coordinates from CSV (convert from right-hand z-up to unity's left-hand y-up)
            CoordConversion.CoordSystem rhZUp = new CoordConversion.CoordSystem(
                CoordConversion.CoordSystem.Handedness.RightHanded,
                CoordConversion.CoordSystem.Axis.PosZ,
                CoordConversion.CoordSystem.Axis.PosY
            );
            beforePointList = CSVToPoints.LoadFromCSV(Path.Combine(dataFilePath, beforeFileName), rhZUp);
            afterPointList = CSVToPoints.LoadFromCSV(Path.Combine(dataFilePath, afterFileName), rhZUp);

            // Find the data bounds
            pointsBounds = new Bounds(beforePointList[0], Vector3.zero);
            foreach (var pt in beforePointList)
            {
                pointsBounds.Encapsulate(pt);
            }

            if (beforePointList.Count != afterPointList.Count)
            {
                Debug.LogError("Before and After points must have same length!");
                return;
            }
            differences = new List<float>(beforePointList.Count);
            for (int i = 0; i < beforePointList.Count; i++)
            {
                differences.Add(afterPointList[i].y - beforePointList[i].y);
            }
            Loaded = true;
        }
    }
}