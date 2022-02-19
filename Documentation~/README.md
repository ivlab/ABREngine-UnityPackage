# Auto-generating documentation for ABREngine-UnityPackage

[**View the ABREngine Documentation**](https://pages.github.umn.edu/ivlab-cs/ABREngine-UnityPackage/api/IVLab.ABREngine.html)

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
docfx.exe Documentation/docfx.json --serve
```

(You may need to replace `docfx.exe` with the absolute path `C:\Absolute\Path\To\docfx.exe`)

Then go to a browser at http://localhost:8080 to view the docs

**Note:** After you generate, the docs, *before* you commit them, make sure to
*enter the Unity editor at least once so that the corresponding .meta files are
*generated and your end users don't end up with hundreds of errors about missing
*.meta files!