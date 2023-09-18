/* color.js
 *
 * Collection of Color Utilities
 *
 * Copyright (C) 2019-2021, University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>, Daniel F. Keefe <dfk@umn.edu>
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

// TODO: This is probably not the right size but seems to work for the design UI right now...
var $tmpCanvas = $('<canvas>', {
    width: 300,
    height: 150,
});


// Stolen from ColorLoom:
// https://github.umn.edu/ABRAM308/SculptingVisWebApp/blob/master/applets/static/color-loom/sketch.js
export class ColorMap {
    constructor() {
        // List of tuples:
        // [0.01, {r: 0, g: 0, b: 0}]
        // RGB are in 0-1 float range
        this.entries = [];
    }

    static fromXML(xml) {
        let colormap = new ColorMap();
        let $colorXML = $($.parseXML(xml));
        let $colors = $colorXML.find('Point');
        $colors.each((_i, el) => {
            let r = parseFloat($(el).attr('r'));
            let g = parseFloat($(el).attr('g'));
            let b = parseFloat($(el).attr('b'));
            let percentage = parseFloat($(el).attr('x'));
            colormap.addControlPt(percentage, {r: r, g: g, b: b});
        });
        return colormap;
    }

    toXML() {
        // Serialize the colormap to XML
        let $xmlContainer = $('<div>');
        let $colorMap = $('<ColorMap>', {
                attr: {
                    space: "CIELAB",
                    indexedLookup: "false",
                    name: "ColorLoom",
                },
            })

        // Using the colormap.xml format
        this.entries.forEach((c) => {
            let perc = c[0];
            let color = c[1];
            $colorMap.append(
                $('<Point>', {
                    attr: {
                        r: color.r,
                        g: color.g,
                        b: color.b,
                        x: perc,
                    },
                })
            )
        });

        let $xmlOutput = $('<ColorMaps>').append($colorMap);

        $xmlContainer.append($xmlOutput);
        let xmlString = $xmlOutput.get(0).outerHTML;

        // Fix the casing in the tags (jQuery lowercases everything)
        xmlString = xmlString.replace(/point/g, 'Point');
        xmlString = xmlString.replace(/colormap/g, 'ColorMap');
        return xmlString;
    }

    toBase64(url=false) {
        let ctx = $tmpCanvas.get(0).getContext('2d');
        this.toCanvas(ctx);

        let thumbnail = $tmpCanvas.get(0).toDataURL('image/png');
        if (!url) {
            let firstComma = thumbnail.indexOf(',');
            return thumbnail.slice(firstComma + 1);
        } else {
            return thumbnail;
        }
    }

    toCanvas(ctx) {
        let mapWidth = $(ctx.canvas).width();
        let mapHeight = $(ctx.canvas).height();
        // Draw a bunch of tiny rectangles with properly interpolated CIE Lab color
        for (let x = 0; x < mapWidth; x++) {
            let percentage = x / mapWidth;
            let color = this.lookupColor(percentage);
            ctx.fillStyle = floatToHex(color);
            ctx.fillRect(x, 0, 1, mapHeight);
        }
    }

    flip() {
        for (let i = 0; i < this.entries.length; i++) {
            this.entries[i][0] = 1.0 - this.entries[i][0];
        }
        this.entries.sort((a, b) => a[0] - b[0]);
    }

    addControlPt(dataVal, col) {
        var entry = [dataVal, col];
        this.entries.push(entry);
        this.entries.sort(function (a, b) { return a[0] - b[0]; });
    }

    editControlPt(origDataVal, newDataVal, newColor) {
        var i = 0;
        while (i < this.entries.length) {
            if (this.entries[i][0] == origDataVal) {
                this.entries[i][0] = newDataVal;
                this.entries[i][1] = newColor;
                this.entries.sort(function (a, b) { return a[0] - b[0]; });
                return;
            }
            i++;
        }
        console.log("ColorMap::editControlPt no control point with data val = " + dataVal);
    }

    removeControlPt(dataVal) {
        var i = 0;
        while (i < this.entries.length) {
            if (entries[i][0] == dataVal) {
                this.entries.splice(i, 1);
                return;
            }
            i++;
        }
        console.log("ColorMap::removeControlPt no control point with data val = " + dataVal);
    }

    lookupColor(dataVal) {
        if (this.entries.length == 0) {
            //console.log("ColorMap::lookupColor empty color map.");
            // return color(255);
            return {r: 1.0, g: 1.0, b: 1.0};
        }
        else if (this.entries.length == 1) {
            return this.entries[0][1];
        }
        else {
            var minVal = this.entries[0][0];
            var maxVal = this.entries[this.entries.length - 1][0];

            // check bounds
            if (dataVal >= maxVal) {
                return this.entries[this.entries.length - 1][1];
            }
            else if (dataVal <= minVal) {
                return this.entries[0][1];
            }
            else {  // value within bounds

                // make i = upper control pt and (i-1) = lower control point
                var i = 1;
                while (this.entries[i][0] < dataVal) {
                    i++;
                }

                // convert the two control points to lab space, interpolate
                // in lab space, then convert back to rgb space
                var c1 = this.entries[i - 1][1];
                var rgb1 = [c1.r, c1.g, c1.b];
                var lab1 = rgb2lab(rgb1);

                var c2 = this.entries[i][1];
                var rgb2 = [c2.r, c2.g, c2.b];
                var lab2 = rgb2lab(rgb2);

                var v1 = this.entries[i - 1][0];
                var v2 = this.entries[i][0];
                var alpha = (dataVal - v1) / (v2 - v1);

                var labFinal = [
                    lab1[0] * (1.0 - alpha) + lab2[0] * alpha,
                    lab1[1] * (1.0 - alpha) + lab2[1] * alpha,
                    lab1[2] * (1.0 - alpha) + lab2[2] * alpha
                ];

                var rgbFinal = lab2rgb(labFinal);
                return {r: rgbFinal[0], g: rgbFinal[1], b: rgbFinal[2]};
            }
        }
    }
}

// Converts a hex color into a floating point representation
export function hexToFloat(hexColor) {
    let rgb = hexToRgb(hexColor);
    return {
        r: rgb.r / 255.0,
        g: rgb.g / 255.0,
        b: rgb.b / 255.0,
    };
}

export function floatToHex(floatRgb) {
    return rgbToHex({
        r: parseInt(floatRgb.r * 255.0),
        g: parseInt(floatRgb.g * 255.0),
        b: parseInt(floatRgb.b * 255.0),
    })
}

// From https://stackoverflow.com/questions/5623838/rgb-to-hex-and-hex-to-rgb
export function hexToRgb(hex) {
    var result = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex);
    return result ? {
        r: parseInt(result[1], 16),
        g: parseInt(result[2], 16),
        b: parseInt(result[3], 16)
    } : null;
}

export function rgbToHex(rgb) {
    return "#" + ((1 << 24) + (rgb.r << 16) + (rgb.g << 8) + rgb.b).toString(16).slice(1);
}

// ----- BEGIN EXTERNAL CODE FOR RGB-LAB CONVERSION -----
// https://github.com/antimatter15/rgb-lab

/*
MIT License
Copyright (c) 2014 Kevin Kwok <antimatter15@gmail.com>
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

// the following functions are based off of the pseudocode
// found on www.easyrgb.com

function lab2rgb(lab) {
    var y = (lab[0] + 16.0) / 116.0,
        x = lab[1] / 500.0 + y,
        z = y - lab[2] / 200.0,
        r, g, b;

    x = 0.95047 * ((x * x * x > 0.008856) ? x * x * x : (x - 16.0 / 116.0) / 7.787);
    y = 1.00000 * ((y * y * y > 0.008856) ? y * y * y : (y - 16.0 / 116.0) / 7.787);
    z = 1.08883 * ((z * z * z > 0.008856) ? z * z * z : (z - 16.0 / 116.0) / 7.787);

    r = x * 3.2406 + y * -1.5372 + z * -0.4986;
    g = x * -0.9689 + y * 1.8758 + z * 0.0415;
    b = x * 0.0557 + y * -0.2040 + z * 1.0570;

    r = (r > 0.0031308) ? (1.055 * Math.pow(r, 1 / 2.4) - 0.055) : 12.92 * r;
    g = (g > 0.0031308) ? (1.055 * Math.pow(g, 1 / 2.4) - 0.055) : 12.92 * g;
    b = (b > 0.0031308) ? (1.055 * Math.pow(b, 1 / 2.4) - 0.055) : 12.92 * b;

    return [Math.max(0, Math.min(1, r)),
    Math.max(0, Math.min(1, g)),
    Math.max(0, Math.min(1, b))]
}


function rgb2lab(rgb) {
    var r = rgb[0],
        g = rgb[1],
        b = rgb[2],
        x, y, z;

    r = (r > 0.04045) ? Math.pow((r + 0.055) / 1.055, 2.4) : r / 12.92;
    g = (g > 0.04045) ? Math.pow((g + 0.055) / 1.055, 2.4) : g / 12.92;
    b = (b > 0.04045) ? Math.pow((b + 0.055) / 1.055, 2.4) : b / 12.92;

    x = (r * 0.4124 + g * 0.3576 + b * 0.1805) / 0.95047;
    y = (r * 0.2126 + g * 0.7152 + b * 0.0722) / 1.00000;
    z = (r * 0.0193 + g * 0.1192 + b * 0.9505) / 1.08883;

    x = (x > 0.008856) ? Math.pow(x, 1.0 / 3.0) : (7.787 * x) + 16.0 / 116.0;
    y = (y > 0.008856) ? Math.pow(y, 1.0 / 3.0) : (7.787 * y) + 16.0 / 116.0;
    z = (z > 0.008856) ? Math.pow(z, 1.0 / 3.0) : (7.787 * z) + 16.0 / 116.0;

    return [(116.0 * y) - 16.0, 500.0 * (x - y), 200.0 * (y - z)]
}

// calculate the perceptual distance between colors in CIELAB
// https://github.com/THEjoezack/ColorMine/blob/master/ColorMine/ColorSpaces/Comparisons/Cie94Comparison.cs

function deltaE(labA, labB) {
    var deltaL = labA[0] - labB[0];
    var deltaA = labA[1] - labB[1];
    var deltaB = labA[2] - labB[2];
    var c1 = Math.sqrt(labA[1] * labA[1] + labA[2] * labA[2]);
    var c2 = Math.sqrt(labB[1] * labB[1] + labB[2] * labB[2]);
    var deltaC = c1 - c2;
    var deltaH = deltaA * deltaA + deltaB * deltaB - deltaC * deltaC;
    deltaH = deltaH < 0 ? 0 : Math.sqrt(deltaH);
    var sc = 1.0 + 0.045 * c1;
    var sh = 1.0 + 0.015 * c1;
    var deltaLKlsl = deltaL / (1.0);
    var deltaCkcsc = deltaC / (sc);
    var deltaHkhsh = deltaH / (sh);
    var i = deltaLKlsl * deltaLKlsl + deltaCkcsc * deltaCkcsc + deltaHkhsh * deltaHkhsh;
    return i < 0 ? 0 : Math.sqrt(i);
}
// ----- END EXTERNAL CODE FOR RGB-LAB CONVERSION -----