#!/bin/bash
# Copyright 2020 Google Inc.
#
# Builds a static Windows ffmpeg.exe with libx264 and libx265.
#
# Run from an MSYS2 MINGW64 shell (https://www.msys2.org):
#   pacman -S --needed mingw-w64-x86_64-gcc mingw-w64-x86_64-pkgconf mingw-w64-x86_64-nasm \
#     mingw-w64-x86_64-x264 mingw-w64-x86_64-x265 mingw-w64-x86_64-zlib make diffutils
#   ./BuildFfmpeg.sh
#
# Output goes to ./bin/ffmpeg.exe and ./licenses/. Set BUILD_DIR to build elsewhere.
# licenses/x264_COPYING and licenses/x265_COPYING are committed, as the MSYS2 packages don't ship them.

set -euo pipefail

FFMPEG_VERSION="${FFMPEG_VERSION:-9.0.2}"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
BUILD_DIR="${BUILD_DIR:-${SCRIPT_DIR}/build_ffmpeg}"
# A Windows-style path (C:/...) breaks the colon-separated PKG_CONFIG_PATH below.
BUILD_DIR="$(cygpath -u "${BUILD_DIR}")"

mkdir -p "${BUILD_DIR}"
cd "${BUILD_DIR}"

# x265.pc lists -lgcc_s, which would add a libgcc_s DLL dependency to an otherwise static exe.
# The copies keep an absolute prefix, since pkgconf would otherwise relocate it to this directory.
MINGW_PREFIX_WIN="$(cygpath -m /mingw64)"
mkdir -p pkgconfig
sed -e 's/-lgcc_s//g' -e "s|^prefix=.*|prefix=${MINGW_PREFIX_WIN}|" \
  /mingw64/lib/pkgconfig/x265.pc > pkgconfig/x265.pc
sed -e "s|^prefix=.*|prefix=${MINGW_PREFIX_WIN}|" \
  /mingw64/lib/pkgconfig/x264.pc > pkgconfig/x264.pc
export PKG_CONFIG_PATH="${BUILD_DIR}/pkgconfig:/mingw64/lib/pkgconfig"

if [ ! -d "ffmpeg-${FFMPEG_VERSION}" ]; then
  curl -sSL -o "ffmpeg-${FFMPEG_VERSION}.tar.xz" "https://ffmpeg.org/releases/ffmpeg-${FFMPEG_VERSION}.tar.xz"
  tar xf "ffmpeg-${FFMPEG_VERSION}.tar.xz"
fi
cd "ffmpeg-${FFMPEG_VERSION}"

./configure \
  --pkg-config-flags="--static --dont-define-prefix" \
  --extra-ldflags=-static \
  --disable-autodetect \
  --disable-shared --enable-static \
  --disable-doc --disable-ffplay --disable-ffprobe \
  --enable-gpl \
  --enable-zlib \
  --enable-libx264 \
  --enable-libx265
make -j"$(nproc)"
strip ffmpeg.exe

mkdir -p "${SCRIPT_DIR}/bin" "${SCRIPT_DIR}/licenses"
cp ffmpeg.exe "${SCRIPT_DIR}/bin/ffmpeg.exe"
cp COPYING.GPLv2 "${SCRIPT_DIR}/licenses/ffmpeg_COPYING"
cp LICENSE.md "${SCRIPT_DIR}/licenses/ffmpeg_LICENSE.md"

echo "Built ffmpeg ${FFMPEG_VERSION}:"
ldd "${SCRIPT_DIR}/bin/ffmpeg.exe" | grep -vi "/c/windows" || true
