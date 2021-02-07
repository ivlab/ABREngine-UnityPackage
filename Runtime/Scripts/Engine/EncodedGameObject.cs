/* EncodedGameObject.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>, Seth Johnson <sethalanjohnson@gmail.com>
 *
 */

using System;
using UnityEngine;

namespace IVLab.ABREngine
{
    public class EncodedGameObject : MonoBehaviour
    {
        [SerializeField]
        public string encodedWithUuid;
        private Guid _encodedWithUuid;

        public void SetUuid(Guid uuid)
        {
            _encodedWithUuid = uuid;
            encodedWithUuid = uuid.ToString();
        }
    }
}