/* KeyData.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>, Seth Johnson <sethalanjohnson@gmail.com>
 *
 */

namespace IVLab.ABREngine
{
    public interface IKeyData
    {
        /// <summary>
        ///     The DataPath that represents this KeyData
        /// </summary>
        string Path { get; }
    }

    public class SurfaceKeyData : IKeyData
    {
        public string Path { get; }
    }

    public class PointKeyData : IKeyData
    {
        public string Path { get; }
    }

    public class LineKeyData : IKeyData
    {
        public string Path { get; }
    }
}