# state.py
#
# Manages the canonical state for an ABR application, and notifies any connected
# graphics engines and design user interfaces when the state has been updated.
#
# Copyright (c) 2021, University of Minnesota
# Author: Bridger Herman <herma582@umn.edu>
#
# This program is free software: you can redistribute it and/or modify
# it under the terms of the GNU General Public License as published by
# the Free Software Foundation, either version 3 of the License, or
# (at your option) any later version.

# This program is distributed in the hope that it will be useful,
# but WITHOUT ANY WARRANTY; without even the implied warranty of
# MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
# GNU General Public License for more details.

# You should have received a copy of the GNU General Public License
# along with this program.  If not, see <https://www.gnu.org/licenses/>.

import os
import datetime
import sys
import jsonschema
import requests
import json
import time
import jsondiff
import logging
from copy import deepcopy
from django.conf import settings
from pathlib import Path
from threading import Lock

from .notifier import MessageTarget, NotifierMessage, notifier
from .visasset_manager import download_visasset

logger = logging.getLogger('django.server')

class State():
    def __init__(self):
        # Make sure the backup location exists
        if not settings.BACKUP_PATH.parent.exists():
            os.makedirs(settings.BACKUP_PATH.parent)
        settings.BACKUP_PATH.touch()
        self.backup_path = settings.BACKUP_PATH.resolve()

        resp = requests.get(settings.SCHEMA_URL)

        # Get most recent backed up schema
        try:
            backup_schema_dir = os.path.join(settings.STATIC_ROOT, 'schemas')
            backup_schema = list(sorted(os.listdir(backup_schema_dir), reverse=True))[0]
        except:
            logger.error('Unable to find backup schema in {0}'.format(backup_schema_dir))
            backup_schema = None

        if backup_schema is not None:
            backup_schema = os.path.join(backup_schema_dir, backup_schema)

        if resp.status_code != 200:
            logger.error('Unable to load schema from url {0}, using backup schema {1}'.format(settings.SCHEMA_URL, backup_schema))
            with open(backup_schema) as fin:
                self.state_schema = json.load(fin)
        else:
            self.state_schema = resp.json()
            # And save a backup copy to the folder if necessary
            need_backup = True
            if backup_schema is not None:
                with open(backup_schema) as fin:
                    if fin.read() == resp.text:
                        need_backup = False
            if need_backup:
                schema_name = datetime.datetime.now().isoformat().replace(':', '_') + '.json'
                if not os.path.isdir(backup_schema_dir):
                    os.makedirs(backup_schema_dir)
                schema_bak_path = Path(backup_schema_dir).joinpath(schema_name)
                with open(schema_bak_path, 'w') as fout:
                    fout.write(resp.text)
                logger.info('Saved backup schema to ' + str(schema_bak_path))

        # Lock around state modifications
        self._state_lock = Lock()

        self._default_state = {
            'version': self.state_schema['properties']['version']['default']
        }

        logger.info('Using ABR Schema, version {}'.format(self._default_state['version']))

        # Initialize a blank starting state
        self._state = deepcopy(self._default_state)

        with self._state_lock:
            jsonschema.validate(self._state, self.state_schema)

        # Make the temporary state for pending modifications
        self._pending_state = deepcopy(self._state)

        # JSON diffs for undoing/redoing
        self.undo_stack = []
        self.redo_stack = []

    # Validate the pending state, back it up, populate the undo stack, etc.
    # Returns a string of any validation errors
    def validate_and_backup(self):
        try:
            # Sometimes validation fails (collection changed during iteration??)
            tries = 10
            for i in range(tries):
                try:
                    jsonschema.validate(self._pending_state, self.state_schema)
                    break
                except RuntimeError:
                    pass

            # If we've successfully validated the state, make a backup. Keep
            # up to a certain amount of backups if something crashes
            self.make_backup()

            # Save the new state
            # Also store a stack of undos, based on json diff
            # Clear the redo stack, because if we made a change to the state all
            # the previous redos are invalid
            with self._state_lock:
                state_diff = jsondiff.diff(self._pending_state, self._state, syntax='symmetric', marshal=True)
                if len(state_diff) > 0: # Only record the change if there's actually a diff
                    self.undo_stack.append(state_diff)
                    self.redo_stack.clear()
                    self._state = deepcopy(self._pending_state)

            # Tell any connected clients that we've updated the state
            notifier.notify(NotifierMessage(MessageTarget.State))

            return ''
        except jsonschema.ValidationError as e:
            # Discard the pending state and reset if it was invalid
            self._pending_state = deepcopy(self._state)

            path = '/'.join(e.path)
            return 'Schema validation failed - {}: {}'.format(path, e.message)

    # CRUD operations
    def get_path(self, item_path):
        with self._state_lock:
            try:
                return self._get_path(self._state, item_path)
            except KeyError:
                return None
        
    def _get_path(self, sub_state, sub_path_parts):
        if len(sub_path_parts) == 0:
            return sub_state
        elif len(sub_path_parts) == 1:
            return sub_state[sub_path_parts[0]]
        else:
            root = sub_path_parts[0]
            rest = sub_path_parts[1:]
            return self._get_path(sub_state[root], rest)

    def set_path(self, item_path, new_value):
        if len(item_path) == 0:
            self._pending_state = new_value
        else:
            self._set_path(self._pending_state, item_path, new_value)

        final_result = self.validate_and_backup()

        # If there aren't any errors and DOWNLOAD_VISASSETS is set, download the
        # visassets
        if len(final_result) == 0 and settings.DOWNLOAD_VISASSETS:
            all_visassets = self._find_all(
                # Has inputValue, it's a VisAsset, and it's not a localVisAsset defined in this state
                lambda v: 'inputValue' in v and \
                    'inputGenre' in v and \
                    v['inputGenre'] == 'VisAsset' and \
                    'localVisAssets' in self._state and \
                    v['inputValue'] not in self._state['localVisAssets'],
                self._state,
                []
            )
            vis_asset_fails = ''
            for input_value_object in all_visassets:
                failed = download_visasset(input_value_object['inputValue'], None) # we don't know where it might've come from
                vis_asset_fails += '\n' + str(failed) if len(failed) > 0 else ''
            if len(vis_asset_fails) > 0:
                vis_asset_fails = '\nFailed to download VisAssets: ' + vis_asset_fails
            logger.warning(vis_asset_fails)
            notifier.notify(NotifierMessage(MessageTarget.VisAssetsCache))

        return final_result

    def _set_path(self, sub_state, sub_path_parts, new_value):
        if len(sub_path_parts) == 1:
            # Relies on dicts being mutable
            sub_state[sub_path_parts[0]] = new_value
        else:
            root = sub_path_parts[0]
            rest = sub_path_parts[1:]

            # Assuming everything we're assigning will be an object
            if root not in sub_state:
                sub_state[root] = {}

            self._set_path(sub_state[root], rest, new_value)

    def remove_path(self, item_path):
        if len(item_path) == 0:
            self._pending_state = deepcopy(self._default_state)
        else:
            self._remove_path(self._pending_state, item_path)
        return self.validate_and_backup()

    def _remove_path(self, sub_state, sub_path_parts):
        if len(sub_path_parts) == 1:
            # Relies on dicts being mutable
            del sub_state[sub_path_parts[0]]
        else:
            root = sub_path_parts[0]
            rest = sub_path_parts[1:]
            if root in sub_state:
                self._remove_path(sub_state[root], rest)

    def remove_all(self, value):
        self._pending_state = self._remove_all(value, deepcopy(self._state))
        self.validate_and_backup()

    def _remove_all(self, value, sub_state):
        if len(sub_state) == 0:
            return sub_state
        else:
            if value in sub_state:
                del sub_state[value]
            for sub_value in sub_state:
                if isinstance(sub_state[sub_value], dict):
                    sub_state[sub_value] = self._remove_all(value, sub_state[sub_value])
            return sub_state

    def _find_all(self, condition, sub_state, out_items):
        if len(sub_state) == 0:
            return sub_state
        else:
            if condition(sub_state):
                out_items.append(sub_state)
            for sub_value in sub_state:
                if isinstance(sub_state[sub_value], dict):
                    self._find_all(condition, sub_state[sub_value], out_items)
            return out_items


    def make_backup(self):
        '''
            Save a backup of the state to the backup file. Discard backups more
            than a certain amount.
        '''
        try:
            with open(self.backup_path, 'r') as backup_file:
                backup_json = json.load(backup_file)
        except FileNotFoundError:
            backup_json = {}
        except json.decoder.JSONDecodeError:
            backup_json = {}

        to_delete = set()
        for time_key in backup_json:
            t = float(time_key)
            if time.time() - t > settings.BACKUP_DELETE_INTERVAL:
                to_delete.add(time_key)
        for time_key in to_delete:
            del backup_json[time_key]

        with self._state_lock:
            backup_json[time.time()] = json.dumps(self._state)

        with open(self.backup_path, 'w') as backup_file:
            json.dump(backup_json, backup_file)

    def restore_backup(self):
        '''
            Restore a backup from a file
        '''
        # Sort the backup entries and obtain the first (newest) one
        # backup_entries = list(sorted(map(lambda d: (float(d[0]), d[1]), backup_json.items()), key=lambda d: d[0]))
        # key_time, most_recent_diff = backup_entries[-1]
        raise NotImplementedError()

    def undo(self):
        '''
            Obtain the previous state diff and apply it. Uses JSON diff to
            minimize memory usage.
        '''

        try:
            diff_w_previous = self.undo_stack.pop()
        except IndexError:
            return 'Nothing to undo'

        with self._state_lock:
            undone_state = jsondiff.patch(self._state, diff_w_previous, syntax='symmetric', marshal=True)
            self._state = undone_state
            self._pending_state = undone_state
        self.redo_stack.append(diff_w_previous)

        # Tell any connected clients that we've updated the state
        notifier.notify(NotifierMessage(MessageTarget.State))

        return ''

    def redo(self):
        '''
            "Undo the undo" by unpatching with the latest item in the redo
            stack. jsondiff doesn't explicitly support unpatching so we go to
            the internals here
        '''

        try:
            diff_w_next = self.redo_stack.pop()
        except IndexError:
            return 'Nothing to redo'

        with self._state_lock:
            undone_state = jsondiff.JsonDiffer(syntax='symmetric', marshal=True).unpatch(self._state, diff_w_next)
            self._state = undone_state
            self._pending_state = undone_state
        self.undo_stack.append(diff_w_next)

        # Tell any connected clients that we've updated the state
        notifier.notify(NotifierMessage(MessageTarget.State))

        return ''

state = State()