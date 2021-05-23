// Copyright 2021 The Open Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using UnityEngine;
using Node = UnityEngine.XR.XRNode;
using NodeState = UnityEngine.XR.XRNodeState;

#if OCULUS_SUPPORTED
namespace TiltBrush {

public class OculusMRCCameraUpdate : MonoBehaviour {

  private OVRPose? calibratedCameraPose = null;

  void Update() {
    if (!calibratedCameraPose.HasValue) {
      if (!OVRPlugin.Media.GetInitialized()) {
        return;
      }

      OVRPlugin.CameraIntrinsics cameraIntrinsics;
      OVRPlugin.CameraExtrinsics cameraExtrinsics;

      if (OVRPlugin.GetMixedRealityCameraInfo(0, out cameraExtrinsics, out cameraIntrinsics)) {
        calibratedCameraPose = cameraExtrinsics.RelativePose.ToOVRPose();
      } else {
        return;
      }
    }

    OVRPose cameraStagePoseInUnits = calibratedCameraPose.Value;

    // Converting position from meters to decimeters (unit used by Open Brush)
    cameraStagePoseInUnits.position *= App.METERS_TO_UNITS;

    // Workaround to fix the OVRExtensions.ToWorldSpacePose() and 
    // OVRComposition.ComputeCameraWorldSpacePose() calls when computing 
    // the Mixed Reality foreground and background camera positions.
    OVRPose headPose = OVRPose.identity;

    Vector3 pos;
    Quaternion rot;

    if (OVRNodeStateProperties.GetNodeStatePropertyVector3(Node.Head, 
        NodeStatePropertyType.Position, OVRPlugin.Node.Head, OVRPlugin.Step.Render, out pos)) {
      headPose.position = pos;
    }

    if (OVRNodeStateProperties.GetNodeStatePropertyQuaternion(Node.Head, 
        NodeStatePropertyType.Orientation, OVRPlugin.Node.Head, OVRPlugin.Step.Render, out rot)) {
      headPose.orientation = rot;
    }

    OVRPose headPoseInUnits = OVRPose.identity;
    headPoseInUnits.position = headPose.position * App.METERS_TO_UNITS;
    headPoseInUnits.orientation = headPose.orientation;

    OVRPose stageToLocalPose = OVRPlugin.GetTrackingTransformRelativePose(
        OVRPlugin.TrackingOrigin.Stage).ToOVRPose();

    OVRPose stageToLocalPoseInUnits = OVRPose.identity;
    stageToLocalPoseInUnits.position = stageToLocalPose.position * App.METERS_TO_UNITS;
    stageToLocalPoseInUnits.orientation = stageToLocalPose.orientation;

    OVRPose cameraWorldPoseInUnits = headPoseInUnits.Inverse() * stageToLocalPoseInUnits * 
        cameraStagePoseInUnits;
    OVRPose cameraStagePoseFix = stageToLocalPose.Inverse() * headPose * cameraWorldPoseInUnits;

    // Override the MRC camera's stage pose
    OVRPlugin.OverrideExternalCameraStaticPose(0, true, cameraStagePoseFix.ToPosef());        
  }
}
} // namespace TiltBrush

#endif // OCULUS_SUPPORTED
