/* Notifier.js
 *
 * Receives notifications from the server whenever a state is updated
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
import { CACHE_UPDATE } from "./StateManager.js";

export class Notifier {
    constructor() {
        this.ws = new WebSocket(`ws://${window.location.host}/ws/`);
        this.initialized = false;

        // Once the WS is open, tell the ABR Engine to send us the state since
        // we're connected
        this.ws.onopen = (_evt) => {
            let data = 'Web client connected';
            this.ws.send(data);
            this.initialized = true;
            $(this).trigger('notifierReady');
        }

        // When a message is received, update the state
        this.ws.onmessage = (evt) => {
            let target = JSON.parse(evt.data)['target'];
            if (target == 'state') {
                globals.stateManager.refreshState();
            } else if (target != null && target.startsWith(CACHE_UPDATE)) {
                let cacheName = target.replace(CACHE_UPDATE, '');
                globals.stateManager.refreshCache(cacheName);
            }
        }
    }

    async ready() {
        return new Promise((resolve, _reject) => {
            $(this).on('notifierReady', () => resolve());
        });
    }
}

