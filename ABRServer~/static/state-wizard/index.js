import { DataPath } from '../common/DataPath.js';
import { download } from '../common/helpers.js';

// Map old EncodingRenderStrategies to new DataImpressions
const strategiesToPlateTypes = {
    'SimpleGlyphEncodingRenderStrategy': 'Glyphs',
    'SimpleLineEncodingRenderStrategy': 'Ribbons',
    'SimpleSurfaceEncodingRenderStrategy': 'Surfaces',
};

// VariableTypes in the old system
const variableTypes = [
    'RawScalarVariable',
    'RawVectorVariable',
    'RangedScalarDataVariable'
];

// Map old input names to equivalent new ones, otherwise assume they're
// the same
const inputNameMap = {
    'Pattern Scale': 'Pattern Size',
    'Pattern Blend': 'Pattern Seam Blend',
    'Width': 'Ribbon Width',
    'Curve': 'Ribbon Curve',
    'Average': 'Ribbon Smooth',
};
const origDefaultDatasetPath = 'Organization/Dataset';
var defaultDatasetPath = origDefaultDatasetPath;

const unitRegex = /(m|%|deg)/;

var schema = {};

var rangesToResolve = {};

// Resolve schema consts to values, if there are any values contained within
// consts
// For example: {
//      "inputValue": { "const": "4m" },
//      "inputType": { "const": "IVLab.ABREngine.LengthPrimitive" }
// }
// resolves to {
//      "inputValue": "4m",
//      "inputType": "IVLab.ABREngine.LengthPrimitive"
// }
// This assumes that no input value will be an object!!
function resolveSchemaConsts(data) {
    let resolvedData = {};
    for (const field in data) {
        if (typeof (data[field]) === 'object' && data[field].const) {
            resolvedData[field] = data[field].const;
        } else if (typeof (data[field]) === 'object' && data[field].default) {
            resolvedData[field] = data[field].default;
        } else {
            resolvedData[field] = data[field];
        }
    }
    return resolvedData;
}

// Get the full list of input names from a plate type from a schema
function getSchemaInputNames(plateType) {
    return Object.keys(schema['definitions']['Plates'][plateType]['properties']);
}

function getSchemaInputWithDefault(plateType, inputName) {
    let inputProps = schema['definitions']['Plates'][plateType]['properties'][inputName]['properties'];
    let resolvedProps = resolveSchemaConsts(inputProps);
    return {
        inputType: resolvedProps.inputType,
        parameterName: resolvedProps.parameterName,
        inputValue: resolvedProps.inputValue,
        inputGenre: resolvedProps.inputGenre,
    }
}

$('#upload-state').on('click', (_evt) => {
    // Create a fake element to handle the actual upload
    let $fileInput = $('<input>', {
        type: 'file',
    }).on('change', (evt) => {
        if (!evt.target.files || !evt.target.files[0]) {
            alert('No files uploaded!');
            return;
        }

        let stateFileName = evt.target.files[0].name;
        // get rid of file extension
        let stateName = stateFileName.replace(/\.[^/.]+$/, ""); // https://stackoverflow.com/a/4250408

        let reader = new FileReader();
        $(reader).on('load', (loadEvt) => {
            // Update the state with the stateManager
            let stateJson = JSON.parse(loadEvt.target.result);
            upgradeState(stateName, stateJson);
        });
        reader.readAsText(evt.target.files[0]);

        $fileInput.remove();
    });
    $('body').append($fileInput);
    $fileInput.click();
});

// For debugging
fetch('/api/schemas/ABRSchema_0-2-0.json')
    .then((resp) => resp.json())
    .then((s) => schema = s);

// GLOBAL new state
var newState = {
    'version': '0.2.0',
    'impressions': {},
    'dataRanges': {},
    'localVisAssets': {},
    'uiData': {
        'compose': {
            'impressionData': {}
        }
    },
};

function upgradeState(stateName, stateJson) {
    // Start generating a new ABR 0.2 state and save it
    // Things that can be upgraded automatically:
    // - VisAsset Inputs
    // - UI metadata
    //
    // Things that need to be manually upgraded
    // - Variable inputs (choose Path)
    // - KeyData inputs (choose Path)
    // - Data Ranges (after variables have been upgraded)
    //
    // Things that need to be redone from defaults
    // - Primitive inputs

    let impressions = readDataImpressions(stateJson);

    // Automatically upgrade the impressions
    for (const impression of impressions) {
        let newImpression = upgradeImpressionAuto(impression, stateJson);
        newState.impressions[newImpression.uuid] = newImpression;
    }

    // Automatically upgrade UI metadata
    newState.uiData.compose.impressionData = stateJson.ui;

    // Populate the wizard to help with items that can't automatically
    // be inferred
    populateWizardForm(stateName, stateJson);

    // Upgrade local VisAssets
    upgradeLocalVisAssets(stateJson);

    $('#export').on('click', (evt) => {
        download(stateName + '_ABR-0-2-0.json', JSON.stringify(getNewState(), null, 4), 'data:application/json,');
    });
}

// Returns the new state but with all the 
function getNewState() {
    let dontUseImpressions = [];
    let $allUseImpressions = $('input.use-impression');
    $allUseImpressions.each((i, el) => {
        if (!$(el).prop('checked')) {
            // 'checkbox-' = 8 chars
            dontUseImpressions.push(el.id.slice(9));
        }
    });

    // Clone the state for export
    let stateToExport = JSON.parse(JSON.stringify(newState));

    for (const impression in newState.impressions) {
        if (dontUseImpressions.indexOf(impression) >= 0) {
            delete stateToExport.impressions[impression];
        }
    }

    for (const impression in newState.uiData.compose.impressionData) {
        if (dontUseImpressions.indexOf(impression) >= 0) {
            delete stateToExport.uiData.compose.impressionData[impression];
        }
    }
    return stateToExport;
}

function populateWizardForm(stateName, stateJson) {
    // Add the state name
    $('#state-name').text(stateName);

    let impressions = readDataImpressions(stateJson);

    // Add all data impressions to the UI
    for (const impression of impressions) {
        let variables = readVariables(impression, stateJson);

        let plateType = strategiesToPlateTypes[impression.type];

        let $impression = $('<div>', {
            class: 'data-impression card',
        }).append($('<header>').append(
            $('<span>', { text: impression.label })
        )).append(
            $('<label>', { 
                title: 'Use this impression in the new state?',
                css: {
                    right: 0,
                }
            }).append(
                $('<input>', {
                    class: 'use-impression',
                    id: `checkbox-${impression.uuid}`,
                    type: 'checkbox',
                    prop: { checked: 'true' }
                })
            ).append($('<span>', {
                class: 'toggle button',
                text: 'Use this impression?'
            }))
        );

        // Choose the KeyData
        let dataObject = findDataObjectForStrategy(impression.uuid, stateJson);

        $impression.append(
                $('<button>', {
                class: 'error',
                text: `Key Data: ${dataObject.label}`,
                title: 'Please provide a path for this key data object. Example: "TACC/Ronne/KeyData/Bathymetry"'
            }).on('click', (evt) => {
                let valid = false;
                let newVarPath = prompt('Choose new Key Data path', `${defaultDatasetPath}/KeyData/${dataObject.label}`);
                if (!DataPath.followsConvention(newVarPath, 'KeyData')) {
                    alert(`Path '${newVarPath} does not follow KeyData convention ${DataPath.getConvention('KeyData')}`);
                } else {
                    valid = true;
                }

                if (valid) {
                    // Update the default dataset to make it easier next time
                    defaultDatasetPath = DataPath.getDatasetPath(newVarPath);
                    $(evt.target).text(`Key Data: ${newVarPath}`);
                    $(evt.target).removeClass('error');
                    $(evt.target).addClass('success');
                    let defaultInputValue = getSchemaInputWithDefault(plateType, 'Key Data');
                    defaultInputValue.inputValue = newVarPath;
                    newState.impressions[impression.uuid].inputValues['Key Data'] = defaultInputValue;
                }
            })
        );

        $impression.append($('<p>', { text: `Variables (${Object.keys(variables).length})` }));

        let $vars = $('<div>');
        for (let inputName in variables) {
            // Switch out for the new input name
            if (Object.keys(inputNameMap).indexOf(inputName) > 0) {
                inputName = inputNameMap[inputName];
            }

            let variable = variables[inputName];
            $vars.append(
                $('<button>', {
                    class: 'error',
                    text: `${inputName}: ${variable.label}`,
                    title: 'Please provide a path for this variable. Example: "TACC/Ronne/ScalarVar/Temperature"'
                }).on('click', (evt) => {
                    let newVarPath = '';
                    let valid = false;
                    if (variable.type.includes('Scalar')) {
                        newVarPath = prompt('Choose new variable data path', `${defaultDatasetPath}/ScalarVar/${variable.label}`);
                        if (!DataPath.followsConvention(newVarPath, 'ScalarVar')) {
                            alert(`Path '${newVarPath} does not follow ScalarVar convention ${DataPath.getConvention('ScalarVar')}`);
                        } else {
                            valid = true;
                        }
                    } else if (variable.type.includes('Vector')) {
                        newVarPath = prompt('Choose new variable data path', `${defaultDatasetPath}/VectorVar/${variable.label}`);
                        if (!DataPath.followsConvention(newVarPath, 'VectorVar')) {
                            alert(`Path '${newVarPath} does not follow VectorVar convention ${DataPath.getConvention('VectorVar')}`);
                        } else {
                            valid = true;
                        }
                    }

                    if (valid) {
                        // Update the default dataset to make it easier next time
                        defaultDatasetPath = DataPath.getDatasetPath(newVarPath);
                        $(evt.target).text(`${inputName}: ${newVarPath}`);
                        $(evt.target).removeClass('error');
                        $(evt.target).addClass('success');
                        let defaultInputValue = getSchemaInputWithDefault(plateType, inputName);
                        defaultInputValue.inputValue = newVarPath;
                        newState.impressions[impression.uuid].inputValues[inputName] = defaultInputValue;

                        // Check if this var has been remapped at all
                        if (Object.keys(rangesToResolve).indexOf(variable.label) >= 0) {
                            if (!newState.dataRanges.scalarRanges) {
                                newState.dataRanges = { scalarRanges: { } };
                            }
                            newState.dataRanges.scalarRanges[defaultInputValue.inputValue] = rangesToResolve[variable.label];
                            refreshDataRanges();
                        }
                    }
                })
            );
        }

        $impression.append($vars);

        let $primitives = $('<div>');
        let primitives = readPrimitives(impression, stateJson);
        for (let inputName in primitives) {
            // Switch out for the new input name
            if (Object.keys(inputNameMap).indexOf(inputName) > 0) {
                inputName = inputNameMap[inputName];
            }

            let primitiveValue = primitives[inputName];
            let newValue = getPrimitiveValueWithUnits(primitiveValue, inputName, plateType);

            $primitives.append(
                $('<button>', {
                    class: 'warning',
                    text: `${inputName}: ${newValue}`,
                    title: 'Please provide a value for this primitive. Defaults have been inferred.'
                }).on('click', (evt) => {
                    let newValueFromUser = prompt('New primitive value (keep units)', newValue);
                    $(evt.target).text(`${inputName}: ${newValueFromUser}`);
                    $(evt.target).removeClass('warning');
                    $(evt.target).addClass('success');
                    newValue.inputValue = newValueFromUser;
                    newState.impressions[impression.uuid].inputValues[inputName] = newValue;
                })
            );
        }

        $impression.append($('<p>', { text: `Primitives (${Object.keys(primitives).length})`}));
        $impression.append($primitives);

        $('#impression-list').append($impression);
    }

    // Populate the variables
    refreshDataRanges();
}

function refreshDataRanges() {
    $('#remapped-vars').empty();
    if (!newState.dataRanges.scalarRanges) {
        return;
    }
    for (const range in newState.dataRanges.scalarRanges) {
        let $rangeChooser = $('<div>', {
            class: 'range-chooser card',
        }).append($('<header>', {
            text: range,
        })).append($('<input>', {
            type: 'number',
            class: 'min-input',
            val: newState.dataRanges.scalarRanges[range].min,
        })).append($('<input>', {
            type: 'number',
            class: 'max-input',
            val: newState.dataRanges.scalarRanges[range].max,
        })).append($('<button>', {
            class: 'warning',
            text: 'Update Range'
        }).on('click', (evt) => {
            let minVal = $(evt.target).siblings('input.min-input').val();
            let maxVal = $(evt.target).siblings('input.max-input').val();
            newState.dataRanges.scalarRanges[range].min = minVal;
            newState.dataRanges.scalarRanges[range].max = maxVal;
            $(evt.target).removeClass('warning');
            $(evt.target).addClass('success');
        }));
        $('#remapped-vars').append($rangeChooser);
    }
}

// Upgrade all the parts of an impression from 0.1.8 to 0.2.0 that can
// be automatically done
function upgradeImpressionAuto(dataImpression, stateJson) {
    let uiDataForImpression = stateJson.ui[dataImpression.uuid];
    let primitives = readPrimitives(dataImpression, stateJson);

    // Populate the top-level data
    let newImpression = {};
    newImpression.plateType = strategiesToPlateTypes[dataImpression.type];
    newImpression.uuid = dataImpression.uuid;
    newImpression.name = dataImpression.label;
    newImpression.renderHints = {};
    newImpression.renderHints.visible = !uiDataForImpression.hidden;
    newImpression.inputValues = {};

    for (var inputName in dataImpression.inputs) {
        // Check if the input name exists for this plate type
        let allInputsForPlateType = getSchemaInputNames(newImpression.plateType);
        if (Object.keys(inputNameMap).indexOf(inputName) >= 0) {
            inputName = inputNameMap[inputName];
        }

        if (allInputsForPlateType.indexOf(inputName) < 0) {
            console.warn(`'${inputName}' is not an input for plate type ${newImpression.plateType}! Ignoring.`);
            continue;
        }

        let oldInputUuid = dataImpression.inputs[inputName];

        // We can only upgrade if it's a VisAsset, otherwise leave at
        // the default
        let newInput = getSchemaInputWithDefault(newImpression.plateType, inputName);
        if (newInput.inputGenre == 'VisAsset') {
            newInput.inputValue = oldInputUuid;
        }

        // If it's a primitive, go ahead and add the value but notify the user
        // that it'll probably be wrong.
        if (newInput.inputGenre == 'Primitive') {
            let primitiveValue = primitives[inputName];
            let newValue = getPrimitiveValueWithUnits(primitiveValue, inputName, newImpression.plateType);
            newInput.inputValue = newValue;
        }

        // Only add if actually defined
        if (newInput.inputValue) {
            newImpression.inputValues[inputName] = newInput;
        }
    }

    return newImpression;
}

function readDataImpressions(stateJson) {
    // Go through each composition node in the old state
    return stateJson.compositionNodes.filter((strat) => Object.keys(strategiesToPlateTypes).indexOf(strat.type) >= 0);
}

function readVariables(dataImpression, stateJson) {
    let compNodeUuids = stateJson.compositionNodes.map((node) => node.uuid);
    let dataNodeUuids = stateJson.dataNodes.map((node) => node.uuid);

    let variables = {};
    for (const inputName in dataImpression.inputs) {
        // Find the variable in dataNodes or comp nodes
        let inputUuid = dataImpression.inputs[inputName];
        let inputIndexComp = compNodeUuids.indexOf(inputUuid);
        let inputIndexData = dataNodeUuids.indexOf(inputUuid);
        if (inputIndexData >= 0) {
            variables[inputName] = stateJson.dataNodes[inputIndexData];
        } else if (inputIndexComp >= 0 && 'RangedScalarDataVariable') {
            // find the variable source of this data var and keep track of its
            // mappings
            let inputValue = stateJson.compositionNodes[inputIndexComp];
            let origVarUuid = inputValue.inputs.InputVariable;
            if (origVarUuid) {
                let origVar = stateJson.dataNodes[dataNodeUuids.indexOf(origVarUuid)];
                rangesToResolve[origVar.label] = {
                    min: origVar.minValue,
                    max: origVar.maxValue,
                }

                variables[inputName] = origVar;
            }
        }
    }
    return variables;
}

function readPrimitives(dataImpression, stateJson) {
    let compNodeUuids = stateJson.compositionNodes.map((node) => node.uuid);

    let primitives = {};
    for (const inputName in dataImpression.inputs) {
        // Find the primitive in compositionNodes
        let inputUuid = dataImpression.inputs[inputName];
        let inputIndex = compNodeUuids.indexOf(inputUuid);
        let inputValue = stateJson.compositionNodes[inputIndex];
        if (inputIndex >= 0 && inputValue.type == 'RealNumber') {
            primitives[inputName] = inputValue.floatVal;
        }
    }
    return primitives;
}

function findDataObjectForStrategy(strategyUuid, stateJson) {
    let encoding = stateJson.compositionNodes.find((node) => node.inputs && node.inputs["Rendering Strategy"] == strategyUuid);
    if (encoding) {
        return stateJson.dataNodes.find((node) => node.uuid == encoding.inputs['Data Object']);
    }
}

function getPrimitiveValueWithUnits(primitiveValue, inputName, plateType) {
    let primitiveDefault = getSchemaInputWithDefault(plateType, inputName);
    let units = primitiveDefault.inputValue.match(unitRegex);
    if (units && units[1] && primitiveValue) {
        return primitiveValue + units[1];
    } else {
        return primitiveDefault.inputValue;
    }
}

function upgradeLocalVisAssets(stateJson) {
    const excludeFields = ['contents', 'stateSpecific'];
    // Copy all the old artifactJson info
    for (const visAsset of stateJson.stateSpecificVisassets) {
        let artifactJson = {};
        for (const field in visAsset) {
            if (excludeFields.indexOf(field) < 0) {
                artifactJson[field] = visAsset[field];
            }
        }

        let newVisAsset = {};
        newVisAsset.artifactJson = artifactJson;

        // Assume that the one we're upgrading is a colormap, specified in colormap.xml
        newVisAsset.artifactDataContents = {
            'colormap.xml': visAsset['contents'],
        };

        newState.localVisAssets[visAsset.uuid] = newVisAsset;
    }
    console.log(newState);
}