# ABR Server

This folder contains the code for the ABR server and the ABR Compose Design
Interface. This is how you create visualizations quickly and visually in ABR!

## Installation

Before you begin, this tutorial assumes that you have a modern version of Python
(>= 3.10) installed; the commands shown here are NOT guaranteed to work with
other versions of Python.

In this tutorial, we set up the Python configuration for the ABR Server app,
which enables you to create visualizations with the ABR Compose design interface.

Effectively, we are setting up a "local" copy of each package the ABR Server
depends on to reduce the chance of conflicts. If you're interested in learning
more, check out the [pipenv project](https://docs.pipenv.org/) for more
information. If you're on Windows, replace `python3` with `py`.

> [!TIP]
> Run all these commands from a terminal in the `ABRServer~` folder (<Your >
> Project>/Packages/ABREngine-UnityPackage/ABRServer~). If you have the read-only
> Unity package (i.e., you just installed ABR from the Unity Package Manager), use
> the menu option **ABR > Open ABRServer~ folder**, then open a terminal in this
> folder.
> # [Windows](#tab/windows)
> - [Windows Terminal
>   instructions](https://johnwargo.com/posts/2024/launch-windows-terminal/)
> - [Git Bash instructions](https://stackoverflow.com/questions/72100187/how-to-open-git-bash-from-specific-folder-in-windows-11)
> - [Command Prompt (cmd)
>   instructions](https://www.howtogeek.com/789662/how-to-open-a-cmd-window-in-a-folder-on-windows/)
>
> # [MacOS](#tab/mac)
> - [Open Terminal from Finder
>   instructions](https://ladedu.com/how-to-open-a-terminal-window-at-any-folder-from-finder-in-macos/)
>
> # [Linux](#tab/linux)
> In most Linux distributions, you can click the "Open Folder in Terminal..." in the
> right-click context menu.

To get started, first make sure you have the `pipenv` package installed in Python.

```
python3 -m pip install --user pipenv
```

Then, install the local dependencies:

```
python3 -m pipenv install
```

> [!NOTE]
> The first time you run this command, you may need to provide Python path:
> 
> ```
> python3 -m pipenv --python=/c/Python311/python.exe install
> python3 -m pipenv --python=/usr/bin/python3 install
> ```

Then, to get ready to run the server, "activate" the Pipenv by entering a shell:

```
python3 -m pipenv shell
```

> [!NOTE]
> If you reboot or close the terminal, you may need to re-run the above command.


## Running the server

The server can be run local-only (on localhost:8000 by default):

```
python3 manage.py runserver
```

The server can also be broadcast to other devices (i.e., if you want to run the
server on a desktop and use the design interface with a tablet):

```
python manage.py runserver 0.0.0.0:8000
```


### Development with the server

To enable live-reloading (automatically refresh browser when a file is
changed), run these commands in separate terminals (the settings_dev 
enables live-reloading to work):

```
python manage.py livereload
```

```
python manage.py runserver --settings=abr_server.settings_dev
```
