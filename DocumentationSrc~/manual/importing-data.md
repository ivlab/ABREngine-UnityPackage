---
uid: importing-data.md
---

# Importing Data

ABR data files consist of two parts - a .bin file and a .json file. Every piece
of @key-data.md will have **both** these files, and they will be organized in folders
according to their data path. For example, for the Gulf of Mexico dataset, we
might have the following folder structure:

```
media/
    datasets/
        E3SM/ --> "Organization"
            GulfOfMexico/ --> "Dataset"
                KeyData/ --> "KeyData - required"
                    Bathymetry.bin --> binary file where data are stored
                    Bathymetry.json --> header file describing the binary
                    ChlorophyllPoints.bin
                    ChlorophyllPoints.json
                    Terrain.bin
                    Terrain.json
```

There are two main options for importing data to ABR:

1. Downloading an existing dataset from the [IVLab's ABR Data Archive](https://drive.google.com/drive/folders/19IMHW-VEzckykO6pXXozkGgsq1hoz31X?usp=sharing)
2. Import Data from ParaView


## Option 1: Download data from ABR Data Archive

We've curated a set of publicly available data for you to check out on the ABR
Data Archive. To download these into your project, you first need to find where
your @media-folder.md is. If you're using the [ABR Design
Interface](xref:creating-design-interface-vis.md), you'll need to look in the
`ABRComponents` folder. Otherwise, check out your [Persistent Data
Path](https://docs.unity3d.com/ScriptReference/Application-persistentDataPath.html),
or whatever other media folder you may have created and set in
@IVLab.ABREngine.ABRConfig.

Once you've located this folder, download the dataset you want from the archive
and unzip it into the `datasets` folder within the `media` folder. Be mindful
that the directory structure should look exactly like described at the top of
this page.


## Option 2: Import data from ParaView

[ParaView](https://paraview.org) is a popular, open-source data analysis and
visualization toolkit developed by [KitWare](https://www.kitware.com/).

### Step 1: Download and install the required components

1. [Download ParaView](https://www.paraview.org/download/). We've tested the
ABR-to-ParaView connector with ParaView version 5.9.
2. Unzip the [EasyParaViewToABR](../../EasyParaViewToABR~/EasyParaViewToABR.zip) zip file to your home folder
3. Follow the EasyParaViewToABR directions below:

[!include[EasyParaViewToABR Instructions](../../EasyParaViewToABR~/README.md)]