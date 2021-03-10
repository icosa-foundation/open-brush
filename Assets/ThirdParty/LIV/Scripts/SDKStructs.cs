using UnityEngine;
using System.Runtime.InteropServices;
using System;

namespace LIV.SDK.Unity
{
    public struct SDKConstants
    {
        public const string SDK_ID = "LSBAQRQ61KGJX6TX6INTQEWQWVQL5GEN";
        public const string SDK_VERSION = "1.5.4";
        public const string ENGINE_NAME = "unity";
    }

    public enum PRIORITY : sbyte
    {
        NONE = 0,
        GAME = 63
    }

    [System.Flags]
    public enum FEATURES : ulong
    {
        NONE = 0L,
        BACKGROUND_RENDER = 1L,
        FOREGROUND_RENDER = 1L << 1,
        COMPLEX_CLIP_PLANE = 1L << 2,
        BACKGROUND_DEPTH_RENDER = 1L << 3,
        OVERRIDE_POST_PROCESSING = 1L << 4,
        FIX_FOREGROUND_ALPHA = 1L << 5,
        GROUND_CLIP_PLANE = 1L << 6,
        RELEASE_CONTROL = 1L << 15,
        OPTIMIZED_RENDER = 1L << 28,
        INTERLACED_RENDER = 1L << 29,
        DEBUG_CLIP_PLANE = 1L << 48,
    }

    public enum TEXTURE_ID : uint
    {
        UNDEFINED = 0,
        BACKGROUND_COLOR_BUFFER_ID = 10,
        FOREGROUND_COLOR_BUFFER_ID = 20,
        OPTIMIZED_COLOR_BUFFER_ID = 30
    }

    public enum TEXTURE_TYPE : uint
    {
        UNDEFINED = 0,
        COLOR_BUFFER = 1
    }

    public enum TEXTURE_FORMAT : uint
    {
        UNDEFINED = 0,
        ARGB32 = 10
    }

    public enum TEXTURE_DEVICE : uint
    {
        UNDEFINED = 0,
        RAW = 1,
        DIRECTX = 2,
        OPENGL = 3,
        VULKAN = 4,
        METAL = 5
    }

    public enum TEXTURE_COLOR_SPACE : uint
    {
        UNDEFINED = 0,
        LINEAR = 1,
        SRGB = 2,
    }

    public enum RENDERING_PIPELINE : uint
    {
        UNDEFINED = 0,
        FORWARD = 1,
        DEFERRED = 2,
        VERTEX_LIT = 3,
        UNIVERSAL = 4,
        HIGH_DEFINITION = 5
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SDKResolution
    {
        public int width, height;
        public static SDKResolution zero {
            get {
                return new SDKResolution() { width = 0, height = 0 };
            }
        }

        public override string ToString()
        {
            return 
$@"SDKResolution:
width: {width}
height: {height}";
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SDKVector3
    {
        public float x, y, z;
        public static SDKVector3 zero {
            get {
                return new SDKVector3() { x = 0, y = 0, z = 0 };
            }
        }

        public static SDKVector3 one {
            get {
                return new SDKVector3() { x = 1, y = 1, z = 1 };
            }
        }

        public static SDKVector3 forward {
            get {
                return new SDKVector3() { x = 0, y = 0, z = 1 };
            }
        }

        public static SDKVector3 up {
            get {
                return new SDKVector3() { x = 0, y = 1, z = 0 };
            }
        }

        public static SDKVector3 right {
            get {
                return new SDKVector3() { x = 1, y = 0, z = 0 };
            }
        }

        public static implicit operator Vector3(SDKVector3 v)
        {
            return new Vector3(v.x, v.y, v.z);
        }

        public static implicit operator SDKVector3(Vector3 v)
        {
            return new SDKVector3() { x = v.x, y = v.y, z = v.z };
        }

        // Delete begin
        public static SDKVector3 operator +(SDKVector3 lhs, SDKVector3 rhs)
        {
            SDKVector3 res;
            res.x = lhs.x + rhs.x;
            res.y = lhs.y + rhs.y;
            res.z = lhs.z + rhs.z;
            return res;
        }

        public static SDKVector3 operator -(SDKVector3 lhs, SDKVector3 rhs)
        {
            SDKVector3 res;
            res.x = lhs.x - rhs.x;
            res.y = lhs.y - rhs.y;
            res.z = lhs.z - rhs.z;
            return res;
        }

        public static SDKVector3 operator *(SDKVector3 lhs, SDKVector3 rhs)
        {
            SDKVector3 res;
            res.x = lhs.x * rhs.x;
            res.y = lhs.y * rhs.y;
            res.z = lhs.z * rhs.z;
            return res;
        }

        public static SDKVector3 operator *(SDKVector3 lhs, float rhs)
        {
            SDKVector3 res;
            res.x = lhs.x * rhs;
            res.y = lhs.y * rhs;
            res.z = lhs.z * rhs;
            return res;
        }
        // delete end

        public override string ToString()
        {
            return
$@"SDKVector3:
x: {x}
y: {y}
z: {z}";
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SDKQuaternion
    {
        public float x, y, z, w;
        public static SDKQuaternion identity {
            get {
                return new SDKQuaternion() { x = 0, y = 0, z = 0, w = 1.0f };
            }
        }

        public static implicit operator Quaternion(SDKQuaternion v)
        {
            return new Quaternion(v.x, v.y, v.z, v.w);
        }

        public static implicit operator SDKQuaternion(Quaternion v)
        {
            return new SDKQuaternion() { x = v.x, y = v.y, z = v.z, w = v.w };
        }

        // Delete begin
        public static SDKQuaternion Euler(float pitch, float yaw, float roll)
        {
            float rollOver2 = roll * 0.5f;
            float sinRollOver2 = Mathf.Sin(rollOver2);
            float cosRollOver2 = Mathf.Cos(rollOver2);
            float pitchOver2 = pitch * 0.5f;
            float sinPitchOver2 = Mathf.Sin(pitchOver2);
            float cosPitchOver2 = Mathf.Cos(pitchOver2);
            float yawOver2 = yaw * 0.5f;
            float sinYawOver2 = Mathf.Sin(yawOver2);
            float cosYawOver2 = Mathf.Cos(yawOver2);

            var w = cosYawOver2 * cosPitchOver2 * cosRollOver2 + sinYawOver2 * sinPitchOver2 * sinRollOver2;
            var x = cosYawOver2 * sinPitchOver2 * cosRollOver2 + sinYawOver2 * cosPitchOver2 * sinRollOver2;
            var y = sinYawOver2 * cosPitchOver2 * cosRollOver2 - cosYawOver2 * sinPitchOver2 * sinRollOver2;
            var z = cosYawOver2 * cosPitchOver2 * sinRollOver2 - sinYawOver2 * sinPitchOver2 * cosRollOver2;

            return new SDKQuaternion() { x = x, y = y, z = z, w = w };
        }

        public static SDKQuaternion operator *(SDKQuaternion lhs, SDKQuaternion rhs)
        {
            float tx = lhs.w * rhs.x + lhs.x * rhs.w + lhs.y * rhs.z - lhs.z * rhs.y;
            float ty = lhs.w * rhs.y + lhs.y * rhs.w + lhs.z * rhs.x - lhs.x * rhs.z;
            float tz = lhs.w * rhs.z + lhs.z * rhs.w + lhs.x * rhs.y - lhs.y * rhs.x;
            float tw = lhs.w * rhs.w - lhs.x * rhs.x - lhs.y * rhs.y - lhs.z * rhs.z;

            return new SDKQuaternion() { x = tx, y = ty, z = tz, w = tw };
        }

        public static SDKVector3 operator *(SDKQuaternion lhs, SDKVector3 rhs)
        {
            float tx = lhs.x * 2.0f;
            float ty = lhs.y * 2.0f;
            float tz = lhs.z * 2.0f;
            float txx = lhs.x * tx;
            float tyy = lhs.y * ty;
            float tzz = lhs.z * tz;
            float txy = lhs.x * ty;
            float txz = lhs.x * tz;
            float tyz = lhs.y * tz;
            float twx = lhs.w * tx;
            float twy = lhs.w * ty;
            float twz = lhs.w * tz;

            SDKVector3 res;
            res.x = (1.0f - (tyy + tzz)) * rhs.x + (txy - twz) * rhs.y + (txz + twy) * rhs.z;
            res.y = (txy + twz) * rhs.x + (1.0f - (txx + tzz)) * rhs.y + (tyz - twx) * rhs.z;
            res.z = (txz - twy) * rhs.x + (tyz + twx) * rhs.y + (1.0f - (txx + tyy)) * rhs.z;
            return res;
        }
        // Delete end
        public override string ToString()
        {
            return
$@"SDKQuaternion:
x: {x}
y: {y}
z: {z}
w: {w}";
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SDKMatrix4x4
    {
        public float m00, m01, m02, m03,
                        m10, m11, m12, m13,
                        m20, m21, m22, m23,
                        m30, m31, m32, m33;

        public static SDKMatrix4x4 identity {
            get {
                return new SDKMatrix4x4()
                {
                    m00 = 1,
                    m01 = 0,
                    m02 = 0,
                    m03 = 0,
                    m10 = 0,
                    m11 = 1,
                    m12 = 0,
                    m13 = 0,
                    m20 = 0,
                    m21 = 0,
                    m22 = 1,
                    m23 = 0,
                    m30 = 0,
                    m31 = 0,
                    m32 = 0,
                    m33 = 1
                };
            }
        }

        public static implicit operator Matrix4x4(SDKMatrix4x4 v)
        {
            return new Matrix4x4()
            {
                m00 = v.m00,
                m01 = v.m01,
                m02 = v.m02,
                m03 = v.m03,
                m10 = v.m10,
                m11 = v.m11,
                m12 = v.m12,
                m13 = v.m13,
                m20 = v.m20,
                m21 = v.m21,
                m22 = v.m22,
                m23 = v.m23,
                m30 = v.m30,
                m31 = v.m31,
                m32 = v.m32,
                m33 = v.m33
            };
        }

        public static implicit operator SDKMatrix4x4(Matrix4x4 v)
        {
            return new SDKMatrix4x4()
            {
                m00 = v.m00,
                m01 = v.m01,
                m02 = v.m02,
                m03 = v.m03,
                m10 = v.m10,
                m11 = v.m11,
                m12 = v.m12,
                m13 = v.m13,
                m20 = v.m20,
                m21 = v.m21,
                m22 = v.m22,
                m23 = v.m23,
                m30 = v.m30,
                m31 = v.m31,
                m32 = v.m32,
                m33 = v.m33
            };
        }

        public static SDKMatrix4x4 Perspective(float vFov, float aspect, float zNear, float zFar)
        {
            float vFovRad = vFov * Mathf.Deg2Rad;
            float hFovRad = 2.0f * Mathf.Atan(Mathf.Tan(vFovRad * 0.5f) * aspect);
            float w = 1.0f / Mathf.Tan(hFovRad * 0.5f);
            float h = 1.0f / Mathf.Tan(vFovRad * 0.5f);
            float q0 = (zFar + zNear) / (zNear - zFar);
            float q1 = 2.0f * (zFar * zNear) / (zNear - zFar);

            return new SDKMatrix4x4()
            {
                m00 = w,
                m01 = 0,
                m02 = 0,
                m03 = 0,
                m10 = 0,
                m11 = h,
                m12 = 0,
                m13 = 0,
                m20 = 0,
                m21 = 0,
                m22 = q0,
                m23 = q1,
                m30 = 0,
                m31 = 0,
                m32 = -1,
                m33 = 0
            };
        }

        // begin delete
        public static SDKMatrix4x4 operator *(SDKMatrix4x4 lhs, SDKMatrix4x4 rhs)
        {
            SDKMatrix4x4 res = SDKMatrix4x4.identity;

            res.m00 = lhs.m00 * rhs.m00 + lhs.m01 * rhs.m10 + lhs.m02 * rhs.m20 + lhs.m03 * rhs.m30;
            res.m01 = lhs.m00 * rhs.m01 + lhs.m01 * rhs.m11 + lhs.m02 * rhs.m21 + lhs.m03 * rhs.m31;
            res.m02 = lhs.m00 * rhs.m02 + lhs.m01 * rhs.m12 + lhs.m02 * rhs.m22 + lhs.m03 * rhs.m32;
            res.m03 = lhs.m00 * rhs.m03 + lhs.m01 * rhs.m13 + lhs.m02 * rhs.m23 + lhs.m03 * rhs.m33;

            res.m10 = lhs.m10 * rhs.m00 + lhs.m11 * rhs.m10 + lhs.m12 * rhs.m20 + lhs.m13 * rhs.m30;
            res.m11 = lhs.m10 * rhs.m01 + lhs.m11 * rhs.m11 + lhs.m12 * rhs.m21 + lhs.m13 * rhs.m31;
            res.m12 = lhs.m10 * rhs.m02 + lhs.m11 * rhs.m12 + lhs.m12 * rhs.m22 + lhs.m13 * rhs.m32;
            res.m13 = lhs.m10 * rhs.m03 + lhs.m11 * rhs.m13 + lhs.m12 * rhs.m23 + lhs.m13 * rhs.m33;

            res.m20 = lhs.m20 * rhs.m00 + lhs.m21 * rhs.m10 + lhs.m22 * rhs.m20 + lhs.m23 * rhs.m30;
            res.m21 = lhs.m20 * rhs.m01 + lhs.m21 * rhs.m11 + lhs.m22 * rhs.m21 + lhs.m23 * rhs.m31;
            res.m22 = lhs.m20 * rhs.m02 + lhs.m21 * rhs.m12 + lhs.m22 * rhs.m22 + lhs.m23 * rhs.m32;
            res.m23 = lhs.m20 * rhs.m03 + lhs.m21 * rhs.m13 + lhs.m22 * rhs.m23 + lhs.m23 * rhs.m33;

            res.m30 = lhs.m30 * rhs.m00 + lhs.m31 * rhs.m10 + lhs.m32 * rhs.m20 + lhs.m33 * rhs.m30;
            res.m31 = lhs.m30 * rhs.m01 + lhs.m31 * rhs.m11 + lhs.m32 * rhs.m21 + lhs.m33 * rhs.m31;
            res.m32 = lhs.m30 * rhs.m02 + lhs.m31 * rhs.m12 + lhs.m32 * rhs.m22 + lhs.m33 * rhs.m32;
            res.m33 = lhs.m30 * rhs.m03 + lhs.m31 * rhs.m13 + lhs.m32 * rhs.m23 + lhs.m33 * rhs.m33;
            return res;
        }

        public static SDKVector3 operator *(SDKMatrix4x4 lhs, SDKVector3 rhs)
        {
            SDKVector3 res;
            res.x = lhs.m00 * rhs.x + lhs.m01 * rhs.y + lhs.m02 * rhs.z;
            res.y = lhs.m10 * rhs.x + lhs.m11 * rhs.y + lhs.m12 * rhs.z;
            res.z = lhs.m20 * rhs.x + lhs.m21 * rhs.y + lhs.m22 * rhs.z;
            return res;
        }

        // Creates a translation matrix.
        public static SDKMatrix4x4 Translate(SDKVector3 value)
        {
            return new SDKMatrix4x4
            {
                m00 = 1.0f,
                m01 = 0.0f,
                m02 = 0.0f,
                m03 = value.x,
                m10 = 0.0f,
                m11 = 1.0f,
                m12 = 0.0f,
                m13 = value.y,
                m20 = 0.0f,
                m21 = 0.0f,
                m22 = 1.0f,
                m23 = value.z,
                m30 = 0.0f,
                m31 = 0.0f,
                m32 = 0.0f,
                m33 = 1.0f
            };
        }

        // Creates a rotation matrix.
        public static SDKMatrix4x4 Rotate(SDKQuaternion value)
        {
            float qx = value.x;
            float qy = value.y;
            float qz = value.z;
            float qw = value.w;

            return new SDKMatrix4x4
            {
                m00 = 1.0f - 2.0f * qy * qy - 2.0f * qz * qz,
                m01 = 2.0f * qx * qy - 2.0f * qz * qw,
                m02 = 2.0f * qx * qz + 2.0f * qy * qw,
                m03 = 0.0f,
                m10 = 2.0f * qx * qy + 2.0f * qz * qw,
                m11 = 1.0f - 2.0f * qx * qx - 2.0f * qz * qz,
                m12 = 2.0f * qy * qz - 2.0f * qx * qw,
                m13 = 0.0f,
                m20 = 2.0f * qx * qz - 2.0f * qy * qw,
                m21 = 2.0f * qy * qz + 2.0f * qx * qw,
                m22 = 1.0f - 2.0f * qx * qx - 2.0f * qy * qy,
                m23 = 0.0f,
                m30 = 0.0f,
                m31 = 0.0f,
                m32 = 0.0f,
                m33 = 1.0f
            };
        }

        // Creates a scaling matrix.
        public static SDKMatrix4x4 Scale(SDKVector3 value)
        {
            return new SDKMatrix4x4
            {
                m00 = value.x,
                m01 = 0.0f,
                m02 = 0.0f,
                m03 = 0.0f,
                m10 = 0.0f,
                m11 = value.y,
                m12 = 0.0f,
                m13 = 0.0f,
                m20 = 0.0f,
                m21 = 0.0f,
                m22 = value.z,
                m23 = 0.0f,
                m30 = 0.0f,
                m31 = 0.0f,
                m32 = 0.0f,
                m33 = 1.0f
            };
        }

        public static SDKMatrix4x4 TRS(SDKVector3 translation, SDKQuaternion rotation, SDKVector3 scale)
        {
            return Translate(translation) * Rotate(rotation) * Scale(scale);
        }
        // end delete

        public override string ToString()
        {
            return
$@"Matrix4x4:
{m00} {m01} {m02} {m03}
{m10} {m11} {m12} {m13}
{m20} {m21} {m22} {m23}
{m30} {m31} {m32} {m33}";
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SDKPlane 
    {
        public float distance;
        public SDKVector3 normal;

        public static implicit operator SDKPlane(Plane v)
        {
            return new SDKPlane()
            {
                distance = v.distance,
                normal = v.normal
            };
        }

        public static SDKPlane empty {
            get {
                return new SDKPlane() { distance = 0f, normal = SDKVector3.up };
            }
        }

        public override string ToString()
        {
            return
$@"SDKPlane:
{distance} {normal}";
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SDKPriority
    {
        public sbyte pose;
        public sbyte clipPlane;
        public sbyte stage;
        public sbyte resolution;
        public sbyte feature;
        public sbyte nearFarAdjustment;
        public sbyte groundPlane;
        public sbyte reserved2;

        public static SDKPriority empty {
            get {
                return new SDKPriority()
                {
                    pose = -(sbyte)PRIORITY.GAME,
                    clipPlane = -(sbyte)PRIORITY.GAME,
                    stage = -(sbyte)PRIORITY.GAME,
                    resolution = -(sbyte)PRIORITY.GAME,
                    feature = -(sbyte)PRIORITY.GAME,
                    nearFarAdjustment = (sbyte)PRIORITY.GAME,
                    groundPlane = -(sbyte)PRIORITY.GAME,
                    reserved2 = -(sbyte)PRIORITY.GAME
                };
            }
        }

        public override string ToString()
        {
            return
$@"Priority:
pose: {pose}, clipPlane: {clipPlane}, stage: {stage}, resolution: {resolution}, feature: {feature}, nearFarAdjustment: {nearFarAdjustment}, groundPlane: {groundPlane}";
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SDKApplicationOutput
    {
        public FEATURES supportedFeatures;
        public string engineName;
        public string engineVersion;
        public string applicationName;
        public string applicationVersion;
        public string xrDeviceName;
        public string graphicsAPI;
        public string sdkID;
        public string sdkVersion;

        public static SDKApplicationOutput empty {
            get {
                return new SDKApplicationOutput()
                {
                    supportedFeatures = FEATURES.NONE,
                    engineName = string.Empty,
                    engineVersion = string.Empty,
                    applicationName = string.Empty,
                    applicationVersion = string.Empty,
                    xrDeviceName = string.Empty,
                    graphicsAPI = string.Empty,
                    sdkID = SDKConstants.SDK_ID,
                    sdkVersion = string.Empty
                };
            }
        }

        public override string ToString()
        {
            return
$@"SDKApplicationOutput:
supportedFeatures: {supportedFeatures}
engineName: {engineName}
engineVersion: {engineVersion}
applicationName: {applicationName}
applicationVersion: {applicationVersion}
xrDeviceName: {xrDeviceName}
graphicsAPI: {graphicsAPI}
sdkID: {sdkID}
sdkVersion: {sdkVersion}";
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SDKInputFrame
    {
        public SDKPose pose;
        public SDKClipPlane clipPlane;
        public SDKTransform stageTransform;
        public FEATURES features;
        public SDKClipPlane groundClipPlane;

        public ulong frameid; // This is actually the time stamp of this frame - its populated by the bridge at creation time.
        public ulong referenceframe; // Use the previous frames frameid to populate this field - it must be set to the correct frame id, or it will fail. 
        public SDKPriority priority; // this is a mixed field combining flags and priority - the contents of this flag are not yet set in stone            

        public static SDKInputFrame empty {
            get {
                return new SDKInputFrame()
                {
                    pose = SDKPose.empty,
                    clipPlane = SDKClipPlane.empty,
                    stageTransform = SDKTransform.empty,
                    features = FEATURES.NONE,
                    groundClipPlane = SDKClipPlane.empty,
                    frameid = 0,
                    referenceframe = 0,
                    priority = SDKPriority.empty
                };
            }
        }

        public void ReleaseControl()
        {
            priority = SDKPriority.empty;
        }

        public void ObtainControl()
        {
            priority = SDKPriority.empty;
            priority.pose = (sbyte)PRIORITY.GAME;
        }

        public override string ToString()
        {
            return
$@"SDKInputFrame:
pose: {pose}
clipPlane: {clipPlane}
stageTransform: {stageTransform}
features: {features}
groundClipPlane: {groundClipPlane}
frameid: {frameid}
referenceframe: {referenceframe}
priority: {priority:X4}";
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SDKOutputFrame
    {
        public RENDERING_PIPELINE renderingPipeline;
        public SDKTrackedSpace trackedSpace;

        public static SDKOutputFrame empty {
            get {
                return new SDKOutputFrame()
                {
                    renderingPipeline = RENDERING_PIPELINE.UNDEFINED,
                    trackedSpace = SDKTrackedSpace.empty
                };
            }
        }

        public override string ToString()
        {
            return
$@"SDKOutputFrame:
renderingPipeline: {renderingPipeline}
trackedSpace: {trackedSpace}";
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SDKTrackedSpace
    {
        public SDKVector3 trackedSpaceWorldPosition;
        public SDKQuaternion trackedSpaceWorldRotation;
        public SDKVector3 trackedSpaceLocalScale;
        public SDKMatrix4x4 trackedSpaceLocalToWorldMatrix;
        public SDKMatrix4x4 trackedSpaceWorldToLocalMatrix;

        public static SDKTrackedSpace empty {
            get {
                return new SDKTrackedSpace()
                {
                    trackedSpaceWorldPosition = SDKVector3.zero,
                    trackedSpaceWorldRotation = SDKQuaternion.identity,
                    trackedSpaceLocalScale = SDKVector3.zero,
                    trackedSpaceLocalToWorldMatrix = SDKMatrix4x4.identity,
                    trackedSpaceWorldToLocalMatrix = SDKMatrix4x4.identity
                };
            }
        }

        public override string ToString()
        {
            return
$@"SDKTrackedSpace:
trackedSpaceWorldPosition: {trackedSpaceWorldPosition}
trackedSpaceWorldRotation: {trackedSpaceWorldRotation}
trackedSpaceLocalScale: {trackedSpaceLocalScale}
trackedSpaceLocalToWorldMatrix: {trackedSpaceLocalToWorldMatrix}
trackedSpaceWorldToLocalMatrix: {trackedSpaceWorldToLocalMatrix}";
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SDKTexture
    {
        public TEXTURE_ID id;
        public System.IntPtr texturePtr;
        public System.IntPtr SharedHandle;
        public TEXTURE_DEVICE device;
        public int dummy;
        public TEXTURE_TYPE type;
        public TEXTURE_FORMAT format;
        public TEXTURE_COLOR_SPACE colorSpace;
        public int width;
        public int height;

        public static SDKTexture empty {
            get {
                return new SDKTexture()
                {
                    id = TEXTURE_ID.UNDEFINED,
                    texturePtr = System.IntPtr.Zero,
                    SharedHandle = System.IntPtr.Zero,
                    device = TEXTURE_DEVICE.UNDEFINED,
                    dummy = 0,
                    type = TEXTURE_TYPE.UNDEFINED,
                    format = TEXTURE_FORMAT.UNDEFINED,
                    colorSpace = TEXTURE_COLOR_SPACE.UNDEFINED,
                    width = 0,
                    height = 0
                };
            }
        }

        public override string ToString()
        {
            return
$@"SDKTexture:
id: {id}
texturePtr: {texturePtr}
SharedHandle: {SharedHandle}
device: {device}
dummy: {dummy}
type: {type}
format: {format}
colorSpace: {colorSpace}
width: {width}
height: {height}";
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SDKTransform
    {
        public SDKVector3 localPosition;
        public SDKQuaternion localRotation;
        public SDKVector3 localScale;

        public static SDKTransform empty {
            get {
                return new SDKTransform()
                {
                    localPosition = SDKVector3.zero,
                    localRotation = SDKQuaternion.identity,
                    localScale = SDKVector3.one
                };
            }
        }

        public override string ToString()
        {
            return
$@"SDKTransform:
localPosition: {localPosition}
localRotation: {localRotation}
localScale: {localScale}";
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SDKClipPlane
    {
        public SDKMatrix4x4 transform;
        public int width;
        public int height;
        public float tesselation;

        public static SDKClipPlane empty {
            get {
                return new SDKClipPlane()
                {
                    transform = SDKMatrix4x4.identity,
                    width = 0,
                    height = 0,
                    tesselation = 0
                };
            }
        }

        public override string ToString()
        {
            return
$@"SDKClipPlane:
transform: {transform}
width: {width}
height: {height}
tesselation: {tesselation}";
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SDKControllerState
    {
        public SDKVector3 hmdposition;
        public SDKQuaternion hmdrotation;

        public SDKVector3 calibrationcameraposition;
        public SDKQuaternion calibrationcamerarotation;

        public SDKVector3 cameraposition;
        public SDKQuaternion camerarotation;

        public SDKVector3 leftposition;
        public SDKQuaternion leftrotation;

        public SDKVector3 rightposition;
        public SDKQuaternion rightrotation;

        public static SDKControllerState empty {
            get {
                return new SDKControllerState()
                {
                    hmdposition = SDKVector3.zero,
                    hmdrotation = SDKQuaternion.identity,
                    calibrationcameraposition = SDKVector3.zero,
                    calibrationcamerarotation = SDKQuaternion.identity,
                    cameraposition = SDKVector3.zero,
                    camerarotation = SDKQuaternion.identity,
                    leftposition = SDKVector3.zero,
                    leftrotation = SDKQuaternion.identity,
                    rightposition = SDKVector3.zero,
                    rightrotation = SDKQuaternion.identity,
                };
            }
        }

        public override string ToString()
        {
            return
$@"SDKControllerState:
hmdposition: {hmdposition}
hmdrotation: {hmdrotation}
calibrationcameraposition: {calibrationcameraposition}
calibrationcamerarotation: {calibrationcamerarotation}
cameraposition: {cameraposition}
camerarotation: {camerarotation}
leftposition: {leftposition}
leftrotation: {leftrotation}
rightposition: {rightposition}
rightrotation: {rightrotation}";
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SDKPose
    {
        public SDKMatrix4x4 projectionMatrix;
        public SDKVector3 localPosition;
        public SDKQuaternion localRotation;
        public float verticalFieldOfView;
        public float nearClipPlane;
        public float farClipPlane;
        public int unused0;
        public int unused1;

        public static SDKPose empty {
            get {
                return new SDKPose()
                {
                    projectionMatrix = SDKMatrix4x4.Perspective(90f, 1f, 0.01f, 1000f),
                    localPosition = SDKVector3.zero,
                    localRotation = SDKQuaternion.identity,                    
                    verticalFieldOfView = 90f,
                    nearClipPlane = 0.01f,
                    farClipPlane = 1000f,
                };
            }
        }

        public override string ToString()
        {
            return
$@"SDKPose:
projectionMatrix: {projectionMatrix}
localPosition: {localPosition}
localRotation: {localRotation}
verticalFieldOfView: {verticalFieldOfView}
nearClipPlane: {nearClipPlane}
farClipPlane: {farClipPlane}";
        }
    }
}