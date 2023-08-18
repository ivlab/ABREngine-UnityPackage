using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace IVLab.ABREngine.ExtensionMethods
{
    public class ScriptableObjectExtensions
    {
        /// <summary>
        /// Get all instances of scriptable objects with given type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        // http://answers.unity.com/answers/1878206/view.html
        public static List<T> GetAllInstances<T>() where T : ScriptableObject
        {
            return AssetDatabase.FindAssets($"t: {typeof(T).Name}").ToList()
                        .Select(AssetDatabase.GUIDToAssetPath)
                        .Select(AssetDatabase.LoadAssetAtPath<T>)
                        .ToList();
        }
    }
}