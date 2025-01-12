#!/bin/bash
docker build -t flatpak-builder .
docker run --rm -v %cd%\output:/app/build-dir flatpak-builder