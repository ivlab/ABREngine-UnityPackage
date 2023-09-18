/* VisAssetGradientEditor.js
 *
 * Copyright (C) 2022, University of Minnesota
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

import { globals } from "../../../../common/globals.js";
import { gradientTypeMap, PuzzlePiece, typeMap } from "../PuzzlePiece.js";
import { width } from "./dialogConsts.js";
import { histogramDragIndicator, histogramDragIndicatorDone } from "./HistogramEditor.js";

var currentGradient = null;
const MaxTexturesPerGradient = 16; // the number defined in GradientBlendMap.cs in the ABREngine.
const MinSectionWidth = 0.04; // twice the blend width defined in GradientBlendMap.cs in the ABREngine

export function gradientPreviewThumbnail(gradient) {
    let $thumb = $('<div>', {
        class: 'vis-asset-gradient-thumb',
    });

    for (const va of gradient.visAssets) {
        let thumbUrl = `/media/visassets/${va}/thumbnail.png`;
        $thumb.append($('<img>', {
            src: thumbUrl,
            css: {'max-width': `${100 / gradient.visAssets.length}%` },
        }));
    }
    return $thumb;
}

function ResizableSection(sectionWidth, uuid, leftPerc, rightPerc, resizable) {
    let $section = $('<div>', {
        class: 'resizable-section editor-trashable',
        width: sectionWidth,
    });
    $section.data({uuid});
    let $leftDrop = $('<div>', {
        class: 'quick-drop quick-drop-left',
    });
    let $centerDrop = $('<div>', {
        class: 'quick-drop quick-drop-center',
    });
    let $rightDrop = $('<div>', {
        class: 'quick-drop quick-drop-right',
    });

    let dropIndicatorOver = (evt, ui) => $(evt.target).addClass('active');
    let dropIndicatorOut = (evt, ui) => $(evt.target).removeClass('active');

    // Left and right drop zones add new visassets
    let dropIndicatorDrop = (evt, ui) => {
        $(evt.target).removeClass('active');
        let left = $(evt.target).hasClass('quick-drop-left');
        let newVisAssetUuid = $(ui.draggable).data('inputValue');
        let newVisAssetType = $(ui.draggable).data('inputType');
        let thisUuid = $section.data('uuid');
        addGradientStopRelative(newVisAssetUuid, newVisAssetType, thisUuid, left);
        updateGradientDisplay();
        saveGradient();
    };
    // Center zone replaces current visAsset
    let dropIndicatorCenter = (evt, ui) => {
        $(evt.target).removeClass('active');
        let newVisAssetUuid = $(ui.draggable).data('inputValue');
        let newVisAssetType = $(ui.draggable).data('inputType');
        if (!gradientTypeValid(newVisAssetType)) {
            return;
        }
        let thisUuid = $section.data('uuid');
        let thisIndex = currentGradient.visAssets.indexOf(thisUuid);
        if (thisIndex >= 0) {
            currentGradient.visAssets[thisIndex] = newVisAssetUuid;
            updateGradientDisplay();
            saveGradient();
        }
    };

    let dropParams = {
        tolerance: 'intersect',
        accept: '.puzzle-piece',
        over: dropIndicatorOver,
        out: dropIndicatorOut,
        drop: dropIndicatorDrop,
    };

    $leftDrop.droppable(dropParams);
    $rightDrop.droppable(dropParams);
    $centerDrop.droppable({
        tolerance: 'intersect',
        accept: '.puzzle-piece',
        over: dropIndicatorOver,
        out: dropIndicatorOut,
        drop: dropIndicatorCenter,
    });

    $section.append($leftDrop);
    $section.append($centerDrop);
    $section.append($rightDrop);

    $section.append(
        $('<img>', {
            class: 'rounded',
            src: `/media/visassets/${uuid}/thumbnail.png`,
        })
    );

    $section.draggable({
        helper: 'clone',
        scroll: false,
        cursorAt: {top: 5, left: 5},
        start: (evt, ui) => {
            // Explicitly set small size to trash it easier
            $(ui.helper).width('10rem');
            $(ui.helper).height('4rem');
        }
    });

    let percentage = 0;
    let startWidth = 0;
    let nextStartWidth = 0;
    if (resizable) {
        $section.resizable({
            handles: 'e',
            start: (evt, ui) => {
                startWidth = $(evt.target).width();
                nextStartWidth = $(evt.target).next().width();
                $(evt.target).resizable('option', 'minWidth', $section.parent().width() * MinSectionWidth);
            },
            resize: (evt, ui) => {
                let sectionWidth = $(evt.target).width();
                let widthDiff = startWidth - sectionWidth;

                $(evt.target).next().width(nextStartWidth + widthDiff);

                let $panel = $(evt.target).parents('.gradient-panel');
                percentage = ($(evt.target).position().left + $(evt.target).width()
                    - $panel.position().left) / $panel.width();

                let percFormat = `${(percentage * 100).toFixed(0)}%`;
                $(evt.target).find('.right-section-label').text(percFormat);
                $(evt.target).next().find('.left-section-label').text(percFormat);
                histogramDragIndicator(percentage);
            },
            stop: (evt, ui) => {
                // Update the actual gradient
                let visAssetIndex = currentGradient.visAssets.indexOf($section.data('uuid'));
                if (visAssetIndex >= 0 && visAssetIndex < currentGradient.points.length) {
                    currentGradient.points[visAssetIndex] = percentage;
                    saveGradient();
                }
                histogramDragIndicatorDone();
            }
        });
    }

    return $section;
}

function getVisAssetType(abrType) {
    return Object.keys(typeMap).find(k => typeMap[k] == abrType && (!typeMap[currentGradient.gradientType] || typeMap[currentGradient.gradientType] == abrType));
}

function gradientTypeValid(abrType) {
    let vaType = getVisAssetType(abrType);
    let gradValid = Object.keys(gradientTypeMap).indexOf(vaType) >= 0;
    return vaType && gradValid;
}

function addGradientStopRelative(uuid, abrType, existingUuid, leftOf) {
    if (currentGradient.visAssets.length >= MaxTexturesPerGradient) {
        alert(`Cannot add more than ${MaxTexturesPerGradient} VisAssets in a gradient`);
        return;
    }

    if (!gradientTypeValid(abrType)) {
        return;
    }
    let thisIndex = currentGradient.visAssets.indexOf(existingUuid);
    let pointsIndex = thisIndex - 1;
    let thisPercentage = pointsIndex >= 0 ? currentGradient.points[pointsIndex] : 0.0;
    let nextPercentage = thisIndex < currentGradient.points.length ? currentGradient.points[thisIndex] : 1.0;

    // insert the new uuid to the left (or right) of existing uuid
    let newPoint = (nextPercentage - thisPercentage) / 2.0 + thisPercentage;
    currentGradient.points.splice(thisIndex, 0, newPoint);
    if (leftOf) {
        currentGradient.visAssets.splice(thisIndex, 0, uuid);
    } else {
        currentGradient.visAssets.splice(thisIndex + 1, 0, uuid);
    }
}

function addGradientStop(uuid, abrType, position) {
    if (!gradientTypeValid(abrType)) {
        return;
    }
    currentGradient.gradientType = getVisAssetType(abrType);
    currentGradient.visAssets.push(uuid);
    if (currentGradient.visAssets.length > 1) {
        currentGradient.points.push(position);
    }
}

function updateGradientDisplay() {
    let $gradient = $('#the-gradient');
    $gradient.empty();

    // Take 1px borders into account
    let panelWidth = $gradient.width() - currentGradient.visAssets.length - 1;
    for (let vaIndex = 0; vaIndex < currentGradient.visAssets.length; vaIndex++) {
        let thisPercentage = currentGradient.points[vaIndex - 1] || 0.0;
        let nextPercentage = currentGradient.points[vaIndex] || 1.0;
        let $section = ResizableSection(
            panelWidth * (nextPercentage - thisPercentage),
            currentGradient.visAssets[vaIndex],
            thisPercentage,
            nextPercentage,
            vaIndex < currentGradient.visAssets.length - 1
        );
        // enable the trash
        $section.data({
            trashed: (evt, ui) => {
                let sectionUuid = $(ui.draggable).data('uuid');
                $(ui.draggable).remove();
                let thisIndex = currentGradient.visAssets.indexOf(sectionUuid);
                if (thisIndex >= 0) {
                    currentGradient.visAssets.splice(thisIndex, 1);
                    if (currentGradient.visAssets.length > 1 && thisIndex - 1 >= 0) {
                        currentGradient.points.splice(thisIndex - 1, 1);
                    } else if (currentGradient.visAssets.length > 1 && thisIndex == 0) {
                        currentGradient.points.shift();
                    } else {
                        currentGradient.points = [];
                    }
                    updateGradientDisplay();
                    saveGradient();
                }
            }
        });
        $gradient.append($section);
    }

    if (currentGradient.visAssets.length == 0) {
        let $starterIndicator = $('<div>', {
            class: 'visasset-starter-indicator',
        });
        // t is the visasset type of the gradient
        let t = currentGradient.gradientType;
        $starterIndicator.append(PuzzlePiece(t[0].toUpperCase() + t.slice(1), gradientTypeMap[t], true, 'drop-zone'));
        $starterIndicator.append($('<p>', {
            text: `Drag and drop ${t}s to create a gradient`,
        }));
        $gradient.append($starterIndicator);
    }
}

function saveGradient() {
    globals.stateManager.update(`visAssetGradients/${currentGradient.uuid}`, currentGradient);
}

export async function VisAssetGradientEditor(inputProps) {
    let gradientUuid = inputProps.inputValue;
    let $visAssetGradientDialog = $('<div>', {
        id: 'vis-asset-gradient-dialog',
        class: 'module-editor',
        width: width,
    });

    // Build the gradient and allow it to respond to new VisAssets that are drag-n-dropped
    let $gradient = $('<div>', {
        id: 'the-gradient',
        class: 'gradient-panel rounded',
    }).droppable({
        tolerance: 'touch',
        accept: '.puzzle-piece',
        over: (evt, ui) => {
            if (currentGradient.visAssets.length == 0) {
                $(evt.target).addClass('active');
            }
        },
        out: (evt, ui) => {
            if (currentGradient.visAssets.length == 0) {
                $(evt.target).removeClass('active');
            }
        },
        drop: (evt, ui) => {
            if (currentGradient.visAssets.length == 0) {
                $(evt.target).removeClass('active');
                let visAssetUuid = $(ui.draggable).data('inputValue');
                let visAssetType = $(ui.draggable).data('inputType');
                addGradientStop(visAssetUuid, visAssetType, 0.0);
                updateGradientDisplay();
                saveGradient();
            }
        }
    });

    $visAssetGradientDialog.append($gradient);

    // Retrieve gradient from state, if any
    if (globals.stateManager.state.visAssetGradients && globals.stateManager.state.visAssetGradients[gradientUuid]) {
        currentGradient = globals.stateManager.state.visAssetGradients[gradientUuid];
    } else {
        // Otherwise, set up default
        currentGradient = {
            uuid: gradientUuid,
            gradientScale: 'discrete',
            gradientType: Object.keys(gradientTypeMap).find(k => gradientTypeMap[k] == inputProps.inputType),
            points: [],
            visAssets: [],
        }
    }

    $visAssetGradientDialog.on('ABR_AddedToEditor', () => {
        updateGradientDisplay();

    });

    return $visAssetGradientDialog;
}