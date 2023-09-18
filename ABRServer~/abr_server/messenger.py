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

from channels.generic.websocket import WebsocketConsumer
import logging
import json
import uuid
import jsonschema
import requests
from django.conf import settings

from .notifier import notifier

logger = logging.getLogger('django.server')

class ClientMessenger(WebsocketConsumer):
    def __init__(self, *args, **kwargs):
        resp = requests.get(settings.WS_SEND_SCHEMA)
        if resp.status_code != 200:
            logger.error('Unable to load schema from url {0}'.format(settings.WS_SEND_SCHEMA))
            return
        self.outgoing_schema = resp.json()

        resp = requests.get(settings.WS_RECEIVE_SCHEMA)
        if resp.status_code != 200:
            logger.error('Unable to load schema from url {0}'.format(settings.WS_RECEIVE_SCHEMA))
            return
        self.incoming_schema = resp.json()

        self.id = None
        super().__init__(*args, **kwargs)

    def connect(self):
        self.accept()
        logger.debug('WebSocket client connected')
        self.id = notifier.subscribe_ws(self)

    def disconnect(self, status):
        logger.debug('WebSocket client disconnected: {}'.format(status))
        notifier.unsubscribe_ws(self.id)

    def receive(self, text_data=None, bytes_data=None):
        default_ret = super().receive(text_data=text_data, bytes_data=bytes_data)

        if len(text_data) > 0:
            try:
                incoming_json = json.loads(text_data)
                jsonschema.validate(incoming_json, self.incoming_schema)
            except json.decoder.JSONDecodeError:
                logger.error('Incoming WebSocket message is not JSON')
            except jsonschema.ValidationError as e:
                logger.error('Incoming WebSocket JSON failed to validate: ' + str(e))
            else:
                notifier.receive(incoming_json, self.id)

        return default_ret

    def send_json(self, msg_json):
        try:
            jsonschema.validate(msg_json, self.outgoing_schema)
        except jsonschema.ValidationError as e:
            logger.error('Outgoing WebSocket JSON failed to validate: ' + str(e))
        else:
            self.send(json.dumps(msg_json))