# notifier.py
#
# Notifies WebSockets when the state has been updated!
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

import json
import uuid
from threading import Lock
from django.conf import settings
import logging
from enum import Enum

logger = logging.getLogger('django.server')

class MessageTarget(str, Enum):
    State = "state"
    VisAssetsCache = "CacheUpdate-visassets"

class NotifierMessage:
    def __init__(self, target):
        self.target = target

    def to_json(self):
        return {
            '$schema': settings.WS_SEND_SCHEMA,
            'target': self.target
        }

    def __str__(self):
        return json.dumps(self.to_json())

class StateNotifier:
    def __init__(self):
        self._connection_lock = Lock()
        self.ws_connections = {}

        # Dictionary of routes for {target -> {uuid1: fn, uuid2: fn, uuid3: fn}}
        # For example: {'thumbnail': [<function that saves a png>]}
        self.targets = {}


    def subscribe_ws(self, ws):
        sub_id = uuid.uuid4()
        with self._connection_lock:
            self.ws_connections[str(sub_id)] = ws
        logger.debug('Subscribed notifier WebSocket')
        return sub_id

    def unsubscribe_ws(self, sub_id):
        with self._connection_lock:
            if str(sub_id) in self.ws_connections:
                del self.ws_connections[str(sub_id)]
        logger.debug('Unsubscribed notifier WebSocket')

    def notify(self, message):
        '''
            Send out a message to all connected parties on WebSocket
        '''
        for _id, ws in self.ws_connections.items():
            ws.send_json(message.to_json())

    def receive(self, incoming_json, ws_id):
        '''Receive a message from a connected WebSocket'''
        # Perform all actions assocated with this particular route
        route = incoming_json['target']
        if route in self.targets:
            for (_action_id, action) in self.targets[route].items():
                action(incoming_json, ws_id)
        else:
            logger.error('Incoming WebSocket route `{}` does not exist'.format(route))

    def add_action(self, target_route, action_fn):
        '''Add an action to be performed when `target_route` receives a payload
        over the WebSocket. `action_fn` should take two arguments: the
        received message and the sender's ID.  Returns a new UUID associated
        with this action, can be used to remove from actions'''
        action_id = uuid.uuid4()
        target_actions = self.targets.get(target_route, {})
        target_actions[action_id] = action_fn
        self.targets[target_route] = target_actions
        return action_id

    def remove_action(self, target_route, action_id):
        try:
            del self.targets[target_route][action_id]
            return True
        except KeyError:
            return False


notifier = StateNotifier()