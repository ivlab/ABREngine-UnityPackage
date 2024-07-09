---
uid: intro.md
title: Introduction and Overview
---

# Artifact-Based Rendering Engine

The purpose of the Artifact-Based Rendering Engine (ABR Engine) is to provide artists with tools and techniques to create high-fidelity visualizations of multivariate spatiotemporal data using their own traditional-media creations, such as:

Colormaps inspired by nature

![Linear brown to white colormap](resources/linear-brown.png)

Sketched / inked lines

![Angled / semi-straight line](resources/angled-semi-straight.png)

Textures gathered from the real world

![Desert sand texture](resources/desert.png)

Hand-sculpted clay glyphs

![Clay rice point glyph](resources/clayrice-point.png)

With ABR as a tool in their studio, artists have created images like the following:

![Gulf of Mexico biogeochemistry visualization created by artist Stephanie Zeller](resources/gulf.png)
Gulf of Mexico biogeochemistry visualization created by artist Stephanie Zeller

![Ocean currents underneath the Filchner-Ronne Ice Shelf in the antarctic](resources/antarctic.png)
Ocean currents underneath the Filchner-Ronne Ice Shelf in the antarctic by artist Francesca Samsel


The ABR Engine was created and is maintained by the [Sculpting Visualizations Collective](https://sculpting-vis.org).

## Getting Started with ABR

<!-- ABR has two modes. GUI mode is for rapidly
creating visualizations with no programming involved. C# mode is useful for
creating programmatic visualizations, and this mode includes newer and
experimental features of ABR that haven't yet been incorporated to the GUI mode.
Before getting into specifics of either of these modes, it is important to
understand a few concepts about ABR. Give each of these core concepts a read
through before diving into creating with ABR: -->
ABR can be used as both a programming-free visualization design tool (i.e., ABR
Compose mode), and as a standalone visualization package in Unity (i.e., C# mode).
Before getting into specifics of either of these modes, it is important to
understand a few concepts about ABR. Give each of these core concepts a read
through before diving into creating with ABR:

[!INCLUDE [core-concepts.md](concepts/core-concepts.md)]

### Next steps

1. Follow along with the @abr-vis-app.md tutorial to set up ABR to create your
first visualization. This tutorial introduces ABR Compose mode.

2. Follow along with the @abr-cs.md tutorial to set up ABR with C#. This
tutorial introduces scripting with ABR, which enables you to develop deeply
customized and interactive applications with ABR.


<!-- ### Getting Started with ABR C#

Please see [Creating your first C# ABR Visualization](creating-cs-abr-vis.md).

### Getting Started with ABR GUI

![ABR design interface](resources/design-interface-fire-wide.png)
ABR design interface for a visualization of wildfire data.

Please see @creating-design-interface-vis.md. -->