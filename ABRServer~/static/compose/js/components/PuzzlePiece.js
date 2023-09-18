/* PuzzlePiece.js
 *
 * Copyright (C) 2021, University of Minnesota
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
import { CACHE_UPDATE, resolveSchemaConsts } from "../../../common/StateManager.js";
import { uuid } from "../../../common/UUID.js";
import { ColorMap } from "./ColormapEditor/color.js";
import { gradientToColormap } from "./ColormapEditor/GradientEditor.js";
import { PrimitiveInput } from "./Primitives.js";
import { VariableList } from "./VariableList.js";
import { EditorDialog, TITLE_STRINGS } from "./ColormapEditor/EditorDialog.js";
import { gradientPreviewThumbnail } from "./ColormapEditor/VisAssetGradientEditor.js";

const cssObjectFitMap = {
    'IVLab.ABREngine.ColormapVisAsset': 'fill',
    'IVLab.ABREngine.PrimitiveGradient': 'fill',
    'IVLab.ABREngine.LineTextureVisAsset': 'cover',
    'IVLab.ABREngine.SurfaceTextureVisAsset': 'contain',
    'IVLab.ABREngine.GlyphVisAsset': 'contain',
}

export const typeMap = {
    'colormap': 'IVLab.ABREngine.ColormapVisAsset',
    'glyph': 'IVLab.ABREngine.GlyphVisAsset',
    'line': 'IVLab.ABREngine.LineTextureVisAsset',
    'texture': 'IVLab.ABREngine.SurfaceTextureVisAsset',
};

export const gradientTypeMap = {
    'glyph': 'IVLab.ABREngine.GlyphGradient',
    'line': 'IVLab.ABREngine.LineTextureGradient',
    'texture': 'IVLab.ABREngine.SurfaceTextureGradient',
};

export function PuzzlePiece(label, inputType, leftConnector, addClasses) {
    let $element = $('<div>', {
        class: 'puzzle-piece rounded ' + addClasses,
    });

    let $connector = PuzzleConnector(inputType, addClasses);

    // If the connector should be on the right, leave the order. Otherwise,
    // make the connector show up to the left of the label
    if (!leftConnector) {
        $element.append(PuzzleLabel(label));
        $element.append($connector);
    } else {
        $element.append($connector);
        $element.append(PuzzleLabel(label));
    }

    $element.attr('title', label);
    $element.on('dragstart', (evt, ui) => {
        // Find all drop zones that match this input type
        let elType = $(evt.target).data('inputType');
        let $dropZones = $('.input-socket .puzzle-piece.drop-zone');
        let validTypes = [elType];
        let isVisAsset = Object.keys(typeMap)
            .map((t) => { return {'visAssetType': t, 'abrType': typeMap[t]}})
            .find((tt) => tt['abrType'] == elType);
        let isGradient = Object.keys(gradientTypeMap)
            .map((t) => { return {'visAssetType': t, 'abrType': gradientTypeMap[t]}})
            .find((tt) => tt['abrType'] == elType);
        if (isVisAsset) {
            validTypes = validTypes + [isVisAsset['abrType'], gradientTypeMap[isVisAsset['visAssetType']]];
        }
        if (isGradient) {
            validTypes = validTypes + [isGradient['abrType'], typeMap[isGradient['visAssetType']]];
        }
        let $sameTypeZones = $dropZones.filter((i, el) => validTypes.indexOf($(el).data('inputType')) >= 0);
        $sameTypeZones.addClass('highlighted');
    });
    $element.on('dragstop', (evt, ui) => {
        $('.puzzle-piece.drop-zone').removeClass('highlighted');
    });
    return $element;
}

export function PuzzlePieceWithThumbnail(uuid, inputType, leftConnector, addClasses, cssObjectFit) {
    let thumbUrl;
    let visassets = globals.stateManager.getCache('visassets');
    let localVisAssets = globals.stateManager.state.localVisAssets;
    let gradients = globals.stateManager.state.primitiveGradients;
    let visAssetGradients = globals.stateManager.state.visAssetGradients;

    let $thumb = $('<img>', {
        class: 'artifact-thumbnail rounded',
        src: `${STATIC_URL}compose/${inputType}_default.png`,
    });

    if (visassets && visassets[uuid]) {
        let previewImg = visassets[uuid]['preview'];
        $thumb.attr('src', `/media/visassets/${uuid}/${previewImg}`);
    } else if (localVisAssets && localVisAssets[uuid]) {
        // TODO assuming colormap xml for now
        let colormapXml = localVisAssets[uuid].artifactDataContents['colormap.xml'];
        let colormapObj = ColorMap.fromXML(colormapXml);
        $thumb.attr('src', colormapObj.toBase64(true));
    } else if (gradients && gradients[uuid]) {
        let colormap = gradientToColormap(gradients[uuid]);
        $thumb.attr('src', colormap.toBase64(true));
    } else if (visAssetGradients && visAssetGradients[uuid]) {
        $thumb = gradientPreviewThumbnail(visAssetGradients[uuid], 300, 50);
    }

    if (cssObjectFit) {
        $thumb.css('object-fit', cssObjectFit);
    }

    let $tooltip = $('#thumbnail-tooltip')
        .css('width', '30rem')
        .css('height', '10rem')
        .css('overflow', 'hidden');
    let tooltipOffset = 20; // pixels to upper left of mouse to center image on
    let timer = null;
    let $clone = $thumb.clone()
        .css('width', '100%')
        .css('height', '100%')
        .css('object-fit', cssObjectFit);
    $thumb.on('mouseover', (_evt) => {
        $tooltip.css('visibility', 'visible');
        $tooltip.append($clone);
        timer = setTimeout(() => $tooltip.css('visibility', 'hidden'), 2000);
    }).on('mousemove', (evt) => {
        $tooltip.css('top', `${evt.pageY - $clone.height() - tooltipOffset}px`);
        $tooltip.css('left', `${evt.pageX - $clone.width()}px`);
        clearTimeout(timer);
        timer = setTimeout(() => $tooltip.css('visibility', 'hidden'), 2000);
    }).on('mouseout', (_evt) => {
        $tooltip.css('visibility', 'hidden');
        clearTimeout(timer);
        timer = null;
        $tooltip.empty();
    });

    let $ret = PuzzlePiece($thumb, inputType, leftConnector, addClasses);

    // If it's a localVisAsset or visAsset Gradient, indicate it as such
    if (globals.stateManager.keyExists(['localVisAssets'], uuid)) {
        $ret.find('.puzzle-label').append($('<p>', {
            class: 'custom-indicator rounded',
            attr: { title: 'This colormap is custom' },
            text: 'C',
        }));
    }
    if (globals.stateManager.keyExists(['visAssetGradients'], uuid)) {
        $ret.find('.puzzle-label').append($('<p>', {
            class: 'custom-indicator rounded',
            attr: { title: 'VisAsset Gradient' },
            text: 'G',
        }));
    }

    return $ret;

}

// Can be either something waiting for an input or the input itself
export function InputPuzzlePiece(inputName, inputProps, addClass) {
    let $el;
    let resolvedProps = resolveSchemaConsts(inputProps);

    if (resolvedProps.inputGenre == 'VisAsset') {
        if (resolvedProps && !resolvedProps.inputValue) {
            $el = PuzzlePiece(inputName, resolvedProps.inputType, true, addClass);

            // Special case to enable visasset gradients to be created by clicking
            let visassetType = Object.keys(typeMap).find((k) => typeMap[k] == resolvedProps.inputType);
            if (Object.keys(gradientTypeMap).indexOf(visassetType) >= 0) {
                let gradientType = gradientTypeMap[visassetType];
                let humanReadable = TITLE_STRINGS[gradientType];
                resolvedProps.inputType = gradientType;
                $el.attr('title', $el.attr('title') + '\nClick to add a ' + humanReadable);
                $el.addClass('hover-bright');
                $el.css('cursor', 'pointer');
                let clickEvt = (evt) => {
                    // Create a new uuid if there's no input value yet
                    let impressionUuid = $el.parents('.data-impression').data('uuid');
                    let updatePromise = null;
                    if (!resolvedProps.inputValue) {
                        resolvedProps.inputValue = uuid();
                        updatePromise = globals.stateManager.update(`impressions/${impressionUuid}/inputValues/${inputName}`, resolvedProps);
                    } else {
                        updatePromise = new Promise((resolve, reject) => resolve());
                    }
                    updatePromise.then(() => {
                        EditorDialog(resolvedProps, impressionUuid);
                    });
                };
                $el.on('dblclick', clickEvt);
                $el.on('click', clickEvt);
            }
        } else {
            let uuid = resolvedProps.inputValue;
            // Add the family / class to tooltip
            let visassets = globals.stateManager.getCache('visassets');
            let localVisAssets = globals.stateManager.state.localVisAssets;
            let vaFamily = '';
            let vaClass = '';
            if (visassets && visassets[uuid]) {
                vaClass = visassets[uuid]['class'] || '';
                vaFamily = visassets[uuid]['family'] || '';
            } else if (localVisAssets && localVisAssets[uuid]) {
                vaClass = localVisAssets[uuid].artifactJson['class'] || '';
                vaFamily = localVisAssets[uuid].artifactJson['family'] || '';
            }
            let familyClass = `${vaClass} - ${vaFamily}`;

            let args = [
                resolvedProps.inputValue,
                resolvedProps.inputType,
                true,
                addClass,
                cssObjectFitMap[resolvedProps.inputType]
            ];

            $el = PuzzlePieceWithThumbnail(...args);
            $el.attr('title', familyClass);

            // Allow the colormap to be edited
            if (resolvedProps.inputType == 'IVLab.ABREngine.ColormapVisAsset') {
                $el.attr('title', $el.attr('title') + '\nClick to customize');
                $el.addClass('hover-bright');
                let dragging = false;
                $el.on('dragstart', () => dragging = true);
                $el.on('dragend', () => dragging = false);
                $el.css('cursor', 'pointer');
                let clickEvt = (evt) => {
                    if (!dragging) {
                        let impressionUuid = $el.parents('.data-impression').data('uuid');
                        EditorDialog(resolvedProps, impressionUuid);
                    }
                };
                $el.on('dblclick', clickEvt);
                $el.on('click', clickEvt);
            }

            // Allow the gradient to be edited
            let gradient = false;
            if (Object.values(gradientTypeMap).indexOf(resolvedProps.inputType) >= 0) {
                $el.attr('title', $el.attr('title') + '\nClick to customize');
                $el.addClass('hover-bright');
                let dragging = false;
                $el.on('dragstart', () => dragging = true);
                $el.on('dragend', () => dragging = false);
                $el.css('cursor', 'pointer');
                let clickEvt = (evt) => {
                    if (!dragging) {
                        // let gradUuid = $el.data('inputValue');
                        // VisAssetGradientDialog(gradUuid);
                        let impressionUuid = $el.parents('.data-impression').data('uuid');
                        EditorDialog(resolvedProps, impressionUuid);
                    }
                };
                $el.on('dblclick', clickEvt);
                $el.on('click', clickEvt);

                gradient = true;
            }

            // Handle right-click to copy VisAsset import code for ABR
            $el.attr('title', $el.attr('title') + '\nRight-click to copy C# ABR code');
            $el.on('contextmenu', (evt) => {
                evt.preventDefault();
                // Get the UUID
                let uuid = $el.data('inputValue');
                if (!gradient) {
                    navigator.clipboard.writeText(uuid);
                    $.toast({
                        text: `Copied UUID ${uuid} import code to clipboard`,
                        hideAfter: 2000,
                        position: 'bottom-right'
                    });
                } else {
                    $.toast({
                        text: `Cannot copy gradient UUIDs`,
                        icon: 'warning',
                        hideAfter: 2000,
                        position: 'bottom-right'
                    });
                }
            });
        }
    } else if (resolvedProps.inputGenre == 'Variable') {
        if (resolvedProps && resolvedProps.inputValue) {
            $el = PuzzlePiece(DataPath.getName(resolvedProps.inputValue), resolvedProps.inputType, false, addClass);
        } else {
            $el = PuzzlePiece(inputName, resolvedProps.inputType, false, addClass);
        }
        $el.attr('title', resolvedProps && resolvedProps.inputValue ? resolvedProps.inputValue : 'No Variable');

        $el.attr('title', $el.attr('title') + '\nClick to change variable');
        $el.css('cursor', 'pointer');
        $el.addClass('hover-bright');
        let dragging = false;
        $el.on('dragstart', () => dragging = true);
        $el.on('dragend', () => dragging = false);

        // Allow the user to easily change the variable to another associated with this keydata
        $el.on('click', (evt) => {
            if (dragging) {
                return;
            }
            let impressionUuid = $(evt.target).parents('.data-impression').data('uuid');
            let keyDatas = globals.stateManager.findPath((s) => {
                return s.hasOwnProperty('inputGenre') &&
                    s['inputGenre'] == 'KeyData' && 
                s.hasOwnProperty('parameterName') &&
                    s['parameterName'] == 'Key Data'
            });
            let keyDataPath = keyDatas.find((p) => p.split('/')[2] == impressionUuid);
            if (!keyDataPath)
            {
                return;
            }
            let keyDataInput = globals.stateManager.getPath(keyDataPath).inputValue;
            let [org, dataset, _, kd] = DataPath.getPathParts(keyDataInput);
            let rawMetadata = globals.dataCache[org][dataset][kd];
            if (!rawMetadata) {
                alert(`Key data '${keyDataInput}' does not exist in the Data Palette.`)
                return;
            }
            let varNames = [];
            if (resolvedProps.inputType == 'IVLab.ABREngine.ScalarDataVariable') {
                varNames = rawMetadata.scalarArrayNames;
            } else if (resolvedProps.inputType == 'IVLab.ABREngine.VectorDataVariable') {
                varNames = rawMetadata.vectorArrayNames;
            }

            let impressionPath = keyDataPath.split('/').slice(0, 4);
            let statePath = impressionPath.join('/') + `/${inputName}`;
            let $varList = VariableList(varNames, statePath, resolvedProps, keyDataInput);
            $varList.appendTo('body');
            $varList.css('position', 'absolute');
            $varList.css('left', $(evt.target).offset().left);
            $varList.css('top', $(evt.target).offset().top);
        })
    } else if (resolvedProps.inputGenre == 'KeyData') {
        if (resolvedProps && resolvedProps.inputValue) {
            $el = PuzzlePiece(DataPath.getName(resolvedProps.inputValue), resolvedProps.inputType, false, 'keydata ' + addClass);
        } else {
            $el = PuzzlePiece(inputName, resolvedProps.inputType, false, 'keydata ' + addClass);
        }
        $el.attr('title', resolvedProps && resolvedProps.inputValue ? resolvedProps.inputValue : null);
    } else if (resolvedProps.inputGenre == 'Primitive') {
        $el = PrimitiveInput(inputName, inputName, resolvedProps);
        $el.addClass('no-drag');
        $el.addClass(addClass);
    } else if (resolvedProps.inputGenre == 'PrimitiveGradient') {
        let gradientUuid = null;
        let args = [
            resolvedProps.inputValue,
            resolvedProps.inputType,
            false,
            addClass,
            'fill'
        ];
        if (resolvedProps && resolvedProps.inputValue) {
            $el = PuzzlePieceWithThumbnail(...args);
            $el.attr('title', 'Click to edit gradient');
            gradientUuid = resolvedProps.inputValue;
        } else {
            $el = PuzzlePiece(inputName, resolvedProps.inputType, false, addClass);
            $el.attr('title', 'Click to add gradient');
        }
        $el.css('cursor', 'pointer');

        let dragging = false;
        $el.on('dragstart', () => dragging = true);
        $el.on('dragend', () => dragging = false);

        $el.on('click', (evt) => {
            if (dragging) {
                return;
            }
            let impressionUuid = $(evt.target).parents('.data-impression').data('uuid');
            // Create a new uuid if there's no input value yet
            let updatePromise = null;
            if (!resolvedProps.inputValue) {
                resolvedProps.inputValue = uuid();
                updatePromise = globals.stateManager.update(`impressions/${impressionUuid}/inputValues/${inputName}`, resolvedProps);
            } else {
                updatePromise = new Promise((resolve, reject) => resolve());
            }
            updatePromise.then(() => {
                EditorDialog(resolvedProps, impressionUuid);
            });
        });
    }

    $el.data('inputName', inputName);
    $el.data('parameterName', resolvedProps.parameterName);
    $el.data('inputGenre', resolvedProps.inputGenre);
    $el.data('inputType', resolvedProps.inputType);
    $el.data('inputValue', resolvedProps.inputValue);
    return $el;
}

// A puzzle piece that's already assigned on a data impression; when it's
// removed it will send a message to the server telling it that it's removed
export function AssignedInputPuzzlePiece(inputName, inputProps, addClass=undefined) {
    let $input = InputPuzzlePiece(inputName, inputProps, addClass);
    if (!$input.hasClass('no-drag')) {
        $input.draggable({
            cursor: 'grabbing',
            drag: (evt, ui) => {
                evt.stopPropagation();
            },
            stop: (evt, _ui) => {
                if ($(evt.target).data('draggedOut')) {
                    // Unassign this input
                    let uuid = $(evt.target).parents('.data-impression').data('uuid');
                    globals.stateManager.removePath(`impressions/${uuid}/inputValues/${inputName}`);
                }
            }
        });
    }
    $input.css('position', 'absolute');
    $input.css('top', 0);
    $input.css('left', 0);
    return $input;
}

// A "swatch"; a puzzle piece that lives in a palette
export function SwatchInputPuzzlePiece(inputName, inputProps) {
    return InputPuzzlePiece(inputName, inputProps).draggable({
        helper: 'clone',
        cursor: 'grabbing',
        drag: (evt, ui) => {
            // Enable swatch puzzle pieces to float OVER any dialogs that need it
            let $d = $('.puzzle-piece-overlay-dialog');
            if ($d.length > 0) {
                let pos = $(ui.helper).position();
                let dPos = $d.parents('.ui-dialog').position();
                let gPos = $d.position();
                let cPos = {left: dPos.left - gPos.left, top: dPos.top - gPos.top};
                if (pos.left > cPos.left && pos.left < cPos.left + $d.width() &&
                    pos.top > cPos.top && pos.top < cPos.top + $d.height()
                ) {
                    $(ui.helper).appendTo($d);
                    $(ui.helper).css('position', 'fixed');
                }
            }
        }
    }).addClass('swatch');
}

// A connector for a puzzle piece
export function PuzzleConnector(type, addClasses='', background=false) {
    let backgroundClass = background ? 'background' : 'foreground-contrast';
    let $connector = $('<div>', { class: `puzzle-connector ${backgroundClass} ${addClasses}` })
        .css('mask', `url(${STATIC_URL}compose/puzzle_pieces/${type}.svg)`)
        .css('-webkit-mask', `url(${STATIC_URL}compose/puzzle_pieces/${type}.svg)`);
    return $connector;
}

// A puzzle piece label
function PuzzleLabel(name) {
    if (name instanceof jQuery) {
        return $('<div>', { class: 'puzzle-label rounded' }).append(name);
    } else {
        return $('<div>', { class: 'puzzle-label rounded' }).append($('<p>', { text: name }));
    }
}

// Get the color variable for the data impression this input is associated with
function getColorVar($el) {
    let impressionUuid = $el.parents('.data-impression').data('uuid');
    let colorVars = globals.stateManager.findPath((s) => {
        return s.hasOwnProperty('inputGenre') &&
            s['inputGenre'] == 'Variable' && 
        s.hasOwnProperty('inputType') &&
            s['inputType'] == 'IVLab.ABREngine.ScalarDataVariable' && 
        s.hasOwnProperty('parameterName') &&
            s['parameterName'] == 'Color'
    });
    let colorVarPath = colorVars.find((p) => p.split('/')[2] == impressionUuid);


    let colorVar = null;
    if (colorVarPath) { 
        colorVar = globals.stateManager.getPath(colorVarPath);
    }
    return colorVar;
}


// Get the key data for the data impression this input is associated with
function getKeyData($el) {
    let impressionUuid = $el.parents('.data-impression').data('uuid');
    let keyDatas = globals.stateManager.findPath((s) => {
        return s.hasOwnProperty('inputGenre') &&
            s['inputGenre'] == 'KeyData' && 
        s.hasOwnProperty('parameterName') &&
            s['parameterName'] == 'Key Data'
    });

    let keyDataPath = keyDatas.find((p) => p.split('/')[2] == impressionUuid);
    let keyData = null;
    if (keyDataPath) {
        keyData = globals.stateManager.getPath(keyDataPath);
    }
    return keyData;
}