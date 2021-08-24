/* VolumeLightManager.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Author: Matthias Broske <brosk014@umn.edu>
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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VolumeLightManager : MonoBehaviour
{
    private Material volumeMaterial;
    private Vector4[] lightViewSpaceDirections = new Vector4[3];
    private float[] lightIntensities = new float[3];

    private void Start()
    {
        volumeMaterial = Resources.Load<Material>("ABR_DataVolume");
    }

    // Update is called once per frame
    void Update()
    {
        int lightCount = 0;
        foreach (Transform lightTransform in transform)
        {
            lightViewSpaceDirections[lightCount] = Camera.main.worldToCameraMatrix.MultiplyVector(lightTransform.rotation * Vector3.forward);
            lightIntensities[lightCount] = lightTransform.gameObject.GetComponent<Light>().intensity;
            lightCount++;
        }

        volumeMaterial.SetInt("_LightCount", lightCount);
        volumeMaterial.SetVectorArray("_ViewSpaceLightDirections", lightViewSpaceDirections);
        volumeMaterial.SetFloatArray("_LightIntensities", lightIntensities);
    }
}
