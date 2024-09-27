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
> Run all these commands from a terminal in the `ABRServer~` folder (<Your 
> Project>/Packages/ABREngine-UnityPackage/ABRServer~). If you have the read-only
> Unity package (i.e., you just installed ABR from the Unity Package Manager), use
> the menu option **ABR > Open ABRServer~ folder** to open the ABRServer folder.
> After you've opened the folder, follow the instructions for your operating
> system to open a terminal.
> 
> # [Windows](#tab/windows)
> - [Windows Terminal
>   instructions](https://johnwargo.com/posts/2024/launch-windows-terminal/)
> - [Git Bash instructions](https://stackoverflow.com/questions/72100187/how-to-open-git-bash-from-specific-folder-in-windows-11)
> - [Command Prompt (cmd)
>   instructions](https://www.howtogeek.com/789662/how-to-open-a-cmd-window-in-a-folder-on-windows/)
>
> # [MacOS](#tab/mac)
> From Finder, the easiest way to open a terminal on a Mac is:
> 1. Enable Path Bar (*View > Show Path Bar*)
> 2. Once you've opened Finder to the ABRServer~ folder, right/two-finger click
>    on the rightmost folder in the newly enabled Path Bar
> 3. Click "Open in Terminal"
>
> # [Linux](#tab/linux)
> In most Linux distributions, you can click the "Open Folder in Terminal..." in the
> right-click context menu.

To get started, first make sure you have the `pipenv` package installed in
Python. We are using `python3 -m pip` instead of `pip` directly to ensure we
have the correct pip/Python version pairing.

```
python3 -m pip install --user pipenv
```

Then, install the local dependencies:

```
python3 -m pipenv install
```

> [!NOTE]
> The first time you run this command, you may need to provide Python path. You
> can usually get the Python path with a command like `which python3` or `where
> python3`. Again, on Windows, replace `python3` with `py`. For example:
> 
> # [Windows](#tab/windows)
> ```
> py -m pipenv --python=/c/Python311/python.exe install
> ```
>
> # [MacOS](#tab/mac)
> ```
> python3 -m pipenv --python=/opt/homebrew/bin/python3 install
> ```
>
> # [Linux](#tab/linux)
> ```
> python3 -m pipenv --python=/usr/bin/python3 install
> ```

Then, to get ready to run the server, "activate" the Pipenv by entering a shell:

```
python3 -m pipenv shell
```

> [!NOTE]
> If you reboot or close the terminal, you will need to re-run the above command.


## Running the server

The server can be run local-only (on localhost:8000 by default):

```
python3 manage.py runserver
```

> [!NOTE]
> If running the server fails with an error related to urllib3 (this happened on on at least one mac machine recently), the fix was to uninstall and reinstall this dependency with: pip uninstall urllib3; pip install urllib3


The server can also be broadcast to other devices (i.e., if you want to run the
server on a desktop and use the design interface with a tablet):

```
python manage.py runserver 0.0.0.0:8000
```

After this command is running, test it but opening <http://localhost:8000> in a
web browser! You should see the ABR design interface appear:


![A screenshot of the ABR design interface loaded in a web browser.](/DocumentationSrc~/manual/resources/abr-vis-app-2-interface.png)


### Development with the server

This step is optional, and only recommended if you are editing the Python or
JavaScript files for the Server or Design Interface. To enable live-reloading
(automatically refresh browser when a file is changed), run these commands in
separate terminals (the settings_dev enables live-reloading to work):

```
python manage.py livereload
```

```
python manage.py runserver --settings=abr_server.settings_dev
```
