# SendToABR.py
#
# Copyright (c) 2021, Texas Advanced Computing Center and University of
# Minnesota
#
# Authors: Greg Abram <gda@tacc.utexas.edu> and Bridger Herman
# <herma582@umn.edu>
#
## THIS PLUGIN USES A PACKAGED VERSION OF ABR_DATA_FORMAT AND MAY NOT BE UP TO DATE

import sys
import os
from paraview.util.vtkAlgorithm import *
plugin_folder = os.path.abspath(os.path.expanduser('~/EasyParaViewToABR/'))
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
        self.host  = 'localhost'
        self.port  = 1900
        self.logfile = ""

    def FillInputPortInformation(self, port, info):
        info.Set(self.INPUT_REQUIRED_DATA_TYPE(), "vtkDataSet")
        return 1

    @smproperty.stringvector(name="Host", default_values="localhost")
    def SetHost(self, value):
        self.host = value
        self.Modified()
        return

    @smproperty.intvector(name="Port", default_values=1900)
    def SetPort(self, value):
        self.port = value
        self.Modified()

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

    def Log(self, msg):
        import os
        if self.logfile == "":
            try:
                self.logfile = os.environ['SendToABRLog']
            except:
                self.logfile = "none"
                return
        if self.logfile != "none":
            f = open(self.logfile, "a+")
            f.write('{}\n'.format(msg))
            f.close()
        print(msg)
        return

    def RequestData(self, request, inInfoVec, outInfoVec):
        print("Attached Send to ABR")
        import sys, os
        import json
        import struct
        import socket
        from enum import Enum
        import numpy as np
        from vtk.numpy_interface import dataset_adapter as dsa
        import vtk
        from abr_data_format import ABRDataFormat, UnityMeshTopology, get_unity_topology

        from paraview import servermanager as sm
        from paraview.simple import GetActiveView

        self.Log("Starting Send to ABR")
        print("Starting Send to ABR")

        if 'UnitySyncer' not in dir(sm):
            self.Log("Installing Unity syncer")
            def callback(caller, *args):
                from struct import pack
                from paraview import servermanager as sm
                if 'UnityModified' not in dir(sm) or sm.UnityModified == 1:
                    sm.UnityModified = 0
                    import socket
                    sm.UnityFrame = sm.UnityFrame + 1
                    try:
                        s = socket.socket()
                        s.connect((self.host, self.port))
                    except:
                        sm.UnityLogger(sm.UnityLogfile, "Update callback could not connect to {}:{}".format(self.host, self.port))
                        return
                    else:
                        sm.UnityLogger(sm.UnityLogfile, "update")
                        update = 'update'.encode()
                        print('Sending update')
                        s.send(pack('>I', len(update)))
                        s.send(update)

                        ack_length = struct.unpack('>I', s.recv(4))[0]
                        ack = s.recv(ack_length)
                        print('Got ack of update')
                        sm.UnityLogger(sm.UnityLogfile, "Got ack of update")

                        ok_length = struct.unpack('>I', s.recv(4))[0]
                        ok = s.recv(ok_length)
                        print('Got ok of update')
                        sm.UnityLogger(sm.UnityLogfile, "Got ok of update")
            def UnityLogger(fname, msg):
                if fname != "none":
                    f = open(fname, "a")
                    f.write('{}\n'.format(msg))
                    f.close()
            try:
                sm.UnityLogger = UnityLogger
                sm.UnityLogfile = self.logfile
                sm.UnitySyncer = GetActiveView().AddObserver('EndEvent', callback, 1.0)
                sm.UnityFrame  = 0
            except Exception as e:
                self.Log("Error installing update manager")
                print(e)

        self.Log("Trying to connect to {}:{}".format(self.host, self.port))

        try:
            s = socket.socket(socket.AF_INET, socket.SOCK_STREAM, 0)
            s.connect((self.host, self.port))
        except:
            s = None

        if s == None:
            self.Log('Connection failed')
        else:
            self.Log('Connected')

        try:
            unstructured_grid = vtk.vtkUnstructuredGrid.GetData(inInfoVec[0], 0)
            vtk_data = unstructured_grid
            if unstructured_grid == None:
                poly_data = vtk.vtkPolyData.GetData(inInfoVec[0], 0)
                if poly_data == None:
                    image_data = vtk.vtkImageData.GetData(inInfoVec[0], 0)
                    if image_data == None:
                        raise ValueError("Can only handle ImageData, PolyData, and UnstructuredGrids")
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

        outpt = vtk.vtkUnstructuredGrid.GetData(outInfoVec, 0)
        outpt.ShallowCopy(formatted_data.vtk_data)

        if s != None:

            self.Log("Starting send of label `{}`".format(self.label))

            def snd(skt, bytes):
                offset = 0
                knt = len(bytes)
                while knt > 0:
                    n = skt.send(bytes[offset:])
                    knt = knt - n
                    offset = offset + n

            lab = self.label.encode()
            s.send(struct.pack('>i', len(lab)))
            s.send(lab)

            self.Log("Sent label message")

            stringified_json = json.dumps(formatted_data.json_header).encode()
            s.send(struct.pack('>I', len(stringified_json)))
            snd(s, stringified_json)

            self.Log("Sent Json header message")

            s.send(struct.pack('>I', formatted_data.bufsize))

            if (formatted_data.data_is_unstructured):
                snd(s, formatted_data.vertex_array.tobytes())
                self.Log("Sent vertex array")

            snd(s, formatted_data.cells.tobytes())

            self.Log("Sent cells")

            for i in range(len(formatted_data.json_header['scalarArrayNames'])):
                snd(s, formatted_data.scalar_arrays[i].tobytes())

            for i in range(len(formatted_data.json_header['vectorArrayNames'])):
                snd(s, formatted_data.vector_arrays[i].tobytes())

            self.Log("Finished send... waiting for ack")

            # Receive the length as an int
            length_bytes = s.recv(4)
            ack_length = struct.unpack('>I', length_bytes)[0]

            # Then receive the actual ack
            ack = s.recv(ack_length)
            ack = bytes(filter(lambda b: b != 0, ack))
            ack = ack.decode()

            self.Log("Got ack")

            sm.UnityModified = 1

            s.close()
        else:
            self.Log("No connection ... no send")


        self.Log('Done')
        return 1