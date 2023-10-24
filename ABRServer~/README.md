# ABR Server

This folder contains the code for the ABR server and the ABR Compose Design Interface.

## Installation

To get started with development, first make sure you have the `pipenv` package.
This enables all developers of the server to share the same Python configuration
for this app. Essentially, the Pipenv contains a "local" copy of each dependency
that's unique to this project to reduce the chance of conflicting dependencies
with your system Python installation. Check out the [pipenv
project](https://docs.pipenv.org/) for more information. If you're on Windows,
replace `python3` with `py`.

All these commands are tested with Python 3.10; they are NOT guaranteed to work
with other versions of Python.

```
python3 pip install --user pipenv
```

Then, install the local dependencies:

```
python3 -m pipenv install
```

The first time you run this command, you may need to provide Python path:

```
python3 -m pipenv --python=/c/Python311/python.exe install
```

Then, to begin development, "activate" the Pipenv by entering a shell:

```
python3 -m pipenv shell
```

From here, you should have access to all the dependencies of the project.


## Running the server

The server can be run local-only (on localhost:8000 by default):

```
python manage.py runserver
```

The server can also be broadcast to other devices:

```
python manage.py runserver 0.0.0.0:8000
```


To enable live-reloading (automatically refresh browser when a file is
changed), run these commands in separate terminals (the settings_dev above
enables live-reloading to work):

```
python manage.py livereload
```

```
python manage.py runserver --settings=abr_server.settings_dev
```



## Building the executable version

The ABR server can also be built to an executable for easy distribution.

First, you need the `pyinstaller` package. If you've followed the steps above
with installing and activating the pipenv shell, this should already be taken care of. You can check by running:

```
pyinstaller --version
```

If for some reason that doesn't work, you can run:

```
python3 -m pip install pyinstaller
```

Restart your terminal to make sure `pyinstaller` ends up on your PATH.


Then, to build the executable, run one of the following commands depending on
your OS (add more here for OS's that aren't supported yet):

Windows x64:

```
pyinstaller  --name="ABRServer-Windows-X64" --hidden-import="compose" --hidden-import="compose.urls" --hidden-import="api" --hidden-import="api.urls" --hidden-import="abr_server.routing" --hidden-import="_socket" --add-data="templates:templates" --add-data="static:static" --add-data="abr_server.cfg:." manage.py
```

Mac x64 (Intel):

```
pyinstaller  --name="ABRServer-OSX-X64" --hidden-import="compose" --hidden-import="compose.urls" --hidden-import="api" --hidden-import="api.urls" --hidden-import="abr_server.routing" --hidden-import="_socket" --add-data="templates:templates" --add-data="static:static" --add-data="abr_server.cfg:." manage.py
```

Mac ARM (M1 or M2):

```
pyinstaller  --name="ABRServer-OSX-ARM64" --hidden-import="compose" --hidden-import="compose.urls" --hidden-import="api" --hidden-import="api.urls" --hidden-import="abr_server.routing" --hidden-import="_socket" --add-data="templates:templates" --add-data="static:static" --add-data="abr_server.cfg:." manage.py
```

this will output an executable (for the OS/architecture that you run
pyinstaller on) to the folder `./dist/ABRServer`. You can zip this up, etc.
for distribution. In Unity, the ABRServer script will also look in these
folders depending on what OS you're on.
