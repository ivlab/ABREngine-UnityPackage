/* ComposeManager.js
 *
 * Window/dialog manager for the compose UI
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

import { globals } from '../../common/globals.js';
import { STATE_UPDATE_EVENT, CACHE_UPDATE } from '../../common/StateManager.js';
import * as Components from './components/Components.js';
import { COMPOSITION_LOADER_ID } from './components/Components.js';
import { DataImpression } from './components/DataImpression.js';

export class ComposeManager {
    constructor() {
        // Tooltips for larger images on hover adapted from
        // https://chartio.com/resources/tutorials/how-to-show-data-on-mouseover-in-d3js/
        $('body').append($('<div>', {
            id: 'thumbnail-tooltip',
            css: {
                'position': 'absolute',
                'z-index': '100',
                'visibility': 'hidden',
            },
        }));

        this.header = Components.Header();

        this.$element = $('#compose-manager');

        this.$element.append(this.header);
        this.$element.append($('<div>', {
            id: 'panel-container',
        })
            .append(Components.DataPanel())
            .append(Components.CompositionPanel())
            .append(Components.DesignPanel())
        );

        globals.stateManager.subscribe(this.$element);
        this.$element.on(STATE_UPDATE_EVENT, (_evt) =>  this.syncWithState() );
        this.syncWithState();

        // Prepare the Design Panel to be refreshed when new visassets come in
        globals.stateManager.subscribeCache('visassets', this.$element);
        this.$element.on(CACHE_UPDATE + 'visassets', (evt) => {
            evt.stopPropagation();

            // Keep track of open/closed panels
            let collapsibleStates = [];
            $(evt.target).find('.collapsible-header').each((_i, el) => {
                collapsibleStates.push($(el).hasClass('active'));
            });

            $('#design-panel').remove();
            this.$element.children('#panel-container').append(Components.DesignPanel());

            // Reset open/closed panels
            $(evt.target).find('.collapsible-div').each((i, el) => {
                let $header = $(el).find('.collapsible-header');
                if ($header.hasClass('active') && !collapsibleStates[i] || !$header.hasClass('active') && collapsibleStates[i]) {
                    $header.trigger('click');
                }
            });
        });

        // Allow the page to receive drag/dropped VisAssets from the library, then tell
        // the server to download them
        this.$element.on('dragover', (evt) => {
            evt.preventDefault();
        });
        this.$element.on('drop', (evt) => {
            if (!$(evt.target).hasClass('ui-droppable')) {
                evt.preventDefault();
                evt.originalEvent.dataTransfer.items[0].getAsString(async (visAssetData) => {
                    let uuidRegex = /[0-9a-fA-F]{8}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{12}/;
                    let matches = uuidRegex.exec(visAssetData);

                    let dependencies = [];
                    let hostPath = '';
                    if (visAssetData.startsWith('data:image/png')) {
                        // First, try to see if it is a gradient (incoming png)
                        let commaIndex = visAssetData.indexOf(',');
                        let header = visAssetData.slice(0, commaIndex);
                        let headerItems = header.split(';');
                        hostPath = headerItems[headerItems.length - 2];
                        let gradientDataEncoded = headerItems[headerItems.length - 1];
                        let gradient = JSON.parse(decodeURIComponent(gradientDataEncoded));

                        // Add the gradient to state
                        await globals.stateManager.update('visAssetGradients/' + gradient.uuid, gradient);

                        // Queue up the dependencies to download
                        dependencies = gradient.visAssets;
                    } else if (matches[0]) {
                        // Otherwise, it's a single VisAsset
                        dependencies = [matches[0]];

                        // Find URL the artifact is coming from
                        let firstIndex = visAssetData.indexOf(dependencies[0]);
                        hostPath = visAssetData.slice(0, firstIndex);
                    }

                    // Start downloading every VisAsset dependency or single VisAsset
                    let fetchPromises = [];
                    for (const artifactUuid of dependencies) {
                        $('.loading-spinner').css('visibility', 'visible');
                        fetchPromises.push(fetch('/api/download-visasset/' + artifactUuid, {
                            method: 'POST',
                            headers: {
                                // 'X-CSRFToken': csrftoken,
                            },
                            mode: 'same-origin',
                            body: JSON.stringify({'hostPath': hostPath}),
                        }));
                    }
                    // Wait until everything is downloaded until loading disappears
                    Promise.all(fetchPromises).then(() => {
                        $('.loading-spinner').css('visibility', 'hidden');
                    });
                });
            }
        });

        dragscroll.reset();
    }

    syncWithState() {
        // TODO: incorporate jsondiffpatch
        let impressions = globals.stateManager.state['impressions'];
        let uiData = {};
        if (globals.stateManager.state) {
            if (globals.stateManager.state.uiData) {
                if (globals.stateManager.state.uiData.compose) {
                    if (globals.stateManager.state.uiData.compose.impressionData) {
                        uiData = globals.stateManager.state.uiData.compose.impressionData;
                    }
                }
            }
        }

        let $compositionLoader = $('#composition-panel').find('#' + COMPOSITION_LOADER_ID);
        $compositionLoader.empty();

        for (const imprId in impressions) {
            let impression = impressions[imprId];
            let uiDataForImpression = uiData[impression.uuid];
            let $impression = DataImpression(impression.plateType, impression.uuid, impression.name, uiDataForImpression);
            $compositionLoader.append($impression);
        }
    }
}