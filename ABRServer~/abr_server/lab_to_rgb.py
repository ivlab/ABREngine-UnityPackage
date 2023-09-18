#    ----- BEGIN EXTERNAL CODE FOR RGB-LAB CONVERSION -----
#    https://github.com/antimatter15/rgb-lab
#
#
#   MIT License
#   Copyright (c) 2014 Kevin Kwok <antimatter15@gmail.com>
#   Permission is hereby granted, free of charge, to any person obtaining a copy
#   of this software and associated documentation files (the "Software"), to deal
#   in the Software without restriction, including without limitation the rights
#   to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
#   copies of the Software, and to permit persons to whom the Software is
#   furnished to do so, subject to the following conditions:
#   The above copyright notice and this permission notice shall be included in all
#   copies or substantial portions of the Software.
#   THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
#   IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
#   FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
#   AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
#   LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
#   OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
#   SOFTWARE.
#
# the following functions are based off of the pseudocode
# found on www.easyrgb.com

import math

# public static List<float> lab2rgb(List<float> lab) {
def lab2rgb(lab):
    y = (lab[0] + 16.0) / 116.0
    x = lab[1] / 500.0 + y
    z = y - lab[2] / 200.0

    x = 0.95047 * (x * x * x if (x * x * x > 0.008856) else (x - 16.0 / 116.0) / 7.787)
    y = 1.00000 * (y * y * y if (y * y * y > 0.008856) else (y - 16.0 / 116.0) / 7.787)
    z = 1.08883 * (z * z * z if (z * z * z > 0.008856) else (z - 16.0 / 116.0) / 7.787)

    r = x * 3.2406 + y * -1.5372 + z * -0.4986
    g = x * -0.96890 + y * 1.8758 + z * 0.0415
    b = x * 0.05570 + y * -0.2040 + z * 1.0570

    r = (1.055 * math.pow(r, 1.0 / 2.4) - 0.055) if (r > 0.0031308) else 12.92 * r
    g = (1.055 * math.pow(g, 1.0 / 2.4) - 0.055) if (g > 0.0031308) else 12.92 * g
    b = (1.055 * math.pow(b, 1.0 / 2.4) - 0.055) if (b > 0.0031308) else 12.92 * b

    # colors are 0-1 float
    return (
        max(0.0, min(1.0, r)),
        max(0.0, min(1.0, g)),
        max(0.0, min(1.0, b))
    )


# public static List<float> rgb2lab(List<float> rgb) {
def rgb2lab(rgb):
    # Colors are 0-1 float
    r = rgb[0]
    g = rgb[1]
    b = rgb[2]

    r = math.pow((r + 0.055) / 1.055, 2.4) if (r > 0.04045) else r / 12.92
    g = math.pow((g + 0.055) / 1.055, 2.4) if (g > 0.04045) else g / 12.92
    b = math.pow((b + 0.055) / 1.055, 2.4) if (b > 0.04045) else b / 12.92

    x = (r * 0.4124 + g * 0.3576 + b * 0.1805) / 0.95047
    y = (r * 0.2126 + g * 0.7152 + b * 0.0722) / 1.00000
    z = (r * 0.0193 + g * 0.1192 + b * 0.9505) / 1.08883

    x = math.pow(x, 1.0 / 3.0) if (x > 0.008856) else (7.787 * x) + 16.0 / 116.0
    y = math.pow(y, 1.0 / 3.0) if (y > 0.008856) else (7.787 * y) + 16.0 / 116.0
    z = math.pow(z, 1.0 / 3.0) if (z > 0.008856) else (7.787 * z) + 16.0 / 116.0
    
    return [
        (116.0 * y) - 16.0,
        500.0 * (x - y),
        200.0 * (y - z)
    ]