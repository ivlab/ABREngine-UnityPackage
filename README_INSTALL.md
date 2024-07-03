# To install `ABREngine` in a Unity Project

The first three sections refer to the ABREngine Unity Package. For install
instructions on the ABR Server, see the [ABR Server](#abr-server) section below.


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
5. After ABREngine is installed, close and reopen your Unity project. Click
"Ignore" when it prompts you about compile errors in the project.
6. Now, there should be a new "ABR" menu tab that has appeared on the top bar
(next to "Window"). Click **ABR > Import ABR Dependencies**.
  - This will install all of ABR's dependencies automatically.
  - Installing all the dependencies will take some time.
  - If you get a permission denied error, try clicking "Import ABR Dependencies" again.

Once you've set up the ABREngine package, there are a couple more steps to make
sure your editor is set up correctly. We recommend using either Visual Studio
Code or Visual Studio to develop, since they are both well-supported by Unity.

> [!TIP]
> Ensure that your
> [external script editor](https://learn.unity.com/tutorial/set-your-default-script-editor-ide)
> is set up correctly, and that under "Generate .csproj files for:", "Embedded
> Packages", "Local Packages", and "Local Tarball" are all checked.
>
> Additionally, make sure you have the [latest .NET
> SDK](https://dotnet.microsoft.com/en-us/download/dotnet) installed. Avoid
> using dotnet versions installed from places besides this official installer,
> e.g., homebrew on MacOS.


### Editing with Visual Studio Code

1. In Package Manager, uninstall the "Visual Studio Code Editor" package (it is
   outdated, and now uses the same package as Visual Studio)
2. Install or update the "Visual Studio Editor" package to the latest version
(you will likely need to periodically keep this up to date).
3. In Unity preferences / External Tools, set the editor to "Visual Studio
Code", and make sure Generate .csproj files is checked for at least "Embedded"
and "Local" packages.
4. In the Project tab, right-click anywhere in the open space and click "Open C#
Project". If all is configured correctly, VS Code should now open.


### Editing with Visual Studio

1. In Package Manager, uninstall the "Visual Studio Code Editor" package
2. Install or update the "Visual Studio Editor" package to the latest version
(you will likely need to periodically keep this up to date).
3. In Unity preferences / External Tools, set the editor to "Visual Studio", and
make sure Generate .csproj files is checked for at least "Embedded"
 and "Local" packages.
4. In the Project tab, right-click anywhere in the open space and click "Open C#
Project". If all is configured correctly, Visual Studio should now open.


# Next steps: Get started creating a visualization

We recommend that you read the @intro.md before going any further, and also
checking out the @core-concepts.md!


After you've read the introduction, to get started creating a visualization:

1. Follow the instructions below to start the ABR server.
2. Import the ABR Vis App example by opening the package manager and navigating
to the ABR package, twirling down "Samples", and clicking "Import" for the "ABR
Vis App" sample.
3. Follow the @abr-vis-app.md tutorial.

If you're looking to make additions or changes to the ABREngine package itself,
head to [Development Mode](#development-mode) for more information.


---


[!INCLUDE [ABR Server](./ABRServer~/README.md)]


---

## Development Mode
Collectively, the lab now recommends a development process where you start by
adding the package to your project in read-only mode, as described above.  This
way, your Unity project files will always maintain a link to download the latest
version of the package from git whenever the project is loaded, and all users of
the package will be including it the same way.  If/when you have a need to edit
the package, the process is then to "temporarily" switch into development mode
by cloning a temporary copy of the package.  Then, edit this source as needed,
test your edits for as long as you like, etc.  When you get to a good stopping
point, commit and push the changes to github *from within this temporary clone
inside the Packages directory*.  Once the latest version of your package is on
github, you can then "switch out of development mode" by deleting the cloned
repo.  This will cause Unity to revert to using the read-only version of the
package, which it keeps in its internal package cache, and we can trigger Unity
to update this version to the latest by removing the packages-lock.json file.
In summary:

0. Follow the read-only mode steps above.
1. Navigate your terminal or Git tool into your Unity project's main folder and
clone this repository into the packages folder, e.g., ```cd Packages; git clone
git@github.com:ivlab/ABREngine-UnityPackage.git```.  This will create a
ABREngine-UnityPackage folder that contains all the sourcecode in the package.
2. Go for it.  Edit the source you just checked out; add files, etc.  However,
BE VERY CAREFUL NOT TO ADD THE ABREngine-UnityPackage FOLDER TO YOUR PROJECT'S
GIT REPO.  We are essentially cloning one git repo inside another here, but we
do not want to add the package repo as a submodule or subdirectory of the
project's repo, we just want to temporarily work with the source.
3. When you are ready to commit and push changes to the package repo, go for it.
JUST MAKE SURE YOU DO THIS FROM WITHIN THE Packages/ABREngine-UnityPackage
DIRECTORY!  
4. Once these changes are up on github, you can switch out of "development mode"
by simply deleting the ABREngine-UnityPackage directory.  The presence of that
directory is like a temporary override.  Once it is gone, Unity will revert back
to using the cached version of ABREngine that it originally downloaded from git.
5. The final step is to force a refresh of the package cache so that Unity will
pull in the new version of the package you just saved to github.  To do this,
simply delete the
[packages-lock.json](https://docs.unity3d.com/Manual/upm-conflicts-auto.html)
file inside your project's Packages folder.

