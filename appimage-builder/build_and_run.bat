@echo off
set IMAGE_NAME=uflauncher-appimage
set CONTAINER_NAME=uflauncher-container

echo Changing to the parent directory...
cd ..

echo Building Docker image...
docker build -t %IMAGE_NAME% -f appimage-builder/Dockerfile .

echo Running Docker container...
docker run --rm -it --privileged --cap-add=SYS_ADMIN -v %CD%:/output %IMAGE_NAME%

echo Copying AppImage to output directory...
copy /Y output\UnrealFrontiersLauncher.AppImage .\

echo Done! Press any key to exit...
pause
