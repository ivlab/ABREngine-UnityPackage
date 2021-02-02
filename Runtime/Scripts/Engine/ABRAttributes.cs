/* ABRAttributes.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>, Seth Johnson <sethalanjohnson@gmail.com>
 *
 */

namespace IVLab.ABREngine
{
    /// <summary>
    ///     Input attribute used for annotating an ABR input to a data
    ///     impression (VisAsset, DataVariable, etc.)
    /// </summary>
    public class ABRInputAttribute : System.Attribute
    {
        public string inputName;
        public string parameterName;
        public ABRInputAttribute(string inputName, string parameterName)
        {
            this.inputName = inputName;
            this.parameterName = parameterName;
        }
    }

    /// <summary>
    ///     Attribute to match up this class with the string plate name from the
    ///     ABR Schema
    /// </summary>
    public class ABRPlateType : System.Attribute
    {
        public string plateType;
        public ABRPlateType(string plateType)
        {
            this.plateType = plateType;
        }
    }
}