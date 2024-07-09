# colormap_utilities.py
#
# Copyright (c) 2021, University of Minnesota
# Author: Bridger Herman <herma582@umn.edu>, Daniel F. Keefe <dfk@umn.edu>
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

# This file is inspired by ColorLoop:
# https://github.umn.edu/ABRAM308/SculptingVisWebApp/blob/master/applets/static/color-loom/sketch.js

from xml.etree import ElementTree
from PIL import Image

from abr_server.lab_to_rgb import *

class ColormapInternal:
    def __init__(self):
        # Internal representation of a colormap: [(t1, (r1, g1, b1)), (t2, (r2,
        # g2, b2)), ...]
        # RGB are in 0-1 float range
        self.entries = []

    def add_control_point(self, data_val, color):
        self.entries.append((data_val, color))
        self.entries.sort(key=lambda t: t[0])

    def lookup_color(self, data_val):
        '''
            Look up a color within the colormap, interpolating between control
            points in CIELab space
        '''
        if len(self.entries) == 0:
            return (0, 0, 0) # Color.black
        elif len(self.entries) == 1:
            return self.entries[0][1] # Only color in the map
        else:
            min_val = self.entries[0][0]
            max_val = self.entries[-1][0]

            # check bounds
            if data_val >= max_val:
                return self.entries[-1][1]
            elif data_val <= min_val:
                return self.entries[0][1]
            else:
                # value within bounds
                # make i = upper control pt and (i-1) = lower control point
                i = 1
                while self.entries[i][0] < data_val:
                    i += 1

                # convert the two control points to lab space, interpolate
                # in lab space, then convert back to rgb space
                c1 = self.entries[i - 1][1]
                c2 = self.entries[i][1]

                lab1 = rgb2lab(c1)
                lab2 = rgb2lab(c2)

                v1 = self.entries[i - 1][0]
                v2 = self.entries[i][0]
                alpha = (data_val - v1) / (v2 - v1)

                lab_final = (
                    lab1[0] * (1.0 - alpha) + lab2[0] * alpha,
                    lab1[1] * (1.0 - alpha) + lab2[1] * alpha,
                    lab1[2] * (1.0 - alpha) + lab2[2] * alpha,
                )

                rgb_final = lab2rgb(lab_final)
                return rgb_final

def colormap_from_xml(xml_text, width, height, colormap_path):
    '''
    Read a colormap XML file and produce bytes for an image with particular dimensions
    '''
    root = ElementTree.fromstring(xml_text)
    if root.tag == 'ColorMaps':
        colormap_node = root.find('./ColorMap')
    else:
        colormap_node = root

    colormap = ColormapInternal()
    for point_node in colormap_node:
        x = float(point_node.attrib['x'])
        r = float(point_node.attrib['r'])
        g = float(point_node.attrib['g'])
        b = float(point_node.attrib['b'])
        color = (r, g, b)
        colormap.add_control_point(x, color)

    create_image_from_colormap(colormap, width, height, colormap_path)

def create_image_from_colormap(colormap, width, height, image_path):
    '''
    Given a colormap (ColormapInternal), create an image by drawing a bunch of little vertical rectangles
    '''
    # Initialize image to black
    pixels = [0 for _ in range(width * height)]

    # Add first row to image
    for col in range(0, width):
        color_float = colormap.lookup_color(col / float(width))
        color_int = tuple([int(c * 255) for c in color_float])
        pixels[col] = color_int

    # Copy to the rest of the image if greater than one row
    for row in range(1, height):
        for col in range(0, width):
            # Convert to 0-255 range
            pixels[col + row * width] = pixels[col]

    img = Image.new('RGB', (width, height))
    img.putdata(pixels)
    img.save(image_path)