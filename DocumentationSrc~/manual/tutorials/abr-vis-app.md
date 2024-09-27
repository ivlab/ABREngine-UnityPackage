---
uid: abr-vis-app.md
title: ABR Vis App
---

# ABR Vis App

This example is a pre-built app that you can use to design visualizations with
the ABR Compose design interface. Follow along with this tutorial to get started
building your first visualization.


## Part 1: Setup

Before you begin, make sure you have [installed ABR and all its dependencies](../install.md).

First, import the ABR Vis App sample. You can do this by opening the package
manager and navigating to the ABR package, twirling down "Samples", and clicking
"Import" for the "ABR Vis App" sample.

When Unity asks about importing the TextMeshPro (TMP) essentials, click *Import
TMP Essential*.

Once the sample has loaded, open the "Main" scene in the "Scenes" folder of "ABR
Vis App". You should see a scene like the following:

![A screenshot of the Unity editor with the ABR vis app scene loaded. The ABREngine GameObject is selected in the left Hierarchy, and the ABRConfig_VisApp configuration is selected in the right Inspector.](../resources/abr-vis-app-1-scene.png)


Now, verify that the ABREngine has the correct path to the ABRServer's media folder.  In the Scene Hierarchy, click on the ABREngine GameObject to select it.  Then, look in the Inspector view, and you will see an option for selecting the current ABREngine configuration.  Make sure the configuration named `ABRConfig_VisApp` is selected!  Then, click on `Open Current ABR Config..`.  Now, you need to check that the path under "Media Path" is the same path that the ABRServer is using for its media directory.  

Note: There may be a bug in ABR here... do relative paths work in this Media Path field?  Had trouble with a relative path (Sept. 2024) but found everything worked fine if using an absolute path.

Lastly, we need some data to visualize! In Unity, click *ABR > Copy Example Data
to Media Folder*. This will make some example data available to the ABR design
interface and the ABR Engine!  Open the media folder to double-check that this worked, you should see files under: `media/datasets/Demo/*`.


## Part 2: Running the server

To design visualizations with the ABR design interface, you'll need to start the
ABR server. Instructions for this can be found in the [ABR Server
README](../abr-server.md). Essentially, the ABR Compose design interface lives
within the ABR Server, so you need to run that Python server first in order to
design visualizations with ABR.

Once the server is installed, the only command you should need to run is:

```
python3 manage.py runserver
```

> [!TIP]
> Recall that this command is run in a terminal inside the ABRServer~ folder,
> which can be opened from Unity by clicking *ABR > Open ABRServer~ Folder*. You
> may also need to re-activate the pipenv if you've closed the terminal; do so
> by running `python3 -m pipenv shell` in the ABRServer~ folder.

After you've started the server, visit http://localhost:8000 in a
web browser. You should now see the ABR design interface:

![A screenshot of the ABR design interface loaded in a web browser. The left side shows the test data we imported in Part 1, and the right side shows the available VisAssets to design a visualization with.](../resources/abr-vis-app-2-interface.png)


## Next steps

At this point, you're ready to get started designing a visualization! Your next
step is to follow along with the @creating-design-interface-vis.md tutorial.

If you're interested in using your own data with ABR, check out @importing-data.md.

If you'd like to design visualizations using C# scripting, check out @abr-cs.md.
