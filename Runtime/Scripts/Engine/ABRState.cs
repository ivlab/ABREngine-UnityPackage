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
    /// <summary>
    /// The ABRStateParser takes a (text) ABR state from JSON and loads its
    /// components into Unity, or takes the current state of objects in the
    /// Unity scene and translates it back into text.
    /// </summary>
    public class ABRStateParser
    {
        /// <summary>
        /// The `LoadState` method, the workhorse of this
        /// class, has side effects that range from populating new GameObjects for
        /// data impressions, to loading new data, to loading in VisAssets. By the
        /// end of `LoadState`, the visualization should be complete.
        /// </summary>
        public async Task<JObject> LoadState<T>(string stateText, JObject previousState)
        where T : IABRStateLoader, new()
        {
            await ABREngine.Instance.WaitUntilInitialized();
            UnityThreadScheduler.GetInstance();

            JObject stateJson = await (new T()).GetState(stateText);

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
                        if (!ABREngine.Instance.VisAssets.TryGetVisAsset(visAssetUUID, out existing))
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
                        if (!ABREngine.Instance.Data.TryGetRawDataset(rawData, out existing))
                        {
                            // Try to grab from media dir
                            await ABREngine.Instance.Data.LoadRawDataset<MediaDataLoader>(rawData);

                            // ... if not found, then try to grab from Resources media dir
                            if (!ABREngine.Instance.Data.TryGetRawDataset(rawData, out existing))
                            {
                                await ABREngine.Instance.Data.LoadRawDataset<ResourcesDataLoader>(rawData);
                            }

                            // If not found in cache, load from data server, if there is one
                            if (ABREngine.Instance.Config.Info.dataServer != null && !ABREngine.Instance.Data.TryGetRawDataset(rawData, out existing))
                            {
                                await ABREngine.Instance.Data.LoadRawDataset<HttpDataLoader>(rawData);
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
                                if (!dataset.TryGetKeyData(value.inputValue, out keyData))
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
                                if (!ABREngine.Instance.Data.TryGetDataset(datasetPath, out dataset))
                                {
                                    Debug.LogWarningFormat("Unable to find dataset `{0}`", datasetPath);
                                    continue;
                                }

                                if (DataPath.FollowsConvention(value.inputValue, DataPath.DataPathType.ScalarVar))
                                {
                                    ScalarDataVariable variable;
                                    dataset.TryGetScalarVar(value.inputValue, out variable);
                                    variable.SpecificRanges.Clear(); // Will be repopulated later in state
                                    possibleInput = variable as IABRInput;
                                }
                                else if (DataPath.FollowsConvention(value.inputValue, DataPath.DataPathType.VectorVar))
                                {
                                    VectorDataVariable variable;
                                    dataset.TryGetVectorVar(value.inputValue, out variable);
                                    variable.SpecificRanges.Clear(); // Will be repopulated later in state
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
                            else if (value?.inputGenre == ABRInputGenre.PrimitiveGradient.ToString("G"))
                            {
                                // Attempt to construct a primitive gradient
                                try
                                {
                                    string uuid = value.inputValue;
                                    int? pointsLength = state?.primitiveGradients?[uuid]?.points?.Count;
                                    int? valuesLength = state?.primitiveGradients?[uuid]?.values?.Count;
                                    if (pointsLength != valuesLength || pointsLength == null || valuesLength == null)
                                    {
                                        Debug.LogError("Invalid Primitive Gradient: \"points\" and \"values\" arrays must have same length" +
                                            " and cannot be null.");
                                    }
                                    else
                                    {
                                        float[] points = new float[(int)pointsLength];
                                        string[] values = new string[(int)valuesLength];
                                        for (int i = 0; i < pointsLength; i++)
                                        {
                                            points[i] = state.primitiveGradients[uuid].points[i];
                                            values[i] = state.primitiveGradients[uuid].values[i];
                                        }
                                        possibleInput = new PrimitiveGradient(new Guid(uuid), points, values) as IABRInput;
                                    }
                                }
                                catch (KeyNotFoundException)
                                {
                                    Debug.LogErrorFormat("Invalid Primitive Gradient input: Primitive gradient with uuid {0} does not exist.", value.inputValue);
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
                        // If the input values are different a change has occurred
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
                    // Ensure that the "style changed" flag is also enabled if a colormap was edited, so either -
                    // - scalar range changed for the color variable of this impression:
                    bool scalarRangeChanged = state?.dataRanges?.Equals(previousABRState?.dataRanges) == false;
                    // OR
                    // - local vis asset colormap used by this impression had its contents changed:
                    bool colormapChanged = false;
                    JToken colormapDiff = diffFromPrevious?.SelectToken("localVisAssets");
                    if (impression.Value?.inputValues != null && impression.Value.inputValues.ContainsKey("Colormap"))
                    {
                        string colormapUuid = impression.Value.inputValues["Colormap"].inputValue;
                        if (colormapDiff?.SelectToken(colormapUuid) != null)
                            colormapChanged = true;
                    }
                    // OR
                    // - opacity map primitive gradient attached to this impression was changed -
                    bool opacityMapChanged = false;
                    JToken primitiveGradientDiff = diffFromPrevious?.SelectToken("primitiveGradients");
                    if (impression.Value?.inputValues != null && impression.Value.inputValues.ContainsKey("Opacitymap"))
                    {
                        string primitiveGradientUuid = impression.Value.inputValues["Opacitymap"].inputValue;
                        if (primitiveGradientDiff?.SelectToken(primitiveGradientUuid) != null)
                            opacityMapChanged = true;
                    }
                    // Toggle the "style changed" flag accordingly
                    if (scalarRangeChanged || colormapChanged || opacityMapChanged)
                    {
                        dataImpression.RenderHints.StyleChanged = true;
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
                // If it's a scalar var, set the globally-defined range in the data variable.
                foreach (var scalarRange in state.dataRanges.scalarRanges)
                {
                    string scalarPath = scalarRange.Key;
                    if (DataPath.FollowsConvention(scalarPath, DataPath.DataPathType.ScalarVar))
                    {
                        Dataset dataset;
                        if (ABREngine.Instance.Data.TryGetDataset(DataPath.GetDatasetPath(scalarPath), out dataset))
                        {
                            ScalarDataVariable variable;
                            if (dataset.TryGetScalarVar(scalarPath, out variable))
                            {
                                // Assign the min/max value from the state
                                variable.Range.min = scalarRange.Value.min;
                                variable.Range.max = scalarRange.Value.max;
                                variable.CustomizedRange = true;
                            }
                        }
                    }
                }
            }
            if (state.dataRanges?.specificScalarRanges != null)
            {
                // If it's key data, dig deeper to find this scalar var's actual range
                foreach (var keyDataRange in state.dataRanges.specificScalarRanges)
                {
                    string keydataPath = keyDataRange.Key;
                    if (DataPath.FollowsConvention(keydataPath, DataPath.DataPathType.KeyData))
                    {
                        foreach (var scalarRange in keyDataRange.Value)
                        {
                            Dataset dataset;
                            if (ABREngine.Instance.Data.TryGetDataset(DataPath.GetDatasetPath(scalarRange.Key), out dataset))
                            {
                                ScalarDataVariable variable;
                                if (dataset.TryGetScalarVar(scalarRange.Key, out variable))
                                {
                                    // Add this key data to the list of specific data ranges (which override the above globally-defined ranges)
                                    variable.SpecificRanges[keyDataRange.Key] = scalarRange.Value;
                                    variable.CustomizedRange = true;
                                }
                            }
                        }
                    }
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
                if (lightParent.GetComponent<VolumeLightManager>() == null)
                    lightParent.AddComponent<VolumeLightManager>();

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

        /// <summary>
        /// The SerializeState method takes the current state of the ABR unity
        /// scene and attempts to put it back into JSON form. There are several
        /// fields that aren't stored anywhere in the ABREngine, and must thus
        /// rely on the JSON version of the previous state.
        /// </summary>
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
                    scalarRanges = new Dictionary<string, DataRange<float>>(),
                    specificScalarRanges = new Dictionary<string, Dictionary<string, DataRange<float>>>()
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
                                        saveRanges.scalarRanges[inputVar.Path] = inputVar.Range;
                                        foreach (var specificRange in inputVar.SpecificRanges)
                                        {
                                            if (saveRanges.specificScalarRanges.ContainsKey(specificRange.Key))
                                            {
                                                saveRanges.specificScalarRanges[specificRange.Key][inputVar.Path] = specificRange.Value;
                                            }
                                            else
                                            {
                                                var tmp = new Dictionary<string, DataRange<float>>();
                                                tmp.Add(inputVar.Path, inputVar.Range);
                                                saveRanges.specificScalarRanges.Add(specificRange.Key, tmp);
                                            }
                                        }
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
        public Dictionary<string, RawPrimitiveGradient> primitiveGradients; 
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

        public Dictionary<string, DataRange<float>> scalarRanges;
        public Dictionary<string, Dictionary<string, DataRange<float>>> specificScalarRanges;

        public override bool Equals(object obj)
        {
            return this.Equals(obj as RawDataRanges);
        }

        public bool Equals(RawDataRanges other)
        {
            if (other == null)
            {
                // Debug.Log("other was null");
                return false;
            }
            // First, go through scalarRanges
            if (this.scalarRanges != null)
            {
                if (other.scalarRanges == null)
                {
                    // Debug.Log("other.scalarRanges was null");
                    return false;
                }
                foreach (var path in this.scalarRanges)
                {
                    if (!other.scalarRanges.ContainsKey(path.Key))
                    {
                        // Debug.Log("other.scalarRanges didn't have scalar " + path.Key);
                        return false;
                    }
                    else
                    {
                        if (!this.scalarRanges[path.Key].Equals(other.scalarRanges[path.Key]))
                        {
                            // Debug.LogFormat("other.scalarRanges key {0} didn't match {1}", other.scalarRanges[path.Key], this.scalarRanges[path.Key]);
                            return false;
                        }
                    }
                }
            }

            // Then, go through specificScalarRanges
            if (this.specificScalarRanges != null)
            {
                if (other.specificScalarRanges != null)
                {
                    // Debug.Log("other.specificScalarRanges was null");
                    return false;
                }
                foreach (var kdPath in this.specificScalarRanges)
                {
                    if (!other.specificScalarRanges.ContainsKey(kdPath.Key))
                    {
                        // Debug.Log("other.specificScalarRanges didn't have keydata path " + kdPath.Key);
                        return false;
                    }
                    else
                    {
                        var thisRanges = this.specificScalarRanges[kdPath.Key];
                        var otherRanges = other.specificScalarRanges[kdPath.Key];
                        foreach (var path in thisRanges)
                        {
                            if (!otherRanges.ContainsKey(path.Key))
                            {
                                // Debug.Log("otherRanges didn't have scalar path " + path.Key);
                                return false;
                            }
                            else
                            {
                                if (!thisRanges[path.Key].Equals(otherRanges[path.Key]))
                                {
                                    // Debug.LogFormat("otherRanges key {0} didn't match {1}", otherRanges[path.Key], thisRanges[path.Key]);
                                    return false;
                                }
                            }
                        }
                    }
                }
            }
            return true;
        }
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