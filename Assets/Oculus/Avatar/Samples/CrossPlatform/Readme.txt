This CrossPlatform sample scene demonstrates how Oculus Avatars can now be used on non-Oculus devices.

For more detailed information on this sample and cross-platform Avatar functionality, including details on customization and redistribution requirements, see the topic on this sample at: https://developer.oculus.com/documentation/avatarsdk/latest/concepts/avatars-unity-crossplat-sample/

Setup instructions:
1 - Install Steam and SteamVR
2 - Create a new Unity Project
3 - Import the Oculus Integration from the Unity Asset Store.
4 - Open the CrossPlatform scene (in Assets/Oculus/Avatar/Samples/CrossPlatform)
5 - Use the Oculus Dashboard (https://dashboard.oculus.com/) to create a placeholder Rift app and copy the App ID
6 - Paste the App ID in Unity under Oculus Avatars > Edit Configuration > Oculus Rift App Id
7 - Enable OpenVR:
	Open PlayerSettings in the Inspector tab (from menu bar, Edit > Project Settings > Player)	
	Under Virtual Reality SDKs, add OpenVR if it's not there already
	Remove Oculus (or drag it below OpenVR) so that OpenVR is used when the scene is played.
8 - Click Play

The scene should play using SteamVR rather than the Oculus platform, and you'll see 12 different Oculus Avatars in front of you.


