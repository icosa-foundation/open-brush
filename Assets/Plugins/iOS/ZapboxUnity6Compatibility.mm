// Zapbox XR SDK 0.3.1's precompiled GfxPluginZapbox.a references the global
// _skipPresent variable that was supplied by Unity's generated iOS player in
// Unity 2022 and earlier. Unity 6 no longer defines it, which makes Xcode fail
// to link UnityFramework with an undefined __skipPresent Mach-O symbol.
//
// Keep this as a weak definition so a Unity player that still supplies the
// original strong definition takes precedence. On Unity 6 this is only an ABI
// compatibility shim: Unity no longer observes the value. Zapbox should
// ultimately rebuild its native library without relying on this private Unity
// implementation detail.
bool _skipPresent __attribute__((weak)) = false;
