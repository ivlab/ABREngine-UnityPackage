/* DataImpression.js
 *
 * Instantiated form of a Plate, contains inputs that can be changed by an artist
 *
 * Copyright (C) 2023, University of Minnesota
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

import { DataPath } from "../../../common/DataPath.js";
import { globals } from "../../../common/globals.js";
import { dialogWidth } from './dialogConsts.js';
import { CACHE_UPDATE, resolveSchemaConsts } from '../../../common/StateManager.js';
import { COMPOSITION_LOADER_ID } from '../components/Components.js';
import { InputPuzzlePiece, AssignedInputPuzzlePiece } from "./PuzzlePiece.js";
import { uuid } from "../../../common/UUID.js";

// This defines the "topology" of the data impression inputs - i.e., which
// inputs are "paired" with which. This is a change from past behaviour because
// previously, we assumed that the schema would have `parameterName`... now we
// are explicitly defining the UI relationships here and NOT in the schema.
//
// We take a tiered approach to building the UI:
// - Tier 1 is the "must-have" inputs. This is usually for the immediate visual design and data variable mappings.
// - Tier 2 are the inputs that artists like to use to fine tune a visualization.
// - Tier 3 contains the inputs that are not often used.
//
// The tiers are defined as individual list elements in the below object.
// The string keys in this object should line up with the ABR Schema.
// Key Data are assumed to be a "special" input that doesn't go in here
//
// Each input is defined as pairs: [left input, right input]
// Non-draggable (e.g. primitive) inputs are just one per row
//
// These names correspond to the schema loaded in Validator.js.
const DataImpressionInputTopology = {
    "Glyphs": [
        // Tier 1
        [
            ["Color Variable", "Colormap"],
            ["Glyph Variable", "Glyph"],
            ["Forward Variable", null],
            ["Up Variable", null],
        ],
        // Tier 2
        [
            ["Glyph Size"],
            ["Glyph Density"],
            ["Use Random Orientation"]
        ],
        // Tier 3
        [
            ["Show Outline", "Outline Width"],
            ["Force Outline Color", "Outline Color"],
            ["Glyph Level Of Detail"],
            [null, "NaN Color"]
        ]
    ],
    "Ribbons": [
        // Tier 1
        [
            ["Color Variable", "Colormap"],
            ["Texture Variable", "Texture"]
        ],
        // Tier 2
        [
            ["Ribbon Width"],
            ["Ribbon Rotation"],
            ["Ribbon Curve"],
            ["Ribbon Smooth"],
            ["Ribbon Brightness"],
            ["Texture Cutoff"]
        ],
        // Tier 3
        [
            [null, "NaN Color"],
            [null, "NaN Texture"],
        ]
    ],
    "Surfaces": [
        // Tier 1
        [
            ["Color Variable", "Colormap"],
            ["Pattern Variable", "Pattern"],
        ],
        // Tier 2
        [
            ["Pattern Size"],
            ["Pattern Saturation"],
            ["Pattern Intensity"],
            ["Pattern Seam Blend"]
        ],
        // Tier 3
        [
            ["Opacity"],
            ["Show Outline", "Show Only Outline"],
            ["Outline Width", "Outline Color"],
            [null, "NaN Color"],
            [null, "NaN Pattern"]
        ]
    ],
    "Volumes": [
        // Tier 1
        [
            ["Color Variable", "Colormap"],
            [null, "Opacitymap"]
        ],
        // Tier 2
        [
            ["Volume Brightness"],
            ["Volume Opacity Multiplier"],
            ["Volume Lighting"]
        ],
        // Tier 3
        [
            [null, "NaN Color"],
            ["NaN Opacity"]
        ]
    ]
}

export function DataImpression(plateType, uuid, name, impressionData) {
    let maxInputTierToShow = 1;
    if (impressionData && typeof(impressionData.maxInputTierToShow !== 'undefined')) {
        maxInputTierToShow = impressionData.maxInputTierToShow;
    }

    let $element = $('<div>', { class: 'data-impression rounded' })
        .data({
            uuid,
            plateType,
        });
    let $tower = $('<div>', {
        class: 'data-impression-tower rounded',
    });
    $element.append($tower);

    let $composition = $('#' + COMPOSITION_LOADER_ID);
    let position = {
        top: $composition.css('left'),
        left: $composition.css('top'),
    }
    if (impressionData && impressionData.position) {
        position = impressionData.position;
    }
    $element.css({
        position: 'absolute',
        top: position.top,
        left: position.left,
    });

    let collapsed = false;
    if (maxInputTierToShow == 0) {
        collapsed = true;
    }

    $element.append($('<div>', {
        class: 'data-impression-header rounded-top',
    }).css({ cursor: 'grabbing'}).append(
        $('<p>', { text: name, })
    ));

    let inputValues = null;
    if (globals.stateManager.state && globals.stateManager.state.impressions)
    {
        if (globals.stateManager.state.impressions[uuid]) {
            inputValues = globals.stateManager.state.impressions[uuid].inputValues;
        }
    }

    let plateSchema = globals.schema.definitions.Plates[plateType].properties;

    // Get inputs for this plate type
    let parameterTiers = DataImpressionInputTopology[plateType];

    $element.append(DataImpressionSummary(uuid, name, impressionData, inputValues, parameterTiers));

    // Construct KeyData input
    let kdInputName = 'Key Data';
    let $kdParam = $('<div>', { class: 'keydata parameter rounded-bottom' });
    let $kdSocket = InputSocket(kdInputName, plateSchema[kdInputName].properties, 'keydata');
    if (inputValues && inputValues[kdInputName]) {
        let $kdInput = AssignedInputPuzzlePiece(kdInputName, inputValues[kdInputName], 'keydata');
        $kdInput.appendTo($kdSocket);
    }
    $kdParam.append($kdSocket);

    if (!collapsed) {
        $element.append($kdParam);
    }

    let $parameterList = $('<div>', {
        class: 'parameter-list',
    });

    const $moreButton = $('<button>', {
        class: 'material-icons',
        text: 'expand_more'
    }).on('click', () => {
        globals.stateManager.update('uiData/compose/impressionData/' + uuid + '/maxInputTierToShow', maxInputTierToShow + 1);
    });
    const $lessButton = ($('<button>', {
        class: 'material-icons',
        text: 'expand_less'
    }).on('click', () => {
        globals.stateManager.update('uiData/compose/impressionData/' + uuid + '/maxInputTierToShow', maxInputTierToShow - 1);
    }));
    let $expandButtons = $('<div>', {
        class: 'impression-expand-buttons'
    });

    let everyTierShown = true;
    // Add a new row of inputs for each parameter
    for (const tierIndex in parameterTiers) {
        if (+tierIndex + 1 > maxInputTierToShow) {
            if (+tierIndex > 0) {
                $expandButtons.append($lessButton);
            }
            $expandButtons.append($moreButton);
            everyTierShown = false;
            break;
        }

        const tier = parameterTiers[tierIndex];
        let $tier = $('<div>', {
            class: 'input-tier rounded'
        });

        let isOnlyPrimitives = tier.every(t => t.length == 1);
        if (isOnlyPrimitives) {
            $tier.addClass('primitive-tier');
        }

        for (const inputPair of tier) {
            let $param = Parameter();
            if (inputPair.length == 1) {
                $param = Parameter('narrow');
            }
            // Construct each input, and overlay the value puzzle piece if it exists
            for (const inputName of inputPair) {
                if (!inputName) {
                    $param.append($('<div>'));
                    continue;
                }

                let $socket = InputSocket(inputName, plateSchema[inputName].properties);
                if (inputValues && inputValues[inputName]) {
                    let $input = AssignedInputPuzzlePiece(inputName, inputValues[inputName]);
                    $input.appendTo($socket);

                    // Prime the input to be reloaded and replaced when visassets
                    // get updated
                    globals.stateManager.subscribeCache('visassets', $socket);
                    $socket.on(CACHE_UPDATE + 'visassets', (evt) => {
                        evt.stopPropagation();
                        let $reloaded = AssignedInputPuzzlePiece(inputName, inputValues[inputName]);
                        $input.replaceWith($reloaded);
                    });
                }
                $param.append($socket);
            }
            $tier.append($param);
        }
        $parameterList.append($tier);
    }

    if (everyTierShown) {
        $expandButtons.append($lessButton);
    }

    if (!collapsed) {
        $element.append($parameterList);
    }

    $element.append($expandButtons);

    // Only need to update the UI position when dragging, not the whole impression
    $element.draggable({
        handle: '.data-impression-header',
        drag: (evt, ui) => {
            evt.stopPropagation();
        },
        stop: (evt, ui) => {
            // If we're not hovering over the trash, send the update
            if (!$(ui.helper).hasClass('removing')) {
                let pos = ui.helper.position();
                let imprId = $(evt.target).data('uuid');
                globals.stateManager.update('uiData/compose/impressionData/' + imprId + '/position', pos);
            }
        }
    });

    return $element;
}

// A socket that can be dropped into
function InputSocket(inputName, inputProps, addClass=undefined) {
    let $socket = $('<div>', {
        class: 'input-socket ' + addClass,
    });
    // It's an input, but we can't drag it
    let $dropZone = InputPuzzlePiece(inputName, inputProps);
    $dropZone.addClass('drop-zone');

    $dropZone.droppable({
        tolerance: 'touch',
        out: (_evt, ui) => {
            $(ui.draggable).data('draggedOut', true);
        },
        drop: (evt, ui) => {
            // Skip socket drop events if piece is inside a dialog
            if (ui.helper.parents('.puzzle-piece-overlay-dialog').length > 0) {
                return;
            }

            // Get the impression that this input is a part of
            let $impression = $(evt.target).closest('.data-impression');
            let impressionId = $impression.data('uuid');
            let plateType = $impression.data('plateType');

            // Get the default values for this input, in case there's nothing
            // there already
            let defaultInputsSchema = globals.schema.definitions.Plates[plateType].properties[inputName].properties;
            let defaultInputs = resolveSchemaConsts(defaultInputsSchema);

            // See if there's an input there already, if not assign the defaults
            let impressionState;
            if (globals.stateManager.state['impressions'] && globals.stateManager.state['impressions'][impressionId]) {
                impressionState = globals.stateManager.state['impressions'][impressionId];
            }
            let inputState;
            if (impressionState && impressionState.inputValues && impressionState.inputValues[inputName]) {
                inputState = impressionState.inputValues[inputName];
            } else {
                inputState = defaultInputs;
            }

            // Ensure the dropped type matches the actual type -- account for
            // polymorphic VisAsset types that can take gradients or regular
            let droppedType = ui.draggable.data('inputType');
            let validTypes;
            if (inputProps.inputType.oneOf) {
                validTypes = inputProps.inputType.oneOf.map(t => t.const);
            } else {
                validTypes = [inputProps.inputType.const];
            }
            if (validTypes.indexOf(droppedType) >= 0) {
                // Update the inputState with value and type (type may be
                // different, but compatible)
                let droppedValue = ui.draggable.data('inputValue');
                inputState['inputValue'] = droppedValue;
                inputState['inputType'] = droppedType;

                // Send the update to the server
                globals.stateManager.update(`impressions/${impressionId}/inputValues/${inputName}`, inputState);
                
                // Append a temp version that will be replaced when we get an
                // update back from the server
                let $tmp = ui.draggable.clone();
                $tmp.addClass('tentative');
                $tmp.css('position', 'absolute');
                $tmp.css('top', 0);
                $tmp.css('left', 0);
                $tmp.appendTo($socket);

                // If the data impression hasn't been renamed by user (with the pencil icon),
                // default impression's name to the name of the key data applied to it
                if (!globals.stateManager.state['impressions'][impressionId].isRenamedByUser) {
                    if (droppedType.substring(droppedType.length - 7) === "KeyData") {
                        let newName = DataPath.getName(droppedValue);
                        globals.stateManager.update(`/impressions/${impressionId}/name`, newName);  
                    }
                }
            }
        }
    });

    $socket.append($dropZone);

    return $socket;
}

function Parameter(addClass) {
    return $('<div>', { class: 'parameter ' + addClass });
}

function DataImpressionSummary(uuid, name, impressionData, inputValues, parameterTiers) {
    let collapsed = false;
    if (impressionData && typeof(impressionData.maxInputTierToShow !== 'undefined'), impressionData.maxInputTierToShow == 0) {
        collapsed = true;
    }

    let oldVisibility = true;
    if (globals.stateManager.state.impressions && globals.stateManager.state.impressions[uuid]) {
        if (globals.stateManager.state.impressions[uuid].renderHints) {
            oldVisibility = globals.stateManager.state.impressions[uuid].renderHints.Visible;
        }
    }
    let $el = $('<div>', {
        class: 'data-impression-summary rounded-bottom'
    }).append(
        $('<div>', { class: 'impression-controls' }).append(
            $('<button>', {
                class: 'material-icons rounded',
                text: oldVisibility ? 'visibility' : 'visibility_off',
                title: oldVisibility ? 'This data impression is visible' : 'This data impression is not shown',
            }).on('click', (evt) => {
                globals.stateManager.update(`/impressions/${uuid}/renderHints/Visible`, !oldVisibility);
            })
        ).append(
            $('<button>', {
                class: 'rounded',
                title: 'Edit this data impression\'s properties'
            }).append(
                $('<span>', { class: 'material-icons', text: 'edit'})
            ).on('click', (evt) => {
                editDataImpression(uuid);
            })
        ).append(
            $('<button>', {
                class: 'rounded',
                title: 'Duplicate this data impression'
            }).append(
                $('<span>', { class: 'material-icons', text: 'content_copy'})
            ).on('click', (evt) => {
                duplicateDataImpression(uuid);
            })
        )
    );

    if (collapsed) {
        $el.append($('<hr>'))
        let $props = $('<div>', {
            class: 'summary-properties parameter'
        });
        let kdInputName = 'Key Data';
        if (inputValues && inputValues[kdInputName]) {
            let $kd = AssignedInputPuzzlePiece(kdInputName, inputValues[kdInputName], 'summary');
            $kd.off('click');
            if ($kd.hasClass('ui-draggable')) {
                $kd.draggable('destroy');
            }
            $props.append($kd);
        }

        for (const tier of parameterTiers) {
            // Summary skips all inputs besides Tier 1
            if (+tier > 0) {
                break;
            }
            for (const inputPair of tier) {
                let $row = $('<div>');
                for (const inputName of inputPair) {
                    if (inputValues && inputValues[inputName]) {
                        // Display a non-editable version of the piece in the summary block
                        let $input = AssignedInputPuzzlePiece(inputName, inputValues[inputName], 'summary');
                        let $inputLabel = $input.find('.puzzle-label');
                        $inputLabel.addClass('summary');

                        // Special case for primitive inputs - skip them
                        let textInput = $inputLabel.find('input');
                        if (textInput.length == 0) {
                            $row.append($inputLabel);
                        }
                    }
                }
                $props.append($row);
            }
        }
        $el.append($props);
    }

    return $el;
}

function duplicateDataImpression(oldUuid) {
    let oldImpressionContents = globals.stateManager.state.impressions[oldUuid];
    let oldImpressionData = globals.stateManager.state.uiData.compose.impressionData[oldUuid];
    let newImpression = {};
    let newImpressionData = {};
    Object.assign(newImpression, oldImpressionContents);
    Object.assign(newImpressionData, oldImpressionData);

    let newUuid = uuid();
    newImpression.uuid = newUuid;
    newImpressionData.position.top += 100;
    newImpressionData.position.left += 100;
    globals.stateManager.update(`/impressions/${newUuid}`, newImpression);
    globals.stateManager.update('uiData/compose/impressionData/' + newUuid, newImpressionData);
}

function editDataImpression(uuid) {
    let $diEditor = $('<div>', {
        class: 'data-impression-editor',
    });

    if ($('.data-impression-editor').length > 0 && $('.data-impression-editor').dialog('isOpen')) {
        alert('There is already an editor dialog open.');
        return;
    }

    // Get rid of any previous instances of the editor dialog that were hidden
    // jQuery UI dialogs just hide the dialog when it's closed
    $('.data-impression-editor').remove();

    let name = 'Data Impression';
    if (globals.stateManager.state.impressions[uuid]) {
        name = globals.stateManager.state.impressions[uuid].name;
    }

    let tags = [];
    if (globals.stateManager.state.impressions[uuid] && globals.stateManager.state.impressions[uuid].tags) {
        tags = globals.stateManager.state.impressions[uuid].tags;
    }

    let $nameEditor = $('<label>', {
        id: 'di-name',
        text: 'Name:'
    }).append($('<input>', {
        type: 'text',
        val: name
    }));
    $diEditor.append($nameEditor);

    let $tagEditor = $('<label>', {
        id: 'di-tags',
        text: 'Tags:'
    }).append($('<input>', {
        type: 'text',
        title: 'Enter tags, separated by commas',
        val: tags.join(',')
    }));
    $diEditor.append($tagEditor);

    let saveProperties = () => {
        let newName = $('#di-name input').val();
        globals.stateManager.update('impressions/' + uuid + '/name', newName);

        let newTagsRaw = $('#di-tags input').val();
        if (newTagsRaw.length > 0) {
            let newTags = newTagsRaw.split(',');
            globals.stateManager.update('impressions/' + uuid + '/tags', newTags);
        } else {
            globals.stateManager.removePath('impressions/' + uuid + '/tags');
        }
    }

    let $saveButton = $('<button>', {
        text: 'Save'
    }).prepend($('<span>', { class: 'material-icons', text: 'save' })).on('click', (evt) => {
        saveProperties();
        $diEditor.dialog('close');
    });
    $diEditor.append($saveButton);

    $diEditor.on('keypress', (evt) => {
        if (evt.key == 'Enter') {
            saveProperties();
            $diEditor.dialog('close')
        }
    });
    
    $diEditor.dialog({
        'title': 'Edit Data Impression',
        position: {my: 'center center', at: 'center center', of: window}
    });
}