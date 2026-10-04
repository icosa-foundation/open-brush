# Copyright 2020 Google Inc.
#
# FFMPEG building script for use with Open Brush.
# Runs BuildFfmpeg.sh in an MSYS2 MINGW64 shell, installing the packages it needs first.
# Requires MSYS2 (https://www.msys2.org), by default at C:\msys64.
#
# bin/ffmpeg-arm64 and bin/ffmpeg-x86_64 (macOS) are prebuilt static release builds from
# https://ffmpeg.martin-riedl.de, kept as separate files because a universal binary exceeds
# GitHub's file size limit.

param([string]$Msys2Root = "C:\msys64")

$bash = Join-Path $Msys2Root "usr\bin\bash.exe"
if (-not (Test-Path $bash)) {
    Write-Error "MSYS2 not found at ${Msys2Root}"
    exit 1
}

$env:MSYSTEM = "MINGW64"
$env:CHERE_INVOKING = "1"

$packages = "mingw-w64-x86_64-gcc mingw-w64-x86_64-pkgconf mingw-w64-x86_64-nasm " +
    "mingw-w64-x86_64-x264 mingw-w64-x86_64-x265 mingw-w64-x86_64-zlib make diffutils"
& $bash -lc "pacman -S --needed --noconfirm ${packages}"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& $bash -lc "./BuildFfmpeg.sh"
exit $LASTEXITCODE
