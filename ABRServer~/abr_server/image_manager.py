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

import logging
from django.conf import settings
import base64
from pathlib import Path

from .notifier import notifier

logger = logging.getLogger('django.server')

class ImageManager:
    def __init__(self):
        notifier.add_action('thumbnail', self.save_thumbnail)

    def save_thumbnail(self, msg, sender_id):
        content = msg['content']
        content_binary = base64.b64decode(content)
        thumbnail_path = settings.THUMBNAILS_PATH
        if not thumbnail_path.exists():
            thumbnail_path.mkdir(parents=True)

        # TODO: Shouldn't do this until sender IDs are unique only across
        # instances (e.g. Unity on the same computer should have the same ID
        # when it's started/stopped multiple times)
        #
        # Write the thumbnail from a particular sender
        # with open(thumbnail_path.joinpath('latest-thumbnail_' + str(sender_id) + '.png'), 'wb') as fout:
        #     fout.write(content_binary)

        # Write the thumbnail for most-recent
        with open(thumbnail_path.joinpath('latest-thumbnail.png'), 'wb') as fout:
            fout.write(content_binary)

image_manager = ImageManager()