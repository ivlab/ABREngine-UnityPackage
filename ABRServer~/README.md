# ABR Server

THIS REPOSITORY IS ARCHIVED and no longer maintained. The ABR Server core code
is now maintained inside the
[ABREngine-UnityPackage](https://github.com/ivlab/ABREngine-UnityPackage).

## Installation

To get started with development, first make sure you have the `pipenv` package.
This enables all developers of the server to share the same Python configuration
for this app. Essentially, the Pipenv contains a "local" copy of each dependency
that's unique to this project to reduce the chance of conflicting dependencies
with your system Python installation. Check out the [pipenv
project](https://docs.pipenv.org/) for more information. If you're on Windows,
replace `python3` with `py`.

All these commands are tested with Python 3.8; they are NOT guaranteed to work
with other versions of Python.

```
python3 pip install --user pipenv
```

Then, install the local dependencies:

```
python3 -m pipenv install
```

Then, to begin development, "activate" the Pipenv by entering a shell:

```
python3 -m pipenv shell
```

From here, you should have access to all the dependencies of the project.


## Building the executable version

The ABR server can also be built to an executable for easy distribution.

First, you need the `pyinstaller` package:

```
python3 -m pip install pyinstaller
```

Restart your terminal to make sure `pyinstaller` ends up on your PATH.


Then, to build the executable, run the following command:

```
pyinstaller  --name="abr-server" --hidden-import="compose" --hidden-import="compose.urls" --hidden-import="api" --hidden-import="api.urls" --hidden-import="abr_server.routing" --add-data="templates:templates" --add-data="static:static" manage.py
```

this will output an executable (for the OS/architecture that you run pyinstaller on) to the folder `./dist/abr-server`. You can zip this up, etc. for distribution.

## Old news below here, probably delete

This is the main server component of the ABR architecture.

![The four-component ABR Architecture, including Design User Interfaces, Server,
Graphics Engines, and Data
Hosts.](https://www.sculpting-vis.org/wp-content/uploads/2021/05/abr_components.png)


## What is the server?

The server acts as an intermediary between the rest of the architecture, and
stores the *current* version of the visualization state. The visualization
state is described by a [formal json schema](./static/schemas/ABRSchema_0-2-0.json),
and is validated against the schema each time it is updated.

The server is a Python Django server. By default, it runs in debug mode which is
fine for self-contained apps, but should we want to deploy to a proper server
(e.g. https://sculptingvis.tacc.utexas.edu), nginx has been briefly tested for
such a purpose (see [abr_server_nginx.conf](./abr_server_nginx.conf) and
uwsgi_params.)

Check out [CONTRIBUTING.md](./CONTRIBUTING.md) if you want to make changes to the server.


## Installation and setup

1. The abr_server depends on the abr_data_format - the custom preprocessed
geometric representation that ABR uses. Please follow the [instructions in that
repository](https://github.umn.edu/ivlab-cs/ABRUtilities/tree/master/ABRDataFormat)
for installing the `abr_data_format` Python package.
2. Install the dependencies: `python -m pip install -r requirements.txt`
3. Setup the static files: `python manage.py collectstatic`


## Run the server

The server can be run local-only (on localhost:8000 by default):

```
python manage.py runserver
```

The server can also be broadcast to other devices:

```
python manage.py runserver 0.0.0.0:8000
```

To enable live-reloading (automatically refresh browser when a file is changed), run:

```
python manage.py livereload
```


## Docker VisManager Build Instructions

### Prerequisites:

You do NOT need a Docker Hub account to build the docker image locally, but if you intend to deploy to the web for broad use (e.g. in class) then you DO need one. Go to hub.docker.com and create a personal account. or create one to share.

Then, sign into Docker Desktop with your new Docker Hub account (upper right > Sign In).

Note: in the instructions below, replace all instances of `bridgerherman` with your docker hub ID.

### Building the image

To build the Docker image for release, use the following steps.

1. Ensure Docker is running
2. Run the following command in this folder (root `abr_server` folder) (replace `bridgerherman` with your docker hub user id and replace `v1.0.2` with your version):

```
docker build -t bridgerherman/sculpting-vis-app:v1.0.2 .
```

3. Test and update the docker install scripts in the `installers` folder. At a minimum, run, for example:

```
docker create --name "sculpting-vis-app" -p 8000:8000 -v "./media:/media" bridgerherman/sculpting-vis-app:v1.0.2
```


4. When you're ready and confident all works as expected, push to Docker Hub so it's accessible to others:

```
docker push bridgerherman/sculpting-vis-app:v1.0.2
```



## Windows Embedded VisManager build instructions

### Install and Configure Embedded Python

1. Make a folder somewhere called `VisManager-WindowsEmbedded`
2. Make a subfolder of that folder called `python38`.
3. Download a version of the Python embeddable release. Python 3.8 is recommended:
    - [Download the Python 3.8 Windows embeddable package (64-bit)](https://www.python.org/ftp/python/3.8.10/python-3.8.10-embed-amd64.zip)
    - In `python38` folder you created, unzip the contents of the zip file you just downloaded.
4. In the file `python38/python38._pth`, ensure the `import site` is uncommented, the file should look like this (without this, PIP won't install correctly):

```python
python38.zip
.

# Uncomment to run site.main() automatically
import site
```

### Copy ABR Server files

1. Copy the entire `abr_server` folder (THIS folder - copy from one level up) and paste it into the `VisManager-WindowsEmbedded` folder.

### Create the Installation and Run scripts

1. In the VisManager-WindowsEmbedded folder, create a file called `InstallVisManager.bat` and paste the following contents into it:

```
set this_dir=%~dp0
set python="%this_dir%\python38\python.exe"
set PYTHONPATH="%PYTHONPATH%;%this_dir%\python38\Lib;%this_dir%\python38\Lib\site-packages"

cd abr_server

curl -O https://bootstrap.pypa.io/get-pip.py
%python% get-pip.py

%python% -m pip install -r requirements.txt
```

2. In the VisManager-WindowsEmbedded folder, create a file called `RunVisManager.bat` and paste the following contents into it:

```
set this_dir=%~dp0
set python="%this_dir%\python38\python.exe"
set PYTHONPATH="%PYTHONPATH%;%this_dir%\python38\Lib;%this_dir%\python38\Lib\site-packages"

cd abr_server

%python% manage.py collectstatic --noinput
%python% manage.py migrate

%python% manage.py runserver --noreload 0.0.0.0:8000
```

**TODO:** the above instructions don't seem to work, I'm not sure how I actually
*got this to work in Spring 2022. Stuck on the `RunVisManager.bat` script.
*`python manage.py collectstatic` doesn't recognize `collectstatic` as a
*command.