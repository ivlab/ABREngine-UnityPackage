# Artifact-Based Rendering Engine

## Introduction

We introduce Artifact-Based Rendering (ABR), a framework of tools, algorithms,
and processes that makes it possible to produce real, data-driven 3D scientific
visualizations with a visual language derived entirely from colors, lines,
textures, and forms created using traditional physical media or found in nature.
A theory and process for ABR is presented to address three current needs: (i)
designing better visualizations by making it possible for non-programmers to
rapidly design and critique many alternative data-to-visual mappings; (ii)
expanding the visual vocabulary used in scientific visualizations to depict
increasingly complex multivariate data; (iii) bringing a more engaging, natural,
and human-relatable handcrafted aesthetic to data visualization. New tools and
algorithms to support ABR include front-end applets for constructing
artifact-based colormaps, optimizing 3D scanned meshes for use in data
visualization, and synthesizing textures from artifacts. These are complemented
by an interactive rendering engine with custom algorithms and interfaces that
demonstrate multiple new visual styles for depicting point, line, surface, and
volume data. A within-the-research-team design study provides early evidence of
the shift in visualization design processes that ABR is believed to enable when
compared to traditional scientific visualization systems. Qualitative user
feedback on applications to climate science and brain imaging support the
utility of ABR for scientific discovery and public communication.

This Unity package provides features and functionality for the Artifact-Based
Rendering technique, as described by the
[paper from VIS 2019](https://arxiv.org/pdf/1907.13178.pdf).


## Installation

Install via the Unity Package Manager (UPM). In Unity, go to *Window > Package
Manager > + > git package* and paste the following URL (install dependencies first!):

```
ssh://git@github.umn.edu/ivlab-cs/ABREngine-UnityPackage.git#v0.2.1
```


### Dependencies

NOTE: Currently in Unity, these need to be manually installed. Go to *Window >
Package Manager > + > git package...* and paste each of these URLs *before* you
paste the ABREngine's URL.

```
ssh://git@github.umn.edu/ivlab-cs/JsonSchema-UnityPackage.git

ssh://git@github.umn.edu/ivlab-cs/OBJImport-UnityPackage.git

ssh://git@github.umn.edu/ivlab-cs/JsonDiffPatch-UnityPackage.git

ssh://git@github.umn.edu/ivlab-cs/IVLab-Utilities-UnityPackage.git
```


## Quick Start for Developers Using the ABREngine

**Step 1:** Install the package using UPM as described above.

**Step 2:** Drag and drop the ABREngine prefab into your scene. In the Project
*tab, search In Packages for "ABREngine".

**Step 3:** Edit your ABR Configuration. Please see [ABR
*Configuration](abr-config.html) for more details.


## Quick Start for Contributing to / Editing the ABREngine

**Step 1:** Clone the ABREngine-UnityPackage git repository into the `Packages`
*folder of your Unity project.

**Step 2:** Same as above Step 2

**Step 3:** Same as above Step 3

**Step 4:** Make your changes on a branch, and make a pull request. See
*CONTRIBUTING.md in the ABREngine-UnityPackage repository for more information.