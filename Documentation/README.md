# Auto-generating documentation for ABREngine-UnityPackage

[**View the ABREngine Documentation**](https://pages.github.umn.edu/ivlab-cs/ABREngine-UnityPackage/api)

Documentation is generated using
[DocFx](https://dotnet.github.io/docfx/index.html). There's a [handy
repo](https://github.com/NormandErwan/DocFxForUnity) for using this with Unity,
which we build on.

Documentation should be generated for each release of ABR. Commit the HTML files
in the /docs folder of this repo.

## Required components and installation

- DocFx is needed to generate the documentation. Download the latest stable
version from [DocFx releases](https://github.com/dotnet/docfx/releases).
- Unzip to somewhere useful to you, optionally somewhere on PATH.


## Generating docs

Run the following command from the root of this repo:

Windows:

```
docfx.exe Documentation/docfx.json
```

(You may need to replace `docfx.exe` with the absolute path `C:\Absolute\Path\To\docfx.exe`)