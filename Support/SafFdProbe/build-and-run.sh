#!/usr/bin/env bash
# Builds and runs the standalone SAF file-descriptor probe on a connected device.
#
# This validates the provider-side assumptions behind the fd-backed SAF storage
# design (see google-play-saf-fd-backed-storage-plan.md) without building Open
# Brush. It takes seconds rather than a full Unity Android build, so it is the
# cheap way to re-check a new device, OS version, or Documents provider.
#
# It does NOT cover the IL2CPP/.NET half of the probe -- SafeFileHandle over a
# detached descriptor. For that, run AndroidSafStorage.RunFileDescriptorProbe
# from a real Google Play build.
#
# Usage: ./build-and-run.sh [path/to/android/sdk] [path/to/jdk]

set -euo pipefail
cd "$(dirname "$0")"

UNITY_ANDROID="/Applications/Unity/Hub/Editor/6000.6.0f1/PlaybackEngines/AndroidPlayer"
SDK="${1:-${ANDROID_SDK_ROOT:-${ANDROID_HOME:-$UNITY_ANDROID/SDK}}}"
JDK="${2:-${JAVA_HOME:-$UNITY_ANDROID/OpenJDK}}"

BT="$(ls -d "$SDK"/build-tools/* | sort -V | tail -1)"
AJ="$(ls "$SDK"/platforms/android-*/android.jar | sort -V | tail -1)"
echo "sdk=$SDK"
echo "build-tools=$BT"
echo "android.jar=$AJ"

rm -rf out && mkdir -p out/classes
"$BT/aapt2" compile --dir res -o out/res.zip
"$BT/aapt2" link -o out/base.apk -I "$AJ" --manifest AndroidManifest.xml \
    --min-sdk-version 29 --target-sdk-version 36 out/res.zip
"$JDK/bin/javac" -nowarn -cp "$AJ" -d out/classes \
    src/com/openbrush/fdprobe/ProbeActivity.java
"$BT/d8" --min-api 29 --lib "$AJ" --output out $(find out/classes -name '*.class')
cd out
cp base.apk unsigned.apk && zip -qj unsigned.apk classes.dex
"$JDK/bin/keytool" -genkeypair -keystore debug.ks -storepass android \
    -keypass android -alias d -keyalg RSA -keysize 2048 -validity 3650 \
    -dname "CN=probe" 2>/dev/null
"$BT/zipalign" -f 4 unsigned.apk aligned.apk
"$BT/apksigner" sign --ks debug.ks --ks-pass pass:android \
    --key-pass pass:android --out probe.apk aligned.apk
cd ..

adb install -r out/probe.apk
adb logcat -c
adb shell am start -n com.openbrush.fdprobe/.ProbeActivity

echo
echo "Pick a folder on the device (Documents is representative), then Allow."
echo "Waiting for results..."
for _ in $(seq 1 60); do
    if adb logcat -d -s OBFDPROBE 2>/dev/null | grep -q "probe complete"; then break; fi
    sleep 2
done
echo
adb logcat -d -s OBFDPROBE | sed 's/^.*OBFDPROBE *: *//' | grep -v '^---------'
echo
echo "Uninstalling probe..."
adb uninstall com.openbrush.fdprobe >/dev/null
