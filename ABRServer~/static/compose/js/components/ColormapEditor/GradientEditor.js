/* GradientEditor.js
 *
 * Dialog that enables a user to modify gradients
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
import { ColorMap } from './color.js';
import { getDisplayVal, getFloatVal } from '../Primitives.js';
import { STATE_UPDATE_EVENT } from '../../../../common/StateManager.js';
import { createSvg } from '../../../../common/helpers.js';
import { width, height } from './dialogConsts.js';
import { histogramDragIndicator, histogramDragIndicatorDone, histogramExists } from './HistogramEditor.js';

const DEFAULT_GRADIENT = {
    "points": [
        0.0,
        1.0,
    ],
    "values": [
        '0%',
        '100%'
    ],
};

var currentGradient = null;
var currentGradientUuid = null;

// Returns the new uuid if there is one
export async function GradientEditor(inputProps) {
    // clear some stateful items that might hang around
    currentGradient = null;
    currentGradientUuid = null;

    let gradientUuid = inputProps.inputValue;
    setCurrentGradient(gradientUuid);

    if (currentGradient.points.length != currentGradient.values.length) {
        alert('Gradient is incorrectly formatted.');
        return;
    }

    // Wait until the state is ready before constructing dialog
    await saveGradient();

    let $gradientEditor = $('<div>', {
        id: 'gradient-editor',
        class: 'gradient-editor module-editor',
        css: { padding: '0' }
    });

    // Append the gradient view area
    let $gradientView = $('<div>', {
        id: 'gradient-view',
        title: 'Double click to add a new gradient stop',
        height: height * 2,
        width: width,
    });

    $gradientEditor.append($gradientView);


    globals.stateManager.subscribe($gradientView);
    $gradientView.on(STATE_UPDATE_EVENT, (evt) => {
        setCurrentGradient(currentGradientUuid);
        stopsFromGradient();
    });

    $gradientView.on('dblclick', (evt) => {
        let viewLeft = evt.target.getBoundingClientRect().x;
        let viewTop = evt.target.getBoundingClientRect().y;
        let clickPoint = (evt.clientX - viewLeft) / width;
        let clickValue = 1.0 - ((evt.clientY - viewTop) / (height * 2));

        // insert in the correct place, sorted
        let i = 0;
        while (currentGradient.points[i] < clickPoint) {
            i++;
        }

        currentGradient.points.splice(i, 0, clickPoint);
        currentGradient.values.splice(i, 0, getDisplayVal(clickValue, 'PercentPrimitive'));

        stopsFromGradient();
        saveGradient();
    });

    $gradientEditor.on('ABR_AddedToEditor', () => {
        stopsFromGradient();
    });
    return $gradientEditor;
}

function setCurrentGradient(gradientUuid) {
    if (globals.stateManager.state['primitiveGradients'] && globals.stateManager.state['primitiveGradients'][gradientUuid]) {
        currentGradient = globals.stateManager.state['primitiveGradients'][gradientUuid];
    }
    if (!currentGradient) {
        currentGradient = DEFAULT_GRADIENT;
    }
    currentGradientUuid = gradientUuid;
}

async function saveGradient() {
    await globals.stateManager.update('primitiveGradients/' + currentGradientUuid, currentGradient);
}

function gradientFromStops() {
    let pointValuePairs = [];
    $('.gradient-stop').each((i, el) => {
        let stopWidth = $(el).get(0).clientWidth;
        let percentage = ($(el).position().left + stopWidth / 2.0) / width;
        let stopHeight = $(el).get(0).clientHeight;
        let value = 1.0 - (($(el).position().top + stopHeight / 2.0) / (height * 2));

        // validate that the values and points are 0-1
        value = Math.max(0, Math.min(1, value));
        percentage = Math.max(0, Math.min(1, percentage));

        pointValuePairs.push([
            percentage,
            getDisplayVal(value, 'PercentPrimitive'),
        ]);
    });

    // Sort gradient for display / nicity
    pointValuePairs.sort();

    return {
        points: pointValuePairs.map((pv) => pv[0]),
        values: pointValuePairs.map((pv) => pv[1]),
    };
}

function stopsFromGradient() {
    $('#gradient-view').empty();
    let $svg = $(createSvg('svg'), { id: 'opacity-line' })
        .attr('width', width)
        .attr('height', height * 2)
        .css('position', 'absolute')
        .css('top', 0)
        .css('left', 0);
    let $pointCanvas = $('<div>', { id: 'opacity-points' })
        .css('width', width)
        .css('height', height * 2)
        .css('position', 'absolute')
        .css('top', 0)
        .css('left', 0);

    let [prevX, prevY] = [null, null];
    for (const i in currentGradient.points) {
        let point = currentGradient.points[i];
        let floatValue = getFloatVal(currentGradient.values[i], 'PercentPrimitive');

        // center x, y
        let [x, y] = [point * width, (height * 2) - floatValue * height * 2]

        const pointSize = 15;
        let $point = $('<div>', {
            class: 'gradient-stop editor-trashable',
            width: pointSize,
            height: pointSize,
            title: `Data Value: ${point.toFixed(2)}, Opacity: ${(floatValue* 100).toFixed(0)}%`,
            css: {
                'cursor': 'grab',
                'position': 'absolute',
                'background-color': 'black',
                'left': x - pointSize / 2,
                'top': y - pointSize / 2,
            }
        });

        if (prevX != null && prevY != null) {
            let $line = $(createSvg('line'))
                .attr('x1', prevX)
                .attr('y1', prevY)
                .attr('x2', x)
                .attr('y2', y)
                .css('stroke', 'black');
            $svg.append($line);
        }

        $point.draggable({
            containment: '.editor-dialog',
            drag: (evt, ui) => {
                let stopWidth = $(ui.helper).get(0).clientWidth;
                let percentage = ($(ui.helper).position().left + stopWidth / 2.0) / width;
                if (histogramExists())
                    histogramDragIndicator(percentage);
            },
            stop: (evt, ui) => {
                currentGradient = gradientFromStops();
                saveGradient();
                $('#gradient-view').append(getGradientColormap());
                if (histogramExists())
                    histogramDragIndicatorDone();
            },
        });

        // Allow this to be trashed
        $point.data({
            trashed: (evt, ui) => {
                $(ui.draggable).remove();
                currentGradient = gradientFromStops();
                saveGradient();
            }
        });

        $pointCanvas.append($point);
        [prevX, prevY] = [x, y];
    }
    $('#gradient-view').append($svg);
    $('#gradient-view').append($pointCanvas);

    // Append y axis labels
    for (const p of [0.0, 0.25, 0.50, 0.75, 1.0]) {
        $('#gradient-view').append($('<p>', {
            text: `${100 * p}%`,
            css: {
                position: 'absolute',
                top: `${(1.0 - p) * 100}%`,
                left: 0,
                'font-size': '75%',
                'margin-left': '0.5em'
            }
        }));
    }
    $('#gradient-view').append(getGradientColormap());
}

export function gradientToColormap(gradient) {
    let c = new ColorMap();
    if (gradient.points.length != gradient.values.length) {
        console.error('Gradient points must be the same length as gradient values');
        return null;
    }

    for (const i in gradient.points) {
        let floatValue = getFloatVal(gradient.values[i], 'PercentPrimitive');
        c.addControlPt(gradient.points[i], {
            r: floatValue,
            g: floatValue,
            b: floatValue,
        });
    }
    return c;
}

function getGradientColormap() {
    let colormap = gradientToColormap(currentGradient);
    let b64 = colormap.toBase64(true);
    // Add the visualization of the gradient
    return $('<img>', {
        id: 'gradient-vis',
        width: width - 2, // border px
        height: 10,
        src: b64,
        css: {
            position: 'absolute',
            bottom: -20,
        }
    });

}
