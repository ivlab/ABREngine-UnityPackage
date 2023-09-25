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

using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Schema;
using JsonDiffPatchDotNet;
using UnityEditor;


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
        /// end of `LoadState`, the visualization should be complete in the
        /// Unity scene.
        /// </summary>
        public JObject LoadState<T>(string stateText, JObject previousState)
        where T : IABRStateLoader, new()
        {
            if (stateText == null)
            {
                return null;
            }

            JObject stateJson = new T().GetState(stateText);

            if (stateJson == null)
            {
                return null;
            }

            IList<ValidationError> errors;
            if (!stateJson.IsValid(ABREngine.Instance.Config.Schema, out errors))
            {
                Debug.LogErrorFormat("State is not valid with this version of ABR");
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

            // If >2 values in previous have >2 zeroes, we may assume that the state has been cleared.
            // https://github.com/benjamine/jsondiffpatch/blob/master/docs/deltas.md
            if (diffFromPrevious != null && diffFromPrevious.Type == JTokenType.Object)
            {
                int zeroes = 0;
                JObject diffObject = diffFromPrevious.ToObject<JObject>();
                foreach (var token in diffObject)
                {
                    JToken diff = token.Value;
                    if (diff.Type == JTokenType.Array)
                    {
                        zeroes += diff.Where(t => t.Type == JTokenType.Integer).Count(t => (int)t == 0);
                    }
                }
                if (zeroes >= 4)
                {
                    Debug.Log("Clearing ABR state");
                    ABREngine.Instance.ClearState();
                }
            }

            // If it's an object, that means some impressions have changed
            if (allImpressionsDiff != null && allImpressionsDiff.Type == JTokenType.Object)
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

            // Populate the visasset manager with any local visassets and gradients
            if (stateJson.ContainsKey("localVisAssets"))
            {
                ABREngine.Instance.VisAssets.LocalVisAssets = stateJson["localVisAssets"].ToObject<JObject>();
            }
            if (state.visAssetGradients != null)
            {
                ABREngine.Instance.VisAssets.VisAssetGradients = state.visAssetGradients;
            }

            // Handle new/updated VisAssets, LocalVisAssets and VisAssetGradients
            // Do this here so that we don't import them every time for every
            // data impression, which is EXPENSIVE
            JToken visAssetGradientDiff = diffFromPrevious?.SelectToken("visAssetGradients");
            JToken localVisAssetDiff = diffFromPrevious?.SelectToken("localVisAssets");
            List<Guid> visAssetsToUpdate = new List<Guid>();
            if (visAssetGradientDiff != null && visAssetGradientDiff.Type == JTokenType.Object)
            {
                visAssetsToUpdate.AddRange(visAssetGradientDiff.Select((k) => new Guid((k as JProperty).Name)));
            }
            if (localVisAssetDiff != null && localVisAssetDiff.Type == JTokenType.Object)
            {
                visAssetsToUpdate.AddRange(localVisAssetDiff.Select((k) => new Guid((k as JProperty).Name)));
            }
            foreach (Guid visAssetUUID in visAssetsToUpdate)
            {
                IVisAsset existing = null;
                ABREngine.Instance.VisAssets.TryGetVisAsset(visAssetUUID, out existing);
                if (existing != null
                    && ((ABREngine.Instance.VisAssets.LocalVisAssets?.ContainsKey(existing.Uuid.ToString()) ?? false)
                    || ABREngine.Instance.VisAssets.VisAssetGradients.ContainsKey(existing.Uuid.ToString()))
                )
                {
                    ABREngine.Instance.VisAssets.LoadVisAsset(visAssetUUID, true);
                }
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
                        // This isn't optimial because it still updates EVERY
                        // impression if there are no impressions changes
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
                        // See if we already have the VisAsset; if not then load it.
                        // LocalVisAssets and Gradients should already be up-to-date at this point.
                        var visAssetUUID = new Guid(visAsset);
                        IVisAsset existing;
                        if (!ABREngine.Instance.VisAssets.TryGetVisAsset(visAssetUUID, out existing))
                        {
                            existing = ABREngine.Instance.VisAssets.LoadVisAsset(visAssetUUID);
                        }
                    }

                    foreach (var rawData in rawDataToLoad)
                    {
                        // See if we already have the Raw Dataset; if not then load it
                        RawDataset existing;
                        if (!ABREngine.Instance.Data.TryGetRawDataset(rawData, out existing))
                        {
                            // Try to load and import the dataset if not found
                            ABREngine.Instance.Data.LoadData(rawData);
                        }
                    }

                    // Make the data impression; should only match one type
                    // use the generic `Create` method on the type.
                    Type impressionType = impressionTypes[foundIndex];
                    MethodInfo createMethod = typeof(DataImpression).GetMethod("Create", BindingFlags.Static | BindingFlags.Public);
                    createMethod = createMethod.MakeGenericMethod(new Type[] { impressionType });

                    // args are: UUID, name, syncWithServer
                    object[] impressionArgs = new object[] { new Guid(impression.Value.uuid), impression.Value.name, true };
                    DataImpression dataImpression = createMethod.Invoke(null, impressionArgs) as DataImpression;
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

                            // Try to convert to ABR input
                            IABRInput possibleInput = value?.ToABRInput();

                            // Special case: need to use values from State to
                            // create PrimitiveGradients (in theory, the only
                            // place we'll be doing this...)
                            if (possibleInput == null && value?.inputGenre == ABRInputGenre.PrimitiveGradient.ToString("G"))
                            {
                                // Attempt to construct a primitive gradient
                                try
                                {
                                    string uuid = value.inputValue;
                                    int? pointsLength = state?.primitiveGradients?[uuid]?.points?.Count;
                                    int? valuesLength = state?.primitiveGradients?[uuid]?.values?.Count;
                                    if (pointsLength != valuesLength || pointsLength == null || valuesLength == null)
                                    {
                                        Debug.LogWarning("Invalid Primitive Gradient: \"points\" and \"values\" arrays must have same length" +
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
                                    Debug.LogWarningFormat("Invalid Primitive Gradient input: Primitive gradient with uuid {0} does not exist.", value.inputValue);
                                }
                            }

                            // Verify that we have something to put in the input
                            if (possibleInput != null)
                            {
                                // Verify that the input is assignable from the possibleInput
                                var actualInput = actualInputs.First((i) => inputName == i.inputName);
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

                    // Ensure that the "style changed" flag is also enabled if a colormap was edited, so either -
                    // - scalar range changed for the color variable of this impression:
                    JToken dataRangeDiff = diffFromPrevious?.SelectToken("dataRanges");
                    bool dataRangeChanged = dataRangeDiff != null;
                    // OR
                    // - local vis asset colormap used by this impression had its contents changed:
                    bool colormapChanged = false;
                    if (impression.Value?.inputValues != null && impression.Value.inputValues.ContainsKey("Colormap"))
                    {
                        string colormapUuid = impression.Value.inputValues["Colormap"].inputValue;
                        if (localVisAssetDiff?.SelectToken(colormapUuid) != null)
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
                    if (dataRangeChanged || colormapChanged || opacityMapChanged)
                    {
                        dataImpression.RenderHints.StyleChanged = true;
                    }

                    // React specially to gradient inputs - if gradient changed,
                    // need to select which type of change to trigger (glyphs
                    // behave differently than others)
                    // TODO: We should start supporting a notion of "input
                    // dependencies" - i.e. if a gradient or local visasset has
                    // changed, make sure the proper ABR update methods are
                    // called.
                    if (impression.Value?.inputValues != null)
                    {
                        string[] gradientTypes = assembly.GetTypes()
                            .Where(t => typeof(VisAssetGradient).IsAssignableFrom(t))
                            .Where(t => typeof(VisAssetGradient) != t)
                            .Select(t => t.ToString())
                            .ToArray();
                        var gradientInputNames = impression.Value.inputValues
                            .Where(kv => gradientTypes.Contains(kv.Value.inputType))
                            .Select(kv => kv.Key);
                        var actualGradientInputs = actualInputs
                            .Where(i => gradientInputNames.Contains(i.inputName));
                        foreach (ABRInputAttribute gradientInput in actualGradientInputs)
                        {
                            if (impression.Value?.inputValues != null && impression.Value.inputValues.ContainsKey(gradientInput.inputName))
                            {
                                RawABRInput gradInput = impression.Value.inputValues[gradientInput.inputName];
                                Guid gradientUuid = new Guid(gradInput.inputValue);
                                if (visAssetsToUpdate.Contains(gradientUuid))
                                {
                                    if (gradientInput.updateLevel == UpdateLevel.Data)
                                    {
                                        dataImpression.RenderHints.DataChanged = true;
                                    }
                                    else if (gradientInput.updateLevel == UpdateLevel.Style)
                                    {
                                        dataImpression.RenderHints.StyleChanged = true;
                                    }
                                }
                            }
                        }
                    }

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

                    // Add any tags
                    if (impression.Value.tags != null)
                    {
                        dataImpression.Tags = impression.Value.tags;
                    }

                    // Hide/show the data impression in scene if it has data
                    dataImpression.gameObject.SetActive(dataImpression.GetKeyData() != null);

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
                                    g = ABREngine.Instance.CreateDataImpressionGroup(
                                        group.Value.name,
                                        group.Value.uuid,
                                        group.Value.containerBounds,
                                        group.Value.transformMatrix
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

            // Create/update lights, carefully select/create by name (anywhere in the scene)
            if (state?.scene?.lighting != null)
            {
                List<ABRLight> existingSceneLights = MonoBehaviour.FindObjectsOfType<ABRLight>().ToList();
                ABRLightManager lightManager = MonoBehaviour.FindObjectOfType<ABRLightManager>();
                foreach (var light in state.scene.lighting)
                {
                    // Carefully select ABRLight by its name
                    ABRLight existing = existingSceneLights.Find(l => l.name == light.name);

                    // If not found, create a new one under ABREngine or ABRLightManager
                    if (existing == null)
                    {
                        GameObject go = new GameObject(light.name);
                        existing = go.AddComponent<ABRLight>();
                        if (lightManager != null)
                        {
                            existing.transform.SetParent(lightManager.transform);
                        }
                        else
                        {
                            existing.transform.SetParent(ABREngine.Instance.transform);
                        }
                    }

                    existing.transform.localPosition = light.position;
                    existing.transform.localRotation = light.rotation;

                    Light lightComponent;
                    if (!existing.TryGetComponent<Light>(out lightComponent))
                    {
                        lightComponent = existing.gameObject.AddComponent<Light>();
                    }

                    lightComponent.intensity = light.intensity;
                    lightComponent.type = LightType.Directional;
                    lightComponent.shadows = LightShadows.None;
                }

                List<string> lightsInState = state.scene.lighting.Select((l) => l.name).ToList();

                foreach (ABRLight light in existingSceneLights)
                {
                    if (!lightsInState.Contains(light.gameObject.name))
                    {
                        GameObject.Destroy(light.gameObject);
                    }
                }
            }

            if (state?.scene?.backgroundColor != null)
            {
                ABREngine.Instance.Config.DefaultCamera.backgroundColor = IVLab.Utilities.ColorUtilities.HexToColor(state.scene.backgroundColor);
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
                saveState.version = previousState?["version"]?.ToString() ?? ABREngine.Instance.Config.SchemaJson["properties"]["version"]["default"].ToString();
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
                    saveGroup.name = group.name;
                    saveGroup.transformMatrix = Matrix4x4.TRS(group.transform.localPosition, group.transform.localRotation, group.transform.localScale);
                    Bounds containerBounds;
                    if (group.TryGetContainerBoundsInGroupSpace(out containerBounds))
                        saveGroup.containerBounds = containerBounds;
                    else
                        saveGroup.containerBounds = null;
                    saveGroup.uuid = group.Uuid;
                    saveGroup.impressions = new List<Guid>();

                    // Go through each impression
                    foreach (var impression in group.GetDataImpressions().Values)
                    {
                        RawDataImpression saveImpression = new RawDataImpression();

                        // Retrieve easy values
                        string guid = impression.Uuid.ToString();
                        saveImpression.uuid = guid;
                        saveImpression.name = impression.name;
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
                ABRLight[] lightsInScene = MonoBehaviour.FindObjectsOfType<ABRLight>();
                foreach (ABRLight light in lightsInScene)
                {
                    Light l = light.GetComponent<Light>();
                    saveScene.lighting.Add(new RawLight
                    {
                        name = light.gameObject.name,
                        intensity = l.intensity,
                        position = light.transform.localPosition,
                        rotation = light.transform.localRotation,
                    });
                }

                saveScene.backgroundColor = IVLab.Utilities.ColorUtilities.ColorToHex(ABREngine.Instance.Config.DefaultCamera.backgroundColor);

                saveState.scene = saveScene;
                saveState.dataRanges = saveRanges;

                JsonSerializerSettings settings = new JsonSerializerSettings();
                settings.NullValueHandling = NullValueHandling.Ignore;
                settings.Formatting = Formatting.Indented;
                settings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
                JsonConvert.DefaultSettings = () => settings;

                if (previousState?.ContainsKey("uiData") ?? false)
                {
                    saveState.uiData = previousState["uiData"];
                }

                if (previousState?.ContainsKey("localVisAssets") ?? false)
                {
                    saveState.localVisAssets = previousState["localVisAssets"];
                }

                if (previousState?.ContainsKey("name") ?? false)
                {
                    saveState.name = previousState["name"].ToString();
                }

                if (previousState?.ContainsKey("primitiveGradients") ?? false)
                {
                    saveState.primitiveGradients = previousState["primitiveGradients"].ToObject<Dictionary<string, RawPrimitiveGradient>>();
                }

                if (ABREngine.Instance.VisAssets.VisAssetGradients != null)
                {
                    saveState.visAssetGradients = ABREngine.Instance.VisAssets.VisAssetGradients;
                }

                // return JsonConvert.SerializeObject(saveState, settings);
                return JsonConvert.SerializeObject(saveState, new UnityObjectSerializer());
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                return null;
            }
        }
    }

    /// <summary>
    /// Custom converter to allow less verbose Newtonsoft serialization of Unity
    /// builtin objects. This converter manually handles several cases, add more
    /// as they become necessary.
    /// </summary>
    public class UnityObjectSerializer : JsonConverter
    {
        public override bool CanRead { get { return false; }}

        private BindingFlags fieldFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private Dictionary<Type, string[]> _saveFields = new Dictionary<Type, string[]>();
        private Dictionary<Type, string[]> _remapFieldNames = new Dictionary<Type, string[]>();

        /// <summary>
        /// Build the custom converter and define both the types that are
        /// allowed to be serialized and the string keys that are allowed to
        /// exist post-serialization
        /// </summary>
        public UnityObjectSerializer() : base()
        {
            _saveFields.Add(typeof(Vector3), new string[] {"x", "y", "z"});
            _saveFields.Add(typeof(Quaternion), new string[] {"x", "y", "z", "w"});
            _saveFields.Add(typeof(Matrix4x4), new string[] {"m00", "m01", "m02", "m03", "m10", "m11", "m12", "m13","m20", "m21", "m22", "m23", "m30", "m31", "m32", "m33"});
            _saveFields.Add(typeof(Matrix4x4?), new string[] {"m00", "m01", "m02", "m03", "m10", "m11", "m12", "m13","m20", "m21", "m22", "m23", "m30", "m31", "m32", "m33"});
            _saveFields.Add(typeof(Bounds), new string[] {"m_Center", "m_Extents"});
            _remapFieldNames.Add(typeof(Bounds), new string[] {"center", "extents"});
            _saveFields.Add(typeof(Bounds?), new string[] {"m_Center", "m_Extents"});
            _remapFieldNames.Add(typeof(Bounds?), new string[] {"center", "extents"});
        }


        /// <summary>
        /// We only provide serializers for these types
        /// </summary>
        public override bool CanConvert(Type type)
        {
            return _saveFields.Keys.Contains(type);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            // Only use this custom converter if type matches (we shouldn't get here)
            if (!_saveFields.Keys.Contains(value.GetType()))
            {
                Debug.LogWarning("Tried to convert " + value.GetType() + " with UnityObjectSerializer");
                JToken preOut = JToken.FromObject(value);
                preOut.WriteTo(writer);
                return;
            }

            // Everything from this method will be an object
            JObject output = new JObject();

            // Check each type to see if it matches with the serialized object
            Type objectType = value.GetType();
            foreach (var kv in _saveFields)
            {
                if (objectType.IsAssignableFrom(kv.Key))
                {
                    for (int fn = 0; fn < kv.Value.Length; fn++)
                    {
                        string fieldName = kv.Value[fn];

                        // Use reflection to obtain actual value of the field,
                        // then assign it to the JObject
                        FieldInfo info = objectType.GetField(fieldName, fieldFlags);
                        if (info == null)
                        {
                            string allFields = string.Join(", ", objectType.GetFields(fieldFlags).Select(f => f.Name));
                            Debug.LogWarning($"Unable to find field {fieldName} in object {objectType}. Fields available: {allFields}");
                            continue;
                        }
                        object fieldValue = info.GetValue(value);

                        string newName = fieldName;
                        if (_remapFieldNames.ContainsKey(kv.Key))
                        {
                            newName = _remapFieldNames[kv.Key][fn];
                        }

                        // Recursively deal with further Unity objects using the current serializer
                        output[newName] = JToken.FromObject(fieldValue, serializer);
                    }
                }
            }

            output.WriteTo(writer);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Reading is not supported for this serializer");
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
        public Dictionary<string, RawVisAssetGradient> visAssetGradients;
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
    }

    class RawImpressionGroup
    {
        public List<Guid> impressions;
        public string name;
        public Guid uuid;
        public Bounds? containerBounds;
        public Matrix4x4? transformMatrix;
    }
}