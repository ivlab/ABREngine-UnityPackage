# Easy ParaView to ABR data converter

This ParaView plugin enables direct conversion between ParaView data and ABR
data.

> [!NOTE]
> This plugin assumes you're using at least ParaView 5.8.


## Installation

1. Copy the <./EasyParaViewToABR> folder to your user folder.
    - For example, on Windows: `C:/Users/bridger/EasyParaViewToABR`
    - For example, on Mac: `/Users/bridger/EasyParaViewToABR`
    - For example, on Linux: `/home/bridger/EasyParaViewToABR`
2. Open ParaView.
3. Go to *Tools > Manage Plugins*
4. Click *Load New...*
5. Select the EasyParaViewToABR.py file in the folder you just downloaded
6. Twirl down the newly created EasyParaViewToABR menu item and check the
   "AutoLoad" box - this will tell ParaView to load the plugin every time
   ParaView opens.


## Usage

Use the following steps to get started with ABR.

1. Make sure the @abr-server.md is running
2. In the ParaView pipeline browser, select the data you would like to transfer to ABR.
3. Make sure your data are in the correct format. Look at the "Information" tab in ParaView to see the data's "Type". ABR will accept any of the following - if it's not in one of these formats you may need to convert (see [Converting data to ABR-acceptable format](#converting-data-to-abr-acceptable-format))
    - Polygonal mesh
    - Unstructured grid
    - Uniform rectiliear grid
4. Once your data are in the right format, add a new `EasyParaViewToABR` filter by going to *Filters > Alphabetical*, or typing *Ctrl + Space* and seraching "EasyParaViewToABR"
5. Input the required information in the Properties window (see Example below).
    - **Dataset:** higher-level dataset this Key Data is a part of
    - **Key Data Name:** descriptive name for the key data you are sending to ABR
    - **Organization:** descriptive name for the organization that owns the data
    - Host: IP address and port of ABR Server (leave unchanged unless you are running the ABR Server and ParaView on different computers)
6. Click the green 'Apply' button to send your data to ABR!
    - in the ABR Server logs, you should see messages like "Imported JSON header
      to path....". These are where your ABR data files are located on your
      computer (in the @media-folder.md).


> [!TIP]
> If something isn't working correctly, be sure to check out the ParaView logs
> and ABR server logs. ParaView logs can be accessed by going to *View >
> Output Messages*, and ABR server logs can be seen in the terminal that you
> launched the @abr-server.md from.


#### Converting data to ABR-acceptable format

If you have VOLUME data, make sure it's a Uniform Rectilinear Grid by adding a 'Resample to Image' filter.

If you have SURFACE data, make sure it's either a Polygonal Mesh or an Unstructured Grid by adding an 'Extract Surface' or 'Append Datasets' filter, respectively. Additionally, make sure that your surface is made up of triangles by performing a 'Triangulate' filter.

If you have LINE data, make sure it's an Unstructured Grid by using the 'Append Datasets' filter.

If you have POINTS data, make sure it's an Unstructured Grid by using the 'Append Datasets' filter.