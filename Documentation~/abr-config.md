# Configuring ABR

ABR configuration allows you, the developer, to make modifications to how the
ABR Engine behaves. All configuration options are defined in
[ABRConfig.cs](api/IVLab.ABREngine.ABRConfig.html).

To make changes to the configuration, place a file `ABRConfig.json` in your
`Assets/StreamingAssets` folder in Unity, and define any of the key-value pairs
specified in ABRConfig.cs to change the engine behaviour. If you build your
Unity project, you can modify the ABR Config without rebuilding; the
`ABRConfig.json` file is located at `[BuildFolder]/[AppName]_Data/StreamingAssets/ABRConfig.json`.


## Example configurations

A common configuration for ABR is to allow it to connect to an [ABR
Server](https://github.com/ivlab/abr-server), which allows interactive
design of a visualization using a puzzle-piece based drag-n-drop UI. By defining
the `mediaPath` key, we are telling the ABREngine to look for datasets and
visassets in the same folder the ABR Server is saving them to (i.e., ABR Server
and this Unity Project are sibling directories with `media`).

```
{
    "serverAddres": "http://localhost:8000",
    "statePathOnServer": "api/state",
    "mediaPath": "../media"
}
```


Another common change is to be able to connect an upstream data creation/management application:

```
{
    "dataListenerPort": 1900
}
```

Lastly, for debugging or embedded/console applications of ABREngine, a state to
load on start can be specified. The Engine looks in both Resources and
StreamingAssets folders for this state to load.

```
{
    "loadStateOnStart": "testState.json"
}
```