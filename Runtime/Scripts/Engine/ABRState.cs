/* ABRState.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>
 *
 */

using System.Threading.Tasks;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Schema;
using JsonDiffPatchDotNet;
using IVLab.Utilities;

namespace IVLab.ABREngine
{
    public class ABRStateParser
    {
        private IABRStateLoader _loader;

        public static ABRStateParser GetParser<T>()
        where T : IABRStateLoader, new()
        {
            T loader = new T();
            ABRStateParser parser = new ABRStateParser();
            parser._loader = (T) loader;
            return parser;
        }

        public async Task<JToken> LoadState(string name, JToken previousState)
        {
            JToken stateJson = await _loader.GetState(name);

            IList<ValidationError> errors;
            if (!stateJson.IsValid(ABREngine.Instance.Config.Schema, out errors))
            {
                Debug.LogErrorFormat("State is not valid with ABR schema version {0}", ABREngine.Instance.Config.Info.version);
                foreach (var error in errors)
                {
                    Debug.LogErrorFormat("Error '{0}': Line {1} ({2}):\n    {3}", error.ErrorType, error.LineNumber, error.Path, error.Message);
                    return null;
                }
            }

            // Check the diff from the previous state
            JsonDiffPatch jdp = new JsonDiffPatch();
            JToken diffFromPrevious = jdp?.Diff(previousState, stateJson);
            stateJson = jdp.Patch(previousState, diffFromPrevious);
            JToken allImpressionsDiff = diffFromPrevious?.SelectToken("impressions");
            JObject impressionsObject = null;
            // If it's an array, that means it's either been deleted or created
            if (allImpressionsDiff != null && allImpressionsDiff.Type == JTokenType.Array)
            {
                // if (allImpressionsDiff.ToArray().Where((t) => t.Type ==
                // JTokenType.Integer).Count((t) => (int) t == 0) == 2)
                // {
                    ABREngine.Instance.ClearState();
                    return stateJson;
                // }
            }
            else
            {
                impressionsObject = allImpressionsDiff?.ToObject<JObject>();
            }

            if (impressionsObject != null)
            {
                foreach (var impression in impressionsObject)
                {
                    var impressionDiff = impression.Value;
                    // The original JsonDiffPatch specifies that when something
                    // is removed, there will be 2 zeroes.
                    // https://github.com/benjamine/jsondiffpatch/blob/master/docs/deltas.md
                    var removed = impressionDiff.Type == JTokenType.Array &&
                            impressionDiff.ToArray().Where((t) => t.Type ==
                            JTokenType.Integer).Count((t) => (int) t == 0) == 2;
                    if (removed)
                    {
                        ABREngine.Instance.UnregisterDataImpression(new Guid(impression.Key));
                    }
                }
            }

            RawABRState state = stateJson.ToObject<RawABRState>();
            if (state == null || state.impressions == null)
            {
                return null;
            }

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

                if (impressionsObject != null)
                {
                    bool changed = impressionsObject.ContainsKey(impression.Key);
                    if (!changed)
                    {
                        // Skip this impression if it hasn't been changed
                        continue;
                    }
                }

                // Find what type of impression to create
                string plateType = impression.Value?.plateType;
                int foundIndex = impressionTypeStrings.IndexOf(plateType);
                if (foundIndex < 0)
                {
                    Debug.LogWarningFormat("Plate type `{0}` does not exist in this system", plateType);
                    continue;
                }

                Queue<string> visAssetsToLoad = new Queue<string>();
                Queue<string> rawDataToLoad = new Queue<string>();
                if (impression.Value?.inputValues != null)
                {
                    foreach (var inputValue in impression.Value.inputValues)
                    {
                        // If the input genre is a key data or a visasset, we need
                        // to load it
                        if (inputValue.Value.inputGenre == ABRInputGenre.KeyData)
                        {
                            rawDataToLoad.Enqueue(inputValue.Value.inputValue);
                        }
                        if (inputValue.Value.inputGenre == ABRInputGenre.VisAsset)
                        {
                            visAssetsToLoad.Enqueue(inputValue.Value.inputValue);
                        }
                    }
                }

                foreach (var visAsset in visAssetsToLoad)
                {
                    // See if we already have the VisAsset; if not then load it
                    var visAssetUUID = new Guid(visAsset);
                    IVisAsset existing;
                    ABREngine.Instance.VisAssets.TryGetVisAsset(visAssetUUID, out existing);
                    if (existing == null)
                    {
                        await UnityThreadScheduler.Instance.RunMainThreadWork(
                            () => ABREngine.Instance.VisAssets.LoadVisAsset(visAssetUUID)
                        );
                    }
                }

                foreach (var rawData in rawDataToLoad)
                {
                    // See if we already have the Raw Dataset; if not then load it
                    RawDataset existing;
                    ABREngine.Instance.Data.TryGetRawDataset(rawData, out existing);
                    if (existing == null)
                    {
                        await ABREngine.Instance.Data.LoadRawDatasetFromCache(rawData);
                    }
                }

                // Change any variable ranges that appear in the state
                if (state.dataRanges?.scalarRanges != null)
                {
                    foreach (var scalarRange in state.dataRanges.scalarRanges)
                    {
                        // Get the variable
                        string scalarPath = scalarRange.Key;
                        DataPath.WarnOnDataPathFormat(scalarPath, DataPath.DataPathType.ScalarVar);
                        Dataset dataset;
                        ABREngine.Instance.Data.TryGetDataset(DataPath.GetDatasetPath(scalarPath), out dataset);
                        ScalarDataVariable variable;
                        dataset.TryGetScalarVar(scalarPath, out variable);

                        // Assign the min/max value from the state
                        variable.MinValue = scalarRange.Value.min;
                        variable.MaxValue = scalarRange.Value.max;
                    }
                }

                // Make the data impression; should only match one type
                Type impressionType = impressionTypes[foundIndex];
                ConstructorInfo impressionCtor =
                    impressionType.GetConstructor(new Type[] { typeof(string) });
                string[] impressionArgs = new string[] { impression.Value.uuid };
                IDataImpression dataImpression = impressionCtor.Invoke(impressionArgs) as IDataImpression;
                ABRInputIndexerModule impressionInputs = dataImpression.InputIndexer;

                List<ABRInputAttribute> actualInputs = impressionType.GetFields()
                    .Select((f) => f.GetCustomAttribute<ABRInputAttribute>())
                    .Where((f) => f != null).ToList();

                // Now that everything is loaded, go ahead and populate the state
                if (impression.Value?.inputValues != null)
                {
                    foreach (var inputName in ABREngine.Instance.Config.GetInputNames(plateType))
                    {
                        RawABRInput value = null;
                        if (impression.Value.inputValues.ContainsKey(inputName))
                        {
                            value  = impression.Value.inputValues[inputName];
                        }
                        IABRInput possibleInput = null;
                        if (value?.inputGenre == ABRInputGenre.KeyData)
                        {
                            string datasetPath = DataPath.GetDatasetPath(value.inputValue);
                            Dataset dataset;
                            ABREngine.Instance.Data.TryGetDataset(datasetPath, out dataset);
                            if (dataset == null)
                            {
                                Debug.LogWarningFormat("Unable to find dataset `{0}`", datasetPath);
                                continue;
                            }
                            IKeyData keyData;
                            dataset.TryGetKeyData(value.inputValue, out keyData);
                            if (keyData == null)
                            {
                                Debug.LogWarningFormat("Unable to find Key Data `{0}`", value.inputValue);
                                continue;
                            }
                            possibleInput = keyData as IABRInput;
                        }
                        else if (value?.inputGenre == ABRInputGenre.Variable)
                        {
                            string datasetPath = DataPath.GetDatasetPath(value.inputValue);
                            Dataset dataset;
                            ABREngine.Instance.Data.TryGetDataset(datasetPath, out dataset);
                            if (dataset == null)
                            {
                                Debug.LogWarningFormat("Unable to find dataset `{0}`", datasetPath);
                                continue;
                            }

                            if (DataPath.FollowsConvention(value.inputValue, DataPath.DataPathType.ScalarVar))
                            {
                                ScalarDataVariable variable;
                                dataset.TryGetScalarVar(value.inputValue, out variable);
                                possibleInput = variable as IABRInput;
                            }
                            else if (DataPath.FollowsConvention(value.inputValue, DataPath.DataPathType.ScalarVar))
                            {
                                VectorDataVariable variable;
                                dataset.TryGetVectorVar(value.inputValue, out variable);
                                possibleInput = variable as IABRInput;
                            }

                            if (possibleInput == null)
                            {
                                Debug.LogWarningFormat("Unable to find variable `{0}`", value.inputValue);
                            }
                        }
                        else if (value?.inputGenre == ABRInputGenre.VisAsset)
                        {
                            IVisAsset visAsset = null;
                            await UnityThreadScheduler.Instance.RunMainThreadWork(() => 
                            {
                                ABREngine.Instance.VisAssets.TryGetVisAsset(new Guid(value.inputValue), out visAsset);
                            });
                            if (visAsset == null)
                            {
                                Debug.LogWarningFormat("Unable to find VisAsset `{0}`", value.inputValue);
                                continue;
                            }
                            possibleInput = visAsset as IABRInput;
                        }
                        else if (value?.inputGenre == ABRInputGenre.Primitive)
                        {
                            // Attempt to construct the primitive from the type
                            // provided in the state file
                            Type inputType = Type.GetType(value.inputType);
                            ConstructorInfo inputCtor =
                                inputType.GetConstructor(
                                    BindingFlags.Instance | BindingFlags.Public,
                                    null,
                                    CallingConventions.HasThis,
                                    new Type[] { typeof(string) },
                                    null
                            );
                            string[] args = new string[] { value.inputValue };
                            possibleInput = inputCtor?.Invoke(args) as IABRInput;
                            if (possibleInput == null)
                            {
                                Debug.LogWarningFormat("Unable to create primitive `{0}`", value.inputValue);
                            }
                        }

                        // Verify that we have something to put in the input
                        if (possibleInput != null)
                        {
                            // Verify that the input matches with the parameter (to
                            // avoid possible name collisions), and check that it's
                            // assignable from the possibleInput
                            var actualInput = actualInputs.First((i) => inputName == i.inputName && i.parameterName == value.parameterName);
                            if (impressionInputs.CanAssignInput(inputName, possibleInput) && actualInput != null)
                            {
                                impressionInputs.AssignInput(inputName, possibleInput);
                            }
                        }
                        else
                        {
                            // If not, then assign the input to null
                            impressionInputs.AssignInput(inputName, null);
                        }
                    }
                }

                // Put the impressions in their proper groups, if any
                bool registered = false;
                if (state.scene != null)
                {
                    foreach (var group in state.scene.impressionGroups)
                    {
                        if (group.Value.impressions.Contains(dataImpression.Uuid))
                        {
                            DataImpressionGroup g = ABREngine.Instance.GetDataImpressionGroup(group.Value.uuid);
                            if (g == null)
                            {
                                g = ABREngine.Instance.AddDataImpressionGroup(
                                    group.Value.name,
                                    group.Value.uuid,
                                    group.Value.containerBounds,
                                    group.Value.rootPosition,
                                    group.Value.rootRotation
                                );
                            }
                            ABREngine.Instance.RegisterDataImpression(dataImpression, g, true);
                            registered = true;
                        }
                    }
                }

                // If no groups specified, let the Engine handle it
                if (!registered)
                {
                    ABREngine.Instance.RegisterDataImpression(dataImpression, true);
                }
            }

            return stateJson;
        }
    }

    /// <summary>
    ///     Raw ABR state to load from JSON. Should match the schema as much as
    ///     we need to.
    /// </summary>
    class RawABRState
    {
        public string version;
        public Dictionary<string, RawDataImpression> impressions;
        public RawScene scene;
        public RawDataRanges dataRanges;
    }

    class RawDataImpression
    {
        public string plateType;
        public string uuid;
        public string name;
        public RawRenderingData renderingData;
        public Dictionary<string, RawABRInput> inputValues;
    }

    class RawScene
    {
        public Dictionary<string, RawImpressionGroup> impressionGroups;
    }

    class RawDataRanges
    {
        public class RawRange<T>
        {
            public T min;
            public T max;
        }

        public Dictionary<string, RawRange<float>> scalarRanges;
    }

    class RawImpressionGroup
    {
        public List<Guid> impressions;
        public string name;
        public Guid uuid;
        public Bounds containerBounds;
        public Vector3 rootPosition;
        public Quaternion rootRotation;
        // Not including scale because that would mess with artifacts
    }

    class RawRenderingData { }
}