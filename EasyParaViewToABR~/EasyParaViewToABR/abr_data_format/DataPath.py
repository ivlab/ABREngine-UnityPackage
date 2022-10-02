'''
        A path to a data source, be it a KeyData object, a Scalar Variable,
        a Vector Variable, or something else.
    
        Should take the form of Organization/DatasetName/*

        Example: TACC/GulfOfMexico/KeyData/bathymetry

        Example: TACC/GulfOfMexico/ScalarVar/temperature
'''

# DataPath.py
#
# Copyright (c) 2021, University of Minnesota
#
# Authors: Bridger Herman <herma582@umn.edu>
#
# Reimplementation of the DataPath class found in the ABREngine-UnityPackage

from enum import Enum

LOOKUP = {
    0: 'ScalarVar',
    1: 'VectorVar',
    2: 'KeyData',
    3: 'Dataset',
}

class DataPath:
    class DataPathType(int, Enum):
        ScalarVar = 0,
        VectorVar = 1,
        KeyData = 2,
        Dataset = 3,

        def __str__(self):
            # full_str = super().__str__()
            # return full_str[full_str.find('.') + 1:]
            full_str = LOOKUP[self]
            return full_str

    separator = '/'

    @staticmethod
    def make_path(org_name, dataset_name, data_type, data_name):
        full_data_path = DataPath.join(org_name, dataset_name)
        full_data_path = DataPath.join(full_data_path, str(data_type))
        full_data_path = DataPath.join(full_data_path, data_name)
        return full_data_path

    @staticmethod
    def get_path_parts(data_path):
        return data_path.split(DataPath.separator)
    @staticmethod
    def join(path1, path2):
        if path1.endswith(DataPath.separator):
            return str(path1) + str(path2)
        else:
            return str(path1) + DataPath.separator + str(path2)
    @staticmethod
    def follows_convention(data_path, path_type):
        parts = DataPath.get_path_parts(data_path)
        if path_type != DataPath.DataPathType.Dataset:
            return len(parts) == 4 and parts[2] == str(path_type)
        else:
            return len(parts) == 2
    @staticmethod
    def get_convention(path_type):
        if path_type != DataPath.DataPathType.Dataset:
            return 'Organization/Dataset/{0}/Name'.format(path_type)
        else:
            return 'Organization/Dataset'
    @staticmethod
    def get_organization(data_path):
        return DataPath.get_path_parts(data_path)[0]
    @staticmethod
    def get_dataset(data_path):
        return DataPath.get_path_parts(data_path)[1]
    @staticmethod
    def get_path_type(data_path):
        return DataPath.get_path_parts(data_path)[2]
    @staticmethod
    def get_name(data_path):
        return DataPath.get_path_parts(data_path)[3]
    @staticmethod
    def get_organization_path(data_path):
        return DataPath.get_path_parts(data_path)[0]
    @staticmethod
    def get_dataset_path(data_path):
        return DataPath.join(DataPath.get_organization_path(data_path), DataPath.get_path_parts(data_path)[1])
    @staticmethod
    def get_path_type_path(data_path):
        return DataPath.join(DataPath.get_dataset_path(data_path), DataPath.get_path_parts(data_path)[2])
    @staticmethod
    def get_name_path(data_path):
        return DataPath.join(DataPath.get_path_type_path(data_path), DataPath.get_path_parts(data_path)[3])