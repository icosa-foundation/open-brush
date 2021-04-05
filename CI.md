# CI Documentation

This file will explain the various secrets used in creating formal builds. All of this is currently enabled on the icosa-gallery/open-brush repo, but in case a new repo ever needs to be created, this file explains what needs to be done to configure the repo.

By default, builds can be created with no secrets required, and this in fact what happens for builds from pull requests, where secrets are not available for security reasons.

## Unity Professional License

If you have a Unity Pro license, three secrets should be created: `UNITY_EMAIL`, `UNITY_PASSWORD`, and `UNITY_SERIAL`. If defined, these will be used instead of the default `UNITY_LICENSE` embedded in the github actions workflow, which is a free license. Using a professional license will suppress the splash screen.

## Google & Sketchfab Integration

As described in the main README, a Secrets.asset file should be used to store credentials for authenticating with Google and Sketchfab. For CI builds, create this file using the instructions in the README, and the add two Github secrets named `SECRETS_ASSET` and `SECRETS_ASSET_META` with the contents of Assets/Secrets.asset and Assets/Secrets.asset.meta respectively.

The CI build will automatically enable the use of this file instead of the default SecretsExample.asset in the repo. Do not attempt to commit your Secrets.asset file to source control.

## Signed Android Builds

When building Oculus Android builds, you can use your own keystore instead of the default "Debug" signing keys. This will allow you to upgrade your application, instead of needing to uninstall and reinstall it each time to avoid Signature Mismatch errors. To do this, create a keystore, and a key, and define the following secrets: `ANDROID_KEYSTORE_PASS`, `ANDROID_KEYALIAS_NAME`, and `ANDROID_KEYALIAS_PASS`. The keystore itself should be converted to base64 and stored in a secret named `ANDROID_KEYSTORE_BASE64`; you can get the value to store in the secret by using `base64 -i YOUR_KEYSTORE_NAME.keystore`.
