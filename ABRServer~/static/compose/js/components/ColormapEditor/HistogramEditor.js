/* HistogramEditor.js
 *
 * Component that enables a user to remap data ranges for scalar variables.
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
import { DataPath } from '../../../../common/DataPath.js';
import { margin, width } from './dialogConsts.js';
import { DataRemappingSlider } from './components.js';

const fullHeight = 200;
const histogramHeight = fullHeight - margin.top - margin.bottom;

var zippedHistogram = null;
var currentVarPath = null;
var currentKeyDataPath = null;
var currentMinMax = null;
var customRange = false;

export function histogramDragIndicator(percentage) {
    let fullWidth = $('#histogram-drag-indicator').parent().width();
    let leftMargin = (fullWidth - width) / 2.0;
    let remappedPx = width * percentage + leftMargin;
    $('#histogram-drag-indicator').css('opacity', '100%');
    $('#histogram-drag-indicator').css('left', remappedPx);
    let $label = $('#histogram-drag-indicator').find('p');
    let dataValue = (currentMinMax.max - currentMinMax.min) * percentage + currentMinMax.min;
    $label.text(dataValue.toFixed(2));
}
export function histogramDragIndicatorDone() {
    $('#histogram-drag-indicator').css('opacity', '0%');
}
export function histogramExists() {
    return typeof($('#histogram-editor').get(0)) !== 'undefined';
}

// Histogram component to plug into the editor dialog
export async function HistogramEditor(variableInput, keyDataInput) {
    let $histogramEditor = $('<div>', {
        id: 'histogram-editor',
        class: 'module-editor'
    });

    zippedHistogram = null;
    currentVarPath = null;
    currentKeyDataPath = null;
    currentMinMax = null;
    customRange = false;

    currentVarPath = variableInput.inputValue;
    let variableName = DataPath.getName(currentVarPath);
    currentKeyDataPath = keyDataInput.inputValue;
    let keyDataName = DataPath.getName(currentKeyDataPath);

    // Fetch the histogram from the server
    let url = new URL(`${window.location}api/histogram/${currentKeyDataPath}/${variableName}`);
    url.search = new URLSearchParams(currentMinMax);

    zippedHistogram = await fetch(url).then((resp) => resp.json());

    // Try to get the current min/max from state if it's been redefined
    if (globals.stateManager.state.dataRanges) {
        // Try to get keydata-specific custom range first
        if (globals.stateManager.state.dataRanges.specificScalarRanges && globals.stateManager.state.dataRanges.specificScalarRanges[currentKeyDataPath]) {
            currentMinMax = globals.stateManager.state.dataRanges.specificScalarRanges[currentKeyDataPath][currentVarPath];
            customRange = true;
        }

        // Then if that fails, see if there's a global range for this var
        if (!currentMinMax && globals.stateManager.state.dataRanges.scalarRanges)
        {
            currentMinMax = globals.stateManager.state.dataRanges.scalarRanges[currentVarPath];
            customRange = false;
        }
    }

    // If we still can't find data ranges, use defaults from histogram
    if (!currentMinMax) {
        currentMinMax = {
            min: zippedHistogram.keyDataMin,
            max: zippedHistogram.keyDataMax,
        }
        customRange = false;
    }

    let $histContainer = $('<div>', {
        id: 'histogram-container',
        class: 'centered',
    });

    $histContainer.append($('<div>', {
        id: 'histogram',
    }));

    // Enable a "stick" indicator that anyone can use whilst editing their
    // particular thing so they can see where in the data range it lies
    let $dragIndicator = $('<div>', {
        id: 'histogram-drag-indicator',
        css: {
            position: 'absolute',
            height: histogramHeight,
            width: 2,
            top: margin.top + margin.bottom,
            'background-color': '#3d3d3d',
            opacity: '0%',
        }
    });
    // Append a numeric indicator for the actual data value
    $dragIndicator.append($('<p>', {
        text: '',
        css: {
            position: 'absolute',
            bottom: '-2rem',
            left: '-2rem',
            width: '4rem',
            'text-align': 'center',
        }
    }));
    $histContainer.append($dragIndicator);


    $histogramEditor.append($histContainer);

    // Construct a different data label depending on whether or not we have
    // linked or unlinked this variable w/the global one of the same name
    // Add a button to break the link to a particular variable (make this range specific to the data impression)
    let $dataLabel = $('<div>', { id: 'histogram-data-label' }).append(
        // Add a button to reset data range to original keyData
        $('<button>', {
            class: 'rounded',
            text: 'Fit',
            title: 'Fit data range to key data',
        }).on('click', (evt) => {
            updateHistogram(zippedHistogram.keyDataMin, zippedHistogram.keyDataMax);
        }).prepend($('<span>', {
            class: 'ui-icon ui-icon-arrowthick-2-e-w',
        }))
    ).append(
        // Add the actual label
        $('<p>', {
            html: `<em>${keyDataName} &rarr; <strong>${variableName}</strong></em>`,
        })
    ).append(
        // Add custom range button
        $('<button>', {
            class: 'rounded' + (customRange ? ' custom-range' : ''),
        }).append($('<span>', {
            class: 'ui-icon ui-icon-gear',
            title: customRange ? `Use default range for ${keyDataName}/${variableName}` : `Create custom range for ${keyDataName}/${variableName}`,
        })).on('click', (evt) => {
            let $target = $(evt.target).closest('button');
            $target.toggleClass('custom-range');
            customRange = $target.hasClass('custom-range');

            // If we've just unclicked custom range, get rid of the keydata-specific range
            if (!customRange) {
                globals.stateManager.removePath(`dataRanges/specificScalarRanges/"${currentKeyDataPath}"/"${currentVarPath}"`);
                // Snap the min/max back to their original range
                currentMinMax = globals.stateManager.state.dataRanges.scalarRanges[currentVarPath];
                updateHistogram(currentMinMax.min, currentMinMax.max);
            }
        })
    );

    $histogramEditor.append(
        $('<div>', {
            class: 'centered',
            css: {
                'background-color': 'white',
            }
        }).append(
            $('<div>', {
                class: 'variable-labels',
            }).append($('<input>', {
                id: 'slider-minLabel',
                type: 'number',
                step: 0.0001,
                value: currentMinMax.min.toFixed(4),
                change: function() {
                    currentMinMax.min = parseFloat(this.value);
                    $('.data-remapping-slider').slider('values', 0, currentMinMax.min);
                    updateHistogram(currentMinMax.min, currentMinMax.max);
                },
            })).append(
                $dataLabel
            ).append($('<input>', {
                id: 'slider-maxLabel',
                type: 'number',
                step: 0.0001,
                value: currentMinMax.max.toFixed(4),
                change: function() {
                    currentMinMax.max = parseFloat(this.value);
                    $('.data-remapping-slider').slider('values', 1, currentMinMax.max);
                    updateHistogram(currentMinMax.min, currentMinMax.max);
                },
            }))
        )
    );

    $histogramEditor.append(
        $('<div>', {
            id: 'data-remapper',
            class: 'centered',
        }).append(
            DataRemappingSlider(0, 1, 0, 1, width)
        ).on('mouseup', (evt, ui) => {
            let filterMin = $('.data-remapping-slider').slider('values', 0);
            let filterMax = $('.data-remapping-slider').slider('values', 1);
            currentMinMax = {
                min: filterMin,
                max: filterMax,
            }
            updateHistogram(filterMin, filterMax);
        })
    );

    let $sliderMinHandle = $('#data-remapper > .data-remapping-slider > .ui-slider-handle:nth-child(2)');
    let $sliderMaxHandle = $('#data-remapper > .data-remapping-slider > .ui-slider-handle:nth-child(3)');
    let $handleMarkerBar = $('<div>', {
        class: 'marker-bar handle-marker-bar'
    })

    $sliderMinHandle.append($handleMarkerBar);
    $sliderMaxHandle.append($handleMarkerBar.clone());

    $histogramEditor.on('ABR_AddedToEditor', () => updateHistogram(currentMinMax.min, currentMinMax.max));

    return $histogramEditor;
}

// Move the slider labels as the slider handles are dragged.
// ? Why do it like this: 
//     A different way to do this is to nest each label inside each handle, 
//     but this will trigger the 'slide' event every time we type in the label text boxes
//     -> trigger 'slide' callback
//     -> reset labels' <input> values before we finish typing
// ! Note:
//     Need to find a way to stop repeatedly querying for objects
function updateSliderLabelsPosition() {
    let $sliderMinLabel = $('#slider-minLabel');
    let $sliderMaxLabel = $('#slider-maxLabel');
    let $sliderMinHandle = $('#data-remapper > .data-remapping-slider > .ui-slider-handle:nth-child(2)');
    let $sliderMaxHandle = $('#data-remapper > .data-remapping-slider > .ui-slider-handle:nth-child(3)');

    $sliderMinLabel.offset({
        top: $sliderMinHandle.offset().top - 36,
        left: $sliderMinHandle.offset().left - 40,
    });
    $sliderMaxLabel.offset({
        top: $sliderMaxHandle.offset().top - 36,
        left: $sliderMaxHandle.offset().left - 40,
    });
}

function updateHistogram(minm, maxm) {
    // In case there were any old histograms hanging out
    d3.selectAll('#histogram > *').remove();

    if (typeof(minm) === 'undefined' || typeof(maxm) === 'undefined') {
        [minm, maxm] = d3.extent(zippedHistogram.histogram, (d) => d.binMax);
    }

    let svg = d3.select("#histogram")
    .append("svg")
    .attr("width", width + margin.left + margin.right)
    .attr("height", histogramHeight + margin.top + margin.bottom)
    .append("g")
    .attr("transform",
        "translate(" + margin.left + "," + margin.top + ")");

    // X axis is the values in the histogram
    let x = d3.scaleLinear()
        .domain([minm, maxm])
        .range([0, width]);
    svg.append('g')
        .attr('transform', `translate(0, ${histogramHeight})`)
        .call(d3.axisBottom(x));

    // Y axis is the num items in each bin
    let y = d3.scaleLinear()
        .domain(d3.extent(zippedHistogram.histogram, (d) => d.items))
        .range([histogramHeight, 0]);

    svg.append('path')
        .datum(zippedHistogram.histogram)
        .attr('fill', 'none')
        .attr('stroke', 'steelblue')
        .attr('stroke-width', 1.5)
        .attr('d', d3.line()
            .x((d) => x(d.binMax))
            .y((d) => y(d.items))
        );

    // Update the slider values, and register the callback for interactively
    // changing the min/max
    let extent = maxm - minm;
    let outsideMargin = 0.1;
    let marginExtent = outsideMargin * extent;
    $('.data-remapping-slider')
        .slider('option', 'min', minm - marginExtent)
        .slider('option', 'max', maxm + marginExtent)
        .slider('option', 'step', extent / width)
        .slider('values', 0, minm)
        .slider('values', 1, maxm)
        .slider('option', 'slide', (evt, ui) => {
            let [filterMin, filterMax] = ui.values;
            $('#slider-minLabel').val(filterMin);
            $('#slider-maxLabel').val(filterMax);
            updateSliderLabelsPosition();
        });

    // Update the numbers to reflect
    $('#slider-minLabel').val(minm.toFixed(4));
    $('#slider-maxLabel').val(maxm.toFixed(4));

    updateSliderLabelsPosition();

    currentMinMax.min = minm;
    currentMinMax.max = maxm;

    // Update the Server with the min/max from this slider
    if (customRange) {
        // If it's a custom range for the key data, update the scalar range inside of keydata
        globals.stateManager.update(`dataRanges/specificScalarRanges/"${currentKeyDataPath}"/"${currentVarPath}"`, currentMinMax);
    } else {
        // Otherwise, update the systemwide variable min/max
        globals.stateManager.update(`dataRanges/scalarRanges/"${currentVarPath}"`, currentMinMax);
    }
}