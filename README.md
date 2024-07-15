# ABREngine Main Package

## Installation

Please refer to [README_INSTALL.md](./README_INSTALL.md)

## Documentation

[Documentation for ABR is available online.](https://ivlab.github.io/ABREngine-UnityPackage)



This package contains all the necessary components to run ABR, including:

- ABREngine-UnityPackage
    - Scripts, Shaders, and other assets to work with the Artifact-Based Rendering Engine in Unity.
    - This repo is a Unity Package, and can be installed with the Unity Package
    Manager (see [README_INSTALL.md](./README_INSTALL.md)).
- ABR Server
    - The Python server that hosts the ABR Compose visualization design interface
    - Please see the [ABRServer~](./ABRServer~) folder
    - Check out the [abr_server.cfg](./ABRServer~) configuration file as well -
    particularly, ensure this matches with your use case (especially if you want
    ABREngine's media folder to point to the same location as the ABRServer's
    media folder.)
- ABR Schemas
    - Defines the JSON schemas necessary to create an artist-designed data visualization
    - Please see the [ABRSchemas~](./ABRSchemas~) folder
