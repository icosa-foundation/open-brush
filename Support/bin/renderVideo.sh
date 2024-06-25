#!/bin/bash

# Validate Arguments
if [ $# -ne 3 ]; then
  echo "ERROR: This script was not called with the expected arguments."
  exit 1
fi

sketchPath="$1"
usdaPath="$2"
exeName=$(basename "$3")
exePath="$3"

# Check if process is running
if pgrep -x "$exeName" > /dev/null; then
  echo "Tilt Brush is running, please exit Tilt Brush before rendering"
  exit 1
fi

echo "Sketch: $sketchPath"
echo "Video Path: $usdaPath"
echo ""

# Menu
echo "What would you like to do?"
echo ""
options=("HD 30 FPS" "HD 60 FPS" "4k (UHD-1) 30 FPS" "4k (UHD-1) 60 FPS"
         "Omnidirectional Stereo 360, 4k x 4k 30 FPS"
         "Omnidirectional Stereo 360, 4k x 4k 30 FPS, no quick load"
         "[Fast,Low Quality] Omnidirectional Stereo 360, 1k x 1k 30 FPS"
         "[Fast,Low Quality] Omnidirectional Stereo 360, 1k x 1k 30 FPS, no quick load")

select opt in "${options[@]}"
do
  case $REPLY in
    1) res=1920; resh=1080; fps=30; break;;
    2) res=1920; resh=1080; fps=60; break;;
    3) res=3840; resh=2160; fps=30; break;;
    4) res=3840; resh=2160; fps=60; break;;
    5) 
      echo "Rendering 360 stereo omnidirecitonal stereo 4k x 4k 30fps 360 stereo"
      "$exePath" --renderCameraPath "$usdaPath" --captureOds "$sketchPath"
      exit 0
      ;;
    6) 
      echo "Rendering 360 stereo omnidirecitonal stereo 4k x 4k 30fps 360 stereo, no quick load"
      "$exePath" --noQuickLoad --renderCameraPath "$usdaPath" --captureOds "$sketchPath"
      exit 0
      ;;
    7) 
      echo "Rendering [Fast,Low Quality] 360 stereo omnidirecitonal stereo 1k x 1k 30fps 360 stereo"
      "$exePath" --preview --renderCameraPath "$usdaPath" --captureOds "$sketchPath"
      exit 0
      ;;
    8) 
      echo "Rendering [Fast,Low Quality] 360 stereo omnidirecitonal stereo 1k x 1k 30fps 360 stereo, no quick load"
      "$exePath" --preview --noQuickLoad --renderCameraPath "$usdaPath" --captureOds "$sketchPath"
      exit 0
      ;;
    *) echo "Invalid selection"; exit 1;;
  esac
done

# Render video based on option
echo "Rendering $fps FPS, $res x $resh"
"$exePath" --renderCameraPath "$usdaPath" --Video.OfflineFPS "$fps" --Video.OfflineResolution "$res" "$sketchPath"
