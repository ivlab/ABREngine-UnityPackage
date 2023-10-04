/* CollapsibleDiv.js
 *
 * Reimplementation of the DataPath class found in the ABREngine-UnityPackage
 *
 * Copyright (C) 2020, University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>, Daniel F. Keefe <dfk@umn.edu>
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

// Wrap the contents with an expandable div
// Can optionally persist its state through refresh (uses session storage)
// Defaults to collapsed state
export function CollapsibleDiv(
    header,
    $contents,
) {
    let $header;
    if (header instanceof jQuery) {
        $header = header;
    } else {
        $header = $('<div>', {
            text: header
        });
    }
    $header.addClass('collapsible-header');

    let $collapsibleDiv = $('<div>', {
        class: 'collapsible-div rounded',
    }).append($header.on('click', (evt) => {
        let $target = $(evt.target).closest('.collapsible-header');
        $target.toggleClass('active');
        let content = $target[0].nextElementSibling;
        if (!$target.hasClass('active')) {
            content.style.maxHeight = null;
            $(content).css('visibility', 'hidden');
        } else {
            content.style.maxHeight = content.scrollHeight + "px";
            $(content).css('visibility', 'visible');
        }
    })
    ).append(
        $('<div>', {
            class: 'collapsible-content rounded',
            css: { visibility: 'hidden' }
        }).append($contents)
    );
    return $collapsibleDiv;
}