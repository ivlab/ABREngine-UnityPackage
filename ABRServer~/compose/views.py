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

from django.shortcuts import render

# Create your views here.
def compose(request):
    return render(request, 'compose.html')

def raw_state(request):
    return render(request, 'raw_editor.html')

def sci_interface(request):
    return render(request, 'sci_interface.html')

def state_wizard(request):
    return render(request, 'state_wizard.html')