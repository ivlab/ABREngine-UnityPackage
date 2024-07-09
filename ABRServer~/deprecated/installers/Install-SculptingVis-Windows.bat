@echo off

REM  Install-SculptingVis-Windows.bat
REM  Copyright (c) 2022, University of Minnesota
REM  Author: Bridger Herman, herma582@umn.edu

echo "Installing Sculpting Vis App"

set this_dir=%~dp0
set name="sculpting-vis-app"

@REM Remove any instances so docker create doesn't have issues
echo "- Removing any previous instances of Docker container %name%"
docker container rm %name%


@REM Create the media directory for visassets and datasets, and logs directory
mkdir media
mkdir logs

@REM Create Docker container for ABR server
@REM Forward port 8000 for Design Server
@REM Mount volume for media directory (datasets and visassets)
@REM Download and create the Docker container from the image online, and save the container ID for later
echo "- Downloading ABR Server (this may take a while)"
for /f %%i in ('docker create --name %name% -p 8000:8000 -v "%this_dir%/media:/media" bridgerherman/sculpting-vis-app') do set container_id=%%i

echo "- Container ID: %container_id%"

echo %container_id%>_container_id_sculpting-vis-app
set /p loaded_id=<_container_id_sculpting-vis-app

if exist _container_id_sculpting-vis-app (
    if "%loaded_id%" == "%container_id%" (
        echo "Install Successful!"
    ) else (
        echo "Install failed: container ID mismatch"
    )
) else (
    echo "Install failed: _container_id_sculpting-vis-app does not exist"
)

pause