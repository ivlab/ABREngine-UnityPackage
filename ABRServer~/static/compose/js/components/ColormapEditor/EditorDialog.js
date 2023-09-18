/* EditorDialog.js
 *
 * Dialog that enables a user to remap data ranges for scalar variables and edit
 * colormaps, visasset gradients, and primitive gradients.
 *
 * Copyright (C) 2022, University of Minnesota
 * Authors:
 *   Bridger Herman <herma582@umn.edu>
 *   Kiet Tran <tran0563@umn.edu>
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

import { globals } from '../../../../common/globals.js';
import { ColormapEditor } from './ColormapEditor.js';
import { dialogWidth } from './dialogConsts.js';
import { GradientEditor } from './GradientEditor.js';
import { HistogramEditor } from './HistogramEditor.js';
import { VisAssetGradientEditor } from './VisAssetGradientEditor.js';

const EDITOR_HANDLERS = {
    'IVLab.ABREngine.ColormapVisAsset': ColormapEditor,
    'IVLab.ABREngine.GlyphGradient': VisAssetGradientEditor,
    'IVLab.ABREngine.SurfaceTextureGradient': VisAssetGradientEditor,
    'IVLab.ABREngine.LineTextureGradient': VisAssetGradientEditor,
    'IVLab.ABREngine.PrimitiveGradient': GradientEditor,
};

export const TITLE_STRINGS = {
    'IVLab.ABREngine.ColormapVisAsset': 'Colormap',
    'IVLab.ABREngine.GlyphGradient': 'Glyph Gradient',
    'IVLab.ABREngine.SurfaceTextureGradient': 'Texture Gradient',
    'IVLab.ABREngine.LineTextureGradient': 'Line Gradient',
    'IVLab.ABREngine.PrimitiveGradient': 'Opacity Map',
};

// Unified for editing anything that's assocated with a particular variable.
//
// Exhaustive options (all include variable range editor w/histogram):
// - Colormap (glyph, ribbon, surface, volume)
// - Colormap + Primitive Gradient (volume)
// - VisAsset Gradient (glyph ribbon, surface)
// - VisAsset Gradient + Colormap (glyph ribbon, surface)
//
// Configuration is determined based on variable inputs. For example, if the
// variable `Test/Cube/ScalarVar/XAxis` is assigned to both the Glyph Variable
// and the Colormap Variable, both the VisAsset Gradient Editor and the Colormap
// editor will be shown in the dialog.
//
// Needs the following input:
// - The entire input properties object of the item that was clicked to edit
// - UUID of the data impression the item that was clicked to edit
//
// Each dialog unit is represented as a "Module" that is included or not based
// on inputs from the current data impression.
export async function EditorDialog(inputProps, impressionUuid) {
    let $editorDialog = $('<div>', {
        class: 'editor-dialog puzzle-piece-overlay-dialog' // enable puzzle pieces to float *over* dialog
    });

    // There can only be one editor
    if ($('.editor-dialog').length > 0 && $('.editor-dialog').dialog('isOpen')) {
        alert('There is already an editor dialog open.');
        return;
    }

    // Get rid of any previous instances of the editor dialog that were hidden
    // jQuery UI dialogs just hide the dialog when it's closed
    $('.editor-dialog').remove();

    // SETUP: Figure out what "modules" are needed for this editor.
    // We need to determine the variable that is associated with the input
    // that was clicked to edit. Only display this stuff if the input is
    // associated with a data impression.
    let inputsToConsider = [];
    let variableEditorTitle = '';
    if (impressionUuid) {
        // First, get the key data for the data impression this input is associated with
        let keyDataInput = null;
        let keyDataStatePath = globals.stateManager.findPath((s) => {
            return s.hasOwnProperty('inputGenre') &&
                s['inputGenre'] == 'KeyData' && 
                s.hasOwnProperty('parameterName') &&
                s['parameterName'] == 'Key Data'
        }).find((p) => p.split('/')[2] == impressionUuid);
        if (keyDataStatePath) {
            keyDataInput = globals.stateManager.getPath(keyDataStatePath);
        }

        // Then, get the all other variables associated with this input
        // Find the variable that's paired with this input
        let paramName = inputProps.parameterName
        let associatedVars = globals.stateManager.findAll((s) => {
            return s.hasOwnProperty('inputGenre') &&
                s['inputGenre'] == 'Variable' && 
                s.hasOwnProperty('parameterName') &&
                s['parameterName'] == paramName
        }, `/impressions/${impressionUuid}/inputValues`).map((v) => v.inputValue);

        // Find every variable input that's the same as this one
        let impressionInputs = globals.stateManager.state.impressions[impressionUuid].inputValues;
        let relevantInputNames = Object.keys(impressionInputs).filter((n) => associatedVars.indexOf(impressionInputs[n].inputValue) >= 0);
        let relevantParamNames = relevantInputNames.map((n) => impressionInputs[n].parameterName);

        // And map it back to its design / visasset input
        let associatedDesignInputs = globals.stateManager.findAll((s) => {
            return s.hasOwnProperty('inputGenre') &&
                (s['inputGenre'] == 'VisAsset' || s['inputGenre'] == 'PrimitiveGradient')  && 
                s.hasOwnProperty('parameterName') &&
                relevantParamNames.indexOf(s['parameterName']) >= 0
        }, `/impressions/${impressionUuid}/inputValues`);
        inputsToConsider.push(...associatedDesignInputs);
        // Also add any more design inputs that are associated in this current param name
        inputsToConsider.push(...Object.values((v) => v.parameterName == inputProps.parameterName));

        let trueVariable = impressionInputs[relevantInputNames[0]];
        if (trueVariable) {
            let $histogramModule = await HistogramEditor(trueVariable, keyDataInput);
            $editorDialog.append($histogramModule);
            variableEditorTitle = ' + Data Range ';
        }
    }

    // Make sure AT LEAST the input we passed in shows up, if it exists...
    if (inputProps && !inputsToConsider.find(i => i.inputValue == inputProps.inputValue)) {
        inputsToConsider.push(inputProps);
    }

    let titleString = '';
    if (inputProps) {
        // Find handlers for each valid input
        for (const input of inputsToConsider) {
            let handler = EDITOR_HANDLERS[input.inputType];
            if (handler != null) {
                titleString += TITLE_STRINGS[input.inputType] + ' + ';
                let $moduleEditor = await handler(input);
                if (input.inputValue == inputProps.inputValue) {
                    $moduleEditor.addClass('editor-source');
                }
                $editorDialog.append($moduleEditor);
            }
        }
    }
    titleString = titleString.slice(0, titleString.length - 2) + variableEditorTitle + 'Editor';

    let $trash = $('<img>', {
        id: 'trash',
        src: `${STATIC_URL}compose/trash.svg`,
    }).droppable({
        tolerance: 'touch',
        accept: '.editor-trashable',
        drop: (evt, ui) => {
            let trashedFn = ui.draggable.data('trashed');
            if (trashedFn) {
                trashedFn(evt, ui);
            }
        },
        // Indicate that it's about to be deleted
        over: (_evt, ui) => {
            $(ui.helper).css('opacity', '25%');
        },
        out: (_evt, ui) => {
            $(ui.helper).css('opacity', '100%');
        }
    }).attr('title', 'Drop a gradient stop here to discard');

    $editorDialog.append($trash);


    $editorDialog.dialog({
        'title': titleString,
        position: {my: 'center top', at: 'center top', of: window},
        'minWidth': dialogWidth,
    });

    // Tell each "module" it's been added to editor
    $('.module-editor').trigger('ABR_AddedToEditor');
}