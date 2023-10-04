/* index.js
 *
 * Main file
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

import { Validator } from '../../common/Validator.js';
import { globals } from '../../common/globals.js';
import { ComposeManager } from './ComposeManager.js';
import { Notifier } from '../../common/Notifier.js';
import { StateManager } from '../../common/StateManager.js';

// globals.validator and globals.schema are guaranteed to be defined after this
// finishes
async function initValidator() {
    globals.validator = new Validator('ABRSchema_0-2-0.json');

    let scm = await globals.validator.schema;
    globals.schema = scm;
}

// globals.notifier is guaranteed to be initialized after this
async function initNotifier() {
    let notifier = new Notifier();
    await notifier.ready();
    globals.notifier = notifier;
}

async function initState() {
    let stateManager = new StateManager();
    await stateManager.refreshState();
    globals.stateManager = stateManager;
}

async function initCaches() {
    await globals.stateManager.refreshCache('visassets');
}

async function initAll() {
    await initValidator();
    await initNotifier();
    await initState();
    await initCaches();
}

function init() {
    let toInit = [];

    toInit.push(initAll());

    // Wait for all pre-fetching to finish before loading the UI
    Promise.all(toInit)
        .then(() => new ComposeManager());
}

window.onload = init;