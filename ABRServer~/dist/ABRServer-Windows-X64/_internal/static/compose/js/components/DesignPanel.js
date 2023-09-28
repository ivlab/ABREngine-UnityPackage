/* DesignPanel.js
 *
 * Design panel (right side of the ABR Compose UI)
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

import * as Components from './Components.js';
import { globals } from '../../../common/globals.js';
import { CACHE_UPDATE } from '../../../common/StateManager.js';
import { typeMap, gradientTypeMap } from './PuzzlePiece.js';
import { EditorDialog } from './ColormapEditor/EditorDialog.js';
// import { VisAssetGradientDialog } from './ColormapEditor/VisAssetGradientDialog.js';

export function DesignPanel() {
    let $designPanel = $('<div>', {
        class: 'panel',
        id: 'design-panel',
    });

    $designPanel.append($('<p>', {
        class: 'panel-header',
        text: 'Design Palette',
    }));

    // Populate the plates
    let plateTypes = Object.keys(globals.schema.definitions.Plates)
        .map((plt) => Components.Plate(plt));

    let $plateList = Components.SwatchList('Plates', plateTypes);
    $designPanel.append($plateList);

    let $clearMenuItem = $('<li>').append($('<div>', { title: 'Clear all unused VisAssets' }).append(
        $('<span>', { class: 'material-icons', text: 'delete_sweep' })
    ).append(
        $('<span>', { text: 'Clear unused...'})
    ).on('click', async (evt) => {
        let usedUuids = globals.stateManager.findAll((s) => {
            return s.hasOwnProperty('inputGenre') &&
            s['inputGenre'] == 'VisAsset'
        }).map((v) => v.inputValue);

        let visAssets = Object.keys(globals.stateManager.getCache('visassets'));
        let localVisAssets = globals.stateManager.state.localVisAssets;
        localVisAssets = localVisAssets ? Object.keys(localVisAssets) : [];
        let visAssetsToRemove = [...visAssets, ...localVisAssets].filter((v) => usedUuids.indexOf(v) < 0);

        let confirmed = confirm(`Really delete ${visAssetsToRemove.length} VisAssets?`);
        if (confirmed) {
            for (const va of visAssetsToRemove) {
                if (localVisAssets.includes(va)) {
                    // Use await here to make sure the localVisAsset is removed first before the cache is refreshed
                    await globals.stateManager.removePath('localVisAssets/' + va);
                }
                else {
                    globals.stateManager.removeVisAsset(va);
                }
            }
            // Refresh the cache so that the puzzle piece disappear from the panel
            globals.stateManager.refreshCache('visassets');
        }
    }));

    let outTimer = null;
    let $visAssetMenu = $('<ul>', {
        id: 'vis-asset-menu',
        css: { visibility: 'hidden', position: 'fixed' },
    }).append(
        $clearMenuItem
    ).menu().on('mouseout', (evt) => {
        outTimer = setTimeout(() => $('#vis-asset-menu').css('visibility', 'hidden'), 500);
    }).on('mouseover', (evt) => {
        clearTimeout(outTimer);
        outTimer = null;
    });

    $designPanel.append($('<div>', {
        class: 'section-header'
    }).append($('<p>', { text: 'VisAssets' })
    ).append(
        $('<button>', {
            class: 'rounded',
            text: '...',
            title: 'Show additional options...'
        }).on('click', (evt) => {
            $visAssetMenu.css('left', $(evt.target).position().left - $visAssetMenu.width() - $(evt.target).width());
            $visAssetMenu.css('top', $(evt.target).position().top + $visAssetMenu.height() + $(evt.target).height());
            let visibility = $('#vis-asset-menu').css('visibility');
            let newVisibility = visibility == 'visible' ? 'hidden' : 'visible';
            $visAssetMenu.css('visibility', newVisibility);
        })
    )).append($visAssetMenu);

    // Populate the VisAssets
    let visassets = globals.stateManager.getCache('visassets');
    let visassetsCopy = JSON.parse(JSON.stringify(visassets));
    let localVisAssets = globals.stateManager.state.localVisAssets;
    if (localVisAssets) {
        for (const va in localVisAssets) {
            visassetsCopy[va] = localVisAssets[va].artifactJson;
        }
    }
    let visassetsByType = {};

    // Break up by type
    for (const t in typeMap) {
        visassetsByType[t] = [];
    }

    // First, add VisAsset Gradients to their respective types
    let gradients = globals.stateManager.state.visAssetGradients;
    if (gradients) {
        for (const t in visassetsByType) {
            let gradientsOfType = Object.values(gradients).filter((g) => g.gradientType == t);
            for (const g of gradientsOfType) {
                let mockInput = {
                    inputGenre: 'VisAsset',
                    inputValue: g.uuid,
                    inputType: gradientTypeMap[t]
                }
                let $puzzlePiece = Components.SwatchInputPuzzlePiece(gradientTypeMap[t], mockInput);
                visassetsByType[t].push($puzzlePiece);
            }
        }
    }

    // ... Then, add the individual VisAssets
    for (const va in visassetsCopy) {
        let type = visassetsCopy[va].type;
        let artifactType = visassetsCopy[va].artifactType;
        if (typeof(artifactType) !== 'undefined') {
            console.warn('Use of VisAsset field `artifactType` is deprecated, use `type` instead');
        }
        type = type ? type : artifactType;
        let mockInput = {
            inputGenre: 'VisAsset',
            inputValue: va ,
            inputType: typeMap[type]
        }
        let $puzzlePiece = Components.SwatchInputPuzzlePiece(typeMap[type], mockInput);
        visassetsByType[type].push($puzzlePiece);
    }

    for (const t in visassetsByType) {
        let typeCap = t[0].toUpperCase() + t.slice(1);
        let $title = $('<span>');
        $title.append(Components.PuzzleConnector(typeMap[t]))
        $title.append($('<p>', { text: typeCap }))
        let $visAssetList = Components.SwatchList($title, visassetsByType[t]);
        $designPanel.append($visAssetList);
    }

    return $designPanel;
}
