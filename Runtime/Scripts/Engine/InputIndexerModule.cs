/* InputIndexerModule.cs
 *
 * Copyright (c) 2020-2021, University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>, Seth Johnson
 * <sethalanjohnson@gmail.com>
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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace IVLab.ABREngine
{
    public static class TypeExtentions
    {
        /// <summary>
        /// Check if a type implicitly converts to another.
        /// Source: https://stackoverflow.com/a/2075975
        /// </summary>
        public static bool ImplicitlyConvertsTo(this Type type, Type destinationType)
        {
            if (type == destinationType)
                return true;

            return (from method in type.GetMethods(BindingFlags.Static |
                                                BindingFlags.Public)
                    where method.Name == "op_Implicit" &&
                        method.ReturnType == destinationType
                    select method
                    ).Count() > 0;
        }
    }

    /// <summary>
    /// Convenience class to avoid having to repeatedly manage reflection when
    /// adjusting ABR inputs to Data Impressions.
    /// </summary>
    public class ABRInputIndexerModule
    {
        DataImpression targetObject = null;

        public ABRInputIndexerModule(DataImpression target)
        {
            this.targetObject = target;
        }

        FieldInfo[] _abrInputFields = null;
        Dictionary<string, int> _abrInputIndicesByName = null;
        string[] _abrInputNames = null;


        static BindingFlags bindingFlags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
        protected static FieldInfo[] GetABRInputFields(object target)
        {
            return target.GetType().GetFields(bindingFlags).Where(field => field.GetCustomAttribute<ABRInputAttribute>() != null).ToArray();
        }

        protected static string[] CatalogABRInputNames(FieldInfo[] abrInputs)
        {
            string[] inputNames = new string[abrInputs.Length];
            for (int i = 0; i < abrInputs.Length; i++)
            {
                inputNames[i] = abrInputs[i].GetCustomAttribute<ABRInputAttribute>().inputName;
            }
            return inputNames;
        }
        protected static Dictionary<string, int> IndexABRInputsByName(FieldInfo[] abrInputs)
        {
            Dictionary<string, int> abrInputIndicesByName = new Dictionary<string, int>();

            for (int i = 0; i < abrInputs.Length; i++)
            {
                abrInputIndicesByName[abrInputs[i].GetCustomAttribute<ABRInputAttribute>().inputName] = i;
            }

            return abrInputIndicesByName;
        }


        protected FieldInfo[] ABRInputFields
        {
            get { if (_abrInputFields == null) _abrInputFields = GetABRInputFields(targetObject); return _abrInputFields; }
        }

        protected Dictionary<string, int> ABRInputIndicesByName
        {
            get { if (_abrInputIndicesByName == null) _abrInputIndicesByName = IndexABRInputsByName(ABRInputFields); return _abrInputIndicesByName; }
        }

        public FieldInfo GetInputField(int inputIndex)
        {
            if (inputIndex >= 0)
                return ABRInputFields[inputIndex];
            else
                return null;
        }
        public FieldInfo GetInputField(string inputName)
        {
            int index = GetInputIndex(inputName);
            return GetInputField(index);
        }

        protected void AssignInput(FieldInfo inputField, IABRInput value)
        {
            if (inputField == null) return;
            IABRInput oldValue = inputField?.GetValue(targetObject) as IABRInput;
            inputField?.SetValue(targetObject, value);
        }


        public string[] InputNames
        {
            get
            {
                if (_abrInputNames == null)
                {
                    _abrInputNames = CatalogABRInputNames(ABRInputFields);
                }
                return _abrInputNames;
            }
        }

        public int InputCount
        {
            get { return InputNames.Length; }
        }

        public string GetInputName(int inputIndex)
        {
            return InputNames[inputIndex];
        }


        public int GetInputIndex(string inputName)
        {
            int index;
            if (ABRInputIndicesByName.TryGetValue(inputName, out index) == false) index = -1;

            return index;
        }


        public IABRInput GetInputValue(int inputIndex)
        {
            return GetInputField(inputIndex)?.GetValue(targetObject) as IABRInput;
        }

        public IABRInput GetInputValue(string inputName)
        {
            return GetInputField(inputName)?.GetValue(targetObject) as IABRInput;
        }


        public Type GetInputType(int inputIndex)
        {
            var field = GetInputField(inputIndex);
            var fieldType = field?.FieldType;
            return fieldType;
        }

        public Type GetInputType(string inputName)
        {
            return GetInputField(inputName)?.FieldType;
        }

        public bool CanAssignInput(int inputIndex, IABRInput value)
        {
            Type fieldType = GetInputField(inputIndex)?.FieldType;
            return fieldType != null && (fieldType.IsAssignableFrom(value.GetType()) || fieldType.ImplicitlyConvertsTo(value.GetType()));
        }

        public bool CanAssignInput(string inputName, IABRInput value)
        {
            return CanAssignInput(GetInputIndex(inputName), value);
        }


        public void AssignInput(int inputIndex, IABRInput value)
        {
            //ABRManager.Instance.RegisterInputAssignment(targetObject, inputIndex);
            AssignInput(GetInputField(inputIndex), value);
        }


        public void AssignInput(string inputName, IABRInput value)
        {
            AssignInput(GetInputIndex(inputName), value);
        }

    }
}