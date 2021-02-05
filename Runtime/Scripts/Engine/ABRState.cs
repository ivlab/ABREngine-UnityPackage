/* ABRState.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>
 *
 */

using System;
using System.Threading.Tasks;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

using IVLab.Utilities;

namespace IVLab.ABREngine
{
    public class ABRStateParser
    {
        private IABRStateLoader _loader;

        public static ABRStateParser GetParser<T>()
        where T : ResourceStateFileLoader
        {
            ResourceStateFileLoader loader = new ResourceStateFileLoader();
            ABRStateParser parser = new ABRStateParser();
            parser._loader = loader as T;
            return parser;
        }

        public async void LoadState(string name)
        {
            string stateText = _loader.GetState(name);

            RawABRState state = JsonConvert.DeserializeObject<RawABRState>(stateText);

            var assembly = Assembly.GetExecutingAssembly();
            Type dataImpressionType = typeof(DataImpression);
            List<string> impressionTypeStrings = assembly.GetTypes()
                .Where((t) => t.IsSubclassOf(dataImpressionType) && t != dataImpressionType)
                .Select((t) => t.GetCustomAttribute<ABRPlateType>()?.plateType)
                .ToList();
            List<Type> impressionTypes = assembly.GetTypes()
                .Where((t) => t.IsSubclassOf(dataImpressionType) && t != dataImpressionType)
                .Select((t) => Type.GetType(t?.FullName))
                .ToList();
            foreach (var impression in state.impressions)
            {
                // Find what type of impression to create
                string plateType = impression.Value.plateType;
                int foundIndex = impressionTypeStrings.IndexOf(plateType);
                if (foundIndex < 0)
                {
                    Debug.LogWarningFormat("Plate type `{0}` does not exist in this system", plateType);
                    continue;
                }

                Type impressionType = impressionTypes[foundIndex];

                // Should only match one
                ConstructorInfo[] constructors = impressionType.GetConstructors();
                IDataImpression dataImpression = constructors[0].Invoke(new object[0]) as IDataImpression;
                ABRInputIndexerModule impressionInputs = dataImpression.InputIndexer;

                List<ABRInputAttribute> inputs = impressionType.GetFields()
                    .Select((f) => f.GetCustomAttribute<ABRInputAttribute>())
                    .Where((f) => f != null).ToList();

                Queue<string> visAssetsToLoad = new Queue<string>();
                Queue<string> rawDataToLoad = new Queue<string>();
                foreach (var inputValue in impression.Value.inputValues)
                {
                    // If the input genre is a key data or a visasset, we need
                    // to load it
                    if (inputValue.Value.inputGenre == "KeyData")
                    {
                        rawDataToLoad.Enqueue(inputValue.Value.inputValue);
                    }
                    if (inputValue.Value.inputGenre == "VisAsset")
                    {
                        visAssetsToLoad.Enqueue(inputValue.Value.inputValue);
                    }
                    // Type inputType = Type.GetType(inputValue.Value.inputType);
                    // Debug.Log(inputType);
                    // ConstructorInfo[] inputCtor = inputType.GetConstructors();
                    // string[] args = new string[] { inputValue.Value.inputValue };
                    // IABRInput input = inputCtor[0].Invoke(args) as IABRInput;
                    // Debug.Log(input);
                }
                // foreach (var input in inputs)
                // {
                //     Debug.Log(input.inputName + input.parameterName);
                // }

                foreach (var visAsset in visAssetsToLoad)
                {
                    await UnityThreadScheduler.Instance.RunMainThreadWork(
                        () => VisAssetManager.Instance.LoadVisAsset(new Guid(visAsset))
                    );
                }

                foreach (var rawData in rawDataToLoad)
                {
                    await DataManager.Instance.LoadRawDatasetFromCache(rawData);
                }

            }
        }
    }

    /// <summary>
    ///     Raw ABR state to load from JSON. Should match the schema as much as
    ///     we need to.
    /// </summary>
    class RawABRState
    {
        public string version;
        public Dictionary<Guid, RawDataImpression> impressions;
    }

    class RawDataImpression
    {
        public string plateType;
        public Guid uuid;
        public string name;
        public RawRenderingData renderingData;
        public Dictionary<string, RawABRInput> inputValues;
    }

    class RawRenderingData { }
}