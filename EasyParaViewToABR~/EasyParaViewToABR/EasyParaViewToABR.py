# SendToABR.py
#
# Copyright (c) 2021, Texas Advanced Computing Center and University of
# Minnesota
#
# Authors: Greg Abram <gda@tacc.utexas.edu> and Bridger Herman
# <herma582@umn.edu>
#

import sys
import os
from paraview.util.vtkAlgorithm import *
# plugin_folder = os.path.abspath(os.path.expanduser('~/EasyParaViewToABR/'))
plugin_folder = os.path.abspath(os.path.expanduser('C:/Users/scoot/dev/ABRStyleDependenciesExample/Packages/ABREngine-UnityPackage/EasyParaViewToABR~/EasyParaViewToABR'))
sys.path.append(plugin_folder)
from abr_data_format import DataPath

@smproxy.filter()

@smproperty.input(name="InputDataset", port_index=0)
@smdomain.datatype(dataTypes=["vtkDataSet"], composite_data_supported=False)

class EasyParaViewToABR(VTKPythonAlgorithmBase):
    def __init__(self):
        VTKPythonAlgorithmBase.__init__(self, nInputPorts=1, nOutputPorts=1, outputType="vtkUnstructuredGrid")
        self.dataset = 'Dataset'
        self.organization = 'Organization'
        self.key_data_name = 'KeyDataName'
        self.host  = 'http://localhost:8000/api/import-keydata'
        self.logfile = ""

    @property
    def data_path(self) -> str:
        return '/'.join([self.organization, self.dataset, 'KeyData', self.key_data_name])

    @property
    def abr_import_url(self) -> str:
        return self.host + '/' + self.data_path

    def FillInputPortInformation(self, port, info):
        info.Set(self.INPUT_REQUIRED_DATA_TYPE(), "vtkDataSet")
        return 1

    # TODO: these show in a bizarre order, irrespective of placement in this
    # file or their names.

    @smproperty.stringvector(name="Host", default_values="http://localhost:8000/api/import-keydata")
    def SetHost(self, value):
        self.host = value
        self.Modified()
        return

    @smproperty.stringvector(name="1* Organization", default_values="Organization")
    def SetOrganization(self, value):
        self.organization = value
        self.Modified()
        return

    @smproperty.stringvector(name="2* Dataset", default_values="Dataset")
    def SetDataset(self, value):
        self.dataset = value
        self.Modified()
        return

    @smproperty.stringvector(name="3* Key Data Name", default_values="KeyDataName")
    def SetKeyDataName(self, value):
        self.key_data_name = value
        self.Modified()
        return

    @property
    def label(self):
        path = DataPath.make_path(self.organization, self.dataset, 'KeyData', self.key_data_name)
        return path

    def RequestData(self, request, inInfoVec, outInfoVec):
        print("EasyParaViewToABR: Sending Data to ABR Server")
        import vtk
        from abr_data_format import ABRDataFormat
        import requests

        from paraview import servermanager as sm

        print('Key Data:', self.data_path)
        print('Host:', self.host)

        # get data from ParaView
        try:
            unstructured_grid = vtk.vtkUnstructuredGrid.GetData(inInfoVec[0], 0)
            vtk_data = unstructured_grid
            if unstructured_grid == None:
                poly_data = vtk.vtkPolyData.GetData(inInfoVec[0], 0)
                if poly_data == None:
                    image_data = vtk.vtkImageData.GetData(inInfoVec[0], 0)
                    if image_data == None:
                        raise ValueError("Can only handle ImageData, PolyData, and UnstructuredGrids. Please convert to one of these formats first.")
                    else:
                        vtk_data = image_data
                else:
                    af = vtk.vtkAppendFilter()
                    af.SetInputData(poly_data)
                    af.Update()
                    vtk_data = af.GetOutput()
                    del af
            formatted_data = ABRDataFormat(vtk_data, self.label)
        except ValueError as e:
            print(e)
            return 0

        # give ParaView something to display at the end of this filter?
        outpt = vtk.vtkUnstructuredGrid.GetData(outInfoVec, 0)
        outpt.ShallowCopy(formatted_data.vtk_data)

        # Send JSON to server to be saved
        resp = requests.post(
            self.abr_import_url,
            json=formatted_data.json_header
        )

        if resp.ok:
            print('Successfully sent JSON header, data path on server is ' + resp.text)
        else:
            print('Error sending data header (code {0})'.format(resp.status_code), resp.reason)
            return 0

        # then, send binary data to the server to be saved
        resp = requests.post(
            self.abr_import_url,
            headers={
                'Content-Type': 'octet-stream'
            },
            data=formatted_data.get_data_bytes()
        )

        if resp.ok:
            print('Successfully sent binary data, data path on server is ' + resp.text)
        else:
            print('Error sending binary data (code {0})'.format(resp.status_code), resp.reason)
            return 0

        print('EasyParaViewToABR: Done')
        return 1