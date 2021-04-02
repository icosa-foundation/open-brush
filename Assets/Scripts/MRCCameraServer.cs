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
using System;
using System.Collections; 
using System.Collections.Generic;
using System.Net; 
using System.Net.Sockets; 
using System.Text; 
using System.Threading;
// using System.Diagnostics;
using System.Runtime.InteropServices;

using Node = UnityEngine.XR.XRNode;
using NodeState = UnityEngine.XR.XRNodeState;

#if OCULUS_SUPPORTED

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct CameraPayload {
    public uint protocolIdentifier;
    public float px;
    public float py;
    public float pz;
    public float qx;
    public float qy;
    public float qz;
    public float qw;

    public const int StructSize = sizeof(uint) + 7 * sizeof(float);
    public const uint identifier = 13371337;

    public static CameraPayload FromBytes(byte[] arr) {
        CameraPayload payload = new CameraPayload();

        int size = Marshal.SizeOf(payload);
        // Trace.Assert(size == StructSize);

        IntPtr ptr = Marshal.AllocHGlobal(size);

        Marshal.Copy(arr, 0, ptr, size);

        payload = (CameraPayload)Marshal.PtrToStructure(ptr, payload.GetType());
        Marshal.FreeHGlobal(ptr);

        return payload;
    }

    public OVRPose ToPose() {
        OVRPose result = new OVRPose();
        result.position = new Vector3(px, py, pz);
        result.orientation = new Quaternion(qx, qy, qz, qw);
        return result.flipZ();
    }
}

public class MRCCameraServer: MonoBehaviour {

    private TcpListener tcpListener; 
    private Thread tcpListenerThread; 
    private TcpClient connectedTcpClient;
    
    private OVRPose? calibratedCameraPose = null;

    private OVRPose cameraPose = OVRPose.identity;

    public void Start() {
        tcpListenerThread = new Thread (new ThreadStart(ListenForIncomingRequests));         
        tcpListenerThread.IsBackground = true;         
        tcpListenerThread.Start();     
    }

    public void Update() {
        if (!calibratedCameraPose.HasValue) {
            if (!OVRPlugin.Media.GetInitialized())
                return;

            OVRPlugin.CameraIntrinsics cameraIntrinsics;
            OVRPlugin.CameraExtrinsics cameraExtrinsics;

            if (OVRPlugin.GetMixedRealityCameraInfo(0, out cameraExtrinsics, out cameraIntrinsics)) {
                calibratedCameraPose = cameraExtrinsics.RelativePose.ToOVRPose();
            } else {
                return;
            }
        }

        // The receivedCameraPose is relative to the original calibrated pose, which is itself expressed in stage space.
        OVRPose cameraStagePoseInUnits = calibratedCameraPose.Value * cameraPose;

        // Converting position from meters to decimeters (unit used by Open Brush)
        cameraStagePoseInUnits.position *= TiltBrush.App.METERS_TO_UNITS;

        // Workaround to fix the OVRExtensions.ToWorldSpacePose() and OVRComposition.ComputeCameraWorldSpacePose()
        // when computing the Mixed Reality foreground and background camera positions.
        OVRPose headPose = OVRPose.identity;

        Vector3 pos;
        Quaternion rot;
        if (OVRNodeStateProperties.GetNodeStatePropertyVector3(Node.Head, NodeStatePropertyType.Position, OVRPlugin.Node.Head, OVRPlugin.Step.Render, out pos))
            headPose.position = pos;
        if (OVRNodeStateProperties.GetNodeStatePropertyQuaternion(Node.Head, NodeStatePropertyType.Orientation, OVRPlugin.Node.Head, OVRPlugin.Step.Render, out rot))
            headPose.orientation = rot;

        OVRPose headPoseInUnits = OVRPose.identity;
        headPoseInUnits.position = headPose.position * TiltBrush.App.METERS_TO_UNITS;
        headPoseInUnits.orientation = headPose.orientation;

        OVRPose stageToLocalPose = OVRPlugin.GetTrackingTransformRelativePose(OVRPlugin.TrackingOrigin.Stage).ToOVRPose();
        OVRPose stageToLocalPoseInUnits = OVRPose.identity;
        stageToLocalPoseInUnits.position = stageToLocalPose.position * TiltBrush.App.METERS_TO_UNITS;
        stageToLocalPoseInUnits.orientation = stageToLocalPose.orientation;

        OVRPose cameraWorldPoseInUnits = headPoseInUnits.Inverse() * stageToLocalPoseInUnits * cameraStagePoseInUnits;
        OVRPose cameraStagePoseFix = stageToLocalPose.Inverse() * headPose * cameraWorldPoseInUnits;

        // Override the MRC camera's stage pose
        OVRPlugin.OverrideExternalCameraStaticPose(0, true, cameraStagePoseFix.ToPosef());
    }  

    public const int MaxBufferLength = 65536;
    private void ListenForIncomingRequests () {         
        try {                        
            tcpListener = new TcpListener(IPAddress.Any, 1337);             
            tcpListener.Start();              
            Debug.Log("[CAMERA SERVER] Server is listening");              
            
            byte[][] receivedBuffers = { new byte[MRCCameraServer.MaxBufferLength], new byte[MRCCameraServer.MaxBufferLength] };
            int receivedBufferIndex = 0;
            int receivedBufferDataSize = 0;

            while (true) {                 
                using (connectedTcpClient = tcpListener.AcceptTcpClient()) {

                    using (NetworkStream stream = connectedTcpClient.GetStream()) {                         
                        int length; 

                        int maximumDataSize = MRCCameraServer.MaxBufferLength - receivedBufferDataSize;

                        while ((length = stream.Read(receivedBuffers[receivedBufferIndex], receivedBufferDataSize, maximumDataSize)) != 0) {                         

                            receivedBufferDataSize += length;

                            while (receivedBufferDataSize >= CameraPayload.StructSize) {
                                CameraPayload payload = CameraPayload.FromBytes(receivedBuffers[receivedBufferIndex]);

                                if (payload.protocolIdentifier != CameraPayload.identifier) {
                                    Debug.LogWarning("Header mismatch");
                                    stream.Close();
                                    connectedTcpClient.Close();
                                    return;
                                }

                                // Consider adding a lock
                                cameraPose = payload.ToPose();

                                int newBufferIndex = 1 - receivedBufferIndex;
                                int newBufferDataSize = receivedBufferDataSize - CameraPayload.StructSize;

                                if (newBufferDataSize > 0) {
                                    Array.Copy(receivedBuffers[receivedBufferIndex], CameraPayload.StructSize, receivedBuffers[newBufferIndex], 0, newBufferDataSize);
                                }
                                receivedBufferIndex = newBufferIndex;
                                receivedBufferDataSize = newBufferDataSize;
                                maximumDataSize = MRCCameraServer.MaxBufferLength - receivedBufferDataSize;
                            }                    
                        }                                             
                    }                 
                }             
            }         
        }         
        catch (SocketException socketException) {             
            Debug.Log("SocketException " + socketException.ToString());         
        }     
    }
}

#endif
