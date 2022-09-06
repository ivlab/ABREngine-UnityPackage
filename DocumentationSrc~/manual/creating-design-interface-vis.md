---
uid: creating-design-interface-vis.md
title: Creating your first ABR Visualization with the ABR Design Interface
---

# Creating your first ABR Visualization with the ABR Design Interface

Often, the most effective way to create a visualization is visually! So, we've
provided a way to create visualizations using a drag-and-drop approach: The ABR Design Interface (ABR Compose).

![ABR Design Interface](./resources/design-interface-fire-wide.png)

If you haven't already, please follow all the instructions in
[README_INSTALL.md](../../README_INSTALL.md) to install the ABREngine Unity
Package, design interface and visualization manager before continuing.


## Part 1: Creating the ABREngine GameObject and configuration

Every ABR visualization needs to have the ABREngine GameObject and configuration correctly set up.

In the Unity Editor "Project" tab, search for ABREngine "In Packages". Then, drag-and-drop the ABREngine prefab (blue cube icon) into the Hierarchy.
![](resources/cs-vis_1-abrengine.png)

If you click on the ABREngine GameObject you just created, observe the current ABR configuration selected ("DefaultABRConfig"):
![](resources/di-vis_config.png)

To connect to the Design Interface, we'll want to use the "ServerAndMediaABRConfig":
![](resources/di-vis_server.png)

Save your scene by pressing Ctrl+S or Cmd+S or navigating to *File > Save*.


## Part 2: Verifying the design interface connection

1. Ensure Docker is running and the Vis Manager (`sculpting-vis-app` container)
is running as described in [README_INSTALL.md](../../README_INSTALL.md).

2. In a browser, go to <http://localhost:8000> to open the Design Interface.

3. In Unity, press the Play button. You should see the following output or similar:
![](resources/di-vis_connect.png)


## Part 3: Next Steps

1. @importing-data.md
2. Importing VisAssets (see videos below)
3. Creating a visualization (see videos below)

> [!video https://www.youtube.com/embed/pmawi3QpxE8]

> [!video https://www.youtube.com/embed/OBgYQBr20uE]