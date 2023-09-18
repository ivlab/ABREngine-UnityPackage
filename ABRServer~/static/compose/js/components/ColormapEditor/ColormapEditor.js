/* ColormapEditor.js
 *
 * Dialog that enables a user to modify colormaps as well as remap data ranges for scalar variables.
 *
 * Copyright (C) 2021, University of Minnesota
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
import { uuid } from '../../../../common/UUID.js';
import { ColorMap, floatToHex, hexToFloat } from './color.js';
import { width, height } from './dialogConsts.js';
import { ColorThumb } from './components.js';

var activeColormap = null;
var currentColormapUuid = null;
var currentVisAssetJson = null;

export async function ColormapEditor(inputProps) {
    let vaUuid = inputProps.inputValue;

    // Ensure all globals are zeroed out on initilization of new dialog
    activeColormap = null;
    currentColormapUuid = null;
    currentVisAssetJson = null;

    let visassetJson = null;
    let colormapXml = null;
    if (globals.stateManager.keyExists(['localVisAssets'], vaUuid)) {
        let va = globals.stateManager.state.localVisAssets[vaUuid];
        visassetJson = va.artifactJson;
        colormapXml = va.artifactDataContents[visassetJson['artifactData']['colormap']];
    } else {
        let visassets = globals.stateManager.getCache('visassets');
        if (visassets && visassets[vaUuid]) {
            visassetJson = visassets[vaUuid];
        }

        // Fetch the colormap xml from the server
        if (visassetJson) {
            let xmlName = visassetJson['artifactData']['colormap'];
            let xmlUrl = `/media/visassets/${vaUuid}/${xmlName}`;
            colormapXml = await fetch(xmlUrl).then((resp) => resp.text());
        }
    }

    if (visassetJson == null || colormapXml == null) {
        alert('No colormap to edit!');
        return;
    }

    currentColormapUuid = vaUuid;
    currentVisAssetJson = visassetJson;

    let $colormapEditor = $('<div>', {
        id: 'colormap-editor',
        class: 'colormap-editor module-editor',
    });

    // Append the colormap canvas
    $colormapEditor.append($('<div>', {
        id: 'colormap',
        class: 'centered',
        title: 'Double-click to create a new color'
    }).append($('<canvas>', {
        class: 'colormap-canvas',
        attr: {
            width: width,
            height: height,
        }
    })).on('dblclick', (evt) => {
        let colormapLeftBound = evt.target.getBoundingClientRect().left;
        let colormapWidth = evt.target.width;
        let clickPercent = (evt.clientX - colormapLeftBound) / colormapWidth;
        let colorAtClick = activeColormap.lookupColor(clickPercent);
        $('#color-slider').append(ColorThumb(clickPercent, floatToHex(colorAtClick), () => {
            updateColormap();
            saveColormap();
        }));
        updateColormap();
    }));

    // Append the color swatch area
    $colormapEditor.append($('<div>', {
        id: 'color-slider',
        class: 'centered',
    }));


    // Add the UI buttons
    let $buttons = $('<div>', {
        class: 'centered',
    });

    $buttons.append($('<button>', {
        class: 'flip-colormap colormap-button',
        text: 'Flip',
        title: 'Flip colormap',
    }).on('click', (evt) => {
        activeColormap.flip();
        saveColormap().then((u) => {
            updateColormapDisplay();
            updateColorThumbPositions();
        });
    }).prepend($('<span>', { class: 'ui-icon ui-icon-transferthick-e-w'})));

    $buttons.append($('<button>', {
        class: 'colormap-button',
        text: 'Save copy to library',
        title: 'Save a copy of this colormap to the local library for reuse in other visualizations',
    }).on('click', (evt) => {
        updateColormap();
        saveColormap().then((u) => {
            saveColormapToLibrary(u);
        });
    }).prepend($('<span>', { class: 'ui-icon ui-icon-disk'})));

    $colormapEditor.append($buttons);

    // Wait to render anything until this is part of the DOM
    $colormapEditor.on('ABR_AddedToEditor', () => {
        // Populate the colors from xml
        activeColormap = ColorMap.fromXML(colormapXml);
        activeColormap.entries.forEach((c) => {
            let pt = c[0];
            let color = floatToHex(c[1]);
            let $thumb = ColorThumb(pt, color, () => {
                updateColormap();
                saveColormap();
            });
            $('#color-slider').append($thumb);
        });
        updateColormap();
    });

    return $colormapEditor;
}

async function saveColormap() {
    let oldUuid = currentColormapUuid;
    let artifactJson = currentVisAssetJson;
    // Give it a new uuid if it doesn't already exist in localVisAssets
    let newUuid = oldUuid;
    if (!globals.stateManager.keyExists(['localVisAssets'], oldUuid)) {
        newUuid = uuid();
    }

    artifactJson['uuid'] = newUuid;

    // Gather the xml
    let xml = activeColormap.toXML();
    let data = {
        artifactJson,
        artifactDataContents: {
            'colormap.xml': xml,
        }
    };

    // Update the state with this particular local colormap
    // If the visasset isn't found on the server, abort
    let visAssetFound = await globals.stateManager.update(`localVisAssets/${newUuid}`, data)
        .then(() => true)
        .catch(() => false);
    if (!visAssetFound) {
        return oldUuid;
    }

    // Attach the new colormap, if we've changed UUIDs
    if (newUuid != oldUuid) {
        let pathsToUpdate = globals.stateManager.findPath((el) => {
            return el.hasOwnProperty('inputType') && 
                el.inputType == 'IVLab.ABREngine.ColormapVisAsset' &&
                el.hasOwnProperty('inputValue') && 
                el.inputValue == oldUuid
        });
        for (const p of pathsToUpdate) {
            globals.stateManager.update(`${p}/inputValue`, newUuid);
        }
    }

    currentVisAssetJson = artifactJson;
    currentColormapUuid = newUuid

    return newUuid;
}

async function saveColormapToLibrary(vaUuid) {
    return fetch('/api/save-local-visasset/' + vaUuid, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            // 'X-CSRFToken': csrftoken,
        },
        mode: 'same-origin'
    });
}

function updateColormap() {
    updateSpectrum();
    activeColormap = getColormapFromThumbs();
    updateColorThumbPositions();
    updateColormapDisplay();
}

function getColormapFromThumbs() {
    let mapLeft = $('#colormap .colormap-canvas').position().left;
    let newcmap = new ColorMap();
    $('#color-slider .color-thumb').each((_i, el) => {
        let middle = ($(el).position().left - mapLeft) + $(el).width() / 2;
        let percentage = middle / $('#colormap .colormap-canvas').width();
        let color = $(el).find('input.color-input').val();
        newcmap.addControlPt(percentage, hexToFloat(color));
    });
    return newcmap;
}

function updateColormapDisplay() {
    let ctx = $('#colormap .colormap-canvas').get(0).getContext('2d');
    activeColormap.toCanvas(ctx);
}

function updateColorThumbPositions() {
    $('#color-slider').empty();
    activeColormap.entries.forEach((c) => {
        let pt = c[0];
        let color = floatToHex(c[1]);
        let $thumb = ColorThumb(pt, color, () => {
            updateColormap();
            saveColormap();
        });
        $thumb.data({
            trashed: (evt, ui) => {
                $(ui.draggable).remove();
                activeColormap = getColormapFromThumbs();
            }
        });
        $('#color-slider').append($thumb);
    });
    updateSpectrum();
}

function updateSpectrum() {
    // Remove all old spectrum containers
    $('.sp-container').remove();
    $('.color-input').spectrum({
        type: "color",
        showPalette: false,
        showAlpha: false,
        showButtons: false,
        allowEmpty: false,
        showInitial: true,
        showInput: true,
        showPalette: true,
        preferredFormat: 'hex',
    });
}