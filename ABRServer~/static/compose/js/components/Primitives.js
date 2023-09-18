/* Primitives.js
 *
 * Primitive inputs such as length, angle, percent, etc.
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

import { globals } from "../../../common/globals.js";

// Any special cases that don't directly translate 1:1 as floats
const PRIMITIVE_VALUE_MULTIPLIERS = {
    'PercentPrimitive': 100.0,
};

// Assume 0.01 increment unless otherwise defined here
const PRIMITIVE_INCREMENTS = {
    'AnglePrimitive': 1.0,
    'IntegerPrimitive': 1,
};

export function getFloatVal(primitiveString, shortType) {
    let typeUnitsRegex = new RegExp(globals.schema.definitions.InputStringTypes[shortType].pattern);
    let matches = primitiveString.match(typeUnitsRegex);

    let floatValue = null;
    if (matches && matches.length == 3) {
        let unitStr = matches[2];
        let strNoUnits = primitiveString.replace(unitStr, '');
        floatValue = +strNoUnits;
    } else {
        floatValue = +primitiveString;
    }

    // Convert to actual float value
    if (PRIMITIVE_VALUE_MULTIPLIERS.hasOwnProperty(shortType)) {
        floatValue /= PRIMITIVE_VALUE_MULTIPLIERS[shortType];
    }
    return floatValue;
}

export function getDisplayVal(newValue, shortType) {
    let typeUnitsString = globals.schema.definitions.InputStringTypes[shortType].pattern;
    let unitGroupStart = typeUnitsString.lastIndexOf('(') + 1;
    let unitStr = '';
    if (unitGroupStart > 0) {
        let unitGroupEnd = typeUnitsString.lastIndexOf(')');
        unitStr = typeUnitsString.slice(unitGroupStart, unitGroupEnd);
    }

    // Convert back to display value
    if (PRIMITIVE_VALUE_MULTIPLIERS.hasOwnProperty(shortType)) {
        newValue *= PRIMITIVE_VALUE_MULTIPLIERS[shortType];
    }
    let truncatedValue = newValue.toFixed(2);
    let intValue = parseInt(newValue, 10);
    if (Math.abs(truncatedValue - intValue) < 0.01) {
        newValue = intValue;
    } else {
        newValue = truncatedValue;
    }

    return newValue + unitStr;
}

// Increment a primitive value (e.g. 1m) up or down (positive / negative increment)
function incrementPrimitive(primitiveString, inputType, positive) {
    let shortType = inputType.replace('IVLab.ABREngine.', '');

    // Boolean scrubbing
    if (shortType == "BooleanPrimitive") {
        return positive;
    }

    // Float & Other primitives scrubbing
    let floatValue = getFloatVal(primitiveString, shortType);

    // Determine the amount that we should increment by
    let amount = 0.01;
    if (PRIMITIVE_INCREMENTS.hasOwnProperty(shortType)) {
        amount = PRIMITIVE_INCREMENTS[shortType];
    }
    if (!positive) {
        amount *= -1.0;
    }
    // Increment
    let newValue = floatValue + amount;

    // LengthPrimitive shouldn't be negative
    if (shortType == "LengthPrimitive") {
        newValue = (newValue < 0) ? 0 : newValue;
    }

    return getDisplayVal(newValue, shortType);
}

export function PrimitiveInput(inputName, shortInputName, resolvedProps) {
    let $el = $('<div>', {
        class: 'puzzle-label rounded',
        title: `${inputName}: ${resolvedProps.inputValue}`,
    });
    $el.append($('<p>', {
        class: 'primitive-name',
        text: shortInputName,
    }));
    let $input = $('<input>', {
        class: 'primitive-input',
        type: 'text',
        val: resolvedProps.inputValue
    }).on('change', (evt) => {
        // Get the impression that this input is a part of
        let $impression = $(evt.target).closest('.data-impression');
        let impressionId = $impression.data('uuid');
        let plateType = $impression.data('plateType');
        if (!plateType) {
            return;
        }

        // Get the default values for this input, in case there's nothing
        // there already
        let defaultInputsSchema = globals.schema.definitions.Plates[plateType].properties[inputName].properties;
        let defaultInputs = {};
        for (const p in defaultInputsSchema) {
            defaultInputs[p] = defaultInputsSchema[p].const;
        }

        // See if there's an input there already, if not assign the defaults
        let impressionState = globals.stateManager.state['impressions'][impressionId];
        let inputState;
        if (impressionState && impressionState.inputValues && impressionState.inputValues[inputName]) {
            inputState = impressionState.inputValues[inputName];
        } else {
            inputState = defaultInputs;
        }

        // Assign the new input
        let oldInputValue = inputState['inputValue'];
        inputState['inputValue'] = $(evt.target).val();

        // Send the update to the server
        globals.stateManager.update(`impressions/${impressionId}/inputValues/${inputName}`, inputState).then(() => {
            $(evt.target).attr('disabled', false);
        }).catch((err) => {
            alert(`'${inputState['inputValue']}' is not valid for type ${resolvedProps.inputType}.`)
            globals.stateManager.refreshState();
        });
        
        // Temporarily disable until we've heard back from the server
        $(evt.target).attr('disabled', true);
    });

    let $label = ScrubbableInput($input, resolvedProps.inputType);

    $el.append($label);
    let $container = $('<div>', { class: 'puzzle-piece rounded' });
    $container.append($el);
    return $container;
}

export function ScrubbableInput($input, inputType) {
    let $label = $('<label>');
    $label.append($input);

    let dragging = false;
    let previousX = null;
    $label.append(
        $('<span>', {
            class: 'input-scrubbable no-drag ui-icon ui-icon-triangle-2-e-w',
        }).on('mousedown', (evt) => {
            dragging = true;
            evt.stopPropagation();
        })
    );

    $('body').on('mousemove', (evt) => {
        if (dragging) {
            let newValue = $input.val();
            if (evt.clientX > previousX) {
                newValue = incrementPrimitive(newValue, inputType, true);
            } else {
                newValue = incrementPrimitive(newValue, inputType, false);
            }
            $input.val(newValue);
            evt.stopPropagation();
        }
        previousX = evt.clientX;
    }).on('mouseup', (evt) => {
        if (dragging) {
            $input.trigger('change');
            dragging = false;
            evt.stopPropagation();
        }
    });

    return $label;
}