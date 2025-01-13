#!/bin/bash
docker build -t flatpak-builder .
docker run --rm --privileged -v %cd%\output:/app/build-dir flatpak-builder