# ABRDataFormat.py
#
# Copyright (c) 2021, Texas Advanced Computing Center and University of
# Minnesota
#
# Authors: Greg Abram <gda@tacc.utexas.edu> and Bridger Herman
# <herma582@umn.edu>

import vtk
from enum import Enum
import numpy as np
from vtk.numpy_interface import dataset_adapter as dsa
import json

# https://docs.unity3d.com/ScriptReference/MeshTopology.html
# https://stackoverflow.com/a/51976841
class UnityMeshTopology(int, Enum):
    Triangles = 0,
    Quads = 2,
    Lines = 3,
    LineStrip = 4,
    Points = 5,
    Volume = 100,

VTK_TO_TOPOLOGY = {
    # Point data
    vtk.VTK_VERTEX: UnityMeshTopology.Points,
    vtk.VTK_POLY_VERTEX: UnityMeshTopology.Points,

    # Line data
    vtk.VTK_POLY_LINE: UnityMeshTopology.LineStrip,
    vtk.VTK_LINE: UnityMeshTopology.Lines,

    # Surface data
    vtk.VTK_QUAD: UnityMeshTopology.Quads,
    vtk.VTK_TRIANGLE: UnityMeshTopology.Triangles,

    # Volume data
    vtk.VTK_TETRA: UnityMeshTopology.Volume,
    vtk.VTK_VOXEL: UnityMeshTopology.Volume,
    vtk.VTK_HEXAHEDRON: UnityMeshTopology.Volume,
    vtk.VTK_WEDGE: UnityMeshTopology.Volume,
    vtk.VTK_PYRAMID: UnityMeshTopology.Volume,
    vtk.VTK_PENTAGONAL_PRISM: UnityMeshTopology.Volume,
    vtk.VTK_HEXAGONAL_PRISM: UnityMeshTopology.Volume,
}

def get_unity_topology(vtk_data_type):
    try:
        return VTK_TO_TOPOLOGY[vtk_data_type]
    except KeyError:
        return None

class ABRDataFormat:
    def __init__(self, vtk_data, label):
        self.json_header = None
        self.label = label
        self.bufsize = None
        self.vertex_array = None
        self.cells = None
        self.scalar_arrays = None
        self.vector_arrays = None
        self.vtk_data = vtk_data
        vtk_data_type = self.vtk_data.GetDataObjectType()
        self.data_is_unstructured = vtk_data_type == vtk.vtkUnstructuredGrid().GetDataObjectType() or vtk_data_type == vtk.vtkPolyData().GetDataObjectType()
        if not (self.data_is_unstructured or vtk_data_type == vtk.vtkImageData().GetDataObjectType()):
            raise ValueError("Unsupported vtk data type: " + vtk_data_type)

        num_points = self.vtk_data.GetNumberOfPoints()
        num_cells = self.vtk_data.GetNumberOfCells()
        dimensions = None

        if self.vtk_data.GetNumberOfCells() > 0:

            first_cell_type = self.vtk_data.GetCell(0).GetCellType()
            topology = VTK_TO_TOPOLOGY[first_cell_type]
            differences = [self.vtk_data.GetCell(i).GetCellType() for i in range(self.vtk_data.GetNumberOfCells()) if first_cell_type != self.vtk_data.GetCell(i).GetCellType()]
            if len(differences) > 0:
                print('WARNING (label {}): {} discrepancies found from first cell type!'.format(label, len(differences)))
                print('    Make sure all cells are of the same type.')
                print('    First cell is of type: {}'.format(first_cell_type))
                print('    First difference fromm first cell type: {}'.format(differences[0]))

            np_dataset = dsa.WrapDataObject(self.vtk_data)

            self.scalar_arrays = []
            self.vector_arrays = []

            scalar_mins = []
            scalar_maxes = []
            scalar_array_names = []
            vector_array_names = []

            point_data = np_dataset.PointData

            # quietly ignore any arrays that are not scalar or 3-vector

            for name, arr in zip(point_data.keys(), point_data):
                if len(arr.shape) == 1 or arr.shape[1] == 1:
                    arr = np.nan_to_num(arr).astype('f4')
                    scalar_array_names.append(name)
                    self.scalar_arrays.append(arr)
                    scalar_mins.append(float(np.amin(arr)))
                    scalar_maxes.append(float(np.max(arr)))
                elif len(arr.shape) == 2 and arr.shape[1] == 3:
                    arr = np.nan_to_num(arr).astype('f4')
                    self.vector_arrays.append(arr)
                    vector_array_names.append(name)

            # Flip the z component of vector assuming it's a 3-vec. This also is based on
            # the assumption that a 3-vec represents something spatial, and that Paraview
            # is right-handed and Unity is left-handed. Also flip z for scalars if data is
            # volumetric. Also convert NANs and create list of dicts
            if (self.data_is_unstructured):
                self.vertex_array = np.nan_to_num(np_dataset.Points * [1, 1, -1]).astype('f4')
            else:
                dimensions = self.vtk_data.GetDimensions()
                for i in range(len(self.scalar_arrays)):
                    self.scalar_arrays[i] = np.nan_to_num(np.flip(self.scalar_arrays[i].reshape(dimensions[2], dimensions[1], dimensions[0]), 0).flatten())

            if (topology == UnityMeshTopology.Lines) or (topology == UnityMeshTopology.Triangles)or (topology == UnityMeshTopology.Quads) or (topology == UnityMeshTopology.LineStrip):
                k = 1
            else:
                k = 0

            if k == 0:
                cells = np.column_stack(([1]*np_dataset.GetNumberOfPoints(), np.arange(np_dataset.GetNumberOfPoints()))).flatten()
                num_cells = np_dataset.GetNumberOfPoints()
            else:
                cells = np_dataset.Cells

            self.cells = cells.astype('i4')

            b = np.array(np_dataset.VTKObject.GetBounds())
            c = ((b[[1,3,5]] + b[[0,2,4]]) / 2.0).tolist()
            e = ((b[[1,3,5]] - b[[0,2,4]]) / 2.0).tolist()
            bounds = {
                # For Unity
                'm_Center': {'x': c[0], 'y': c[1], 'z': -c[2]},
                'm_Extent': {'x': e[0], 'y': e[1], 'z': e[2]},
                # For Newtonsoft
                'center': {'x': c[0], 'y': c[1], 'z': -c[2]},
                'extents': {'x': e[0], 'y': e[1], 'z': e[2]},
            }

            data = {
                'meshTopology': int(topology),
                'num_points': num_points,
                'num_cells': num_cells,
                'num_cell_indices': cells.size,
                'scalarArrayNames': scalar_array_names,
                'vectorArrayNames': vector_array_names,
                'bounds': bounds,
                'dimensions': dimensions,
                'scalarMaxes': scalar_maxes,
                'scalarMins': scalar_mins
            }

            self.json_header = data

            # Get total size of data block
            bufsize = 0

            # space for points
            if (self.data_is_unstructured):
                bufsize = bufsize + 4*(3*num_points)

            # add space for point-dep variables
            bufsize = bufsize + 4*((len(scalar_array_names) + 3*len(vector_array_names)) * num_points)

            # add space for indices
            self.bufsize = bufsize + 4*cells.size
        else:
            raise ValueError("Unstructured grid contains no cells")

    def get_data_bytes(self):
        final_bytes = bytes()
        if (self.data_is_unstructured):
            final_bytes += self.vertex_array.tobytes()
        final_bytes += self.cells.tobytes()
        for i in range(len(self.json_header['scalarArrayNames'])):
            final_bytes += self.scalar_arrays[i].tobytes()

        for i in range(len(self.json_header['vectorArrayNames'])):
            final_bytes += self.vector_arrays[i].tobytes()
        return final_bytes