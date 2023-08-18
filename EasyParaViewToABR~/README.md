# Easy ParaView to ABR data converter

This ParaView plugin enables direct conversion between ParaView data and ABR data. **NOTE: this plugin assumes you're using at least ParaView 5.8.**


## Installation

1. Copy the <./EasyParaViewToABR> folder to your user folder.
    - For example, on Windows: `C:/Users/bridger/EasyParaViewToABR`
    - For example, on Mac: `/Users/bridger/EasyParaViewToABR`
    - For example, on Linux: `/home/bridger/EasyParaViewToABR`
2. Open ParaView.
3. Go to *Tools > Manage Plugins*
4. Click *Load New...*
5. Select the EasyParaViewToABR.py file in the folder you just downloaded
6. (Optional) Twirl down the newly created EasyParaViewToABR menu item and check the "AutoLoad" box


## Usage

### Part 1: Setting up ABR Config

1. Open your Unity Project that uses ABR
2. Click on the `ABREngine` GameObject in your scene and Open the Current ABR Config
3. Under the "Network-Based VisAssets and Data Configuration" section, change the "Data Listener Port" to `1900` (should match [ParaView plugin below](#part-2-transferring-data-from-paraview-to-abr))


### Part 2: Transferring Data from ParaView to ABR

1. Make sure ABR is running!!
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
    - Host: (optional) IP address of the machine ABR is running on
    - Port: (optional) Port that the ABR data listener is running on
6. Click the green 'Apply' button to send your data to ABR!
7. You may need to stop the Unity project and start it again for the data to show up.


#### Converting data to ABR-acceptable format

If you have VOLUME data, make sure it's a Uniform Rectilinear Grid by adding a 'Resample to Image' filter.

If you have SURFACE data, make sure it's either a Polygonal Mesh or an Unstructured Grid by adding an 'Extract Surface' or 'Append Datasets' filter, respectively. Additionally, make sure that your surface is made up of triangles by performing a 'Triangulate' filter.

If you have LINE data, make sure it's an Unstructured Grid by using the 'Append Datasets' filter.

If you have POINTS data, make sure it's an Unstructured Grid by using the 'Append Datasets' filter.