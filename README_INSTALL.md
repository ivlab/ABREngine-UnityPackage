# To install `ABREngine` in a Unity Project

The first three sections refer to the ABREngine Unity Package. For install instructions on the ABR Server / Visualization Manager, see the [Installing the Vis Manager](#installing-the-vis-manager). Note, if you intend to use the Vis Manager, this will affect where you create your Unity project (with the Vis Manager, the [Unity Project must be cloned in the folder the Vis Manager is Expecting](#step-0-create-an-abr-folder))

## Prereqs
To install dependencies from github.com as described in the next section, you will need to either:
1. Setup your account for SSH access to github.com by creating a [GitHub SSH key](https://docs.github.com/en/github-ae@latest/github/authenticating-to-github/connecting-to-github-with-ssh/generating-a-new-ssh-key-and-adding-it-to-the-ssh-agent) and using a [Git credential helper](https://git-scm.com/docs/gitcredentials) or a blank ssh key password.
2. Alternatively, since this repo is publicly accessible, you should be able to replace the `ssh://git@github.com` in the commands below with the HTTPS equivalent `https://github.com`.


## Install via the Unity Package Manager
To use the package in a read-only mode, the same way you would for packages downloaded directly from Unity:
1. In Unity, open Window -> Package Manager.
2. Click the ```+``` button
3. Select ```Add package from git URL```
4. Paste ```git@github.com:ivlab/ABREngine-UnityPackage.git``` for the latest package
5. Repeat steps 2-4 for each of these additional package dependencies:
  - `ssh://git@github.com/ivlab/JsonSchema-UnityPackage.git`
  - `ssh://git@github.com/ivlab/OBJImport-UnityPackage.git`
  - `ssh://git@github.com/ivlab/JsonDiffPatch-UnityPackage.git`
  - `ssh://git@github.com/ivlab/IVLab-Utilities-UnityPackage.git`
6. Installing the Json packages in Step 5 should have triggered Unity to automatically install the Newtonsoft Json package; however, at the time of writing, it installs version 2.0.0, and we need a newer version.  To update to the latest version, in the Package Manager, go to the section for packages from Unity Technologies, then click on the Newtonsoft Json package to expand it.  Then, click on the button in the bottom right of the window that says "Update to 3.0.2" (or whatever the current latest version is.  If it shows a warning about upgrading potentially breaking your project, you can (probably) safely ignore this, since the project won't work unless you upgrade :)

## Development Mode
Collectively, the lab now recommends a development process where you start by adding the package to your project in read-only mode, as described above.  This way, your Unity project files will always maintain a link to download the latest version of the package from git whenever the project is loaded, and all users of the package will be including it the same way.  If/when you have a need to edit the package, the process is then to "temporarily" switch into development mode by cloning a temporary copy of the package.  Then, edit this source as needed, test your edits for as long as you like, etc.  When you get to a good stopping point, commit and push the changes to github *from within this temporary clone inside the Packages directory*.  Once the latest version of your package is on github, you can then "switch out of development mode" by deleting the cloned repo.  This will cause Unity to revert to using the read-only version of the package, which it keeps in its internal package cache, and we can trigger Unity to update this version to the latest by removing the packages-lock.json file.  In summary:

0. Follow the read-only mode steps above.
1. Navigate your terminal or Git tool into your Unity project's main folder and clone this repository into the packages folder, e.g., ```cd Packages; git clone git@github.umn.edu:ivlab-cs/ABREngine-UnityPackage.git```.  This will create a ABREngine-UnityPackage folder that contains all the sourcecode in the package.
2. Go for it.  Edit the source you just checked out; add files, etc.  However, BE VERY CAREFUL NOT TO ADD THE ABREngine-UnityPackage FOLDER TO YOUR PROJECT'S GIT REPO.  We are essentially cloning one git repo inside another here, but we do not want to add the package repo as a submodule or subdirectory of the project's repo, we just want to temporarily work with the source.
3. When you are ready to commit and push changes to the package repo, go for it.  JUST MAKE SURE YOU DO THIS FROM WITHIN THE Packages/ABREngine-UnityPackage DIRECTORY!  
4. Once these changes are up on github, you can switch out of "development mode" by simply deleting the ABREngine-UnityPackage directory.  The presence of that directory is like a temporary override.  Once it is gone, Unity will revert back to using the cached version of ABREngine that it originally downloaded from git.
5. The final step is to force a refresh of the package cache so that Unity will pull in the new version of the package you just saved to github.  To do this, simply delete the [packages-lock.json](https://docs.unity3d.com/Manual/upm-conflicts-auto.html) file inside your project's Packages folder.

Next step for getting started with ABR in C#: Follow the @creating-cs-abr-vis.md tutorial.


# Installing the Vis Manager

If you'd rather create visualizations, well, visually (as opposed to with code), then we recommend that you install the ABR Vis Manager and Design Interface. Then, you'll be able to create visualization by dragging and dropping:

![ABR Design Interface](/DocumentationSrc~/manual/resources/design-interface-fire-wide.png)

The vis manager has several components; it's important to follow each step closely.

## Step 0: Create an ABR folder

Create a folder named `ABRComponents`  somewhere convenient on your computer
(the name doesn't really matter, name it something that makes sense to you). We
will put the Vis Manager components inside this folder.

You will also need to put your Unity Project that [ABR was imported into](#install-via-the-unity-package-manager) in this folder!


## Step 1: Install Docker

Download and install [Docker](https://www.docker.com/get-started) for your operating system.


## Step 2: Install the Vis Manager

1. Open Docker (Docker Desktop)
  - By default, Docker will start automatically each time your computer starts - to some this is annoying. You can change this setting under the *Settings Gear &#9881; > General > Start Docker Desktop when you log in*

2. Download [Install-SculptingVis.zip](https://drive.google.com/file/d/1olPz7oJL-fDPDPTyiHNAo7imozI9BpG2/view?usp=sharing) into the `ABRComponents` folder you created in [Step 0](#step-0-create-an-abr-folder)
  - *Note: Do not use a Google Drive plugin to unzip the file before downloading - download the whole zip file and unzip it on your computer!*

3. Unzip the `Install-SculptingVis.zip` file inside the `ABRComponents` folder, and ensure the `ABRComponents` folder has the installer file for your operating system:
  - `Install-SculptingVis-Mac.bat` - MacOS
  - `Install-SculptingVis-Windows.bat` - Windows
  - `Install-SculptingVis-Linux.sh` - Linux

4. Run the installer
  1. Double-click the installer file for your OS
    - *On Windows, you may need to click "More Info" > "Allow Anyway"*
    - *On MacOS, you may need to right click, cmd+click "Open", then click "Open"*
  2. Wait for the Vis Manager to download and install
    - *This may take a while*

5. Test the Vis Manager
  1. Verify that `sculpting-vis-app` shows up in your list of Docker Containers
  2. In Docker, click the *Start Button &#9654;* on the `sculpting-vis-app`
  3. In a browser, go to <http://localhost:8000> - you should now see the Design Interface (ABR Compose).

Next step for getting started with ABR with the design interface: Follow the @creating-design-interface-vis.md tutorial.
