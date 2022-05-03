How To build Stereo180Video Sample for Android

Step 1: Copy Assets/Oculus/SampleFramework/Usage/Stereo180Video/StreamingAssets to Assets/StreamingAssets
Step 2: Rename Assets/Oculus/SampleFramework/Usage/Stereo180Video/Plugins/Android/java/com/oculus/videoplayer/MyVideoPlayer.java.removeme to MyVideoPlayer.java
Step 3: For the audio360 and audio360-exo28 plugins in Assets/Oculus/SampleFramework/Usage/Stereo180Video/Plugins/Android/Audio360/, make sure the Android checkbox is checked.
Step 4: Open PlayerSettings, in the "Publishing Settings" section, change the Build System to Gradle. If you don't already have a custom gradle template, check the "Custom Gradle Template" checkbox.
Step 5: Open Assets\Plugins\mainTemplate.gradle. Take a look at the version number for `com.android.tools.build:gradle`. If this number is 2.3.0, continue to Step 6a. If it is 3.2.0 or greater skip to Step 6b.
Step 6a:
	Make the following modifications to mainTemplate.gradle:
		In buildscript {repositories { ... } } add `google()`
		In allprojects {repositories { ... } } add `google()` and `jcenter()`
		In dependencies { ... } add `compile 'com.google.android.exoplayer:exoplayer:2.8.4'`
		In android { ... } add `sourceSets.main.java.srcDir "$projectDir/../../Assets/Oculus/SampleFramework/Usage/Stereo180Video/Plugins/Android/java"`

	You should now be able to build and run the Sample. Skip Step 6b.
Step 6b:
	Make the following modifications to mainTemplate.gradle:
		In dependencies { ... } add `implementation 'com.google.android.exoplayer:exoplayer:2.8.4'`
	If you are using an earlier version of Unity than 2018.2, also make the following modification to mainTemplate.gradle:
		In android { ... } add `sourceSets.main.java.srcDir "$projectDir/../../Assets/Oculus/SampleFramework/Usage/Stereo180Video/Plugins/Android/java"`

	You should now be able to build and run the Sample.