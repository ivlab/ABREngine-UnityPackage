---
uid: media-folder.md
name: Media Folder
---

# The Media Folder

The media folder is where all datasets and visassets are stored. Every ABR project should have one or more `media` folders.

By default in the ABR configuration, the media folder is located in the [Application.persistentDataPath](https://docs.unity3d.com/ScriptReference/Application-persistentDataPath.html). However, this can be changed to a more convenient location, including the Unity project's own Assets folder by modifying the `mediaPath` in the ABR Configuration that is used for the ABREngine.