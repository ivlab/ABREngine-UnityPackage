# Copyright (C) 2021, University of Minnesota
# Authors: Bridger Herman <herma582@umn.edu>

# This program is free software: you can redistribute it and/or modify
# it under the terms of the GNU General Public License as published by
# the Free Software Foundation, either version 3 of the License, or
# (at your option) any later version.

# This program is distributed in the hope that it will be useful,
# but WITHOUT ANY WARRANTY; without even the implied warranty of
# MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
# GNU General Public License for more details.

# You should have received a copy of the GNU General Public License
# along with this program.  If not, see <https://www.gnu.org/licenses/>.

import os
import json
import fnmatch
import numpy as np
from django.shortcuts import redirect
from django.views.decorators.csrf import csrf_exempt
from django.http import HttpResponse, JsonResponse

from django.conf import settings

from abr_server.state import state
from abr_server.notifier import MessageTarget, NotifierMessage, notifier
from abr_server import visasset_manager

VISASSET_CACHE = {}
DATA_CACHE = {}

# Create your views here.
def index(request):
    return HttpResponse('Nothing to see here; this URL is for computers')

def schema(request, schema_name):
    return redirect(settings.STATIC_URL + 'schemas/{}'.format(schema_name))

# State access and modification methods
@csrf_exempt
def modify_state(request):
    item_path_parts = clean_state_path('/api/state', request.path)
    if request.method == 'GET':
        resp = state.get_path(item_path_parts)
        return JsonResponse({'state': resp})
    elif request.method == 'PUT':
        err_message = state.set_path(item_path_parts, json.loads(request.body))
        if len(err_message) > 0:
            return HttpResponse(err_message, status=400)
        else:
            return HttpResponse()

@csrf_exempt
def remove_path(request):
    item_path_parts = clean_state_path('/api/remove-path', request.path)

    if request.method == 'DELETE':
        state.remove_path(item_path_parts)
        return HttpResponse('OK')
    else:
        return HttpResponse('Method for remove must be DELETE', status=400)

@csrf_exempt
def remove(request, value):
    if request.method == 'DELETE':
        state.remove_all(value)
        return HttpResponse('OK')
    else:
        return HttpResponse('Method for remove must be DELETE', status=400)

@csrf_exempt
def undo(request):
    if request.method == 'POST':
        err_message = state.undo()
        if len(err_message) > 0:
            return HttpResponse(err_message, status=400)
        else:
            return HttpResponse()
    else:
        return HttpResponse('Method for undo must be POST', status=400)

@csrf_exempt
def redo(request):
    if request.method == 'POST':
        err_message = state.redo()
        if len(err_message) > 0:
            return HttpResponse(err_message, status=400)
        else:
            return HttpResponse()
    else:
        return HttpResponse('Method for redo must be POST', status=400)


# https://stackoverflow.com/a/4581997
def get_client_ip(request):
    x_forwarded_for = request.META.get('HTTP_X_FORWARDED_FOR')
    if x_forwarded_for:
        ip = x_forwarded_for.split(',')[0]
    else:
        ip = request.META.get('REMOTE_ADDR')
    return ip



def list_visassets(request):
    visasset_list = os.listdir(settings.VISASSET_PATH)
    visasset_list.sort()
    for va in visasset_list:
        if va not in VISASSET_CACHE:
            artifact_json_path = settings.VISASSET_PATH.joinpath(va).joinpath('artifact.json')
            if artifact_json_path.exists():
                with open(artifact_json_path) as fin:
                    VISASSET_CACHE[va] = json.load(fin)
    return JsonResponse(VISASSET_CACHE)

def list_datasets(request):
    for org in os.listdir(settings.DATASET_PATH):
        if org in DATA_CACHE:
            org_data = DATA_CACHE[org]
        else:
            org_data = {}
        org_disk_path = settings.DATASET_PATH.joinpath(org)
        # Skip any "organization" that's not a directory
        if not org_disk_path.is_dir():
            continue
        for dataset in os.listdir(org_disk_path):
            dataset_disk_path = org_disk_path.joinpath(dataset)
            if dataset_disk_path.is_dir():
                dataset_disk_path = dataset_disk_path.joinpath('KeyData')
            else:
                # Skip anything that's not a directory
                continue
            if dataset in org_data:
                keydata_dict = org_data[dataset]
            else:
                keydata_dict = {}
            for keydata_file in os.listdir(dataset_disk_path):
                keydata_path = dataset_disk_path.joinpath(keydata_file)
                keydata_name = keydata_path.stem
                # Only add keydata that aren't already there
                if keydata_name in keydata_dict:
                    continue
                # Retrieve the metadata and put it in the DATA_CACHE
                if fnmatch.fnmatch(keydata_file, '*.json'):
                    with open(keydata_path) as json_header:
                        keydata_dict[keydata_name] = json.load(json_header)
            org_data[dataset] = keydata_dict
        DATA_CACHE[org] = org_data
    return JsonResponse(DATA_CACHE)



@csrf_exempt
def download_visasset(request, uuid):
    if request.method == 'POST':
        try:
            host_path = json.loads(request.body)['hostPath']
        except:
            host_path = settings.VISASSET_LIBRARY
        failed_downloads = visasset_manager.download_visasset(uuid, host_path)
        if len(failed_downloads) == 0:
            notifier.notify(NotifierMessage(MessageTarget.VisAssetsCache))
            return HttpResponse('Downloaded files', status=200)
        else:
            return HttpResponse('Failed to download files: {}'.format(failed_downloads), status=500)
    else:
        return HttpResponse('Method for download must be POST', status=400)

@csrf_exempt
def save_visasset(request, uuid):
    if request.method == 'POST':
        visasset_data = state.get_path(['localVisAssets', uuid])
        save_success = visasset_manager.save_from_local(visasset_data)
        if save_success:
            notifier.notify(NotifierMessage(MessageTarget.VisAssetsCache))
            return HttpResponse('Saved visasset', status=200)
        else:
            return HttpResponse('Unable to save Local VisAsset', status=400)
    else:
        return HttpResponse('Method for download must be POST', status=400)

@csrf_exempt
def remove_visasset(request, uuid):
    if request.method == 'DELETE':
        visasset_manager.remove_visasset(uuid)
        del VISASSET_CACHE[uuid]
        notifier.notify(NotifierMessage(MessageTarget.VisAssetsCache))
        return HttpResponse()
    else:
        return HttpResponse('Method for download must be DELETE', status=400)

def clean_state_path(method_path, request_path):
    # Parse the URL into its sub-components (we know it'll be /state/* that gets us here)
    # For paths with keys that contain slashes (/), need to quote them
    quote_parts = request_path.split('"')

    # Build the path list
    item_path_parts = []
    for i in range(len(quote_parts)):
        if len(quote_parts[i]) <= 0:
            continue

        # At the beginning, get rid of the extra state parts
        q = quote_parts[i].replace(method_path, '')

        if i % 2 == 0:
            item_path_parts.extend([x for x in q.split('/') if len(x) > 0])
        else:
            if len(q) > 0:
                item_path_parts.append(q)
    return item_path_parts


# Get the histogram for any cached data living on this server
def get_histogram(request, org_name, dataset_name, key_data_name, variable_label, bins=540):
    keydata_path = os.path.join(settings.MEDIA_ROOT, 'datasets', org_name, dataset_name, 'KeyData', key_data_name + '.json')
    binfile_path = os.path.join(settings.MEDIA_ROOT, 'datasets', org_name, dataset_name, 'KeyData', key_data_name + '.bin')

    # Load in the key data
    with open(keydata_path) as kd_file:
        kd = json.load(kd_file)

        try:
            variable_index = kd['scalarArrayNames'].index(variable_label)
        except ValueError:
            return HttpResponse('No variable named {}'.format(variable_label), status=400)

        binfile = open(binfile_path, 'rb')

        offset = kd['num_points']*(3+variable_index)*4 + kd['num_cell_indices']*4
        variable_float_data = np.fromfile(binfile, dtype='f4', count=kd['num_points'], offset=offset)

        # variable_float_data = kd['scalarArrays'][variable_index]['array']
        hist, bins_bounds = np.histogram(variable_float_data, bins=bins)
        hist_list = hist.tolist()
        bin_bound_list = bins_bounds.tolist()
        bin_bound_list = bin_bound_list[1:]
        assert len(hist_list) == len(bin_bound_list)
        zipped = [{
            'binMax': bin_bound_list[i],
            'items': hist_list[i],
        } for i in range(len(hist_list))]

        binfile.close()

        # Put two more bins in to allow the variable to go to its *actual*
        # bounds, not just the bounds of this key data
        #
        # Need all four of these so that the line chart accurately reflects that
        # there are ZERO items between the full min and the key_data min
        variable_kd_min = kd['scalarMins'][variable_index]
        variable_kd_max = kd['scalarMaxes'][variable_index]
        variable_min = float(request.GET.get('min', variable_kd_min))
        variable_max = float(request.GET.get('max', variable_kd_max))
        zipped.insert(0, {
            'binMax': variable_kd_min,
            'items': 0,
        })
        zipped.insert(0, {
            'binMax': variable_min,
            'items': 0,
        })
        zipped.append({
            'binMax': variable_kd_max,
            'items': 0,
        })
        zipped.append({
            'binMax': variable_max,
            'items': 0,
        })

        return JsonResponse({'histogram': zipped, 'keyDataMin': variable_kd_min, 'keyDataMax': variable_kd_max})