The SocialStarter sample shows how to leverage Oculus Avatar and Oculus Platform features to make a very basic networked social experience.

For full functionality, this sample requires the following:
1. An App ID from the Oculus Dashboard (https://dashboard.oculus.com/).
2. A build associated with the App ID must be uploaded to a release channel on the Dashboard.
3. The email associated with your Oculus account must be added to the Users for the release channel of the build.


Setup instructions:
1. Create a new Unity project.
2. Import the Oculus Integration from the Unity Asset Store. The Oculus Integration contains everything you need to use Oculus Avatar and Oculus Platforms.
3. From the Oculus Dashboard, create an Oculus Rift app and copy the App ID. An App ID is required to use Avatar and Platform features. There must be a build associated with the App ID uploaded to a release channel on the Dashboard for full functionality. The email address associated with your Oculus account must be added to the Users list of the release channel.
4. From the Unity Editor menu bar, select Oculus Avatars > Edit Configuration and place your App ID in the two fields for Oculus Rift App Id and Gear VR App Id.
5. From the Unity Editor menu bar, select Oculus Platform > Edit Settings and place your App ID in the two fields for Oculus Rift App Id and Gear VR App Id.
6. Make sure the prefabs are set correctly. Select the OVRPlayerController object in the scene:
a) Local Avatar Prefab should be set to the LocalAvatar (OvrAvatar) prefab found at Assets > Oculus > Avatar > Content > Prefabs.
b) Remote Avatar Prefab should be set to the RemoteAvatar (OvrAvatar) prefab found at Assets > Oculus > Avatar > Content > Prefabs.
c) From Assets/Oculus/Avatar/Samples/SocialStarter/Assets/Materials, drag Help to the OVRPlayerController’s Rift Material property.
d) From Assets/Oculus/Avatar/Samples/SocialStarter/Assets/Materials, drag GearHelp to the OVRPlayerController’s Gear Material property.


How to use:
1. When you first start up the sample you are placed in a virtual room.  In the virtual room, the color of the floor and sphere are used as indicators.
a) The floor color indicates whether you are the owner of the room. Blue means you are the owner of the room. Green means you are a member of the room that joined via an invitation from the owner.
b) The sphere color indicates whether you are in an online room. White means you are in an online room. Black means that either online room creation or an invite attempt failed for some reason.
2. Your left hand should be holding the instructions UI. If you do not see the instructions, please make sure you followed steps 6c and 6d above. The instructions are as follows:

Rift
Click left stick: Toggle showing the instructions.
Button Y: Toggle the sky camera. This allows you to view the scene from a static third-person camera.
Button X: Room invites. This will bring up the invite UI, which will show a list of your friends that you can invite to the room. This may take a second or two to pop up. Note that this functionality only works if you have uploaded a build to the Dashboard for the App ID you used.
Left stick: Move around.
Right stick: Rotate direction.

Oculus Go and Gear VR
Trigger: Toggle showing the instructions.
Touchpad click: Toggle the sky camera.
Back button: Room invites.
Touchpad: Move around.

3. When a user joins your room, a VoIP connection and a P2P connection will be set up. The P2P connection is used to send Avatar and positional updates. Note that this functionality only works if you have uploaded a build to the Dashboard for the App ID you used.
