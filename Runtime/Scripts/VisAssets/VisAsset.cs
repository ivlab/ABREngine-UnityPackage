/* VisAsset.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Author: Bridger Herman <herma582@umn.edu>
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

using System;
using System.Collections.Generic;
using System.Linq;

namespace IVLab.ABREngine
{
    public interface IVisAsset : IABRInput
    {
        /// <summary>
        /// Globally-unique identifier for this VisAsset
        /// </summary>
        Guid Uuid { get; set; }

        /// <summary>
        /// (currently unused) The time that this VisAsset was imported into ABR
        /// </summary>
        DateTime ImportTime { get; set;}

        /// <summary>
        /// How many VisAssets are in the gradient? (1 if it's not a gradient)
        /// </summary>
        int VisAssetCount { get; }
    }

    /// <summary>
    /// A VisAsset gradient described by a texture (or, series of textures)
    /// </summary>
    public interface ITextureGradient
    {
        /// <summary>
        /// Internal calculations for blend maps used for rendering
        /// </summary>
        GradientBlendMap BlendMaps { get; }
    }

    /// <summary>
    /// Generic type for all VisAssets to inherit from
    /// </summary>
    public class VisAsset
    {
        public Guid Uuid { get; set; }
        public ABRInputGenre Genre { get; } = ABRInputGenre.VisAsset;
        public DateTime ImportTime { get; set; }

        /// <summary>
        /// Typemap where we can look up ABR visasset types and convert to C#
        /// types. The typestring that defines this VisAsset type, from the <a
        /// href="https://github.com/ivlab/abr-schema/blob/feat/gradients/ABRSchema_0-2-0.json">ABR
        /// Schema</a>.
        /// Should match one of: #/definitions/VisAssetType
        /// </summary>
        public static Dictionary<string, Type> VisAssetTypeMap = new Dictionary<string, Type>()
        {
            { "colormap", typeof(ColormapVisAsset) },
            { "glyph", typeof(GlyphVisAsset) },
            { "line", typeof(LineTextureVisAsset) },
            { "texture", typeof(SurfaceTextureVisAsset) },
        };

        public RawABRInput GetRawABRInput()
        {
            return new RawABRInput {
                inputType = this.GetType().ToString(),
                inputValue = this.Uuid.ToString(),
                parameterName = "",// TODO
                inputGenre = Genre.ToString("G"),
            };
        }

        /// <summary>
        /// Check if a ABR VisAsset schema type is valid with this system
        /// </summary>
        public static bool IsValidVisAssetType(string vaType) => VisAssetTypeMap.Keys.Contains(vaType);
    }

    /// <summary>
    /// A gradient consisting of VisAssets of any type. NOTE: Texture-based
    /// gradients (Surface/Line textures and colormaps) must have 4 or fewer
    /// elements.
    /// </summary>
    public interface IVisAssetGradient<T> : IVisAsset
    where T : IVisAsset
    {
        /// <summary>
        /// List of all VisAssets inside this gradient
        /// </summary>
        List<T> VisAssets { get; }

        /// <summary>
        /// List of gradient stops (length of VisAssets - 1)
        /// </summary>
        List<float> Stops { get; }

        /// <summary>
        /// Initialize this gradient with a UUID, some VisAssets, and some
        /// Stops. This is used instead of a constructor because it is much more
        /// flexible.
        /// </summary>
        void Initialize(Guid uuid, List<T> visAssets, List<float> stops);
    }

    /// <summary>
    /// Concrete implementation of a VisAsset Gradient, which is a collection of
    /// VisAssets that also counts as a single VisAsset.
    /// </summary>
    public class VisAssetGradient : VisAsset
    {
        /// <summary>
        /// Convert from a <see cref="RawVisAssetGradient"> to a regular gradient for internal use.
        /// </summary>
        public static T FromRaw<T, S>(RawVisAssetGradient raw)
        where T : IVisAssetGradient<S>, new()
        where S : IVisAsset
        {
            Type gradType = VisAsset.VisAssetTypeMap.FirstOrDefault((kv) => kv.Key == raw.gradientType).Value;
            if (gradType != typeof(S))
            {
                throw new ArgumentException("VisAssetGradient: incoming raw gradient type does not match desired output type");
            }
            List<S> visAssets = raw.visAssets.Select((vaUuid) =>
            {
                IVisAsset va;
                if (ABREngine.Instance.VisAssets.TryGetVisAsset(new Guid(vaUuid), out va))
                    return (S) va;
                return default;
            }).ToList();
            T gradient = new T();
            gradient.Initialize(new Guid(raw.uuid), visAssets, raw.points.ToList());
            return gradient;
        }
    }

    /// <summary>
    /// Serializable version of the VisAsset gradients that interacts with
    /// state/schema.
    /// Each VisAsset type should be responsible for implementing their own
    /// conversion to/from this.
    /// </summary>
    public class RawVisAssetGradient
    {
        public string uuid;
        public string gradientScale = "continuous";
        public string gradientType;
        public float[] points;
        public string[] visAssets;

        /// <summary>
        /// Convert from a regular <see cref="VisAssetGradient"> to raw gradient
        /// for serialization/import/export/interfacing with other apps.
        /// </summary>
        public static RawVisAssetGradient ToRaw<T>(IVisAssetGradient<T> gradient)
        where T : IVisAsset
        {
            RawVisAssetGradient grad = new RawVisAssetGradient();
            grad.uuid = gradient.Uuid.ToString();
            grad.gradientType = VisAsset.VisAssetTypeMap.FirstOrDefault((kv) => kv.Value == typeof(T)).Key;
            grad.points = gradient.Stops.ToArray();
            grad.visAssets = gradient.VisAssets.Select((va) => va.Uuid.ToString()).ToArray();
            return grad;
        }
    }
}