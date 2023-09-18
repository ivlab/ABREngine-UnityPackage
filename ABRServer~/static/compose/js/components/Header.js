/* Header.js
 *
 * Header going across the top of the ABR Compose UI
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
import { download } from "../../../common/helpers.js";

const STORAGE_STATE_PREFIX = '_state_';
const STORAGE_THUMB_PREFIX = '_thumb_';

export function Header() {
    let defaultStateName = globals.schema.properties.name.default;

    let $header = $('<header>', {
        id: 'header',
    });

    // Populate the file functions
    let $fileHeader = $('<div>', {
        id: 'file-header'
    });

    // Save as button
    let $saveStateAsButton = $('<div>')
        .append($('<span>', { class: 'material-icons', text: 'save'}))
        .append($('<span>', { text: 'Save State As...' }))
        .on('click', (_evt) => {
            let $saveAsDialog = $('<div>', {
                title: 'Save state as',
                id: 'save-as-dialog',
            }).dialog({
                resizable: false,
                height: 'auto',
                width: 400,
                modal: true,
                buttons: {
                    "Save": function () {
                        let $input = $(this).find('input');
                        let stateName = $input.val();
                        saveState(stateName);
                        $(this).dialog('destroy');
                    },
                    "Cancel": function () {
                        $(this).dialog('destroy');
                    }
                }
            });

            $saveAsDialog.append($('<div>').append($('<input>', {
                    id: 'abr-state-save-name',
                    type: 'text',
                    val: globals.stateManager.state.name ? globals.stateManager.state.name : defaultStateName,
                }).on('input', (evt) => {
                    let stateName = $(evt.target).val();
                    if (localStorage.getItem(stateName)) {
                        $(evt.target).parent().append($('<p>', {class: 'save-as-warning', text: `Warning: State '${stateName}' already exists`}));
                    } else {
                        $('.save-as-warning').remove();
                        $(evt.target).css('background-color', null);
                    }
                }).on('keyup', (evt) => {
                    if (evt.key == 'Enter') {
                        let $input = $(evt.target);
                        let stateName = $input.val();
                        saveState(stateName);
                        $(evt.target).parents('#save-as-dialog').dialog('destroy');
                    }
                })
            ));
    });

    // Import state button
    let $importStateButton = $('<div>')
        .append($('<span>', { class: 'material-icons', text: 'cloud_upload'}))
        .append($('<span>', { text: 'Import State...' })).on('click', (evt) => {
            // Create a fake element to handle the actual upload
            let $fileInput = $('<input>', {
                type: 'file',
            }).on('change', (evt) => {
                if (!evt.target.files || !evt.target.files[0]) {
                    alert('No files uploaded!');
                    return;
                }

                let stateFileName = evt.target.files[0].name;
                // get rid of file extension
                let stateName = stateFileName.replace(/\.[^/.]+$/, ""); // https://stackoverflow.com/a/4250408

                let reader = new FileReader();
                $(reader).on('load', (loadEvt) => {
                    // Update the state with the stateManager
                    let state = JSON.parse(loadEvt.target.result);
                    state.name = stateName;
                    let stateStr = JSON.stringify(state);
                    localStorage[STORAGE_STATE_PREFIX + stateName] = stateStr;
                    globals.stateManager.updateState(stateStr);
                });
                reader.readAsText(evt.target.files[0]);

                $fileInput.remove();
            });
            $('body').append($fileInput);
            $fileInput.click();
    });

    // Export State Button
    let $exportStateButton = $('<div>')
        .append($('<span>', { class: 'material-icons', text: 'cloud_download'}))
        .append($('<span>', { text: 'Export State...' })).on('click', (evt) => {
            let state = globals.stateManager.state;
            let fileName = state.name + '.json';
            download(fileName, JSON.stringify(state, null, 4), 'data:application/json,');
    });

    // Clear the state
    let $clearStateButton = $('<div>')
        .append($('<span>', { class: 'material-icons', text: 'backspace'}))
        .append($('<span>', { text: 'Clear State...' }))
        .on('click', (_evt) => {
            if (window.confirm('Are you sure you want to clear the state?')) {
                globals.stateManager.removePath('').then(() => window.location.reload() );
            }
    });

    // Open a raw state editor in a new window
    let $rawEditorButton = $('<div>')
        .append($('<span>', { class: 'material-icons', text: 'code'}))
        .append($('<span>', { text: 'Open JSON editor...' }))
        .on('click', (_evt) => {
            window.open(window.location.href + 'raw-editor', '_blank');
    });


    let outTimer = null;
    $('<ul>', {
        id: 'abr-menu',
        css: { visibility: 'hidden' }
    }).append(
        $('<li>').append($saveStateAsButton)
    ).append(
        $('<li>').append($importStateButton)
    ).append(
        $('<li>').append($exportStateButton)
    ).append(
        $('<li>').append($rawEditorButton)
    ).append(
        $('<li>').append($clearStateButton)
    ).menu().appendTo($(document.body)).on('mouseout', (evt) => {
        outTimer = setTimeout(() => $('#abr-menu').css('visibility', 'hidden'), 500);
    }).on('mouseover', (evt) => {
        clearTimeout(outTimer);
        outTimer = null;
    });

    // "ABR" button - like file button; open menu when clicked
    $fileHeader.append(
        $('<button>', { class:  'abr-main-button rounded' }).append(
            $('<img>', { src: `${STATIC_URL}favicon.ico` })
        ).on('click', (evt) => {
            let visibility = $('#abr-menu').css('visibility');
            let newVisibility = visibility == 'visible' ? 'hidden' : 'visible';
            $('#abr-menu').css('visibility', newVisibility);
        })
    );

    // Load a state
    $fileHeader.append($('<button>', {
        class: 'material-icons rounded',
        html: 'folder_open',
        title: 'Load state...',
        id: 'load-state',
    }).on('click', (_evt) => {
        let $loadDialog = $('<div>', {
            title: 'Load state',
            id: 'load-state-dialog',
        }).dialog({
            resizable: false,
            height: 'auto',
            width: $('body').width() * 0.75,
            modal: true,
            position: { my: 'center top', at: 'center top', of: window},
            buttons: {
                "Load": function () {
                    let stateName = $(this).find('.selected-state .state-name').text();
                    if (stateName) {
                        $('#state-header #state-name').text(stateName);

                        // Tell the server to update
                        globals.stateManager.updateState(localStorage.getItem(STORAGE_STATE_PREFIX + stateName));
                        $(this).dialog('destroy');
                    } else {
                        alert('Please select a state to load');
                    }
                },
                "Cancel": function () {
                    $(this).dialog('destroy');
                }
            }
        });

        let $allStates = $('<div>', {
            class: 'state-list'
        });
        for (const item in localStorage) {
            if (item.startsWith(STORAGE_STATE_PREFIX)) {
                let stateName = item.replace(STORAGE_STATE_PREFIX, '');
                $allStates.append($('<div>', {
                    class: 'state-selector rounded',
                    title: 'Select a state and click "Load" or double click a state',
                    css: { cursor: 'pointer' }
                }).on('click', (evt) => {
                    let $target = $(evt.target).closest('.state-selector');
                    $('.selected-state').removeClass('selected-state');
                    $target.addClass('selected-state');

                    // Make the "Load" button change color
                    $('#load-state-dialog')
                        .parents('.ui-dialog')
                        .find('.ui-button')
                        .filter((i, el) => $(el).text() == 'Load')
                        .css('background-color', '#ceedff');
                }).on('dblclick', (evt) => {
                    let stateName = $(evt.target).parents().find('.selected-state .state-name').text();
                    $('#state-header #state-name').text(stateName);
                    // Tell the server to update
                    globals.stateManager.updateState(localStorage.getItem(STORAGE_STATE_PREFIX + stateName));
                    $('#load-state-dialog').dialog('destroy');
                }).append(
                    $('<img>', {
                        class: 'state-thumbnail',
                        src: localStorage[STORAGE_THUMB_PREFIX + stateName],
                    })
                ).append(
                    $('<p>', {
                        class: 'state-name',
                        text: stateName
                    })
                ).append(
                    $('<div>', {
                        class: 'state-hover-controls'
                    }).append(
                        $('<button>', {
                            text: 'Delete',
                            css: { 'background-color': '#ffdddd' }
                        }).prepend(
                            $('<span>', { class: 'ui-icon ui-icon-trash' }
                        )).on('click', (evt) => {
                            let sure = confirm(`Are you sure you want to delete state '${stateName}'?`)
                            if (sure) {
                                localStorage.removeItem(item);
                                $(evt.target).closest('.state-selector').remove();
                            }
                        })
                    )
                ))
            }
        }
        $loadDialog.append($allStates);
    }));

    // Save a state to localStorage
    $fileHeader.append($('<button>', {
        class: 'material-icons rounded',
        html: 'save',
        title: 'Save state', 
    }).on('click', (evt) => {
        let stateName = globals.stateManager.state.name;

        // If the state name is still the default, ask the user to input a new one
        if (!stateName || stateName == defaultStateName) {
            $saveStateAsButton.trigger('click');
        } else {
            saveState(stateName);
            $('#state-header #state-name').text(stateName);
            $('.save-animation').addClass('animating');
            setTimeout(() => {
                $('.save-animation').removeClass('animating');
            }, 2000);
        }
    }));

    // Undo/Redo
    $fileHeader.append($('<button>', {
        class: 'material-icons rounded',
        html: 'undo',
        title: 'Undo', 
    }).on('click', (_evt) => {
        globals.stateManager.undo();
        $('#state-name').text(globals.stateManager.state.name ? globals.stateManager.state.name : defaultStateName);
    }));
    $fileHeader.append($('<button>', {
        class: 'material-icons rounded',
        html: 'redo',
        title: 'Redo', 
    }).on('click', (_evt) => {
        globals.stateManager.redo();
        $('#state-name').text(globals.stateManager.state.name ? globals.stateManager.state.name : defaultStateName);
    }));

    // More settings
    // TODO
    // $fileHeader.append($('<button>', {
    //     class: 'material-icons rounded',
    //     html: 'settings',
    //     title: 'More options...', 
    // }));

    //----------------------------------------------------------------------

    let $stateHeader = $('<div>', {
        id: 'state-header',
    });

    // State name for the header
    $stateHeader.append($('<p>', {
        id: 'state-name',
        text: globals.stateManager.state.name ? globals.stateManager.state.name : defaultStateName,
    }));

    // Loading spinner
    // TODO
    $stateHeader.append($('<p>', {
        class: 'material-icons save-animation',
        text: 'done'
    }))
    $stateHeader.append($('<div>', {
        class: 'abr-state-subscriber loading-spinner',
        title: 'Loading...',
        css: {visibility: 'hidden'},
    }));

    //----------------------------------------------------------------------

    let $screenshotHeader = $('<div>', {
        id: 'screenshot-header',
    });

    // Capture a screenshot
    // TODO
    // $screenshotHeader.append($('<button>', {
    //     class: 'material-icons rounded',
    //     html: 'camera_alt',
    // }).on('click', (_evt) => {
    //     alert('For now, please make screenshots in Unity using the "s" key.')
    // }));

    // Screenshot gallery
    // TODO
    // $screenshotHeader.append($('<button>', {
    //     class: 'material-icons rounded',
    //     html: 'collections', 
    // }));

    // Put all the sub-headers in the main header
    $header.append($fileHeader);
    $header.append($stateHeader);
    $header.append($screenshotHeader);

    return $header;
}

function saveState(stateName) {
    globals.stateManager.update('/name', stateName).then(() => {
        localStorage[STORAGE_STATE_PREFIX + stateName] = JSON.stringify(globals.stateManager.state);
        localStorage[STORAGE_THUMB_PREFIX + stateName] = globals.stateManager.latestThumbnail;
        $('#state-header #state-name').text(stateName);
    });
}