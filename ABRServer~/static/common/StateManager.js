/* StateManager.js
 *
 * Manages the browser-local state for the visualization and handles state communication with server.
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

import { globals } from "./globals.js";

export const STATE_UPDATE_EVENT = 'ABRStateUpdate';
export const CACHE_UPDATE = 'CacheUpdate-';

// Resolve schema consts to values, if there are any values contained within
// consts
// For example: {
//      "inputValue": { "const": "4m" },
//      "inputType": { "const": "IVLab.ABREngine.LengthPrimitive" }
// }
// resolves to {
//      "inputValue": "4m",
//      "inputType": "IVLab.ABREngine.LengthPrimitive"
// }
// This assumes that no input value will be an object!! (all inputValues should
// be strings)
export function resolveSchemaConsts(data) {
    let resolvedData = {};
    for (const field in data) {
        if (typeof(data[field]) === 'object') {
            // If it has a const, take it
            if (data[field].const) {
                resolvedData[field] = data[field].const;
            // If it has a default, take it
            } else if (data[field].default) {
                resolvedData[field] = data[field].default;
            // If it has a oneOf with a const, take the first one
            } else if (data[field].oneOf) {
                let oneOf = data[field].oneOf;
                if (oneOf.length > 0 && oneOf[0].const) {
                    resolvedData[field] = oneOf[0].const;
                }
            }
        } else if (typeof(data[field] === 'string')) {
            resolvedData[field] = data[field];
        }
    }
    return resolvedData;
}

export class StateManager {
    constructor() {
        this._state = {};
        this._previousState = {};
        this._subscribers = [];
        this._cacheSubscribers = {};
        this._caches = {};

        this._thumbnailPoll = null;
        this._thumbPollAttempts = 0;
        this.latestThumbnail = null;
        this.updateLatestThumbnail();
    }

    // Retrieve the latest thumbnail from the server
    async updateLatestThumbnail() {
        let b = await fetch('/media/thumbnails/latest-thumbnail.png?' + Date.now()).then((resp) => resp.blob());
        let updated = new Promise((resolve, reject) => {
            let reader = new FileReader();
            reader.readAsDataURL(b);
            reader.onloadend = () => {
                this.latestThumbnail = reader.result;
                // Debugging: latest thumbnail preview
                // $('#latest-thumbnail').remove();
                // $('body').append($('<img>', {
                //     id: 'latest-thumbnail',
                //     src: this.latestThumbnail,
                //     css: {
                //         'position': 'absolute',
                //         'top': 0,
                //         'right': 0
                //     }
                // }));
                resolve();
            }
        })
        await updated;
    }

    async refreshState() {
        await fetch('/api/state')
            .then((resp) => resp.text())
            .then((newState) => {
                let stateJson = JSON.parse(newState);
                return globals.validator.validate(stateJson.state)
            })
            .then((stateJson) => {
                this._previousState = this._state;
                this._state = stateJson;
                for (const sub of this._subscribers) {
                    $(sub).trigger(STATE_UPDATE_EVENT);
                }
            });

        // Poll for updates to the thumbnail, stop trying when there's a new thumbnail or if we've tried more than 10 times
        if (!this._thumbnailPoll) {
            this._thumbnailPoll = setInterval(async () => {
                let prevThumbnail = `${this.latestThumbnail}`;
                await this.updateLatestThumbnail();
                if (this._thumbPollAttempts > 10 || this.latestThumbnail != prevThumbnail) {
                    clearInterval(this._thumbnailPoll);
                    this._thumbnailPoll = null;
                    this._thumbPollAttempts = 0;
                }
                this._thumbPollAttempts += 1;
            }, 500);
        }
    }

    async updateState(newState) {
        await fetch('/api/state', {
            method: 'PUT',
            headers: {
                'Content-Type': 'application/json',
                // 'X-CSRFToken': csrftoken,
            },
            mode: 'same-origin',
            body: newState,
        });
    }

    // Send an update to a particular object in the state. updateValue MUST be
    // an object.
    async update(updatePath, updateValue) {
        await fetch('/api/state/' + updatePath, {
            method: 'PUT',
            headers: {
                'Content-Type': 'application/json',
                // 'X-CSRFToken': csrftoken,
            },
            mode: 'same-origin',
            body: JSON.stringify(updateValue),
        }).then(async (resp) => {
            if (!resp.ok) {
                let text = await resp.text();
                throw new Error(text);
            }
        });
    }

    // Remove all instances of a particular value from the state
    // Particularly useful when deleting data impressions
    async removeAll(value) {
        await fetch('/api/remove/' + value, {
            method: 'DELETE',
            headers: {
                // 'X-CSRFToken': csrftoken,
            },
            mode: 'same-origin'
        });
    }

    // Remove something at a particular path
    async removePath(path) {
        path = path ? path : '';
        await fetch('/api/remove-path/' + path, {
            method: 'DELETE',
            headers: {
                // 'X-CSRFToken': csrftoken,
            },
            mode: 'same-origin'
        });
    }

    async removeVisAsset(visAssetUUID) {
        await fetch('/api/remove-visasset/' + visAssetUUID, {
            method: 'DELETE',
            headers: {
                // 'X-CSRFToken': csrftoken,
            },
            mode: 'same-origin',
        })
    }

    async undo() {
        await fetch('/api/undo', {
            method: 'POST',
            headers: {
                // 'X-CSRFToken': csrftoken,
            },
            mode: 'same-origin'
                    });
    }

    async redo() {
        await fetch('/api/redo', {
            method: 'POST',
            headers: {
                // 'X-CSRFToken': csrftoken,
            },
            mode: 'same-origin'
                    });
    }

    get state() {
        return this._state;
    }

    get previousState() {
        return this._previousState;
    }

    // Find the elements that satisfy a condition
    findAll(condition, startPath='') {
        let startState = this.state;
        if (startPath.length > 0) {
            startState = this.getPath(startPath);
        }
        let [outItems, outPath] = this._findAll(condition, startState, startPath, [], []);
        return outItems;
    }

    // Find the path(s) that satisfy a condition
    findPath(condition, startPath='') {
        let startState = this.state;
        if (startPath.length > 0) {
            startState = this.getPath(startPath);
        }
        let [outItems, outPath] = this._findAll(condition, startState, startPath, [], []);
        return outPath;
    }

    // Find all occurances of lambda function "condition" in the state
    _findAll(condition, subState, currentPath, outItems, outPath) {
        if (typeof(subState) == 'object' && Object.keys(subState).length == 0) {
            return subState;
        } else {
            if (condition(subState)) {
                outItems.push(subState);
                outPath.push(currentPath);
            }
            for (const subValue in subState) {
                if (typeof(subState) == 'object') {
                    this._findAll(condition, subState[subValue], currentPath + '/' + subValue, outItems, outPath)
                }
            }
            return [outItems, outPath];
        }
    }

    // Get the object located at /path/to/object
    getPath(path) {
        let pathParts = path.slice(1).split('/');
        let subState = this.state;
        for (const subPath of pathParts) {
            if (subState.hasOwnProperty(subPath)) {
                subState = subState[subPath];
            } else {
                return null;
            }
        }
        return subState;
    }

    // Find if a key exists within a specific path
    // e.g. (['localVisAssets'], '04d115b5-8ae7-45ac-b889-2ef0c537b957')
    keyExists(pathArray, key) {
        let obj = this.state;
        for (let i = 0; i < pathArray.length; i++) {
            obj = obj[pathArray[i]];
        }
        if (obj) {
            return obj.hasOwnProperty(key);
        } else {
            return null;
        }
    }

    // Find the length of a particular path in the state
    // e.g. (['primitiveGradients']) => 5
    length(pathArray) {
        let obj = this.state;
        for (let i = 0; i < pathArray.length; i++) {
            obj = obj[pathArray[i]];
        }
        if (obj) {
            if (Array.isArray(obj)) {
                return obj.length;
            } else if (typeof(obj) == 'object') {
                return Object.keys(obj).length;
            }
        } else {
            return -1;
        }
    }

    subscribe($element) {
        this._subscribers.push($element);
    }

    unsubscribe($element) {
        this._subscribers.remove($element);
    }

    async refreshCache(cacheName) {
        await fetch('/api/' + cacheName)
            .then((resp) => resp.json())
            .then((json) => {
                this._caches[cacheName] = json;
                $(`.cache-subscription-${cacheName}`).each((_i, el) => {
                    $(el).trigger(CACHE_UPDATE + cacheName);
                })
            });
    }

    getCache(cacheName) {
        return this._caches[cacheName] ? this._caches[cacheName] : {};
    }

    // Subscribe to when a particular cache is updated
    subscribeCache(cacheName, $element) {
        $element.addClass(`cache-subscription-${cacheName}`);
    }
    unsubscribeCache(cacheName, $element) {
        $element.removeClass(`cache-subscription-${cacheName}`);
    }
}