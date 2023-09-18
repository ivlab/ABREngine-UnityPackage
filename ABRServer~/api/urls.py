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

from django.urls import path, re_path

from . import views

urlpatterns = [
    path('', views.index, name='index'),
    path('schemas/<str:schema_name>/', views.schema),
    path('undo', views.undo),
    path('redo', views.redo),
    re_path('^state/*', views.modify_state),
    re_path('^remove-path/*', views.remove_path),
    path('remove/<str:value>', views.remove),
    path('visassets', views.list_visassets),
    path('datasets', views.list_datasets),
    path('download-visasset/<str:uuid>', views.download_visasset),
    path('remove-visasset/<str:uuid>', views.remove_visasset),
    path('save-local-visasset/<str:uuid>', views.save_visasset),
    path('histogram/<str:org_name>/<str:dataset_name>/KeyData/<str:key_data_name>/<str:variable_label>', views.get_histogram),
]
