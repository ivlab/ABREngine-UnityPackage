/* InputIndexerModule.cs
 *
 * Copyright (c) 2020-2021, University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>, Seth Johnson
 * <sethalanjohnson@gmail.com>
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace IVLab.ABREngine
{
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
            return GetInputField(inputIndex)?.FieldType.IsAssignableFrom(value.GetType()) ?? false;
        }

        public bool CanAssignInput(string inputName, IABRInput value)
        {
            return GetInputField(inputName)?.FieldType.IsAssignableFrom(value.GetType()) ?? false;
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