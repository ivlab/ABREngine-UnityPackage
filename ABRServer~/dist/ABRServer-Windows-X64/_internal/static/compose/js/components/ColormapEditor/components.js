/* components.js
 *
 * Internal components for colormap editor
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
// -------------------- Begin components --------------------

import { histogramDragIndicator, histogramDragIndicatorDone } from "./HistogramEditor.js";

export function ColorThumb(perc, color, colorChangeCallback) {
    let mapLeft = $('#colormap .colormap-canvas').position().left;
    let mapTop = $('#colormap .colormap-canvas').position().top;
    let mapWidth = $('#colormap .colormap-canvas').width();
    let mapHeight = $('#colormap .colormap-canvas').height();
    let left = mapLeft + mapWidth * perc;
    let $thumb = $('<div>', {
        class: 'color-thumb editor-trashable',
    }).draggable({
        containment: ".editor-dialog",
        drag: (_evt, ui) => {
            let middle = ($(ui.helper).position().left - mapLeft) + $(ui.helper).width() / 2;
            let percentage = middle / $('#colormap .colormap-canvas').width();
            histogramDragIndicator(percentage);
            // $(ui.helper).find('.percentage-display').text(`${(percentage * 100).toFixed(0)}%`);
        },
        stop: (evt, ui) => {
            colorChangeCallback(evt, ui);
            histogramDragIndicatorDone();
        },
    }).append(
        $('<input>', {
            class: 'color-input',
            value: color,
        }).on('change', colorChangeCallback)
    // ).append(
    //     $('<div>', {
    //         class: 'percentage-display',
    //         text: `${(perc * 100).toFixed(0)}%`
    //     })
    // ).append(
    //     $('<div>', {
    //         class: 'marker-bar',
    //     }).css('height', mapHeight * 3).css('top', -mapHeight * 3)
    ).css('position', 'absolute');

    // 25 = ((spectrum input) + margin) / 2
    $thumb.css('left', left - 25);
    return $thumb;
}

export function DataRemappingSlider(min, max, val1, val2, width) {
    let outsideMargin = 0.1;
    let expandedWidth = width + (width * outsideMargin * 2);
    return $('<div>', {
        class: 'data-remapping-slider',
        css: {
            width: expandedWidth,
        },
    }).slider({
        range: true,
        min: min,
        max: max,
        values: [val1, val2],
        step: 0.1,
        disabled: false,
        // slide: (evt, ui) => {
        //     console.log(`min/max: ${$(evt.target).slider('option', 'min')}, ${$(evt.target).slider('option', 'max')}`)
        //     console.log(`sliding: ${ui.values}`);
        // },
        // change: (evt, ui) => {
        //     console.log(`changed ${ui.values}`);
        // }
    });
}