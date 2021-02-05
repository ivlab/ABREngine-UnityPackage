/* ABRStateLoader.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>
 *
 */

using System.IO;
using UnityEngine;

namespace IVLab.ABREngine
{
    public interface IABRStateLoader
    {
        string GetState(string name);
    }

    public class ResourceStateFileLoader : IABRStateLoader
    {
        public ResourceStateFileLoader() { }

        public string GetState(string fileName)
        {
            string name = Path.GetFileNameWithoutExtension(fileName);
            TextAsset textAsset = Resources.Load<TextAsset>(name);
            return textAsset.text;
        }
    }
}