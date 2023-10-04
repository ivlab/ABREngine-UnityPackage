/* CompositionPanel.js
 *
 * Composition panel (Center of the ABR Compose UI)
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

import { globals } from "../../../common/globals.js";

export const COMPOSITION_ID = 'composition-panel';
export const COMPOSITION_LOADER_ID = 'composition-loader';

export function CompositionPanel() {
    let $compositionPanel = $('<div>', {
        class: 'panel',
        id: COMPOSITION_ID,
    });

    $compositionPanel.append($('<p>', {
        class: 'panel-header',
        text: 'Visualization Composition',
    }));

    // The composition data impressions are loaded here
    let $loader = $('<div>', {
        id: COMPOSITION_LOADER_ID,
    });

    $compositionPanel.append($('<div>', {
        id: 'composition-scrollbox',
        class: 'dragscroll nochilddrag'
    }).append($loader));


    // Add the trash can functionality
    let $trash = $('<div>', {class: 'trash'}).append(
        $('<img>', {
            src: `${STATIC_URL}compose/trash.svg`,
        })
    ).droppable({
        tolerance: 'pointer',
        classes: {
            "ui-droppable-hover": "trash-droppable-hover"
        },
        drop: (_evt, ui) => {
            let uuidDataImpression = $(ui.draggable).data('uuid');
            let uuidVisAsset = $(ui.draggable).data('inputValue');
            
            // Trash data impressions
            if (uuidDataImpression) {
                $(ui.draggable).remove();
                globals.stateManager.removeAll(uuidDataImpression);
            }

            // Trash VisAssets
            if (uuidVisAsset) {
                // If it's a swatch, remove it from palette
                if ($(ui.draggable).hasClass('swatch')) {
                    let localVisAssets = globals.stateManager.state.localVisAssets;
                    let visAssetGradients = globals.stateManager.state.visAssetGradients;
                    if (localVisAssets && localVisAssets.hasOwnProperty(uuidVisAsset)) {
                        // Trash local VisAssets (Custom Colormaps)
                        globals.stateManager.removePath('localVisAssets/' + uuidVisAsset);
                    } else if (visAssetGradients && visAssetGradients.hasOwnProperty(uuidVisAsset)) {
                        // Trash VisAsset gradients
                        globals.stateManager.removePath('visAssetGradients/' + uuidVisAsset);
                    } else {
                        // Trash other kinds of VisAssets (Colormaps, Glyphs, Line Textures, Surface Textures)
                        globals.stateManager.removeVisAsset(uuidVisAsset);
                    }
                } else {
                    // Otherwise, just remove it from this tower (the user
                    // probably meant to just drag it off the tower)
                    let impressionUuid = $(ui.draggable).parents('.data-impression').data('uuid');
                    let inputName = $(ui.draggable).data('inputName');
                    globals.stateManager.removePath(`impressions/${impressionUuid}/inputValues/${inputName}`);
                }
                $(ui.draggable).remove();
            }
        },
        // Indicate that it's about to be deleted
        over: (_evt, ui) => {
            let value = $(ui.draggable).data('uuid') || $(ui.draggable).data('inputValue');
            if (value) {
                $(ui.helper).addClass('removing');
                $(ui.helper).css('opacity', '25%');
            }
        },
        out: (_evt, ui) => {
            $(ui.helper).removeClass('removing');
            $(ui.helper).css('opacity', '100%');
        }
    }); 

    $compositionPanel.append($trash);

    return $compositionPanel;
}
