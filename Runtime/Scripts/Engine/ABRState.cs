/* ABRState.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
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
            parser._loader = (T)loader;
            return parser;
        }

        public async Task<JObject> LoadState(string name, JObject previousState)
        {
            await ABREngine.Instance.WaitUntilInitialized();
            UnityThreadScheduler.GetInstance();

            JObject stateJson = await _loader.GetState(name);

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
            JToken allImpressionsDiff = diffFromPrevious?.SelectToken("impressions");
            JObject impressionsObject = null;

            // If it's an array, that means it's either been deleted or created
            if (allImpressionsDiff != null && allImpressionsDiff.Type == JTokenType.Array)
            {
                ABREngine.Instance.ClearState();
                return stateJson;
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
                            JTokenType.Integer).Count((t) => (int)t == 0) == 2;
                    if (removed)
                    {
                        ABREngine.Instance.UnregisterDataImpression(new Guid(impression.Key));
                    }
                }
            }

            // Generate ABR states from both the previous and current state json objects
            RawABRState previousABRState = previousState?.ToObject<RawABRState>();
            RawABRState state = stateJson.ToObject<RawABRState>();
            if (state == null)
            {
                return null;
            }

            // Populate the visasset manager with any local visassets
            if (stateJson.ContainsKey("localVisAssets"))
            {
                ABREngine.Instance.VisAssets.LocalVisAssets = stateJson["localVisAssets"].ToObject<JObject>();
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

            if (state.impressions != null)
            {
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
                        foreach (var inputValue in impression.Value.inputValues.Values)
                        {
                            // If the input genre is a key data or a visasset, we need
                            // to load it
                            if (inputValue.inputGenre == ABRInputGenre.KeyData.ToString("G"))
                            {
                                rawDataToLoad.Enqueue(inputValue.inputValue);
                            }
                            if (inputValue.inputGenre == ABRInputGenre.VisAsset.ToString("G"))
                            {
                                visAssetsToLoad.Enqueue(inputValue.inputValue);
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
                            await ABREngine.Instance.VisAssets.LoadVisAsset(visAssetUUID);
                        }

                        // Re-import if it's a LocalVisAsset
                        if (existing != null
                            && ABREngine.Instance.VisAssets.LocalVisAssets != null
                            && ABREngine.Instance.VisAssets.LocalVisAssets.ContainsKey(existing.Uuid.ToString())
                        )
                        {
                            await ABREngine.Instance.VisAssets.LoadVisAsset(visAssetUUID, true);
                        }
                    }

                    foreach (var rawData in rawDataToLoad)
                    {
                        // See if we already have the Raw Dataset; if not then load it
                        RawDataset existing;
                        ABREngine.Instance.Data.TryGetRawDataset(rawData, out existing);
                        if (existing == null)
                        {
                            // Try to grab from cache
                            await ABREngine.Instance.Data.LoadRawDatasetFromCache(rawData);
                            ABREngine.Instance.Data.TryGetRawDataset(rawData, out existing);

                            // If not found in cache, load from data server, if there is one
                            if (ABREngine.Instance.Config.Info.dataServer != null && existing == null)
                            {
                                await ABREngine.Instance.Data.LoadRawDatasetFromURL(rawData, ABREngine.Instance.Config.Info.dataServer);
                            }
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
                                value = impression.Value.inputValues[inputName];
                            }
                            IABRInput possibleInput = null;
                            if (value?.inputGenre == ABRInputGenre.KeyData.ToString("G"))
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
                            else if (value?.inputGenre == ABRInputGenre.Variable.ToString("G"))
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
                            else if (value?.inputGenre == ABRInputGenre.VisAsset.ToString("G"))
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
                            else if (value?.inputGenre == ABRInputGenre.Primitive.ToString("G"))
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

                    // Add any rendering hints
                    if (impression.Value.renderHints != null)
                    {
                        dataImpression.RenderHints = impression.Value.renderHints;
                    }

                    // Attempt to find the previous version of the current impression in the previous abr state 
                    RawDataImpression previousImpression = null;
                    if (previousABRState?.impressions != null)
                    {
                        foreach (var prevImpression in previousABRState.impressions)
                        {
                            if (prevImpression.Value?.uuid == impression.Value?.uuid)
                            {
                                previousImpression = prevImpression.Value;
                                break;
                            }
                        }
                    }
                    // Enable the visibility changed flag if the previous visibility of the impression is different 
                    // from the current visibility
                    bool? previousVisibility = previousImpression?.renderHints?.Visible;
                    bool? currentVisibility = impression.Value?.renderHints?.Visible;
                    if (previousVisibility != currentVisibility)
                    {
                        dataImpression.RenderHints.VisibilityChanged = true;
                    }
                    // Forcefully disable the visibility changed flag otherwise (this is done to counteract the automatic
                    // enabling of "VisibilityChanged" that occurs when visibility is set to false from the loading
                    // of a json state object)
                    else
                    {
                        dataImpression.RenderHints.VisibilityChanged = false;
                    }
                    // Obtain the input values of the previous version of the current impression, if it exists
                    Dictionary<string, RawABRInput> previousInputValues = previousImpression?.inputValues;
                    // Compare the previous input values to the current input values of the impression 
                    // and enable "Changed" flags for any differences
                    foreach (var input in actualInputs)
                    {
                        RawABRInput currentInput = null;
                        if (impression.Value?.inputValues != null && impression.Value.inputValues.ContainsKey(input.inputName))
                        {
                            currentInput = impression.Value.inputValues[input.inputName];
                        }
                        RawABRInput previousInput = null;
                        if (previousInputValues != null && previousInputValues.ContainsKey(input.inputName))
                        {
                            previousInput = previousInputValues[input.inputName];
                        }
                        // If the input values are different a change has occured
                        if (currentInput?.inputValue != previousInput?.inputValue)
                        {
                            // Enable changed flags according to the input that was changed                      
                            if (input.updateLevel == UpdateLevel.Data)
                            {
                                dataImpression.RenderHints.DataChanged = true;
                            }
                            else if (input.updateLevel == UpdateLevel.Style)
                            {
                                dataImpression.RenderHints.StyleChanged = true;
                            }
                        }
                    }

                    // Add any tags
                    if (impression.Value.tags != null)
                    {
                        (dataImpression as DataImpression).Tags = impression.Value.tags;
                    }

                    // Put the impressions in their proper groups, if any
                    bool registered = false;
                    if (state?.scene?.impressionGroups != null)
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
                    variable.CustomizedRange = true;
                }
            }

            // Create/update lights, carefully select/create by name
            if (state?.scene?.lighting != null)
            {
                GameObject lightParent = GameObject.Find("ABRLightParent");
                if (lightParent == null)
                {
                    lightParent = new GameObject("ABRLightParent");
                    lightParent.transform.parent = GameObject.Find("ABREngine").transform;
                }

                foreach (var light in state.scene.lighting)
                {
                    GameObject existing = GameObject.Find(light.name);
                    if (existing == null)
                    {
                        existing = new GameObject(light.name);
                        existing.transform.parent = lightParent.transform;
                    }

                    existing.transform.localPosition = light.position;
                    existing.transform.localRotation = light.rotation;

                    Light lightComponent;
                    if (!existing.TryGetComponent<Light>(out lightComponent))
                    {
                        lightComponent = existing.AddComponent<Light>();
                    }

                    lightComponent.intensity = light.intensity;
                    lightComponent.type = LightType.Directional;
                    lightComponent.shadows = LightShadows.None;
                }

                List<string> lightsInState = state.scene.lighting.Select((l) => l.name).ToList();

                foreach (Transform light in lightParent.transform)
                {
                    if (!lightsInState.Contains(light.gameObject.name))
                    {
                        GameObject.Destroy(light.gameObject);
                    }
                }
            }

            if (state?.scene?.backgroundColor != null)
            {
                Camera.main.backgroundColor = IVLab.Utilities.ColorUtilities.HexToColor(state.scene.backgroundColor);
            }

            return stateJson;
        }

        public string SerializeState(JObject previousState)
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();

                RawABRState saveState = new RawABRState();
                saveState.impressions = new Dictionary<string, RawDataImpression>();
                saveState.version = previousState["version"].ToString();
                RawScene saveScene = new RawScene
                {
                    impressionGroups = new Dictionary<string, RawImpressionGroup>(),
                    lighting = new List<RawLight>()
                };
                RawDataRanges saveRanges = new RawDataRanges
                {
                    scalarRanges = new Dictionary<string, RawDataRanges.RawRange<float>>()
                };

                // Populate data impressions and groups
                foreach (var group in ABREngine.Instance.GetDataImpressionGroups().Values)
                {
                    RawImpressionGroup saveGroup = new RawImpressionGroup();

                    // Save the group info
                    saveGroup.name = group.Name;
                    saveGroup.rootPosition = group.GroupRoot.transform.localPosition;
                    saveGroup.rootRotation = group.GroupRoot.transform.localRotation;
                    saveGroup.containerBounds = group.GroupContainer;
                    saveGroup.uuid = group.Uuid;
                    saveGroup.impressions = new List<Guid>();

                    // Go through each impression
                    foreach (var impression in group.GetDataImpressions().Values)
                    {
                        RawDataImpression saveImpression = new RawDataImpression();

                        // Retrieve easy values
                        string guid = impression.Uuid.ToString();
                        saveImpression.uuid = guid;
                        if (previousState["impressions"].ToObject<JObject>().ContainsKey(guid))
                        {
                            saveImpression.name = previousState["impressions"][guid]["name"].ToString();
                        }
                        else
                        {
                            saveImpression.name = "DataImpression";
                        }
                        saveImpression.renderHints = impression.RenderHints;
                        saveImpression.tags = (impression as DataImpression).Tags;

                        // Retrieve the plate type
                        saveImpression.plateType = assembly.GetTypes()
                            .First((t) => t == impression.GetType())
                            .GetCustomAttribute<ABRPlateType>().plateType;

                        // Retrieve inputs
                        ABRInputIndexerModule impressionInputs = impression.InputIndexer;
                        string[] inputNames = impressionInputs.InputNames;
                        Dictionary<string, RawABRInput> saveInputs = new Dictionary<string, RawABRInput>();

                        List<ABRInputAttribute> actualInputs = impression
                            .GetType()
                            .GetFields()
                            .Select((f) => f.GetCustomAttribute<ABRInputAttribute>())
                            .Where((f) => f != null).ToList();

                        foreach (var inputName in inputNames)
                        {
                            IABRInput input = impressionInputs.GetInputValue(inputName);
                            if (input != null)
                            {
                                RawABRInput saveInput = input.GetRawABRInput();
                                saveInput.parameterName = actualInputs
                                    .First((i) => i.inputName == inputName).parameterName;
                                saveInputs[inputName] = saveInput;

                                // If it's a variable, gather the custom min/max if
                                // they've been changed
                                if (input.GetType() == typeof(ScalarDataVariable))
                                {
                                    ScalarDataVariable inputVar = input as ScalarDataVariable;
                                    if (inputVar.CustomizedRange)
                                    {
                                        RawDataRanges.RawRange<float> scalarRange = new RawDataRanges.RawRange<float>
                                        {
                                            min = inputVar.MinValue,
                                            max = inputVar.MaxValue,
                                        };
                                        saveRanges.scalarRanges[inputVar.Path] = scalarRange;
                                    }
                                }
                            }
                        }

                        saveImpression.inputValues = saveInputs;
                        saveGroup.impressions.Add(impression.Uuid);
                        saveState.impressions[impression.Uuid.ToString()] = saveImpression;
                    }

                    saveScene.impressionGroups[saveGroup.uuid.ToString()] = saveGroup;
                }

                // Update the lights
                GameObject lightParent = GameObject.Find("ABRLightParent");
                if (lightParent != null)
                {
                    foreach (Transform light in lightParent.transform)
                    {
                        Light l = light.GetComponent<Light>();
                        saveScene.lighting.Add(new RawLight
                        {
                            name = light.gameObject.name,
                            intensity = l.intensity,
                            position = light.localPosition,
                            rotation = light.localRotation,
                        });
                    }
                }

                saveScene.backgroundColor = IVLab.Utilities.ColorUtilities.ColorToHex(Camera.main.backgroundColor);

                saveState.scene = saveScene;
                saveState.dataRanges = saveRanges;

                JsonSerializerSettings settings = new JsonSerializerSettings();
                settings.NullValueHandling = NullValueHandling.Ignore;
                settings.Formatting = Formatting.Indented;
                settings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;

                if (previousState.ContainsKey("uiData"))
                {
                    saveState.uiData = previousState["uiData"];
                }

                if (previousState.ContainsKey("localVisAssets"))
                {
                    saveState.localVisAssets = previousState["localVisAssets"];
                }

                if (previousState.ContainsKey("name"))
                {
                    saveState.name = previousState["name"].ToString();
                }

                return JsonConvert.SerializeObject(saveState, settings);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                return null;
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
        public Dictionary<string, RawDataImpression> impressions;
        public RawScene scene;
        public RawDataRanges dataRanges;
        public JToken uiData; // data for UIs, not messing with it at all
        public JToken localVisAssets; // custom vis assets, not messing with them at all
        public string name;
    }

    class RawLight
    {
        public string name;
        public float intensity;
        public Vector3 position;
        public Quaternion rotation;
    }

    class RawDataImpression
    {
        public string plateType;
        public string uuid;
        public string name;
        public RenderHints renderHints;
        public List<string> tags;
        public Dictionary<string, RawABRInput> inputValues;
    }

    class RawScene
    {
        public Dictionary<string, RawImpressionGroup> impressionGroups;
        public List<RawLight> lighting;
        public string backgroundColor;
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
}