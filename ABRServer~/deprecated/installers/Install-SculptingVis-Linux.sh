#!/bin/bash
# Install-SculptingVis-Linux.sh
# Copyright (c) 2022, University of Minnesota
# Author: Bridger Herman, herma582@umn.edu

echo "Installing Sculpting Vis App"

# pwd doesn't work with .command files
this_dir="$( cd "$( dirname "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )"
name="sculpting-vis-app"

# Remove any instances so docker create doesn't have issues
echo "- Removing any previous instances of Docker container %name%"
docker container rm $name

# Create the media directory for visassets and datasets, and logs directory
mkdir -p $this_dir/media
mkdir -p $this_dir/logs

# Create Docker container for ABR server
# Forward port 8000 for Design Server
# Mount volume for media directory (datasets and visassets)
# Download and create the Docker container from the image online, and save the container ID for later
echo "Downloading ABR Server (this may take a while)"
container_id=`docker create --name $name -p 8000:8000 -v "$this_dir/media:/media" bridgerherman/sculpting-vis-app`

echo "Container ID: $container_id"

echo $container_id>"$this_dir/_container_id_sculpting-vis-app"

if test -f "$this_dir/_container_id_sculpting-vis-app"; then
    loaded_id=`cat "$this_dir/_container_id_sculpting-vis-app"`
    if [ "$container_id" == "$loaded_id" ]; then
        echo "Install Successful!"
    else
        echo "Install failed."
    fi
else
    echo "Install failed."
fi

read -p "Press any key to exit..."
exit
