/// icosa-viewer.module.js v251218

import * as $hBQxr$three from "three";
import {DRACOLoader as $hBQxr$DRACOLoader} from "three/examples/jsm/loaders/DRACOLoader.js";
import {GLTFLoader as $hBQxr$GLTFLoader} from "three/examples/jsm/loaders/GLTFLoader.js";
import {OBJLoader as $hBQxr$OBJLoader} from "three/examples/jsm/loaders/OBJLoader.js";
import {MTLLoader as $hBQxr$MTLLoader} from "three/examples/jsm/loaders/MTLLoader.js";
import {FBXLoader as $hBQxr$FBXLoader} from "three/examples/jsm/loaders/FBXLoader.js";
import {PLYLoader as $hBQxr$PLYLoader} from "three/examples/jsm/loaders/PLYLoader.js";
import {STLLoader as $hBQxr$STLLoader} from "three/examples/jsm/loaders/STLLoader.js";
import {USDZLoader as $hBQxr$USDZLoader} from "three/examples/jsm/loaders/USDZLoader.js";
import {VOXLoader as $hBQxr$VOXLoader, VOXMesh as $hBQxr$VOXMesh} from "three/examples/jsm/loaders/VOXLoader.js";
import {GLTFGoogleTiltBrushMaterialExtension as $hBQxr$GLTFGoogleTiltBrushMaterialExtension} from "three-icosa";
import {TiltLoader as $hBQxr$TiltLoader} from "three-tiltloader";
import {XRControllerModelFactory as $hBQxr$XRControllerModelFactory} from "three/examples/jsm/webxr/XRControllerModelFactory.js";

// Copyright 2021-2022 Icosa Gallery
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
/*!
 * camera-controls
 * https://github.com/yomotsu/camera-controls
 * (c) 2017 @yomotsu
 * Released under the MIT License.
 */ // see https://developer.mozilla.org/en-US/docs/Web/API/MouseEvent/buttons#value
const $e1f901905a002d12$var$MOUSE_BUTTON = {
    LEFT: 1,
    RIGHT: 2,
    MIDDLE: 4
};
const $e1f901905a002d12$var$ACTION = Object.freeze({
    NONE: 0,
    ROTATE: 1,
    TRUCK: 2,
    SCREEN_PAN: 4,
    OFFSET: 8,
    DOLLY: 16,
    ZOOM: 32,
    TOUCH_ROTATE: 64,
    TOUCH_TRUCK: 128,
    TOUCH_SCREEN_PAN: 256,
    TOUCH_OFFSET: 512,
    TOUCH_DOLLY: 1024,
    TOUCH_ZOOM: 2048,
    TOUCH_DOLLY_TRUCK: 4096,
    TOUCH_DOLLY_SCREEN_PAN: 8192,
    TOUCH_DOLLY_OFFSET: 16384,
    TOUCH_DOLLY_ROTATE: 32768,
    TOUCH_ZOOM_TRUCK: 65536,
    TOUCH_ZOOM_OFFSET: 131072,
    TOUCH_ZOOM_SCREEN_PAN: 262144,
    TOUCH_ZOOM_ROTATE: 524288
});
const $e1f901905a002d12$var$DOLLY_DIRECTION = {
    NONE: 0,
    IN: 1,
    OUT: -1
};
function $e1f901905a002d12$var$isPerspectiveCamera(camera) {
    return camera.isPerspectiveCamera;
}
function $e1f901905a002d12$var$isOrthographicCamera(camera) {
    return camera.isOrthographicCamera;
}
const $e1f901905a002d12$var$PI_2 = Math.PI * 2;
const $e1f901905a002d12$var$PI_HALF = Math.PI / 2;
const $e1f901905a002d12$var$EPSILON = 1e-5;
const $e1f901905a002d12$var$DEG2RAD = Math.PI / 180;
function $e1f901905a002d12$var$clamp(value, min, max) {
    return Math.max(min, Math.min(max, value));
}
function $e1f901905a002d12$var$approxZero(number, error = $e1f901905a002d12$var$EPSILON) {
    return Math.abs(number) < error;
}
function $e1f901905a002d12$var$approxEquals(a, b, error = $e1f901905a002d12$var$EPSILON) {
    return $e1f901905a002d12$var$approxZero(a - b, error);
}
function $e1f901905a002d12$var$roundToStep(value, step) {
    return Math.round(value / step) * step;
}
function $e1f901905a002d12$var$infinityToMaxNumber(value) {
    if (isFinite(value)) return value;
    if (value < 0) return -Number.MAX_VALUE;
    return Number.MAX_VALUE;
}
function $e1f901905a002d12$var$maxNumberToInfinity(value) {
    if (Math.abs(value) < Number.MAX_VALUE) return value;
    return value * Infinity;
}
// https://docs.unity3d.com/ScriptReference/Mathf.SmoothDamp.html
// https://github.com/Unity-Technologies/UnityCsReference/blob/a2bdfe9b3c4cd4476f44bf52f848063bfaf7b6b9/Runtime/Export/Math/Mathf.cs#L308
function $e1f901905a002d12$var$smoothDamp(current, target, currentVelocityRef, smoothTime, maxSpeed = Infinity, deltaTime) {
    // Based on Game Programming Gems 4 Chapter 1.10
    smoothTime = Math.max(0.0001, smoothTime);
    const omega = 2 / smoothTime;
    const x = omega * deltaTime;
    const exp = 1 / (1 + x + 0.48 * x * x + 0.235 * x * x * x);
    let change = current - target;
    const originalTo = target;
    // Clamp maximum speed
    const maxChange = maxSpeed * smoothTime;
    change = $e1f901905a002d12$var$clamp(change, -maxChange, maxChange);
    target = current - change;
    const temp = (currentVelocityRef.value + omega * change) * deltaTime;
    currentVelocityRef.value = (currentVelocityRef.value - omega * temp) * exp;
    let output = target + (change + temp) * exp;
    // Prevent overshooting
    if (originalTo - current > 0.0 === output > originalTo) {
        output = originalTo;
        currentVelocityRef.value = (output - originalTo) / deltaTime;
    }
    return output;
}
// https://docs.unity3d.com/ScriptReference/Vector3.SmoothDamp.html
// https://github.com/Unity-Technologies/UnityCsReference/blob/a2bdfe9b3c4cd4476f44bf52f848063bfaf7b6b9/Runtime/Export/Math/Vector3.cs#L97
function $e1f901905a002d12$var$smoothDampVec3(current, target, currentVelocityRef, smoothTime, maxSpeed = Infinity, deltaTime, out) {
    // Based on Game Programming Gems 4 Chapter 1.10
    smoothTime = Math.max(0.0001, smoothTime);
    const omega = 2 / smoothTime;
    const x = omega * deltaTime;
    const exp = 1 / (1 + x + 0.48 * x * x + 0.235 * x * x * x);
    let targetX = target.x;
    let targetY = target.y;
    let targetZ = target.z;
    let changeX = current.x - targetX;
    let changeY = current.y - targetY;
    let changeZ = current.z - targetZ;
    const originalToX = targetX;
    const originalToY = targetY;
    const originalToZ = targetZ;
    // Clamp maximum speed
    const maxChange = maxSpeed * smoothTime;
    const maxChangeSq = maxChange * maxChange;
    const magnitudeSq = changeX * changeX + changeY * changeY + changeZ * changeZ;
    if (magnitudeSq > maxChangeSq) {
        const magnitude = Math.sqrt(magnitudeSq);
        changeX = changeX / magnitude * maxChange;
        changeY = changeY / magnitude * maxChange;
        changeZ = changeZ / magnitude * maxChange;
    }
    targetX = current.x - changeX;
    targetY = current.y - changeY;
    targetZ = current.z - changeZ;
    const tempX = (currentVelocityRef.x + omega * changeX) * deltaTime;
    const tempY = (currentVelocityRef.y + omega * changeY) * deltaTime;
    const tempZ = (currentVelocityRef.z + omega * changeZ) * deltaTime;
    currentVelocityRef.x = (currentVelocityRef.x - omega * tempX) * exp;
    currentVelocityRef.y = (currentVelocityRef.y - omega * tempY) * exp;
    currentVelocityRef.z = (currentVelocityRef.z - omega * tempZ) * exp;
    out.x = targetX + (changeX + tempX) * exp;
    out.y = targetY + (changeY + tempY) * exp;
    out.z = targetZ + (changeZ + tempZ) * exp;
    // Prevent overshooting
    const origMinusCurrentX = originalToX - current.x;
    const origMinusCurrentY = originalToY - current.y;
    const origMinusCurrentZ = originalToZ - current.z;
    const outMinusOrigX = out.x - originalToX;
    const outMinusOrigY = out.y - originalToY;
    const outMinusOrigZ = out.z - originalToZ;
    if (origMinusCurrentX * outMinusOrigX + origMinusCurrentY * outMinusOrigY + origMinusCurrentZ * outMinusOrigZ > 0) {
        out.x = originalToX;
        out.y = originalToY;
        out.z = originalToZ;
        currentVelocityRef.x = (out.x - originalToX) / deltaTime;
        currentVelocityRef.y = (out.y - originalToY) / deltaTime;
        currentVelocityRef.z = (out.z - originalToZ) / deltaTime;
    }
    return out;
}
function $e1f901905a002d12$var$extractClientCoordFromEvent(pointers, out) {
    out.set(0, 0);
    pointers.forEach((pointer)=>{
        out.x += pointer.clientX;
        out.y += pointer.clientY;
    });
    out.x /= pointers.length;
    out.y /= pointers.length;
}
function $e1f901905a002d12$var$notSupportedInOrthographicCamera(camera, message) {
    if ($e1f901905a002d12$var$isOrthographicCamera(camera)) {
        console.warn(`${message} is not supported in OrthographicCamera`);
        return true;
    }
    return false;
}
class $e1f901905a002d12$export$ec8b666c5fe2c75a {
    constructor(){
        this._listeners = {};
    }
    /**
     * Adds the specified event listener.
     * @param type event name
     * @param listener handler function
     * @category Methods
     */ addEventListener(type, listener) {
        const listeners = this._listeners;
        if (listeners[type] === undefined) listeners[type] = [];
        if (listeners[type].indexOf(listener) === -1) listeners[type].push(listener);
    }
    /**
     * Presence of the specified event listener.
     * @param type event name
     * @param listener handler function
     * @category Methods
     */ hasEventListener(type, listener) {
        const listeners = this._listeners;
        return listeners[type] !== undefined && listeners[type].indexOf(listener) !== -1;
    }
    /**
     * Removes the specified event listener
     * @param type event name
     * @param listener handler function
     * @category Methods
     */ removeEventListener(type, listener) {
        const listeners = this._listeners;
        const listenerArray = listeners[type];
        if (listenerArray !== undefined) {
            const index = listenerArray.indexOf(listener);
            if (index !== -1) listenerArray.splice(index, 1);
        }
    }
    /**
     * Removes all event listeners
     * @param type event name
     * @category Methods
     */ removeAllEventListeners(type) {
        if (!type) {
            this._listeners = {};
            return;
        }
        if (Array.isArray(this._listeners[type])) this._listeners[type].length = 0;
    }
    /**
     * Fire an event type.
     * @param event DispatcherEvent
     * @category Methods
     */ dispatchEvent(event) {
        const listeners = this._listeners;
        const listenerArray = listeners[event.type];
        if (listenerArray !== undefined) {
            event.target = this;
            const array = listenerArray.slice(0);
            for(let i = 0, l = array.length; i < l; i++)array[i].call(this, event);
        }
    }
}
var $e1f901905a002d12$var$_a;
const $e1f901905a002d12$var$VERSION = '2.10.1'; // will be replaced with `version` in package.json during the build process.
const $e1f901905a002d12$var$TOUCH_DOLLY_FACTOR = 1 / 8;
const $e1f901905a002d12$var$isMac = /Mac/.test(($e1f901905a002d12$var$_a = globalThis === null || globalThis === void 0 ? void 0 : globalThis.navigator) === null || $e1f901905a002d12$var$_a === void 0 ? void 0 : $e1f901905a002d12$var$_a.platform);
let $e1f901905a002d12$var$THREE;
let $e1f901905a002d12$var$_ORIGIN;
let $e1f901905a002d12$var$_AXIS_Y;
let $e1f901905a002d12$var$_AXIS_Z;
let $e1f901905a002d12$var$_v2;
let $e1f901905a002d12$var$_v3A;
let $e1f901905a002d12$var$_v3B;
let $e1f901905a002d12$var$_v3C;
let $e1f901905a002d12$var$_cameraDirection;
let $e1f901905a002d12$var$_xColumn;
let $e1f901905a002d12$var$_yColumn;
let $e1f901905a002d12$var$_zColumn;
let $e1f901905a002d12$var$_deltaTarget;
let $e1f901905a002d12$var$_deltaOffset;
let $e1f901905a002d12$var$_sphericalA;
let $e1f901905a002d12$var$_sphericalB;
let $e1f901905a002d12$var$_box3A;
let $e1f901905a002d12$var$_box3B;
let $e1f901905a002d12$var$_sphere;
let $e1f901905a002d12$var$_quaternionA;
let $e1f901905a002d12$var$_quaternionB;
let $e1f901905a002d12$var$_rotationMatrix;
let $e1f901905a002d12$var$_raycaster;
class $e1f901905a002d12$export$2e2bcd8739ae039 extends $e1f901905a002d12$export$ec8b666c5fe2c75a {
    /**
     * Injects THREE as the dependency. You can then proceed to use CameraControls.
     *
     * e.g
     * ```javascript
     * CameraControls.install( { THREE: THREE } );
     * ```
     *
     * Note: If you do not wish to use enter three.js to reduce file size(tree-shaking for example), make a subset to install.
     *
     * ```js
     * import {
     * 	Vector2,
     * 	Vector3,
     * 	Vector4,
     * 	Quaternion,
     * 	Matrix4,
     * 	Spherical,
     * 	Box3,
     * 	Sphere,
     * 	Raycaster,
     * 	MathUtils,
     * } from 'three';
     *
     * const subsetOfTHREE = {
     * 	Vector2   : Vector2,
     * 	Vector3   : Vector3,
     * 	Vector4   : Vector4,
     * 	Quaternion: Quaternion,
     * 	Matrix4   : Matrix4,
     * 	Spherical : Spherical,
     * 	Box3      : Box3,
     * 	Sphere    : Sphere,
     * 	Raycaster : Raycaster,
     * };

     * CameraControls.install( { THREE: subsetOfTHREE } );
     * ```
     * @category Statics
     */ static install(libs) {
        $e1f901905a002d12$var$THREE = libs.THREE;
        $e1f901905a002d12$var$_ORIGIN = Object.freeze(new $e1f901905a002d12$var$THREE.Vector3(0, 0, 0));
        $e1f901905a002d12$var$_AXIS_Y = Object.freeze(new $e1f901905a002d12$var$THREE.Vector3(0, 1, 0));
        $e1f901905a002d12$var$_AXIS_Z = Object.freeze(new $e1f901905a002d12$var$THREE.Vector3(0, 0, 1));
        $e1f901905a002d12$var$_v2 = new $e1f901905a002d12$var$THREE.Vector2();
        $e1f901905a002d12$var$_v3A = new $e1f901905a002d12$var$THREE.Vector3();
        $e1f901905a002d12$var$_v3B = new $e1f901905a002d12$var$THREE.Vector3();
        $e1f901905a002d12$var$_v3C = new $e1f901905a002d12$var$THREE.Vector3();
        $e1f901905a002d12$var$_cameraDirection = new $e1f901905a002d12$var$THREE.Vector3();
        $e1f901905a002d12$var$_xColumn = new $e1f901905a002d12$var$THREE.Vector3();
        $e1f901905a002d12$var$_yColumn = new $e1f901905a002d12$var$THREE.Vector3();
        $e1f901905a002d12$var$_zColumn = new $e1f901905a002d12$var$THREE.Vector3();
        $e1f901905a002d12$var$_deltaTarget = new $e1f901905a002d12$var$THREE.Vector3();
        $e1f901905a002d12$var$_deltaOffset = new $e1f901905a002d12$var$THREE.Vector3();
        $e1f901905a002d12$var$_sphericalA = new $e1f901905a002d12$var$THREE.Spherical();
        $e1f901905a002d12$var$_sphericalB = new $e1f901905a002d12$var$THREE.Spherical();
        $e1f901905a002d12$var$_box3A = new $e1f901905a002d12$var$THREE.Box3();
        $e1f901905a002d12$var$_box3B = new $e1f901905a002d12$var$THREE.Box3();
        $e1f901905a002d12$var$_sphere = new $e1f901905a002d12$var$THREE.Sphere();
        $e1f901905a002d12$var$_quaternionA = new $e1f901905a002d12$var$THREE.Quaternion();
        $e1f901905a002d12$var$_quaternionB = new $e1f901905a002d12$var$THREE.Quaternion();
        $e1f901905a002d12$var$_rotationMatrix = new $e1f901905a002d12$var$THREE.Matrix4();
        $e1f901905a002d12$var$_raycaster = new $e1f901905a002d12$var$THREE.Raycaster();
    }
    /**
     * list all ACTIONs
     * @category Statics
     */ static get ACTION() {
        return $e1f901905a002d12$var$ACTION;
    }
    /**
     * @deprecated Use `cameraControls.mouseButtons.left = CameraControls.ACTION.SCREEN_PAN` instead.
     */ set verticalDragToForward(_) {
        console.warn('camera-controls: `verticalDragToForward` was removed. Use `mouseButtons.left = CameraControls.ACTION.SCREEN_PAN` instead.');
    }
    /**
     * Creates a `CameraControls` instance.
     *
     * Note:
     * You **must install** three.js before using camera-controls. see [#install](#install)
     * Not doing so will lead to runtime errors (`undefined` references to THREE).
     *
     * e.g.
     * ```
     * CameraControls.install( { THREE } );
     * const cameraControls = new CameraControls( camera, domElement );
     * ```
     *
     * @param camera A `THREE.PerspectiveCamera` or `THREE.OrthographicCamera` to be controlled.
     * @param domElement A `HTMLElement` for the draggable area, usually `renderer.domElement`.
     * @category Constructor
     */ constructor(camera, domElement){
        super();
        /**
         * Minimum vertical angle in radians.
         * The angle has to be between `0` and `.maxPolarAngle` inclusive.
         * The default value is `0`.
         *
         * e.g.
         * ```
         * cameraControls.maxPolarAngle = 0;
         * ```
         * @category Properties
         */ this.minPolarAngle = 0; // radians
        /**
         * Maximum vertical angle in radians.
         * The angle has to be between `.maxPolarAngle` and `Math.PI` inclusive.
         * The default value is `Math.PI`.
         *
         * e.g.
         * ```
         * cameraControls.maxPolarAngle = Math.PI;
         * ```
         * @category Properties
         */ this.maxPolarAngle = Math.PI; // radians
        /**
         * Minimum horizontal angle in radians.
         * The angle has to be less than `.maxAzimuthAngle`.
         * The default value is `- Infinity`.
         *
         * e.g.
         * ```
         * cameraControls.minAzimuthAngle = - Infinity;
         * ```
         * @category Properties
         */ this.minAzimuthAngle = -Infinity; // radians
        /**
         * Maximum horizontal angle in radians.
         * The angle has to be greater than `.minAzimuthAngle`.
         * The default value is `Infinity`.
         *
         * e.g.
         * ```
         * cameraControls.maxAzimuthAngle = Infinity;
         * ```
         * @category Properties
         */ this.maxAzimuthAngle = Infinity; // radians
        // How far you can dolly in and out ( PerspectiveCamera only )
        /**
         * Minimum distance for dolly. The value must be higher than `0`. Default is `Number.EPSILON`.
         * PerspectiveCamera only.
         * @category Properties
         */ this.minDistance = Number.EPSILON;
        /**
         * Maximum distance for dolly. The value must be higher than `minDistance`. Default is `Infinity`.
         * PerspectiveCamera only.
         * @category Properties
         */ this.maxDistance = Infinity;
        /**
         * `true` to enable Infinity Dolly for wheel and pinch. Use this with `minDistance` and `maxDistance`
         * If the Dolly distance is less (or over) than the `minDistance` (or `maxDistance`), `infinityDolly` will keep the distance and pushes the target position instead.
         * @category Properties
         */ this.infinityDolly = false;
        /**
         * Minimum camera zoom.
         * @category Properties
         */ this.minZoom = 0.01;
        /**
         * Maximum camera zoom.
         * @category Properties
         */ this.maxZoom = Infinity;
        /**
         * Approximate time in seconds to reach the target. A smaller value will reach the target faster.
         * @category Properties
         */ this.smoothTime = 0.25;
        /**
         * the smoothTime while dragging
         * @category Properties
         */ this.draggingSmoothTime = 0.125;
        /**
         * Max transition speed in unit-per-seconds
         * @category Properties
         */ this.maxSpeed = Infinity;
        /**
         * Speed of azimuth (horizontal) rotation.
         * @category Properties
         */ this.azimuthRotateSpeed = 1.0;
        /**
         * Speed of polar (vertical) rotation.
         * @category Properties
         */ this.polarRotateSpeed = 1.0;
        /**
         * Speed of mouse-wheel dollying.
         * @category Properties
         */ this.dollySpeed = 1.0;
        /**
         * `true` to invert direction when dollying or zooming via drag
         * @category Properties
         */ this.dollyDragInverted = false;
        /**
         * Speed of drag for truck and pedestal.
         * @category Properties
         */ this.truckSpeed = 2.0;
        /**
         * `true` to enable Dolly-in to the mouse cursor coords.
         * @category Properties
         */ this.dollyToCursor = false;
        /**
         * @category Properties
         */ this.dragToOffset = false;
        /**
         * Friction ratio of the boundary.
         * @category Properties
         */ this.boundaryFriction = 0.0;
        /**
         * Controls how soon the `rest` event fires as the camera slows.
         * @category Properties
         */ this.restThreshold = 0.01;
        /**
         * An array of Meshes to collide with camera.
         * Be aware colliderMeshes may decrease performance. The collision test uses 4 raycasters from the camera since the near plane has 4 corners.
         * @category Properties
         */ this.colliderMeshes = [];
        /**
         * Force cancel user dragging.
         * @category Methods
         */ // cancel will be overwritten in the constructor.
        this.cancel = ()=>{};
        this._enabled = true;
        this._state = $e1f901905a002d12$var$ACTION.NONE;
        this._viewport = null;
        this._changedDolly = 0;
        this._changedZoom = 0;
        this._hasRested = true;
        this._boundaryEnclosesCamera = false;
        this._needsUpdate = true;
        this._updatedLastTime = false;
        this._elementRect = new DOMRect();
        this._isDragging = false;
        this._dragNeedsUpdate = true;
        this._activePointers = [];
        this._lockedPointer = null;
        this._interactiveArea = new DOMRect(0, 0, 1, 1);
        // Use draggingSmoothTime over smoothTime while true.
        // set automatically true on user-dragging start.
        // set automatically false on programmable methods call.
        this._isUserControllingRotate = false;
        this._isUserControllingDolly = false;
        this._isUserControllingTruck = false;
        this._isUserControllingOffset = false;
        this._isUserControllingZoom = false;
        this._lastDollyDirection = $e1f901905a002d12$var$DOLLY_DIRECTION.NONE;
        // velocities for smoothDamp
        this._thetaVelocity = {
            value: 0
        };
        this._phiVelocity = {
            value: 0
        };
        this._radiusVelocity = {
            value: 0
        };
        this._targetVelocity = new $e1f901905a002d12$var$THREE.Vector3();
        this._focalOffsetVelocity = new $e1f901905a002d12$var$THREE.Vector3();
        this._zoomVelocity = {
            value: 0
        };
        this._truckInternal = (deltaX, deltaY, dragToOffset, screenSpacePanning)=>{
            let truckX;
            let pedestalY;
            if ($e1f901905a002d12$var$isPerspectiveCamera(this._camera)) {
                const offset = $e1f901905a002d12$var$_v3A.copy(this._camera.position).sub(this._target);
                // half of the fov is center to top of screen
                const fov = this._camera.getEffectiveFOV() * $e1f901905a002d12$var$DEG2RAD;
                const targetDistance = offset.length() * Math.tan(fov * 0.5);
                truckX = this.truckSpeed * deltaX * targetDistance / this._elementRect.height;
                pedestalY = this.truckSpeed * deltaY * targetDistance / this._elementRect.height;
            } else if ($e1f901905a002d12$var$isOrthographicCamera(this._camera)) {
                const camera = this._camera;
                truckX = this.truckSpeed * deltaX * (camera.right - camera.left) / camera.zoom / this._elementRect.width;
                pedestalY = this.truckSpeed * deltaY * (camera.top - camera.bottom) / camera.zoom / this._elementRect.height;
            } else return;
            if (screenSpacePanning) {
                dragToOffset ? this.setFocalOffset(this._focalOffsetEnd.x + truckX, this._focalOffsetEnd.y, this._focalOffsetEnd.z, true) : this.truck(truckX, 0, true);
                this.forward(-pedestalY, true);
            } else dragToOffset ? this.setFocalOffset(this._focalOffsetEnd.x + truckX, this._focalOffsetEnd.y + pedestalY, this._focalOffsetEnd.z, true) : this.truck(truckX, pedestalY, true);
        };
        this._rotateInternal = (deltaX, deltaY)=>{
            const theta = $e1f901905a002d12$var$PI_2 * this.azimuthRotateSpeed * deltaX / this._elementRect.height; // divide by *height* to refer the resolution
            const phi = $e1f901905a002d12$var$PI_2 * this.polarRotateSpeed * deltaY / this._elementRect.height;
            this.rotate(theta, phi, true);
        };
        this._dollyInternal = (delta, x, y)=>{
            const dollyScale = Math.pow(0.95, -delta * this.dollySpeed);
            const lastDistance = this._sphericalEnd.radius;
            const distance = this._sphericalEnd.radius * dollyScale;
            const clampedDistance = $e1f901905a002d12$var$clamp(distance, this.minDistance, this.maxDistance);
            const overflowedDistance = clampedDistance - distance;
            if (this.infinityDolly && this.dollyToCursor) this._dollyToNoClamp(distance, true);
            else if (this.infinityDolly && !this.dollyToCursor) {
                this.dollyInFixed(overflowedDistance, true);
                this._dollyToNoClamp(clampedDistance, true);
            } else this._dollyToNoClamp(clampedDistance, true);
            if (this.dollyToCursor) {
                this._changedDolly += (this.infinityDolly ? distance : clampedDistance) - lastDistance;
                this._dollyControlCoord.set(x, y);
            }
            this._lastDollyDirection = Math.sign(-delta);
        };
        this._zoomInternal = (delta, x, y)=>{
            const zoomScale = Math.pow(0.95, delta * this.dollySpeed);
            const lastZoom = this._zoom;
            const zoom = this._zoom * zoomScale;
            // for both PerspectiveCamera and OrthographicCamera
            this.zoomTo(zoom, true);
            if (this.dollyToCursor) {
                this._changedZoom += zoom - lastZoom;
                this._dollyControlCoord.set(x, y);
            }
        };
        // Check if the user has installed THREE
        if (typeof $e1f901905a002d12$var$THREE === 'undefined') console.error('camera-controls: `THREE` is undefined. You must first run `CameraControls.install( { THREE: THREE } )`. Check the docs for further information.');
        this._camera = camera;
        this._yAxisUpSpace = new $e1f901905a002d12$var$THREE.Quaternion().setFromUnitVectors(this._camera.up, $e1f901905a002d12$var$_AXIS_Y);
        this._yAxisUpSpaceInverse = this._yAxisUpSpace.clone().invert();
        this._state = $e1f901905a002d12$var$ACTION.NONE;
        // the location
        this._target = new $e1f901905a002d12$var$THREE.Vector3();
        this._targetEnd = this._target.clone();
        this._focalOffset = new $e1f901905a002d12$var$THREE.Vector3();
        this._focalOffsetEnd = this._focalOffset.clone();
        // rotation
        this._spherical = new $e1f901905a002d12$var$THREE.Spherical().setFromVector3($e1f901905a002d12$var$_v3A.copy(this._camera.position).applyQuaternion(this._yAxisUpSpace));
        this._sphericalEnd = this._spherical.clone();
        this._lastDistance = this._spherical.radius;
        this._zoom = this._camera.zoom;
        this._zoomEnd = this._zoom;
        this._lastZoom = this._zoom;
        // collisionTest uses nearPlane.s
        this._nearPlaneCorners = [
            new $e1f901905a002d12$var$THREE.Vector3(),
            new $e1f901905a002d12$var$THREE.Vector3(),
            new $e1f901905a002d12$var$THREE.Vector3(),
            new $e1f901905a002d12$var$THREE.Vector3()
        ];
        this._updateNearPlaneCorners();
        // Target cannot move outside of this box
        this._boundary = new $e1f901905a002d12$var$THREE.Box3(new $e1f901905a002d12$var$THREE.Vector3(-Infinity, -Infinity, -Infinity), new $e1f901905a002d12$var$THREE.Vector3(Infinity, Infinity, Infinity));
        // reset
        this._cameraUp0 = this._camera.up.clone();
        this._target0 = this._target.clone();
        this._position0 = this._camera.position.clone();
        this._zoom0 = this._zoom;
        this._focalOffset0 = this._focalOffset.clone();
        this._dollyControlCoord = new $e1f901905a002d12$var$THREE.Vector2();
        // configs
        this.mouseButtons = {
            left: $e1f901905a002d12$var$ACTION.ROTATE,
            middle: $e1f901905a002d12$var$ACTION.DOLLY,
            right: $e1f901905a002d12$var$ACTION.TRUCK,
            wheel: $e1f901905a002d12$var$isPerspectiveCamera(this._camera) ? $e1f901905a002d12$var$ACTION.DOLLY : $e1f901905a002d12$var$isOrthographicCamera(this._camera) ? $e1f901905a002d12$var$ACTION.ZOOM : $e1f901905a002d12$var$ACTION.NONE
        };
        this.touches = {
            one: $e1f901905a002d12$var$ACTION.TOUCH_ROTATE,
            two: $e1f901905a002d12$var$isPerspectiveCamera(this._camera) ? $e1f901905a002d12$var$ACTION.TOUCH_DOLLY_TRUCK : $e1f901905a002d12$var$isOrthographicCamera(this._camera) ? $e1f901905a002d12$var$ACTION.TOUCH_ZOOM_TRUCK : $e1f901905a002d12$var$ACTION.NONE,
            three: $e1f901905a002d12$var$ACTION.TOUCH_TRUCK
        };
        const dragStartPosition = new $e1f901905a002d12$var$THREE.Vector2();
        const lastDragPosition = new $e1f901905a002d12$var$THREE.Vector2();
        const dollyStart = new $e1f901905a002d12$var$THREE.Vector2();
        const onPointerDown = (event)=>{
            if (!this._enabled || !this._domElement) return;
            if (this._interactiveArea.left !== 0 || this._interactiveArea.top !== 0 || this._interactiveArea.width !== 1 || this._interactiveArea.height !== 1) {
                const elRect = this._domElement.getBoundingClientRect();
                const left = event.clientX / elRect.width;
                const top = event.clientY / elRect.height;
                // check if the interactiveArea contains the drag start position.
                if (left < this._interactiveArea.left || left > this._interactiveArea.right || top < this._interactiveArea.top || top > this._interactiveArea.bottom) return;
            }
            // Don't call `event.preventDefault()` on the pointerdown event
            // to keep receiving pointermove evens outside dragging iframe
            // https://taye.me/blog/tips/2015/11/16/mouse-drag-outside-iframe/
            const mouseButton = event.pointerType !== 'mouse' ? null : (event.buttons & $e1f901905a002d12$var$MOUSE_BUTTON.LEFT) === $e1f901905a002d12$var$MOUSE_BUTTON.LEFT ? $e1f901905a002d12$var$MOUSE_BUTTON.LEFT : (event.buttons & $e1f901905a002d12$var$MOUSE_BUTTON.MIDDLE) === $e1f901905a002d12$var$MOUSE_BUTTON.MIDDLE ? $e1f901905a002d12$var$MOUSE_BUTTON.MIDDLE : (event.buttons & $e1f901905a002d12$var$MOUSE_BUTTON.RIGHT) === $e1f901905a002d12$var$MOUSE_BUTTON.RIGHT ? $e1f901905a002d12$var$MOUSE_BUTTON.RIGHT : null;
            if (mouseButton !== null) {
                const zombiePointer = this._findPointerByMouseButton(mouseButton);
                zombiePointer && this._disposePointer(zombiePointer);
            }
            if ((event.buttons & $e1f901905a002d12$var$MOUSE_BUTTON.LEFT) === $e1f901905a002d12$var$MOUSE_BUTTON.LEFT && this._lockedPointer) return;
            const pointer = {
                pointerId: event.pointerId,
                clientX: event.clientX,
                clientY: event.clientY,
                deltaX: 0,
                deltaY: 0,
                mouseButton: mouseButton
            };
            this._activePointers.push(pointer);
            // eslint-disable-next-line no-undef
            this._domElement.ownerDocument.removeEventListener('pointermove', onPointerMove, {
                passive: false
            });
            this._domElement.ownerDocument.removeEventListener('pointerup', onPointerUp);
            this._domElement.ownerDocument.addEventListener('pointermove', onPointerMove, {
                passive: false
            });
            this._domElement.ownerDocument.addEventListener('pointerup', onPointerUp);
            this._isDragging = true;
            startDragging(event);
        };
        const onPointerMove = (event)=>{
            if (event.cancelable) event.preventDefault();
            const pointerId = event.pointerId;
            const pointer = this._lockedPointer || this._findPointerById(pointerId);
            if (!pointer) return;
            pointer.clientX = event.clientX;
            pointer.clientY = event.clientY;
            pointer.deltaX = event.movementX;
            pointer.deltaY = event.movementY;
            this._state = 0;
            if (event.pointerType === 'touch') switch(this._activePointers.length){
                case 1:
                    this._state = this.touches.one;
                    break;
                case 2:
                    this._state = this.touches.two;
                    break;
                case 3:
                    this._state = this.touches.three;
                    break;
            }
            else {
                if (!this._isDragging && this._lockedPointer || this._isDragging && (event.buttons & $e1f901905a002d12$var$MOUSE_BUTTON.LEFT) === $e1f901905a002d12$var$MOUSE_BUTTON.LEFT) this._state = this._state | this.mouseButtons.left;
                if (this._isDragging && (event.buttons & $e1f901905a002d12$var$MOUSE_BUTTON.MIDDLE) === $e1f901905a002d12$var$MOUSE_BUTTON.MIDDLE) this._state = this._state | this.mouseButtons.middle;
                if (this._isDragging && (event.buttons & $e1f901905a002d12$var$MOUSE_BUTTON.RIGHT) === $e1f901905a002d12$var$MOUSE_BUTTON.RIGHT) this._state = this._state | this.mouseButtons.right;
            }
            dragging();
        };
        const onPointerUp = (event)=>{
            const pointer = this._findPointerById(event.pointerId);
            if (pointer && pointer === this._lockedPointer) return;
            pointer && this._disposePointer(pointer);
            if (event.pointerType === 'touch') switch(this._activePointers.length){
                case 0:
                    this._state = $e1f901905a002d12$var$ACTION.NONE;
                    break;
                case 1:
                    this._state = this.touches.one;
                    break;
                case 2:
                    this._state = this.touches.two;
                    break;
                case 3:
                    this._state = this.touches.three;
                    break;
            }
            else this._state = $e1f901905a002d12$var$ACTION.NONE;
            endDragging();
        };
        let lastScrollTimeStamp = -1;
        const onMouseWheel = (event)=>{
            if (!this._domElement) return;
            if (!this._enabled || this.mouseButtons.wheel === $e1f901905a002d12$var$ACTION.NONE) return;
            if (this._interactiveArea.left !== 0 || this._interactiveArea.top !== 0 || this._interactiveArea.width !== 1 || this._interactiveArea.height !== 1) {
                const elRect = this._domElement.getBoundingClientRect();
                const left = event.clientX / elRect.width;
                const top = event.clientY / elRect.height;
                // check if the interactiveArea contains the drag start position.
                if (left < this._interactiveArea.left || left > this._interactiveArea.right || top < this._interactiveArea.top || top > this._interactiveArea.bottom) return;
            }
            event.preventDefault();
            if (this.dollyToCursor || this.mouseButtons.wheel === $e1f901905a002d12$var$ACTION.ROTATE || this.mouseButtons.wheel === $e1f901905a002d12$var$ACTION.TRUCK) {
                const now = performance.now();
                // only need to fire this at scroll start.
                if (lastScrollTimeStamp - now < 1000) this._getClientRect(this._elementRect);
                lastScrollTimeStamp = now;
            }
            // Ref: https://github.com/cedricpinson/osgjs/blob/00e5a7e9d9206c06fdde0436e1d62ab7cb5ce853/sources/osgViewer/input/source/InputSourceMouse.js#L89-L103
            const deltaYFactor = $e1f901905a002d12$var$isMac ? -1 : -3;
            // Checks event.ctrlKey to detect multi-touch gestures on a trackpad.
            const delta = event.deltaMode === 1 || event.ctrlKey ? event.deltaY / deltaYFactor : event.deltaY / (deltaYFactor * 10);
            const x = this.dollyToCursor ? (event.clientX - this._elementRect.x) / this._elementRect.width * 2 - 1 : 0;
            const y = this.dollyToCursor ? (event.clientY - this._elementRect.y) / this._elementRect.height * -2 + 1 : 0;
            switch(this.mouseButtons.wheel){
                case $e1f901905a002d12$var$ACTION.ROTATE:
                    this._rotateInternal(event.deltaX, event.deltaY);
                    this._isUserControllingRotate = true;
                    break;
                case $e1f901905a002d12$var$ACTION.TRUCK:
                    this._truckInternal(event.deltaX, event.deltaY, false, false);
                    this._isUserControllingTruck = true;
                    break;
                case $e1f901905a002d12$var$ACTION.SCREEN_PAN:
                    this._truckInternal(event.deltaX, event.deltaY, false, true);
                    this._isUserControllingTruck = true;
                    break;
                case $e1f901905a002d12$var$ACTION.OFFSET:
                    this._truckInternal(event.deltaX, event.deltaY, true, false);
                    this._isUserControllingOffset = true;
                    break;
                case $e1f901905a002d12$var$ACTION.DOLLY:
                    this._dollyInternal(-delta, x, y);
                    this._isUserControllingDolly = true;
                    break;
                case $e1f901905a002d12$var$ACTION.ZOOM:
                    this._zoomInternal(-delta, x, y);
                    this._isUserControllingZoom = true;
                    break;
            }
            this.dispatchEvent({
                type: 'control'
            });
        };
        const onContextMenu = (event)=>{
            if (!this._domElement || !this._enabled) return;
            // contextmenu event is fired right after pointerdown
            // remove attached handlers and active pointer, if interrupted by contextmenu.
            if (this.mouseButtons.right === $e1f901905a002d12$export$2e2bcd8739ae039.ACTION.NONE) {
                const pointerId = event instanceof PointerEvent ? event.pointerId : 0;
                const pointer = this._findPointerById(pointerId);
                pointer && this._disposePointer(pointer);
                // eslint-disable-next-line no-undef
                this._domElement.ownerDocument.removeEventListener('pointermove', onPointerMove, {
                    passive: false
                });
                this._domElement.ownerDocument.removeEventListener('pointerup', onPointerUp);
                return;
            }
            event.preventDefault();
        };
        const startDragging = (event)=>{
            if (!this._enabled) return;
            $e1f901905a002d12$var$extractClientCoordFromEvent(this._activePointers, $e1f901905a002d12$var$_v2);
            this._getClientRect(this._elementRect);
            dragStartPosition.copy($e1f901905a002d12$var$_v2);
            lastDragPosition.copy($e1f901905a002d12$var$_v2);
            const isMultiTouch = this._activePointers.length >= 2;
            if (isMultiTouch) {
                // 2 finger pinch
                const dx = $e1f901905a002d12$var$_v2.x - this._activePointers[1].clientX;
                const dy = $e1f901905a002d12$var$_v2.y - this._activePointers[1].clientY;
                const distance = Math.sqrt(dx * dx + dy * dy);
                dollyStart.set(0, distance);
                // center coords of 2 finger truck
                const x = (this._activePointers[0].clientX + this._activePointers[1].clientX) * 0.5;
                const y = (this._activePointers[0].clientY + this._activePointers[1].clientY) * 0.5;
                lastDragPosition.set(x, y);
            }
            this._state = 0;
            if (!event) {
                if (this._lockedPointer) this._state = this._state | this.mouseButtons.left;
            } else if ('pointerType' in event && event.pointerType === 'touch') switch(this._activePointers.length){
                case 1:
                    this._state = this.touches.one;
                    break;
                case 2:
                    this._state = this.touches.two;
                    break;
                case 3:
                    this._state = this.touches.three;
                    break;
            }
            else {
                if (!this._lockedPointer && (event.buttons & $e1f901905a002d12$var$MOUSE_BUTTON.LEFT) === $e1f901905a002d12$var$MOUSE_BUTTON.LEFT) this._state = this._state | this.mouseButtons.left;
                if ((event.buttons & $e1f901905a002d12$var$MOUSE_BUTTON.MIDDLE) === $e1f901905a002d12$var$MOUSE_BUTTON.MIDDLE) this._state = this._state | this.mouseButtons.middle;
                if ((event.buttons & $e1f901905a002d12$var$MOUSE_BUTTON.RIGHT) === $e1f901905a002d12$var$MOUSE_BUTTON.RIGHT) this._state = this._state | this.mouseButtons.right;
            }
            // stop current movement on drag start
            // - rotate
            if ((this._state & $e1f901905a002d12$var$ACTION.ROTATE) === $e1f901905a002d12$var$ACTION.ROTATE || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_ROTATE) === $e1f901905a002d12$var$ACTION.TOUCH_ROTATE || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_DOLLY_ROTATE) === $e1f901905a002d12$var$ACTION.TOUCH_DOLLY_ROTATE || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_ZOOM_ROTATE) === $e1f901905a002d12$var$ACTION.TOUCH_ZOOM_ROTATE) {
                this._sphericalEnd.theta = this._spherical.theta;
                this._sphericalEnd.phi = this._spherical.phi;
                this._thetaVelocity.value = 0;
                this._phiVelocity.value = 0;
            }
            // - truck and screen-pan
            if ((this._state & $e1f901905a002d12$var$ACTION.TRUCK) === $e1f901905a002d12$var$ACTION.TRUCK || (this._state & $e1f901905a002d12$var$ACTION.SCREEN_PAN) === $e1f901905a002d12$var$ACTION.SCREEN_PAN || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_TRUCK) === $e1f901905a002d12$var$ACTION.TOUCH_TRUCK || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_SCREEN_PAN) === $e1f901905a002d12$var$ACTION.TOUCH_SCREEN_PAN || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_DOLLY_TRUCK) === $e1f901905a002d12$var$ACTION.TOUCH_DOLLY_TRUCK || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_DOLLY_SCREEN_PAN) === $e1f901905a002d12$var$ACTION.TOUCH_DOLLY_SCREEN_PAN || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_ZOOM_TRUCK) === $e1f901905a002d12$var$ACTION.TOUCH_ZOOM_TRUCK || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_ZOOM_SCREEN_PAN) === $e1f901905a002d12$var$ACTION.TOUCH_DOLLY_SCREEN_PAN) {
                this._targetEnd.copy(this._target);
                this._targetVelocity.set(0, 0, 0);
            }
            // - dolly
            if ((this._state & $e1f901905a002d12$var$ACTION.DOLLY) === $e1f901905a002d12$var$ACTION.DOLLY || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_DOLLY) === $e1f901905a002d12$var$ACTION.TOUCH_DOLLY || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_DOLLY_TRUCK) === $e1f901905a002d12$var$ACTION.TOUCH_DOLLY_TRUCK || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_DOLLY_SCREEN_PAN) === $e1f901905a002d12$var$ACTION.TOUCH_DOLLY_SCREEN_PAN || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_DOLLY_OFFSET) === $e1f901905a002d12$var$ACTION.TOUCH_DOLLY_OFFSET || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_DOLLY_ROTATE) === $e1f901905a002d12$var$ACTION.TOUCH_DOLLY_ROTATE) {
                this._sphericalEnd.radius = this._spherical.radius;
                this._radiusVelocity.value = 0;
            }
            // - zoom
            if ((this._state & $e1f901905a002d12$var$ACTION.ZOOM) === $e1f901905a002d12$var$ACTION.ZOOM || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_ZOOM) === $e1f901905a002d12$var$ACTION.TOUCH_ZOOM || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_ZOOM_TRUCK) === $e1f901905a002d12$var$ACTION.TOUCH_ZOOM_TRUCK || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_ZOOM_SCREEN_PAN) === $e1f901905a002d12$var$ACTION.TOUCH_ZOOM_SCREEN_PAN || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_ZOOM_OFFSET) === $e1f901905a002d12$var$ACTION.TOUCH_ZOOM_OFFSET || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_ZOOM_ROTATE) === $e1f901905a002d12$var$ACTION.TOUCH_ZOOM_ROTATE) {
                this._zoomEnd = this._zoom;
                this._zoomVelocity.value = 0;
            }
            // - offset
            if ((this._state & $e1f901905a002d12$var$ACTION.OFFSET) === $e1f901905a002d12$var$ACTION.OFFSET || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_OFFSET) === $e1f901905a002d12$var$ACTION.TOUCH_OFFSET || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_DOLLY_OFFSET) === $e1f901905a002d12$var$ACTION.TOUCH_DOLLY_OFFSET || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_ZOOM_OFFSET) === $e1f901905a002d12$var$ACTION.TOUCH_ZOOM_OFFSET) {
                this._focalOffsetEnd.copy(this._focalOffset);
                this._focalOffsetVelocity.set(0, 0, 0);
            }
            this.dispatchEvent({
                type: 'controlstart'
            });
        };
        const dragging = ()=>{
            if (!this._enabled || !this._dragNeedsUpdate) return;
            this._dragNeedsUpdate = false;
            $e1f901905a002d12$var$extractClientCoordFromEvent(this._activePointers, $e1f901905a002d12$var$_v2);
            // When pointer lock is enabled clientX, clientY, screenX, and screenY remain 0.
            // If pointer lock is enabled, use the Delta directory, and assume active-pointer is not multiple.
            const isPointerLockActive = this._domElement && this._domElement.ownerDocument.pointerLockElement === this._domElement;
            const lockedPointer = isPointerLockActive ? this._lockedPointer || this._activePointers[0] : null;
            const deltaX = lockedPointer ? -lockedPointer.deltaX : lastDragPosition.x - $e1f901905a002d12$var$_v2.x;
            const deltaY = lockedPointer ? -lockedPointer.deltaY : lastDragPosition.y - $e1f901905a002d12$var$_v2.y;
            lastDragPosition.copy($e1f901905a002d12$var$_v2);
            // rotate
            if ((this._state & $e1f901905a002d12$var$ACTION.ROTATE) === $e1f901905a002d12$var$ACTION.ROTATE || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_ROTATE) === $e1f901905a002d12$var$ACTION.TOUCH_ROTATE || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_DOLLY_ROTATE) === $e1f901905a002d12$var$ACTION.TOUCH_DOLLY_ROTATE || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_ZOOM_ROTATE) === $e1f901905a002d12$var$ACTION.TOUCH_ZOOM_ROTATE) {
                this._rotateInternal(deltaX, deltaY);
                this._isUserControllingRotate = true;
            }
            // mouse dolly or zoom
            if ((this._state & $e1f901905a002d12$var$ACTION.DOLLY) === $e1f901905a002d12$var$ACTION.DOLLY || (this._state & $e1f901905a002d12$var$ACTION.ZOOM) === $e1f901905a002d12$var$ACTION.ZOOM) {
                const dollyX = this.dollyToCursor ? (dragStartPosition.x - this._elementRect.x) / this._elementRect.width * 2 - 1 : 0;
                const dollyY = this.dollyToCursor ? (dragStartPosition.y - this._elementRect.y) / this._elementRect.height * -2 + 1 : 0;
                const dollyDirection = this.dollyDragInverted ? -1 : 1;
                if ((this._state & $e1f901905a002d12$var$ACTION.DOLLY) === $e1f901905a002d12$var$ACTION.DOLLY) {
                    this._dollyInternal(dollyDirection * deltaY * $e1f901905a002d12$var$TOUCH_DOLLY_FACTOR, dollyX, dollyY);
                    this._isUserControllingDolly = true;
                } else {
                    this._zoomInternal(dollyDirection * deltaY * $e1f901905a002d12$var$TOUCH_DOLLY_FACTOR, dollyX, dollyY);
                    this._isUserControllingZoom = true;
                }
            }
            // touch dolly or zoom
            if ((this._state & $e1f901905a002d12$var$ACTION.TOUCH_DOLLY) === $e1f901905a002d12$var$ACTION.TOUCH_DOLLY || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_ZOOM) === $e1f901905a002d12$var$ACTION.TOUCH_ZOOM || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_DOLLY_TRUCK) === $e1f901905a002d12$var$ACTION.TOUCH_DOLLY_TRUCK || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_ZOOM_TRUCK) === $e1f901905a002d12$var$ACTION.TOUCH_ZOOM_TRUCK || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_DOLLY_SCREEN_PAN) === $e1f901905a002d12$var$ACTION.TOUCH_DOLLY_SCREEN_PAN || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_ZOOM_SCREEN_PAN) === $e1f901905a002d12$var$ACTION.TOUCH_ZOOM_SCREEN_PAN || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_DOLLY_OFFSET) === $e1f901905a002d12$var$ACTION.TOUCH_DOLLY_OFFSET || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_ZOOM_OFFSET) === $e1f901905a002d12$var$ACTION.TOUCH_ZOOM_OFFSET || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_DOLLY_ROTATE) === $e1f901905a002d12$var$ACTION.TOUCH_DOLLY_ROTATE || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_ZOOM_ROTATE) === $e1f901905a002d12$var$ACTION.TOUCH_ZOOM_ROTATE) {
                const dx = $e1f901905a002d12$var$_v2.x - this._activePointers[1].clientX;
                const dy = $e1f901905a002d12$var$_v2.y - this._activePointers[1].clientY;
                const distance = Math.sqrt(dx * dx + dy * dy);
                const dollyDelta = dollyStart.y - distance;
                dollyStart.set(0, distance);
                const dollyX = this.dollyToCursor ? (lastDragPosition.x - this._elementRect.x) / this._elementRect.width * 2 - 1 : 0;
                const dollyY = this.dollyToCursor ? (lastDragPosition.y - this._elementRect.y) / this._elementRect.height * -2 + 1 : 0;
                if ((this._state & $e1f901905a002d12$var$ACTION.TOUCH_DOLLY) === $e1f901905a002d12$var$ACTION.TOUCH_DOLLY || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_DOLLY_ROTATE) === $e1f901905a002d12$var$ACTION.TOUCH_DOLLY_ROTATE || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_DOLLY_TRUCK) === $e1f901905a002d12$var$ACTION.TOUCH_DOLLY_TRUCK || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_DOLLY_SCREEN_PAN) === $e1f901905a002d12$var$ACTION.TOUCH_DOLLY_SCREEN_PAN || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_DOLLY_OFFSET) === $e1f901905a002d12$var$ACTION.TOUCH_DOLLY_OFFSET) {
                    this._dollyInternal(dollyDelta * $e1f901905a002d12$var$TOUCH_DOLLY_FACTOR, dollyX, dollyY);
                    this._isUserControllingDolly = true;
                } else {
                    this._zoomInternal(dollyDelta * $e1f901905a002d12$var$TOUCH_DOLLY_FACTOR, dollyX, dollyY);
                    this._isUserControllingZoom = true;
                }
            }
            // truck
            if ((this._state & $e1f901905a002d12$var$ACTION.TRUCK) === $e1f901905a002d12$var$ACTION.TRUCK || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_TRUCK) === $e1f901905a002d12$var$ACTION.TOUCH_TRUCK || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_DOLLY_TRUCK) === $e1f901905a002d12$var$ACTION.TOUCH_DOLLY_TRUCK || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_ZOOM_TRUCK) === $e1f901905a002d12$var$ACTION.TOUCH_ZOOM_TRUCK) {
                this._truckInternal(deltaX, deltaY, false, false);
                this._isUserControllingTruck = true;
            }
            // screen-pan
            if ((this._state & $e1f901905a002d12$var$ACTION.SCREEN_PAN) === $e1f901905a002d12$var$ACTION.SCREEN_PAN || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_SCREEN_PAN) === $e1f901905a002d12$var$ACTION.TOUCH_SCREEN_PAN || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_DOLLY_SCREEN_PAN) === $e1f901905a002d12$var$ACTION.TOUCH_DOLLY_SCREEN_PAN || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_ZOOM_SCREEN_PAN) === $e1f901905a002d12$var$ACTION.TOUCH_ZOOM_SCREEN_PAN) {
                this._truckInternal(deltaX, deltaY, false, true);
                this._isUserControllingTruck = true;
            }
            // offset
            if ((this._state & $e1f901905a002d12$var$ACTION.OFFSET) === $e1f901905a002d12$var$ACTION.OFFSET || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_OFFSET) === $e1f901905a002d12$var$ACTION.TOUCH_OFFSET || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_DOLLY_OFFSET) === $e1f901905a002d12$var$ACTION.TOUCH_DOLLY_OFFSET || (this._state & $e1f901905a002d12$var$ACTION.TOUCH_ZOOM_OFFSET) === $e1f901905a002d12$var$ACTION.TOUCH_ZOOM_OFFSET) {
                this._truckInternal(deltaX, deltaY, true, false);
                this._isUserControllingOffset = true;
            }
            this.dispatchEvent({
                type: 'control'
            });
        };
        const endDragging = ()=>{
            $e1f901905a002d12$var$extractClientCoordFromEvent(this._activePointers, $e1f901905a002d12$var$_v2);
            lastDragPosition.copy($e1f901905a002d12$var$_v2);
            this._dragNeedsUpdate = false;
            if (this._activePointers.length === 0 || this._activePointers.length === 1 && this._activePointers[0] === this._lockedPointer) this._isDragging = false;
            if (this._activePointers.length === 0 && this._domElement) {
                // eslint-disable-next-line no-undef
                this._domElement.ownerDocument.removeEventListener('pointermove', onPointerMove, {
                    passive: false
                });
                this._domElement.ownerDocument.removeEventListener('pointerup', onPointerUp);
                this.dispatchEvent({
                    type: 'controlend'
                });
            }
        };
        this.lockPointer = ()=>{
            if (!this._enabled || !this._domElement) return;
            this.cancel();
            // Element.requestPointerLock is allowed to happen without any pointer active - create a faux one for compatibility with controls
            this._lockedPointer = {
                pointerId: -1,
                clientX: 0,
                clientY: 0,
                deltaX: 0,
                deltaY: 0,
                mouseButton: null
            };
            this._activePointers.push(this._lockedPointer);
            // eslint-disable-next-line no-undef
            this._domElement.ownerDocument.removeEventListener('pointermove', onPointerMove, {
                passive: false
            });
            this._domElement.ownerDocument.removeEventListener('pointerup', onPointerUp);
            this._domElement.requestPointerLock();
            this._domElement.ownerDocument.addEventListener('pointerlockchange', onPointerLockChange);
            this._domElement.ownerDocument.addEventListener('pointerlockerror', onPointerLockError);
            this._domElement.ownerDocument.addEventListener('pointermove', onPointerMove, {
                passive: false
            });
            this._domElement.ownerDocument.addEventListener('pointerup', onPointerUp);
            startDragging();
        };
        this.unlockPointer = ()=>{
            var _a, _b, _c;
            if (this._lockedPointer !== null) {
                this._disposePointer(this._lockedPointer);
                this._lockedPointer = null;
            }
            (_a = this._domElement) === null || _a === void 0 || _a.ownerDocument.exitPointerLock();
            (_b = this._domElement) === null || _b === void 0 || _b.ownerDocument.removeEventListener('pointerlockchange', onPointerLockChange);
            (_c = this._domElement) === null || _c === void 0 || _c.ownerDocument.removeEventListener('pointerlockerror', onPointerLockError);
            this.cancel();
        };
        const onPointerLockChange = ()=>{
            const isPointerLockActive = this._domElement && this._domElement.ownerDocument.pointerLockElement === this._domElement;
            if (!isPointerLockActive) this.unlockPointer();
        };
        const onPointerLockError = ()=>{
            this.unlockPointer();
        };
        this._addAllEventListeners = (domElement)=>{
            this._domElement = domElement;
            this._domElement.style.touchAction = 'none';
            this._domElement.style.userSelect = 'none';
            this._domElement.style.webkitUserSelect = 'none';
            this._domElement.addEventListener('pointerdown', onPointerDown);
            this._domElement.addEventListener('pointercancel', onPointerUp);
            this._domElement.addEventListener('wheel', onMouseWheel, {
                passive: false
            });
            this._domElement.addEventListener('contextmenu', onContextMenu);
        };
        this._removeAllEventListeners = ()=>{
            if (!this._domElement) return;
            this._domElement.style.touchAction = '';
            this._domElement.style.userSelect = '';
            this._domElement.style.webkitUserSelect = '';
            this._domElement.removeEventListener('pointerdown', onPointerDown);
            this._domElement.removeEventListener('pointercancel', onPointerUp);
            // https://developer.mozilla.org/en-US/docs/Web/API/EventTarget/removeEventListener#matching_event_listeners_for_removal
            // > it's probably wise to use the same values used for the call to `addEventListener()` when calling `removeEventListener()`
            // see https://github.com/microsoft/TypeScript/issues/32912#issuecomment-522142969
            // eslint-disable-next-line no-undef
            this._domElement.removeEventListener('wheel', onMouseWheel, {
                passive: false
            });
            this._domElement.removeEventListener('contextmenu', onContextMenu);
            // eslint-disable-next-line no-undef
            this._domElement.ownerDocument.removeEventListener('pointermove', onPointerMove, {
                passive: false
            });
            this._domElement.ownerDocument.removeEventListener('pointerup', onPointerUp);
            this._domElement.ownerDocument.removeEventListener('pointerlockchange', onPointerLockChange);
            this._domElement.ownerDocument.removeEventListener('pointerlockerror', onPointerLockError);
        };
        this.cancel = ()=>{
            if (this._state === $e1f901905a002d12$var$ACTION.NONE) return;
            this._state = $e1f901905a002d12$var$ACTION.NONE;
            this._activePointers.length = 0;
            endDragging();
        };
        if (domElement) this.connect(domElement);
        this.update(0);
    }
    /**
     * The camera to be controlled
     * @category Properties
     */ get camera() {
        return this._camera;
    }
    set camera(camera) {
        this._camera = camera;
        this.updateCameraUp();
        this._camera.updateProjectionMatrix();
        this._updateNearPlaneCorners();
        this._needsUpdate = true;
    }
    /**
     * Whether or not the controls are enabled.
     * `false` to disable user dragging/touch-move, but all methods works.
     * @category Properties
     */ get enabled() {
        return this._enabled;
    }
    set enabled(enabled) {
        this._enabled = enabled;
        if (!this._domElement) return;
        if (enabled) {
            this._domElement.style.touchAction = 'none';
            this._domElement.style.userSelect = 'none';
            this._domElement.style.webkitUserSelect = 'none';
        } else {
            this.cancel();
            this._domElement.style.touchAction = '';
            this._domElement.style.userSelect = '';
            this._domElement.style.webkitUserSelect = '';
        }
    }
    /**
     * Returns `true` if the controls are active updating.
     * readonly value.
     * @category Properties
     */ get active() {
        return !this._hasRested;
    }
    /**
     * Getter for the current `ACTION`.
     * readonly value.
     * @category Properties
     */ get currentAction() {
        return this._state;
    }
    /**
     * get/set Current distance.
     * @category Properties
     */ get distance() {
        return this._spherical.radius;
    }
    set distance(distance) {
        if (this._spherical.radius === distance && this._sphericalEnd.radius === distance) return;
        this._spherical.radius = distance;
        this._sphericalEnd.radius = distance;
        this._needsUpdate = true;
    }
    // horizontal angle
    /**
     * get/set the azimuth angle (horizontal) in radians.
     * Every 360 degrees turn is added to `.azimuthAngle` value, which is accumulative.
     * @category Properties
     */ get azimuthAngle() {
        return this._spherical.theta;
    }
    set azimuthAngle(azimuthAngle) {
        if (this._spherical.theta === azimuthAngle && this._sphericalEnd.theta === azimuthAngle) return;
        this._spherical.theta = azimuthAngle;
        this._sphericalEnd.theta = azimuthAngle;
        this._needsUpdate = true;
    }
    // vertical angle
    /**
     * get/set the polar angle (vertical) in radians.
     * @category Properties
     */ get polarAngle() {
        return this._spherical.phi;
    }
    set polarAngle(polarAngle) {
        if (this._spherical.phi === polarAngle && this._sphericalEnd.phi === polarAngle) return;
        this._spherical.phi = polarAngle;
        this._sphericalEnd.phi = polarAngle;
        this._needsUpdate = true;
    }
    /**
     * Whether camera position should be enclosed in the boundary or not.
     * @category Properties
     */ get boundaryEnclosesCamera() {
        return this._boundaryEnclosesCamera;
    }
    set boundaryEnclosesCamera(boundaryEnclosesCamera) {
        this._boundaryEnclosesCamera = boundaryEnclosesCamera;
        this._needsUpdate = true;
    }
    /**
     * Set drag-start, touches and wheel enable area in the domElement.
     * each values are between `0` and `1` inclusive, where `0` is left/top and `1` is right/bottom of the screen.
     * e.g. `{ x: 0, y: 0, width: 1, height: 1 }` for entire area.
     * @category Properties
     */ set interactiveArea(interactiveArea) {
        this._interactiveArea.width = $e1f901905a002d12$var$clamp(interactiveArea.width, 0, 1);
        this._interactiveArea.height = $e1f901905a002d12$var$clamp(interactiveArea.height, 0, 1);
        this._interactiveArea.x = $e1f901905a002d12$var$clamp(interactiveArea.x, 0, 1 - this._interactiveArea.width);
        this._interactiveArea.y = $e1f901905a002d12$var$clamp(interactiveArea.y, 0, 1 - this._interactiveArea.height);
    }
    /**
     * Adds the specified event listener.
     * Applicable event types (which is `K`) are:
     * | Event name          | Timing |
     * | ------------------- | ------ |
     * | `'controlstart'`    | When the user starts to control the camera via mouse / touches. ¹ |
     * | `'control'`         | When the user controls the camera (dragging). |
     * | `'controlend'`      | When the user ends to control the camera. ¹ |
     * | `'transitionstart'` | When any kind of transition starts, either user control or using a method with `enableTransition = true` |
     * | `'update'`          | When the camera position is updated. |
     * | `'wake'`            | When the camera starts moving. |
     * | `'rest'`            | When the camera movement is below `.restThreshold` ². |
     * | `'sleep'`           | When the camera end moving. |
     *
     * 1. `mouseButtons.wheel` (Mouse wheel control) does not emit `'controlstart'` and `'controlend'`. `mouseButtons.wheel` uses scroll-event internally, and scroll-event happens intermittently. That means "start" and "end" cannot be detected.
     * 2. Due to damping, `sleep` will usually fire a few seconds after the camera _appears_ to have stopped moving. If you want to do something (e.g. enable UI, perform another transition) at the point when the camera has stopped, you probably want the `rest` event. This can be fine tuned using the `.restThreshold` parameter. See the [Rest and Sleep Example](https://yomotsu.github.io/camera-controls/examples/rest-and-sleep.html).
     *
     * e.g.
     * ```
     * cameraControl.addEventListener( 'controlstart', myCallbackFunction );
     * ```
     * @param type event name
     * @param listener handler function
     * @category Methods
     */ addEventListener(type, listener) {
        super.addEventListener(type, listener);
    }
    /**
     * Removes the specified event listener
     * e.g.
     * ```
     * cameraControl.addEventListener( 'controlstart', myCallbackFunction );
     * ```
     * @param type event name
     * @param listener handler function
     * @category Methods
     */ removeEventListener(type, listener) {
        super.removeEventListener(type, listener);
    }
    /**
     * Rotate azimuthal angle(horizontal) and polar angle(vertical).
     * Every value is added to the current value.
     * @param azimuthAngle Azimuth rotate angle. In radian.
     * @param polarAngle Polar rotate angle. In radian.
     * @param enableTransition Whether to move smoothly or immediately
     * @category Methods
     */ rotate(azimuthAngle, polarAngle, enableTransition = false) {
        return this.rotateTo(this._sphericalEnd.theta + azimuthAngle, this._sphericalEnd.phi + polarAngle, enableTransition);
    }
    /**
     * Rotate azimuthal angle(horizontal) to the given angle and keep the same polar angle(vertical) target.
     *
     * e.g.
     * ```
     * cameraControls.rotateAzimuthTo( 30 * THREE.MathUtils.DEG2RAD, true );
     * ```
     * @param azimuthAngle Azimuth rotate angle. In radian.
     * @param enableTransition Whether to move smoothly or immediately
     * @category Methods
     */ rotateAzimuthTo(azimuthAngle, enableTransition = false) {
        return this.rotateTo(azimuthAngle, this._sphericalEnd.phi, enableTransition);
    }
    /**
     * Rotate polar angle(vertical) to the given angle and keep the same azimuthal angle(horizontal) target.
     *
     * e.g.
     * ```
     * cameraControls.rotatePolarTo( 30 * THREE.MathUtils.DEG2RAD, true );
     * ```
     * @param polarAngle Polar rotate angle. In radian.
     * @param enableTransition Whether to move smoothly or immediately
     * @category Methods
     */ rotatePolarTo(polarAngle, enableTransition = false) {
        return this.rotateTo(this._sphericalEnd.theta, polarAngle, enableTransition);
    }
    /**
     * Rotate azimuthal angle(horizontal) and polar angle(vertical) to the given angle.
     * Camera view will rotate over the orbit pivot absolutely:
     *
     * azimuthAngle
     * ```
     *       0º
     *         \
     * 90º -----+----- -90º
     *           \
     *           180º
     * ```
     * | direction | angle                  |
     * | --------- | ---------------------- |
     * | front     | 0º                     |
     * | left      | 90º (`Math.PI / 2`)    |
     * | right     | -90º (`- Math.PI / 2`) |
     * | back      | 180º (`Math.PI`)       |
     *
     * polarAngle
     * ```
     *     180º
     *      |
     *      90º
     *      |
     *      0º
     * ```
     * | direction            | angle                  |
     * | -------------------- | ---------------------- |
     * | top/sky              | 180º (`Math.PI`)       |
     * | horizontal from view | 90º (`Math.PI / 2`)    |
     * | bottom/floor         | 0º                     |
     *
     * @param azimuthAngle Azimuth rotate angle to. In radian.
     * @param polarAngle Polar rotate angle to. In radian.
     * @param enableTransition  Whether to move smoothly or immediately
     * @category Methods
     */ rotateTo(azimuthAngle, polarAngle, enableTransition = false) {
        this._isUserControllingRotate = false;
        const theta = $e1f901905a002d12$var$clamp(azimuthAngle, this.minAzimuthAngle, this.maxAzimuthAngle);
        const phi = $e1f901905a002d12$var$clamp(polarAngle, this.minPolarAngle, this.maxPolarAngle);
        this._sphericalEnd.theta = theta;
        this._sphericalEnd.phi = phi;
        this._sphericalEnd.makeSafe();
        this._needsUpdate = true;
        if (!enableTransition) {
            this._spherical.theta = this._sphericalEnd.theta;
            this._spherical.phi = this._sphericalEnd.phi;
        }
        const resolveImmediately = !enableTransition || $e1f901905a002d12$var$approxEquals(this._spherical.theta, this._sphericalEnd.theta, this.restThreshold) && $e1f901905a002d12$var$approxEquals(this._spherical.phi, this._sphericalEnd.phi, this.restThreshold);
        return this._createOnRestPromise(resolveImmediately);
    }
    /**
     * Dolly in/out camera position.
     * @param distance Distance of dollyIn. Negative number for dollyOut.
     * @param enableTransition Whether to move smoothly or immediately.
     * @category Methods
     */ dolly(distance, enableTransition = false) {
        return this.dollyTo(this._sphericalEnd.radius - distance, enableTransition);
    }
    /**
     * Dolly in/out camera position to given distance.
     * @param distance Distance of dolly.
     * @param enableTransition Whether to move smoothly or immediately.
     * @category Methods
     */ dollyTo(distance, enableTransition = false) {
        this._isUserControllingDolly = false;
        this._lastDollyDirection = $e1f901905a002d12$var$DOLLY_DIRECTION.NONE;
        this._changedDolly = 0;
        return this._dollyToNoClamp($e1f901905a002d12$var$clamp(distance, this.minDistance, this.maxDistance), enableTransition);
    }
    _dollyToNoClamp(distance, enableTransition = false) {
        const lastRadius = this._sphericalEnd.radius;
        const hasCollider = this.colliderMeshes.length >= 1;
        if (hasCollider) {
            const maxDistanceByCollisionTest = this._collisionTest();
            const isCollided = $e1f901905a002d12$var$approxEquals(maxDistanceByCollisionTest, this._spherical.radius);
            const isDollyIn = lastRadius > distance;
            if (!isDollyIn && isCollided) return Promise.resolve();
            this._sphericalEnd.radius = Math.min(distance, maxDistanceByCollisionTest);
        } else this._sphericalEnd.radius = distance;
        this._needsUpdate = true;
        if (!enableTransition) this._spherical.radius = this._sphericalEnd.radius;
        const resolveImmediately = !enableTransition || $e1f901905a002d12$var$approxEquals(this._spherical.radius, this._sphericalEnd.radius, this.restThreshold);
        return this._createOnRestPromise(resolveImmediately);
    }
    /**
     * Dolly in, but does not change the distance between the target and the camera, and moves the target position instead.
     * Specify a negative value for dolly out.
     * @param distance Distance of dolly.
     * @param enableTransition Whether to move smoothly or immediately.
     * @category Methods
     */ dollyInFixed(distance, enableTransition = false) {
        this._targetEnd.add(this._getCameraDirection($e1f901905a002d12$var$_cameraDirection).multiplyScalar(distance));
        if (!enableTransition) this._target.copy(this._targetEnd);
        const resolveImmediately = !enableTransition || $e1f901905a002d12$var$approxEquals(this._target.x, this._targetEnd.x, this.restThreshold) && $e1f901905a002d12$var$approxEquals(this._target.y, this._targetEnd.y, this.restThreshold) && $e1f901905a002d12$var$approxEquals(this._target.z, this._targetEnd.z, this.restThreshold);
        return this._createOnRestPromise(resolveImmediately);
    }
    /**
     * Zoom in/out camera. The value is added to camera zoom.
     * Limits set with `.minZoom` and `.maxZoom`
     * @param zoomStep zoom scale
     * @param enableTransition Whether to move smoothly or immediately
     * @category Methods
     */ zoom(zoomStep, enableTransition = false) {
        return this.zoomTo(this._zoomEnd + zoomStep, enableTransition);
    }
    /**
     * Zoom in/out camera to given scale. The value overwrites camera zoom.
     * Limits set with .minZoom and .maxZoom
     * @param zoom
     * @param enableTransition
     * @category Methods
     */ zoomTo(zoom, enableTransition = false) {
        this._isUserControllingZoom = false;
        this._zoomEnd = $e1f901905a002d12$var$clamp(zoom, this.minZoom, this.maxZoom);
        this._needsUpdate = true;
        if (!enableTransition) this._zoom = this._zoomEnd;
        const resolveImmediately = !enableTransition || $e1f901905a002d12$var$approxEquals(this._zoom, this._zoomEnd, this.restThreshold);
        this._changedZoom = 0;
        return this._createOnRestPromise(resolveImmediately);
    }
    /**
     * @deprecated `pan()` has been renamed to `truck()`
     * @category Methods
     */ pan(x, y, enableTransition = false) {
        console.warn('`pan` has been renamed to `truck`');
        return this.truck(x, y, enableTransition);
    }
    /**
     * Truck and pedestal camera using current azimuthal angle
     * @param x Horizontal translate amount
     * @param y Vertical translate amount
     * @param enableTransition Whether to move smoothly or immediately
     * @category Methods
     */ truck(x, y, enableTransition = false) {
        this._camera.updateMatrix();
        $e1f901905a002d12$var$_xColumn.setFromMatrixColumn(this._camera.matrix, 0);
        $e1f901905a002d12$var$_yColumn.setFromMatrixColumn(this._camera.matrix, 1);
        $e1f901905a002d12$var$_xColumn.multiplyScalar(x);
        $e1f901905a002d12$var$_yColumn.multiplyScalar(-y);
        const offset = $e1f901905a002d12$var$_v3A.copy($e1f901905a002d12$var$_xColumn).add($e1f901905a002d12$var$_yColumn);
        const to = $e1f901905a002d12$var$_v3B.copy(this._targetEnd).add(offset);
        return this.moveTo(to.x, to.y, to.z, enableTransition);
    }
    /**
     * Move forward / backward.
     * @param distance Amount to move forward / backward. Negative value to move backward
     * @param enableTransition Whether to move smoothly or immediately
     * @category Methods
     */ forward(distance, enableTransition = false) {
        $e1f901905a002d12$var$_v3A.setFromMatrixColumn(this._camera.matrix, 0);
        $e1f901905a002d12$var$_v3A.crossVectors(this._camera.up, $e1f901905a002d12$var$_v3A);
        $e1f901905a002d12$var$_v3A.multiplyScalar(distance);
        const to = $e1f901905a002d12$var$_v3B.copy(this._targetEnd).add($e1f901905a002d12$var$_v3A);
        return this.moveTo(to.x, to.y, to.z, enableTransition);
    }
    /**
     * Move up / down.
     * @param height Amount to move up / down. Negative value to move down
     * @param enableTransition Whether to move smoothly or immediately
     * @category Methods
     */ elevate(height, enableTransition = false) {
        $e1f901905a002d12$var$_v3A.copy(this._camera.up).multiplyScalar(height);
        return this.moveTo(this._targetEnd.x + $e1f901905a002d12$var$_v3A.x, this._targetEnd.y + $e1f901905a002d12$var$_v3A.y, this._targetEnd.z + $e1f901905a002d12$var$_v3A.z, enableTransition);
    }
    /**
     * Move target position to given point.
     * @param x x coord to move center position
     * @param y y coord to move center position
     * @param z z coord to move center position
     * @param enableTransition Whether to move smoothly or immediately
     * @category Methods
     */ moveTo(x, y, z, enableTransition = false) {
        this._isUserControllingTruck = false;
        const offset = $e1f901905a002d12$var$_v3A.set(x, y, z).sub(this._targetEnd);
        this._encloseToBoundary(this._targetEnd, offset, this.boundaryFriction);
        this._needsUpdate = true;
        if (!enableTransition) this._target.copy(this._targetEnd);
        const resolveImmediately = !enableTransition || $e1f901905a002d12$var$approxEquals(this._target.x, this._targetEnd.x, this.restThreshold) && $e1f901905a002d12$var$approxEquals(this._target.y, this._targetEnd.y, this.restThreshold) && $e1f901905a002d12$var$approxEquals(this._target.z, this._targetEnd.z, this.restThreshold);
        return this._createOnRestPromise(resolveImmediately);
    }
    /**
     * Look in the given point direction.
     * @param x point x.
     * @param y point y.
     * @param z point z.
     * @param enableTransition Whether to move smoothly or immediately.
     * @returns Transition end promise
     * @category Methods
     */ lookInDirectionOf(x, y, z, enableTransition = false) {
        const point = $e1f901905a002d12$var$_v3A.set(x, y, z);
        const direction = point.sub(this._targetEnd).normalize();
        const position = direction.multiplyScalar(-this._sphericalEnd.radius).add(this._targetEnd);
        return this.setPosition(position.x, position.y, position.z, enableTransition);
    }
    /**
     * Fit the viewport to the box or the bounding box of the object, using the nearest axis. paddings are in unit.
     * set `cover: true` to fill enter screen.
     * e.g.
     * ```
     * cameraControls.fitToBox( myMesh );
     * ```
     * @param box3OrObject Axis aligned bounding box to fit the view.
     * @param enableTransition Whether to move smoothly or immediately.
     * @param options | `<object>` { cover: boolean, paddingTop: number, paddingLeft: number, paddingBottom: number, paddingRight: number }
     * @returns Transition end promise
     * @category Methods
     */ fitToBox(box3OrObject, enableTransition, { cover: cover = false, paddingLeft: paddingLeft = 0, paddingRight: paddingRight = 0, paddingBottom: paddingBottom = 0, paddingTop: paddingTop = 0 } = {}) {
        const promises = [];
        const aabb = box3OrObject.isBox3 ? $e1f901905a002d12$var$_box3A.copy(box3OrObject) : $e1f901905a002d12$var$_box3A.setFromObject(box3OrObject);
        if (aabb.isEmpty()) {
            console.warn('camera-controls: fitTo() cannot be used with an empty box. Aborting');
            Promise.resolve();
        }
        // round to closest axis ( forward | backward | right | left | top | bottom )
        const theta = $e1f901905a002d12$var$roundToStep(this._sphericalEnd.theta, $e1f901905a002d12$var$PI_HALF);
        const phi = $e1f901905a002d12$var$roundToStep(this._sphericalEnd.phi, $e1f901905a002d12$var$PI_HALF);
        promises.push(this.rotateTo(theta, phi, enableTransition));
        const normal = $e1f901905a002d12$var$_v3A.setFromSpherical(this._sphericalEnd).normalize();
        const rotation = $e1f901905a002d12$var$_quaternionA.setFromUnitVectors(normal, $e1f901905a002d12$var$_AXIS_Z);
        const viewFromPolar = $e1f901905a002d12$var$approxEquals(Math.abs(normal.y), 1);
        if (viewFromPolar) rotation.multiply($e1f901905a002d12$var$_quaternionB.setFromAxisAngle($e1f901905a002d12$var$_AXIS_Y, theta));
        rotation.multiply(this._yAxisUpSpaceInverse);
        // make oriented bounding box
        const bb = $e1f901905a002d12$var$_box3B.makeEmpty();
        // left bottom back corner
        $e1f901905a002d12$var$_v3B.copy(aabb.min).applyQuaternion(rotation);
        bb.expandByPoint($e1f901905a002d12$var$_v3B);
        // right bottom back corner
        $e1f901905a002d12$var$_v3B.copy(aabb.min).setX(aabb.max.x).applyQuaternion(rotation);
        bb.expandByPoint($e1f901905a002d12$var$_v3B);
        // left top back corner
        $e1f901905a002d12$var$_v3B.copy(aabb.min).setY(aabb.max.y).applyQuaternion(rotation);
        bb.expandByPoint($e1f901905a002d12$var$_v3B);
        // right top back corner
        $e1f901905a002d12$var$_v3B.copy(aabb.max).setZ(aabb.min.z).applyQuaternion(rotation);
        bb.expandByPoint($e1f901905a002d12$var$_v3B);
        // left bottom front corner
        $e1f901905a002d12$var$_v3B.copy(aabb.min).setZ(aabb.max.z).applyQuaternion(rotation);
        bb.expandByPoint($e1f901905a002d12$var$_v3B);
        // right bottom front corner
        $e1f901905a002d12$var$_v3B.copy(aabb.max).setY(aabb.min.y).applyQuaternion(rotation);
        bb.expandByPoint($e1f901905a002d12$var$_v3B);
        // left top front corner
        $e1f901905a002d12$var$_v3B.copy(aabb.max).setX(aabb.min.x).applyQuaternion(rotation);
        bb.expandByPoint($e1f901905a002d12$var$_v3B);
        // right top front corner
        $e1f901905a002d12$var$_v3B.copy(aabb.max).applyQuaternion(rotation);
        bb.expandByPoint($e1f901905a002d12$var$_v3B);
        // add padding
        bb.min.x -= paddingLeft;
        bb.min.y -= paddingBottom;
        bb.max.x += paddingRight;
        bb.max.y += paddingTop;
        rotation.setFromUnitVectors($e1f901905a002d12$var$_AXIS_Z, normal);
        if (viewFromPolar) rotation.premultiply($e1f901905a002d12$var$_quaternionB.invert());
        rotation.premultiply(this._yAxisUpSpace);
        const bbSize = bb.getSize($e1f901905a002d12$var$_v3A);
        const center = bb.getCenter($e1f901905a002d12$var$_v3B).applyQuaternion(rotation);
        if ($e1f901905a002d12$var$isPerspectiveCamera(this._camera)) {
            const distance = this.getDistanceToFitBox(bbSize.x, bbSize.y, bbSize.z, cover);
            promises.push(this.moveTo(center.x, center.y, center.z, enableTransition));
            promises.push(this.dollyTo(distance, enableTransition));
            promises.push(this.setFocalOffset(0, 0, 0, enableTransition));
        } else if ($e1f901905a002d12$var$isOrthographicCamera(this._camera)) {
            const camera = this._camera;
            const width = camera.right - camera.left;
            const height = camera.top - camera.bottom;
            const zoom = cover ? Math.max(width / bbSize.x, height / bbSize.y) : Math.min(width / bbSize.x, height / bbSize.y);
            promises.push(this.moveTo(center.x, center.y, center.z, enableTransition));
            promises.push(this.zoomTo(zoom, enableTransition));
            promises.push(this.setFocalOffset(0, 0, 0, enableTransition));
        }
        return Promise.all(promises);
    }
    /**
     * Fit the viewport to the sphere or the bounding sphere of the object.
     * @param sphereOrMesh
     * @param enableTransition
     * @category Methods
     */ fitToSphere(sphereOrMesh, enableTransition) {
        const promises = [];
        const isObject3D = 'isObject3D' in sphereOrMesh;
        const boundingSphere = isObject3D ? $e1f901905a002d12$export$2e2bcd8739ae039.createBoundingSphere(sphereOrMesh, $e1f901905a002d12$var$_sphere) : $e1f901905a002d12$var$_sphere.copy(sphereOrMesh);
        promises.push(this.moveTo(boundingSphere.center.x, boundingSphere.center.y, boundingSphere.center.z, enableTransition));
        if ($e1f901905a002d12$var$isPerspectiveCamera(this._camera)) {
            const distanceToFit = this.getDistanceToFitSphere(boundingSphere.radius);
            promises.push(this.dollyTo(distanceToFit, enableTransition));
        } else if ($e1f901905a002d12$var$isOrthographicCamera(this._camera)) {
            const width = this._camera.right - this._camera.left;
            const height = this._camera.top - this._camera.bottom;
            const diameter = 2 * boundingSphere.radius;
            const zoom = Math.min(width / diameter, height / diameter);
            promises.push(this.zoomTo(zoom, enableTransition));
        }
        promises.push(this.setFocalOffset(0, 0, 0, enableTransition));
        return Promise.all(promises);
    }
    /**
     * Look at the `target` from the `position`.
     * @param positionX
     * @param positionY
     * @param positionZ
     * @param targetX
     * @param targetY
     * @param targetZ
     * @param enableTransition
     * @category Methods
     */ setLookAt(positionX, positionY, positionZ, targetX, targetY, targetZ, enableTransition = false) {
        this._isUserControllingRotate = false;
        this._isUserControllingDolly = false;
        this._isUserControllingTruck = false;
        this._lastDollyDirection = $e1f901905a002d12$var$DOLLY_DIRECTION.NONE;
        this._changedDolly = 0;
        const target = $e1f901905a002d12$var$_v3B.set(targetX, targetY, targetZ);
        const position = $e1f901905a002d12$var$_v3A.set(positionX, positionY, positionZ);
        this._targetEnd.copy(target);
        this._sphericalEnd.setFromVector3(position.sub(target).applyQuaternion(this._yAxisUpSpace));
        this.normalizeRotations();
        this._needsUpdate = true;
        if (!enableTransition) {
            this._target.copy(this._targetEnd);
            this._spherical.copy(this._sphericalEnd);
        }
        const resolveImmediately = !enableTransition || $e1f901905a002d12$var$approxEquals(this._target.x, this._targetEnd.x, this.restThreshold) && $e1f901905a002d12$var$approxEquals(this._target.y, this._targetEnd.y, this.restThreshold) && $e1f901905a002d12$var$approxEquals(this._target.z, this._targetEnd.z, this.restThreshold) && $e1f901905a002d12$var$approxEquals(this._spherical.theta, this._sphericalEnd.theta, this.restThreshold) && $e1f901905a002d12$var$approxEquals(this._spherical.phi, this._sphericalEnd.phi, this.restThreshold) && $e1f901905a002d12$var$approxEquals(this._spherical.radius, this._sphericalEnd.radius, this.restThreshold);
        return this._createOnRestPromise(resolveImmediately);
    }
    /**
     * Similar to setLookAt, but it interpolates between two states.
     * @param positionAX
     * @param positionAY
     * @param positionAZ
     * @param targetAX
     * @param targetAY
     * @param targetAZ
     * @param positionBX
     * @param positionBY
     * @param positionBZ
     * @param targetBX
     * @param targetBY
     * @param targetBZ
     * @param t
     * @param enableTransition
     * @category Methods
     */ lerpLookAt(positionAX, positionAY, positionAZ, targetAX, targetAY, targetAZ, positionBX, positionBY, positionBZ, targetBX, targetBY, targetBZ, t, enableTransition = false) {
        this._isUserControllingRotate = false;
        this._isUserControllingDolly = false;
        this._isUserControllingTruck = false;
        this._lastDollyDirection = $e1f901905a002d12$var$DOLLY_DIRECTION.NONE;
        this._changedDolly = 0;
        const targetA = $e1f901905a002d12$var$_v3A.set(targetAX, targetAY, targetAZ);
        const positionA = $e1f901905a002d12$var$_v3B.set(positionAX, positionAY, positionAZ);
        $e1f901905a002d12$var$_sphericalA.setFromVector3(positionA.sub(targetA).applyQuaternion(this._yAxisUpSpace));
        const targetB = $e1f901905a002d12$var$_v3C.set(targetBX, targetBY, targetBZ);
        const positionB = $e1f901905a002d12$var$_v3B.set(positionBX, positionBY, positionBZ);
        $e1f901905a002d12$var$_sphericalB.setFromVector3(positionB.sub(targetB).applyQuaternion(this._yAxisUpSpace));
        this._targetEnd.copy(targetA.lerp(targetB, t)); // tricky
        const deltaTheta = $e1f901905a002d12$var$_sphericalB.theta - $e1f901905a002d12$var$_sphericalA.theta;
        const deltaPhi = $e1f901905a002d12$var$_sphericalB.phi - $e1f901905a002d12$var$_sphericalA.phi;
        const deltaRadius = $e1f901905a002d12$var$_sphericalB.radius - $e1f901905a002d12$var$_sphericalA.radius;
        this._sphericalEnd.set($e1f901905a002d12$var$_sphericalA.radius + deltaRadius * t, $e1f901905a002d12$var$_sphericalA.phi + deltaPhi * t, $e1f901905a002d12$var$_sphericalA.theta + deltaTheta * t);
        this.normalizeRotations();
        this._needsUpdate = true;
        if (!enableTransition) {
            this._target.copy(this._targetEnd);
            this._spherical.copy(this._sphericalEnd);
        }
        const resolveImmediately = !enableTransition || $e1f901905a002d12$var$approxEquals(this._target.x, this._targetEnd.x, this.restThreshold) && $e1f901905a002d12$var$approxEquals(this._target.y, this._targetEnd.y, this.restThreshold) && $e1f901905a002d12$var$approxEquals(this._target.z, this._targetEnd.z, this.restThreshold) && $e1f901905a002d12$var$approxEquals(this._spherical.theta, this._sphericalEnd.theta, this.restThreshold) && $e1f901905a002d12$var$approxEquals(this._spherical.phi, this._sphericalEnd.phi, this.restThreshold) && $e1f901905a002d12$var$approxEquals(this._spherical.radius, this._sphericalEnd.radius, this.restThreshold);
        return this._createOnRestPromise(resolveImmediately);
    }
    /**
     * Set angle and distance by given position.
     * An alias of `setLookAt()`, without target change. Thus keep gazing at the current target
     * @param positionX
     * @param positionY
     * @param positionZ
     * @param enableTransition
     * @category Methods
     */ setPosition(positionX, positionY, positionZ, enableTransition = false) {
        return this.setLookAt(positionX, positionY, positionZ, this._targetEnd.x, this._targetEnd.y, this._targetEnd.z, enableTransition);
    }
    /**
     * Set the target position where gaze at.
     * An alias of `setLookAt()`, without position change. Thus keep the same position.
     * @param targetX
     * @param targetY
     * @param targetZ
     * @param enableTransition
     * @category Methods
     */ setTarget(targetX, targetY, targetZ, enableTransition = false) {
        const pos = this.getPosition($e1f901905a002d12$var$_v3A);
        const promise = this.setLookAt(pos.x, pos.y, pos.z, targetX, targetY, targetZ, enableTransition);
        // see https://github.com/yomotsu/camera-controls/issues/335
        this._sphericalEnd.phi = $e1f901905a002d12$var$clamp(this._sphericalEnd.phi, this.minPolarAngle, this.maxPolarAngle);
        return promise;
    }
    /**
     * Set focal offset using the screen parallel coordinates. z doesn't affect in Orthographic as with Dolly.
     * @param x
     * @param y
     * @param z
     * @param enableTransition
     * @category Methods
     */ setFocalOffset(x, y, z, enableTransition = false) {
        this._isUserControllingOffset = false;
        this._focalOffsetEnd.set(x, y, z);
        this._needsUpdate = true;
        if (!enableTransition) this._focalOffset.copy(this._focalOffsetEnd);
        const resolveImmediately = !enableTransition || $e1f901905a002d12$var$approxEquals(this._focalOffset.x, this._focalOffsetEnd.x, this.restThreshold) && $e1f901905a002d12$var$approxEquals(this._focalOffset.y, this._focalOffsetEnd.y, this.restThreshold) && $e1f901905a002d12$var$approxEquals(this._focalOffset.z, this._focalOffsetEnd.z, this.restThreshold);
        return this._createOnRestPromise(resolveImmediately);
    }
    /**
     * Set orbit point without moving the camera.
     * SHOULD NOT RUN DURING ANIMATIONS. `setOrbitPoint()` will immediately fix the positions.
     * @param targetX
     * @param targetY
     * @param targetZ
     * @category Methods
     */ setOrbitPoint(targetX, targetY, targetZ) {
        this._camera.updateMatrixWorld();
        $e1f901905a002d12$var$_xColumn.setFromMatrixColumn(this._camera.matrixWorldInverse, 0);
        $e1f901905a002d12$var$_yColumn.setFromMatrixColumn(this._camera.matrixWorldInverse, 1);
        $e1f901905a002d12$var$_zColumn.setFromMatrixColumn(this._camera.matrixWorldInverse, 2);
        const position = $e1f901905a002d12$var$_v3A.set(targetX, targetY, targetZ);
        const distance = position.distanceTo(this._camera.position);
        const cameraToPoint = position.sub(this._camera.position);
        $e1f901905a002d12$var$_xColumn.multiplyScalar(cameraToPoint.x);
        $e1f901905a002d12$var$_yColumn.multiplyScalar(cameraToPoint.y);
        $e1f901905a002d12$var$_zColumn.multiplyScalar(cameraToPoint.z);
        $e1f901905a002d12$var$_v3A.copy($e1f901905a002d12$var$_xColumn).add($e1f901905a002d12$var$_yColumn).add($e1f901905a002d12$var$_zColumn);
        $e1f901905a002d12$var$_v3A.z = $e1f901905a002d12$var$_v3A.z + distance;
        this.dollyTo(distance, false);
        this.setFocalOffset(-$e1f901905a002d12$var$_v3A.x, $e1f901905a002d12$var$_v3A.y, -$e1f901905a002d12$var$_v3A.z, false);
        this.moveTo(targetX, targetY, targetZ, false);
    }
    /**
     * Set the boundary box that encloses the target of the camera. box3 is in THREE.Box3
     * @param box3
     * @category Methods
     */ setBoundary(box3) {
        if (!box3) {
            this._boundary.min.set(-Infinity, -Infinity, -Infinity);
            this._boundary.max.set(Infinity, Infinity, Infinity);
            this._needsUpdate = true;
            return;
        }
        this._boundary.copy(box3);
        this._boundary.clampPoint(this._targetEnd, this._targetEnd);
        this._needsUpdate = true;
    }
    /**
     * Set (or unset) the current viewport.
     * Set this when you want to use renderer viewport and .dollyToCursor feature at the same time.
     * @param viewportOrX
     * @param y
     * @param width
     * @param height
     * @category Methods
     */ setViewport(viewportOrX, y, width, height) {
        if (viewportOrX === null) {
            this._viewport = null;
            return;
        }
        this._viewport = this._viewport || new $e1f901905a002d12$var$THREE.Vector4();
        if (typeof viewportOrX === 'number') this._viewport.set(viewportOrX, y, width, height);
        else this._viewport.copy(viewportOrX);
    }
    /**
     * Calculate the distance to fit the box.
     * @param width box width
     * @param height box height
     * @param depth box depth
     * @returns distance
     * @category Methods
     */ getDistanceToFitBox(width, height, depth, cover = false) {
        if ($e1f901905a002d12$var$notSupportedInOrthographicCamera(this._camera, 'getDistanceToFitBox')) return this._spherical.radius;
        const boundingRectAspect = width / height;
        const fov = this._camera.getEffectiveFOV() * $e1f901905a002d12$var$DEG2RAD;
        const aspect = this._camera.aspect;
        const heightToFit = (cover ? boundingRectAspect > aspect : boundingRectAspect < aspect) ? height : width / aspect;
        return heightToFit * 0.5 / Math.tan(fov * 0.5) + depth * 0.5;
    }
    /**
     * Calculate the distance to fit the sphere.
     * @param radius sphere radius
     * @returns distance
     * @category Methods
     */ getDistanceToFitSphere(radius) {
        if ($e1f901905a002d12$var$notSupportedInOrthographicCamera(this._camera, 'getDistanceToFitSphere')) return this._spherical.radius;
        // https://stackoverflow.com/a/44849975
        const vFOV = this._camera.getEffectiveFOV() * $e1f901905a002d12$var$DEG2RAD;
        const hFOV = Math.atan(Math.tan(vFOV * 0.5) * this._camera.aspect) * 2;
        const fov = 1 < this._camera.aspect ? vFOV : hFOV;
        return radius / Math.sin(fov * 0.5);
    }
    /**
     * Returns the orbit center position, where the camera looking at.
     * @param out The receiving Vector3 instance to copy the result
     * @param receiveEndValue Whether receive the transition end coords or current. default is `true`
     * @category Methods
     */ getTarget(out, receiveEndValue = true) {
        const _out = !!out && out.isVector3 ? out : new $e1f901905a002d12$var$THREE.Vector3();
        return _out.copy(receiveEndValue ? this._targetEnd : this._target);
    }
    /**
     * Returns the camera position.
     * @param out The receiving Vector3 instance to copy the result
     * @param receiveEndValue Whether receive the transition end coords or current. default is `true`
     * @category Methods
     */ getPosition(out, receiveEndValue = true) {
        const _out = !!out && out.isVector3 ? out : new $e1f901905a002d12$var$THREE.Vector3();
        return _out.setFromSpherical(receiveEndValue ? this._sphericalEnd : this._spherical).applyQuaternion(this._yAxisUpSpaceInverse).add(receiveEndValue ? this._targetEnd : this._target);
    }
    /**
     * Returns the spherical coordinates of the orbit.
     * @param out The receiving Spherical instance to copy the result
     * @param receiveEndValue Whether receive the transition end coords or current. default is `true`
     * @category Methods
     */ getSpherical(out, receiveEndValue = true) {
        const _out = out || new $e1f901905a002d12$var$THREE.Spherical();
        return _out.copy(receiveEndValue ? this._sphericalEnd : this._spherical);
    }
    /**
     * Returns the focal offset, which is how much the camera appears to be translated in screen parallel coordinates.
     * @param out The receiving Vector3 instance to copy the result
     * @param receiveEndValue Whether receive the transition end coords or current. default is `true`
     * @category Methods
     */ getFocalOffset(out, receiveEndValue = true) {
        const _out = !!out && out.isVector3 ? out : new $e1f901905a002d12$var$THREE.Vector3();
        return _out.copy(receiveEndValue ? this._focalOffsetEnd : this._focalOffset);
    }
    /**
     * Normalize camera azimuth angle rotation between 0 and 360 degrees.
     * @category Methods
     */ normalizeRotations() {
        this._sphericalEnd.theta = this._sphericalEnd.theta % $e1f901905a002d12$var$PI_2;
        if (this._sphericalEnd.theta < 0) this._sphericalEnd.theta += $e1f901905a002d12$var$PI_2;
        this._spherical.theta += $e1f901905a002d12$var$PI_2 * Math.round((this._sphericalEnd.theta - this._spherical.theta) / $e1f901905a002d12$var$PI_2);
    }
    /**
     * stop all transitions.
     */ stop() {
        this._focalOffset.copy(this._focalOffsetEnd);
        this._target.copy(this._targetEnd);
        this._spherical.copy(this._sphericalEnd);
        this._zoom = this._zoomEnd;
    }
    /**
     * Reset all rotation and position to defaults.
     * @param enableTransition
     * @category Methods
     */ reset(enableTransition = false) {
        if (!$e1f901905a002d12$var$approxEquals(this._camera.up.x, this._cameraUp0.x) || !$e1f901905a002d12$var$approxEquals(this._camera.up.y, this._cameraUp0.y) || !$e1f901905a002d12$var$approxEquals(this._camera.up.z, this._cameraUp0.z)) {
            this._camera.up.copy(this._cameraUp0);
            const position = this.getPosition($e1f901905a002d12$var$_v3A);
            this.updateCameraUp();
            this.setPosition(position.x, position.y, position.z);
        }
        const promises = [
            this.setLookAt(this._position0.x, this._position0.y, this._position0.z, this._target0.x, this._target0.y, this._target0.z, enableTransition),
            this.setFocalOffset(this._focalOffset0.x, this._focalOffset0.y, this._focalOffset0.z, enableTransition),
            this.zoomTo(this._zoom0, enableTransition)
        ];
        return Promise.all(promises);
    }
    /**
     * Set current camera position as the default position.
     * @category Methods
     */ saveState() {
        this._cameraUp0.copy(this._camera.up);
        this.getTarget(this._target0);
        this.getPosition(this._position0);
        this._zoom0 = this._zoom;
        this._focalOffset0.copy(this._focalOffset);
    }
    /**
     * Sync camera-up direction.
     * When camera-up vector is changed, `.updateCameraUp()` must be called.
     * @category Methods
     */ updateCameraUp() {
        this._yAxisUpSpace.setFromUnitVectors(this._camera.up, $e1f901905a002d12$var$_AXIS_Y);
        this._yAxisUpSpaceInverse.copy(this._yAxisUpSpace).invert();
    }
    /**
     * Apply current camera-up direction to the camera.
     * The orbit system will be re-initialized with the current position.
     * @category Methods
     */ applyCameraUp() {
        const cameraDirection = $e1f901905a002d12$var$_v3A.subVectors(this._target, this._camera.position).normalize();
        // So first find the vector off to the side, orthogonal to both this.object.up and
        // the "view" vector.
        const side = $e1f901905a002d12$var$_v3B.crossVectors(cameraDirection, this._camera.up);
        // Then find the vector orthogonal to both this "side" vector and the "view" vector.
        // This vector will be the new "up" vector.
        this._camera.up.crossVectors(side, cameraDirection).normalize();
        this._camera.updateMatrixWorld();
        const position = this.getPosition($e1f901905a002d12$var$_v3A);
        this.updateCameraUp();
        this.setPosition(position.x, position.y, position.z);
    }
    /**
     * Update camera position and directions.
     * This should be called in your tick loop every time, and returns true if re-rendering is needed.
     * @param delta
     * @returns updated
     * @category Methods
     */ update(delta) {
		if( ! this._enabled ) return ///	
		
        const deltaTheta = this._sphericalEnd.theta - this._spherical.theta;
        const deltaPhi = this._sphericalEnd.phi - this._spherical.phi;
        const deltaRadius = this._sphericalEnd.radius - this._spherical.radius;
        const deltaTarget = $e1f901905a002d12$var$_deltaTarget.subVectors(this._targetEnd, this._target);
        const deltaOffset = $e1f901905a002d12$var$_deltaOffset.subVectors(this._focalOffsetEnd, this._focalOffset);
        const deltaZoom = this._zoomEnd - this._zoom;
        // update theta
        if ($e1f901905a002d12$var$approxZero(deltaTheta)) {
            this._thetaVelocity.value = 0;
            this._spherical.theta = this._sphericalEnd.theta;
        } else {
            const smoothTime = this._isUserControllingRotate ? this.draggingSmoothTime : this.smoothTime;
            this._spherical.theta = $e1f901905a002d12$var$smoothDamp(this._spherical.theta, this._sphericalEnd.theta, this._thetaVelocity, smoothTime, Infinity, delta);
            this._needsUpdate = true;
        }
        // update phi
        if ($e1f901905a002d12$var$approxZero(deltaPhi)) {
            this._phiVelocity.value = 0;
            this._spherical.phi = this._sphericalEnd.phi;
        } else {
            const smoothTime = this._isUserControllingRotate ? this.draggingSmoothTime : this.smoothTime;
            this._spherical.phi = $e1f901905a002d12$var$smoothDamp(this._spherical.phi, this._sphericalEnd.phi, this._phiVelocity, smoothTime, Infinity, delta);
            this._needsUpdate = true;
        }
        // update distance
        if ($e1f901905a002d12$var$approxZero(deltaRadius)) {
            this._radiusVelocity.value = 0;
            this._spherical.radius = this._sphericalEnd.radius;
        } else {
            const smoothTime = this._isUserControllingDolly ? this.draggingSmoothTime : this.smoothTime;
            this._spherical.radius = $e1f901905a002d12$var$smoothDamp(this._spherical.radius, this._sphericalEnd.radius, this._radiusVelocity, smoothTime, this.maxSpeed, delta);
            this._needsUpdate = true;
        }
        // update target position
        if ($e1f901905a002d12$var$approxZero(deltaTarget.x) && $e1f901905a002d12$var$approxZero(deltaTarget.y) && $e1f901905a002d12$var$approxZero(deltaTarget.z)) {
            this._targetVelocity.set(0, 0, 0);
            this._target.copy(this._targetEnd);
        } else {
            const smoothTime = this._isUserControllingTruck ? this.draggingSmoothTime : this.smoothTime;
            $e1f901905a002d12$var$smoothDampVec3(this._target, this._targetEnd, this._targetVelocity, smoothTime, this.maxSpeed, delta, this._target);
            this._needsUpdate = true;
        }
        // update focalOffset
        if ($e1f901905a002d12$var$approxZero(deltaOffset.x) && $e1f901905a002d12$var$approxZero(deltaOffset.y) && $e1f901905a002d12$var$approxZero(deltaOffset.z)) {
            this._focalOffsetVelocity.set(0, 0, 0);
            this._focalOffset.copy(this._focalOffsetEnd);
        } else {
            const smoothTime = this._isUserControllingOffset ? this.draggingSmoothTime : this.smoothTime;
            $e1f901905a002d12$var$smoothDampVec3(this._focalOffset, this._focalOffsetEnd, this._focalOffsetVelocity, smoothTime, this.maxSpeed, delta, this._focalOffset);
            this._needsUpdate = true;
        }
        // update zoom
        if ($e1f901905a002d12$var$approxZero(deltaZoom)) {
            this._zoomVelocity.value = 0;
            this._zoom = this._zoomEnd;
        } else {
            const smoothTime = this._isUserControllingZoom ? this.draggingSmoothTime : this.smoothTime;
            this._zoom = $e1f901905a002d12$var$smoothDamp(this._zoom, this._zoomEnd, this._zoomVelocity, smoothTime, Infinity, delta);
        }
        if (this.dollyToCursor) {
            if ($e1f901905a002d12$var$isPerspectiveCamera(this._camera) && this._changedDolly !== 0) {
                const dollyControlAmount = this._spherical.radius - this._lastDistance;
                const camera = this._camera;
                const cameraDirection = this._getCameraDirection($e1f901905a002d12$var$_cameraDirection);
                const planeX = $e1f901905a002d12$var$_v3A.copy(cameraDirection).cross(camera.up).normalize();
                if (planeX.lengthSq() === 0) planeX.x = 1.0;
                const planeY = $e1f901905a002d12$var$_v3B.crossVectors(planeX, cameraDirection);
                const worldToScreen = this._sphericalEnd.radius * Math.tan(camera.getEffectiveFOV() * $e1f901905a002d12$var$DEG2RAD * 0.5);
                const prevRadius = this._sphericalEnd.radius - dollyControlAmount;
                const lerpRatio = (prevRadius - this._sphericalEnd.radius) / this._sphericalEnd.radius;
                const cursor = $e1f901905a002d12$var$_v3C.copy(this._targetEnd).add(planeX.multiplyScalar(this._dollyControlCoord.x * worldToScreen * camera.aspect)).add(planeY.multiplyScalar(this._dollyControlCoord.y * worldToScreen));
                const newTargetEnd = $e1f901905a002d12$var$_v3A.copy(this._targetEnd).lerp(cursor, lerpRatio);
                const isMin = this._lastDollyDirection === $e1f901905a002d12$var$DOLLY_DIRECTION.IN && this._spherical.radius <= this.minDistance;
                const isMax = this._lastDollyDirection === $e1f901905a002d12$var$DOLLY_DIRECTION.OUT && this.maxDistance <= this._spherical.radius;
                if (this.infinityDolly && (isMin || isMax)) {
                    this._sphericalEnd.radius -= dollyControlAmount;
                    this._spherical.radius -= dollyControlAmount;
                    const dollyAmount = $e1f901905a002d12$var$_v3B.copy(cameraDirection).multiplyScalar(-dollyControlAmount);
                    newTargetEnd.add(dollyAmount);
                }
                // target position may be moved beyond boundary.
                this._boundary.clampPoint(newTargetEnd, newTargetEnd);
                const targetEndDiff = $e1f901905a002d12$var$_v3B.subVectors(newTargetEnd, this._targetEnd);
                this._targetEnd.copy(newTargetEnd);
                this._target.add(targetEndDiff);
                this._changedDolly -= dollyControlAmount;
                if ($e1f901905a002d12$var$approxZero(this._changedDolly)) this._changedDolly = 0;
            } else if ($e1f901905a002d12$var$isOrthographicCamera(this._camera) && this._changedZoom !== 0) {
                const dollyControlAmount = this._zoom - this._lastZoom;
                const camera = this._camera;
                const worldCursorPosition = $e1f901905a002d12$var$_v3A.set(this._dollyControlCoord.x, this._dollyControlCoord.y, (camera.near + camera.far) / (camera.near - camera.far)).unproject(camera);
                const quaternion = $e1f901905a002d12$var$_v3B.set(0, 0, -1).applyQuaternion(camera.quaternion);
                const cursor = $e1f901905a002d12$var$_v3C.copy(worldCursorPosition).add(quaternion.multiplyScalar(-worldCursorPosition.dot(camera.up)));
                const prevZoom = this._zoom - dollyControlAmount;
                const lerpRatio = -(prevZoom - this._zoom) / this._zoom;
                // find the "distance" (aka plane constant in three.js) of Plane
                // from a given position (this._targetEnd) and normal vector (cameraDirection)
                // https://www.maplesoft.com/support/help/maple/view.aspx?path=MathApps%2FEquationOfAPlaneNormal#bkmrk0
                const cameraDirection = this._getCameraDirection($e1f901905a002d12$var$_cameraDirection);
                const prevPlaneConstant = this._targetEnd.dot(cameraDirection);
                const newTargetEnd = $e1f901905a002d12$var$_v3A.copy(this._targetEnd).lerp(cursor, lerpRatio);
                const newPlaneConstant = newTargetEnd.dot(cameraDirection);
                // Pull back the camera depth that has moved, to be the camera stationary as zoom
                const pullBack = cameraDirection.multiplyScalar(newPlaneConstant - prevPlaneConstant);
                newTargetEnd.sub(pullBack);
                // target position may be moved beyond boundary.
                this._boundary.clampPoint(newTargetEnd, newTargetEnd);
                const targetEndDiff = $e1f901905a002d12$var$_v3B.subVectors(newTargetEnd, this._targetEnd);
                this._targetEnd.copy(newTargetEnd);
                this._target.add(targetEndDiff);
                // this._target.copy( this._targetEnd );
                this._changedZoom -= dollyControlAmount;
                if ($e1f901905a002d12$var$approxZero(this._changedZoom)) this._changedZoom = 0;
            }
        }
        if (this._camera.zoom !== this._zoom) {
            this._camera.zoom = this._zoom;
            this._camera.updateProjectionMatrix();
            this._updateNearPlaneCorners();
            this._needsUpdate = true;
        }
        this._dragNeedsUpdate = true;
        // collision detection
        const maxDistance = this._collisionTest();
        this._spherical.radius = Math.min(this._spherical.radius, maxDistance);
        // decompose spherical to the camera position
        this._spherical.makeSafe();
        this._camera.position.setFromSpherical(this._spherical).applyQuaternion(this._yAxisUpSpaceInverse).add(this._target);
        this._camera.lookAt(this._target);
        // set offset after the orbit movement
        const affectOffset = !$e1f901905a002d12$var$approxZero(this._focalOffset.x) || !$e1f901905a002d12$var$approxZero(this._focalOffset.y) || !$e1f901905a002d12$var$approxZero(this._focalOffset.z);
        if (affectOffset) {
            $e1f901905a002d12$var$_xColumn.setFromMatrixColumn(this._camera.matrix, 0);
            $e1f901905a002d12$var$_yColumn.setFromMatrixColumn(this._camera.matrix, 1);
            $e1f901905a002d12$var$_zColumn.setFromMatrixColumn(this._camera.matrix, 2);
            $e1f901905a002d12$var$_xColumn.multiplyScalar(this._focalOffset.x);
            $e1f901905a002d12$var$_yColumn.multiplyScalar(-this._focalOffset.y);
            $e1f901905a002d12$var$_zColumn.multiplyScalar(this._focalOffset.z); // notice: z-offset will not affect in Orthographic.
            $e1f901905a002d12$var$_v3A.copy($e1f901905a002d12$var$_xColumn).add($e1f901905a002d12$var$_yColumn).add($e1f901905a002d12$var$_zColumn);
            this._camera.position.add($e1f901905a002d12$var$_v3A);
            this._camera.updateMatrixWorld();
        }
        if (this._boundaryEnclosesCamera) this._encloseToBoundary(this._camera.position.copy(this._target), $e1f901905a002d12$var$_v3A.setFromSpherical(this._spherical).applyQuaternion(this._yAxisUpSpaceInverse), 1.0);
        const updated = this._needsUpdate;
        if (updated && !this._updatedLastTime) {
            this._hasRested = false;
            this.dispatchEvent({
                type: 'wake'
            });
            this.dispatchEvent({
                type: 'update'
            });
        } else if (updated) {
            this.dispatchEvent({
                type: 'update'
            });
            if ($e1f901905a002d12$var$approxZero(deltaTheta, this.restThreshold) && $e1f901905a002d12$var$approxZero(deltaPhi, this.restThreshold) && $e1f901905a002d12$var$approxZero(deltaRadius, this.restThreshold) && $e1f901905a002d12$var$approxZero(deltaTarget.x, this.restThreshold) && $e1f901905a002d12$var$approxZero(deltaTarget.y, this.restThreshold) && $e1f901905a002d12$var$approxZero(deltaTarget.z, this.restThreshold) && $e1f901905a002d12$var$approxZero(deltaOffset.x, this.restThreshold) && $e1f901905a002d12$var$approxZero(deltaOffset.y, this.restThreshold) && $e1f901905a002d12$var$approxZero(deltaOffset.z, this.restThreshold) && $e1f901905a002d12$var$approxZero(deltaZoom, this.restThreshold) && !this._hasRested) {
                this._hasRested = true;
                this.dispatchEvent({
                    type: 'rest'
                });
            }
        } else if (!updated && this._updatedLastTime) this.dispatchEvent({
            type: 'sleep'
        });
        this._lastDistance = this._spherical.radius;
        this._lastZoom = this._zoom;
        this._updatedLastTime = updated;
        this._needsUpdate = false;
        return updated;
    }
    /**
     * Get all state in JSON string
     * @category Methods
     */ toJSON() {
        return JSON.stringify({
            enabled: this._enabled,
            minDistance: this.minDistance,
            maxDistance: $e1f901905a002d12$var$infinityToMaxNumber(this.maxDistance),
            minZoom: this.minZoom,
            maxZoom: $e1f901905a002d12$var$infinityToMaxNumber(this.maxZoom),
            minPolarAngle: this.minPolarAngle,
            maxPolarAngle: $e1f901905a002d12$var$infinityToMaxNumber(this.maxPolarAngle),
            minAzimuthAngle: $e1f901905a002d12$var$infinityToMaxNumber(this.minAzimuthAngle),
            maxAzimuthAngle: $e1f901905a002d12$var$infinityToMaxNumber(this.maxAzimuthAngle),
            smoothTime: this.smoothTime,
            draggingSmoothTime: this.draggingSmoothTime,
            dollySpeed: this.dollySpeed,
            truckSpeed: this.truckSpeed,
            dollyToCursor: this.dollyToCursor,
            target: this._targetEnd.toArray(),
            position: $e1f901905a002d12$var$_v3A.setFromSpherical(this._sphericalEnd).add(this._targetEnd).toArray(),
            zoom: this._zoomEnd,
            focalOffset: this._focalOffsetEnd.toArray(),
            target0: this._target0.toArray(),
            position0: this._position0.toArray(),
            zoom0: this._zoom0,
            focalOffset0: this._focalOffset0.toArray()
        });
    }
    /**
     * Reproduce the control state with JSON. enableTransition is where anim or not in a boolean.
     * @param json
     * @param enableTransition
     * @category Methods
     */ fromJSON(json, enableTransition = false) {
        const obj = JSON.parse(json);
        this.enabled = obj.enabled;
        this.minDistance = obj.minDistance;
        this.maxDistance = $e1f901905a002d12$var$maxNumberToInfinity(obj.maxDistance);
        this.minZoom = obj.minZoom;
        this.maxZoom = $e1f901905a002d12$var$maxNumberToInfinity(obj.maxZoom);
        this.minPolarAngle = obj.minPolarAngle;
        this.maxPolarAngle = $e1f901905a002d12$var$maxNumberToInfinity(obj.maxPolarAngle);
        this.minAzimuthAngle = $e1f901905a002d12$var$maxNumberToInfinity(obj.minAzimuthAngle);
        this.maxAzimuthAngle = $e1f901905a002d12$var$maxNumberToInfinity(obj.maxAzimuthAngle);
        this.smoothTime = obj.smoothTime;
        this.draggingSmoothTime = obj.draggingSmoothTime;
        this.dollySpeed = obj.dollySpeed;
        this.truckSpeed = obj.truckSpeed;
        this.dollyToCursor = obj.dollyToCursor;
        this._target0.fromArray(obj.target0);
        this._position0.fromArray(obj.position0);
        this._zoom0 = obj.zoom0;
        this._focalOffset0.fromArray(obj.focalOffset0);
        this.moveTo(obj.target[0], obj.target[1], obj.target[2], enableTransition);
        $e1f901905a002d12$var$_sphericalA.setFromVector3($e1f901905a002d12$var$_v3A.fromArray(obj.position).sub(this._targetEnd).applyQuaternion(this._yAxisUpSpace));
        this.rotateTo($e1f901905a002d12$var$_sphericalA.theta, $e1f901905a002d12$var$_sphericalA.phi, enableTransition);
        this.dollyTo($e1f901905a002d12$var$_sphericalA.radius, enableTransition);
        this.zoomTo(obj.zoom, enableTransition);
        this.setFocalOffset(obj.focalOffset[0], obj.focalOffset[1], obj.focalOffset[2], enableTransition);
        this._needsUpdate = true;
    }
    /**
     * Attach all internal event handlers to enable drag control.
     * @category Methods
     */ connect(domElement) {
        if (this._domElement) {
            console.warn('camera-controls is already connected.');
            return;
        }
        domElement.setAttribute('data-camera-controls-version', $e1f901905a002d12$var$VERSION);
        this._addAllEventListeners(domElement);
        this._getClientRect(this._elementRect);
    }
    /**
     * Detach all internal event handlers to disable drag control.
     */ disconnect() {
        this.cancel();
        this._removeAllEventListeners();
        if (this._domElement) {
            this._domElement.removeAttribute('data-camera-controls-version');
            this._domElement = undefined;
        }
    }
    /**
     * Dispose the cameraControls instance itself, remove all eventListeners.
     * @category Methods
     */ dispose() {
        // remove all user event listeners
        this.removeAllEventListeners();
        // remove all internal event listeners
        this.disconnect();
    }
    // it's okay to expose public though
    _getTargetDirection(out) {
        // divide by distance to normalize, lighter than `Vector3.prototype.normalize()`
        return out.setFromSpherical(this._spherical).divideScalar(this._spherical.radius).applyQuaternion(this._yAxisUpSpaceInverse);
    }
    // it's okay to expose public though
    _getCameraDirection(out) {
        return this._getTargetDirection(out).negate();
    }
    _findPointerById(pointerId) {
        return this._activePointers.find((activePointer)=>activePointer.pointerId === pointerId);
    }
    _findPointerByMouseButton(mouseButton) {
        return this._activePointers.find((activePointer)=>activePointer.mouseButton === mouseButton);
    }
    _disposePointer(pointer) {
        this._activePointers.splice(this._activePointers.indexOf(pointer), 1);
    }
    _encloseToBoundary(position, offset, friction) {
        const offsetLength2 = offset.lengthSq();
        if (offsetLength2 === 0.0) return position;
        // See: https://twitter.com/FMS_Cat/status/1106508958640988161
        const newTarget = $e1f901905a002d12$var$_v3B.copy(offset).add(position); // target
        const clampedTarget = this._boundary.clampPoint(newTarget, $e1f901905a002d12$var$_v3C); // clamped target
        const deltaClampedTarget = clampedTarget.sub(newTarget); // newTarget -> clampedTarget
        const deltaClampedTargetLength2 = deltaClampedTarget.lengthSq(); // squared length of deltaClampedTarget
        if (deltaClampedTargetLength2 === 0.0) return position.add(offset);
        else if (deltaClampedTargetLength2 === offsetLength2) return position;
        else if (friction === 0.0) return position.add(offset).add(deltaClampedTarget);
        else {
            const offsetFactor = 1.0 + friction * deltaClampedTargetLength2 / offset.dot(deltaClampedTarget);
            return position.add($e1f901905a002d12$var$_v3B.copy(offset).multiplyScalar(offsetFactor)).add(deltaClampedTarget.multiplyScalar(1.0 - friction));
        }
    }
    _updateNearPlaneCorners() {
        if ($e1f901905a002d12$var$isPerspectiveCamera(this._camera)) {
            const camera = this._camera;
            const near = camera.near;
            const fov = camera.getEffectiveFOV() * $e1f901905a002d12$var$DEG2RAD;
            const heightHalf = Math.tan(fov * 0.5) * near; // near plain half height
            const widthHalf = heightHalf * camera.aspect; // near plain half width
            this._nearPlaneCorners[0].set(-widthHalf, -heightHalf, 0);
            this._nearPlaneCorners[1].set(widthHalf, -heightHalf, 0);
            this._nearPlaneCorners[2].set(widthHalf, heightHalf, 0);
            this._nearPlaneCorners[3].set(-widthHalf, heightHalf, 0);
        } else if ($e1f901905a002d12$var$isOrthographicCamera(this._camera)) {
            const camera = this._camera;
            const zoomInv = 1 / camera.zoom;
            const left = camera.left * zoomInv;
            const right = camera.right * zoomInv;
            const top = camera.top * zoomInv;
            const bottom = camera.bottom * zoomInv;
            this._nearPlaneCorners[0].set(left, top, 0);
            this._nearPlaneCorners[1].set(right, top, 0);
            this._nearPlaneCorners[2].set(right, bottom, 0);
            this._nearPlaneCorners[3].set(left, bottom, 0);
        }
    }
    // lateUpdate
    _collisionTest() {
        let distance = Infinity;
        const hasCollider = this.colliderMeshes.length >= 1;
        if (!hasCollider) return distance;
        if ($e1f901905a002d12$var$notSupportedInOrthographicCamera(this._camera, '_collisionTest')) return distance;
        const rayDirection = this._getTargetDirection($e1f901905a002d12$var$_cameraDirection);
        $e1f901905a002d12$var$_rotationMatrix.lookAt($e1f901905a002d12$var$_ORIGIN, rayDirection, this._camera.up);
        for(let i = 0; i < 4; i++){
            const nearPlaneCorner = $e1f901905a002d12$var$_v3B.copy(this._nearPlaneCorners[i]);
            nearPlaneCorner.applyMatrix4($e1f901905a002d12$var$_rotationMatrix);
            const origin = $e1f901905a002d12$var$_v3C.addVectors(this._target, nearPlaneCorner);
            $e1f901905a002d12$var$_raycaster.set(origin, rayDirection);
            $e1f901905a002d12$var$_raycaster.far = this._spherical.radius + 1;
            const intersects = $e1f901905a002d12$var$_raycaster.intersectObjects(this.colliderMeshes);
            if (intersects.length !== 0 && intersects[0].distance < distance) distance = intersects[0].distance;
        }
        return distance;
    }
    /**
     * Get its client rect and package into given `DOMRect` .
     */ _getClientRect(target) {
        if (!this._domElement) return;
        const rect = this._domElement.getBoundingClientRect();
        target.x = rect.left;
        target.y = rect.top;
        if (this._viewport) {
            target.x += this._viewport.x;
            target.y += rect.height - this._viewport.w - this._viewport.y;
            target.width = this._viewport.z;
            target.height = this._viewport.w;
        } else {
            target.width = rect.width;
            target.height = rect.height;
        }
        return target;
    }
    _createOnRestPromise(resolveImmediately) {
        if (resolveImmediately) return Promise.resolve();
        this._hasRested = false;
        this.dispatchEvent({
            type: 'transitionstart'
        });
        return new Promise((resolve)=>{
            const onResolve = ()=>{
                this.removeEventListener('rest', onResolve);
                resolve();
            };
            this.addEventListener('rest', onResolve);
        });
    }
    // eslint-disable-next-line @typescript-eslint/no-unused-vars
    _addAllEventListeners(_domElement) {}
    _removeAllEventListeners() {}
    /**
     * backward compatible
     * @deprecated use smoothTime (in seconds) instead
     * @category Properties
     */ get dampingFactor() {
        console.warn('.dampingFactor has been deprecated. use smoothTime (in seconds) instead.');
        return 0;
    }
    /**
     * backward compatible
     * @deprecated use smoothTime (in seconds) instead
     * @category Properties
     */ set dampingFactor(_) {
        console.warn('.dampingFactor has been deprecated. use smoothTime (in seconds) instead.');
    }
    /**
     * backward compatible
     * @deprecated use draggingSmoothTime (in seconds) instead
     * @category Properties
     */ get draggingDampingFactor() {
        console.warn('.draggingDampingFactor has been deprecated. use draggingSmoothTime (in seconds) instead.');
        return 0;
    }
    /**
     * backward compatible
     * @deprecated use draggingSmoothTime (in seconds) instead
     * @category Properties
     */ set draggingDampingFactor(_) {
        console.warn('.draggingDampingFactor has been deprecated. use draggingSmoothTime (in seconds) instead.');
    }
    static createBoundingSphere(object3d, out = new $e1f901905a002d12$var$THREE.Sphere()) {
        const boundingSphere = out;
        const center = boundingSphere.center;
        $e1f901905a002d12$var$_box3A.makeEmpty();
        // find the center
        object3d.traverseVisible((object)=>{
            if (!object.isMesh) return;
            $e1f901905a002d12$var$_box3A.expandByObject(object);
        });
        $e1f901905a002d12$var$_box3A.getCenter(center);
        // find the radius
        let maxRadiusSq = 0;
        object3d.traverseVisible((object)=>{
            if (!object.isMesh) return;
            const mesh = object;
            if (!mesh.geometry) return;
            const geometry = mesh.geometry.clone();
            geometry.applyMatrix4(mesh.matrixWorld);
            const bufferGeometry = geometry;
            const position = bufferGeometry.attributes.position;
            for(let i = 0, l = position.count; i < l; i++){
                $e1f901905a002d12$var$_v3A.fromBufferAttribute(position, i);
                maxRadiusSq = Math.max(maxRadiusSq, center.distanceToSquared($e1f901905a002d12$var$_v3A));
            }
        });
        boundingSphere.radius = Math.sqrt(maxRadiusSq);
        return boundingSphere;
    }
}












class $a681b8b24de9c7d6$export$d1c1e163c7960c6 {
    static createButton(renderer, sessionInit = {}) {
        const button = document.createElement('button');
        function showStartXR(mode) {
            let currentSession = null;
            async function onSessionStarted(session) {
                session.addEventListener('end', onSessionEnded);
                await renderer.xr.setSession(session);
                button.textContent = 'STOP XR';
                currentSession = session;
            }
            function onSessionEnded() {
                currentSession.removeEventListener('end', onSessionEnded);
                button.textContent = 'START XR';
                currentSession = null;
            }
            //
            button.style.display = '';
            button.style.cursor = 'pointer';
            button.style.left = 'calc(50% - 50px)';
            button.style.width = '100px';
            button.textContent = 'START XR';
            const sessionOptions = {
                ...sessionInit,
                optionalFeatures: [
                    'local-floor',
                    'bounded-floor',
                    ...sessionInit.optionalFeatures || []
                ]
            };
            button.onmouseenter = function() {
                button.style.opacity = '1.0';
            };
            button.onmouseleave = function() {
                button.style.opacity = '0.5';
            };
            button.onclick = function() {
                if (currentSession === null) navigator.xr.requestSession(mode, sessionOptions).then(onSessionStarted);
                else {
                    currentSession.end();
                    if (navigator.xr.offerSession !== undefined) navigator.xr.offerSession(mode, sessionOptions).then(onSessionStarted).catch((err)=>{
                        console.warn(err);
                    });
                }
            };
            if (navigator.xr.offerSession !== undefined) navigator.xr.offerSession(mode, sessionOptions).then(onSessionStarted).catch((err)=>{
                console.warn(err);
            });
        }
        function disableButton() {
            button.style.display = '';
            button.style.cursor = 'auto';
            button.style.left = 'calc(50% - 75px)';
            button.style.width = '150px';
            button.onmouseenter = null;
            button.onmouseleave = null;
            button.onclick = null;
        }
        function showXRNotSupported() {
            disableButton();
            button.textContent = 'No headset found';
            button.style.display = 'none';
        }
        function showXRNotAllowed(exception) {
            disableButton();
            console.warn('Exception when trying to call xr.isSessionSupported', exception);
            button.textContent = 'XR NOT ALLOWED';
        }
        function stylizeElement(element) {
            element.style.position = 'absolute';
            element.style.bottom = '20px';
            element.style.padding = '12px 6px';
            element.style.border = '1px solid #fff';
            element.style.borderRadius = '4px';
            element.style.background = 'rgba(0,0,0,0.1)';
            element.style.color = '#fff';
            element.style.font = 'normal 13px sans-serif';
            element.style.textAlign = 'center';
            element.style.opacity = '0.5';
            element.style.outline = 'none';
            element.style.zIndex = '999';
        }
        if ('xr' in navigator) {
            button.id = 'XRButton';
            button.style.display = 'none';
            stylizeElement(button);
            navigator.xr.isSessionSupported('immersive-ar').then(function(supported) {
                // Disable AR
                if (false) showStartXR('immersive-ar'); // was: if (supported)
                else navigator.xr.isSessionSupported('immersive-vr').then(function(supported) {
                    if (supported) showStartXR('immersive-vr');
                    else showXRNotSupported();
                }).catch(showXRNotAllowed);
            }).catch(showXRNotAllowed);
            return button;
        } else {
            const message = document.createElement('a');
            if (window.isSecureContext === false) {
                message.href = document.location.href.replace(/^http:/, 'https:');
                message.innerHTML = 'WEBXR NEEDS HTTPS'; // TODO Improve message
            } else // message.href = 'https://immersiveweb.dev/';
            // message.innerHTML = 'WEBXR NOT AVAILABLE';
            message.style = 'display: none';
            message.style.left = 'calc(50% - 90px)';
            message.style.width = '180px';
            message.style.textDecoration = 'none';
            stylizeElement(message);
            return message;
        }
    }
}




// Copyright 2021-2022 Icosa Gallery
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

/*!
 * hold-event
 * https://github.com/yomotsu/hold-event
 * (c) 2020 @yomotsu
 * Released under the MIT License.
 */ var $8ae143a90d3c4f75$export$5d78b97103c6f2c7;
(function(HOLD_EVENT_TYPE) {
    HOLD_EVENT_TYPE["HOLD_START"] = "holdStart";
    HOLD_EVENT_TYPE["HOLD_END"] = "holdEnd";
    HOLD_EVENT_TYPE["HOLDING"] = "holding";
})($8ae143a90d3c4f75$export$5d78b97103c6f2c7 || ($8ae143a90d3c4f75$export$5d78b97103c6f2c7 = {}));
/*! *****************************************************************************
Copyright (c) Microsoft Corporation. All rights reserved.
Licensed under the Apache License, Version 2.0 (the "License"); you may not use
this file except in compliance with the License. You may obtain a copy of the
License at http://www.apache.org/licenses/LICENSE-2.0

THIS CODE IS PROVIDED ON AN *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY IMPLIED
WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
MERCHANTABLITY OR NON-INFRINGEMENT.

See the Apache Version 2.0 License for specific language governing permissions
and limitations under the License.
***************************************************************************** */ /* global Reflect, Promise */ var $8ae143a90d3c4f75$var$extendStatics = function(d, b) {
    $8ae143a90d3c4f75$var$extendStatics = Object.setPrototypeOf || ({
        __proto__: []
    }) instanceof Array && function(d, b) {
        d.__proto__ = b;
    } || function(d, b) {
        for(var p in b)if (b.hasOwnProperty(p)) d[p] = b[p];
    };
    return $8ae143a90d3c4f75$var$extendStatics(d, b);
};
function $8ae143a90d3c4f75$var$__extends(d, b) {
    $8ae143a90d3c4f75$var$extendStatics(d, b);
    function __() {
        this.constructor = d;
    }
    d.prototype = b === null ? Object.create(b) : (__.prototype = b.prototype, new __());
}
var $8ae143a90d3c4f75$var$EventDispatcher = function() {
    function EventDispatcher() {
        this._listeners = {};
    }
    EventDispatcher.prototype.addEventListener = function(type, listener) {
        var listeners = this._listeners;
        if (listeners[type] === undefined) listeners[type] = [];
        if (listeners[type].indexOf(listener) === -1) listeners[type].push(listener);
    };
    EventDispatcher.prototype.removeEventListener = function(type, listener) {
        var listeners = this._listeners;
        var listenerArray = listeners[type];
        if (listenerArray !== undefined) {
            var index = listenerArray.indexOf(listener);
            if (index !== -1) listenerArray.splice(index, 1);
        }
    };
    EventDispatcher.prototype.dispatchEvent = function(event) {
        var listeners = this._listeners;
        var listenerArray = listeners[event.type];
        if (listenerArray !== undefined) {
            event.target = this;
            var array = listenerArray.slice(0);
            for(var i = 0, l = array.length; i < l; i++)array[i].call(this, event);
        }
    };
    return EventDispatcher;
}();
var $8ae143a90d3c4f75$var$Hold = function(_super) {
    $8ae143a90d3c4f75$var$__extends(Hold, _super);
    function Hold(holdIntervalDelay) {
        if (holdIntervalDelay === void 0) holdIntervalDelay = 100;
        var _this = _super.call(this) || this;
        _this.holdIntervalDelay = 100;
        _this._enabled = true;
        _this._holding = false;
        _this._intervalId = -1;
        _this._deltaTime = 0;
        _this._elapsedTime = 0;
        _this._lastTime = 0;
        _this._holdStart = function(event) {
            if (!_this._enabled) return;
            if (_this._holding) return;
            _this._deltaTime = 0;
            _this._elapsedTime = 0;
            _this._lastTime = performance.now();
            _this.dispatchEvent({
                type: $8ae143a90d3c4f75$export$5d78b97103c6f2c7.HOLD_START,
                deltaTime: _this._deltaTime,
                elapsedTime: _this._elapsedTime,
                originalEvent: event
            });
            _this._holding = true;
            _this._intervalId = window.setInterval(function() {
                var now = performance.now();
                _this._deltaTime = now - _this._lastTime;
                _this._elapsedTime += _this._deltaTime;
                _this._lastTime = performance.now();
                _this.dispatchEvent({
                    type: $8ae143a90d3c4f75$export$5d78b97103c6f2c7.HOLDING,
                    deltaTime: _this._deltaTime,
                    elapsedTime: _this._elapsedTime
                });
            }, _this.holdIntervalDelay);
        };
        _this._holdEnd = function(event) {
            if (!_this._enabled) return;
            if (!_this._holding) return;
            var now = performance.now();
            _this._deltaTime = now - _this._lastTime;
            _this._elapsedTime += _this._deltaTime;
            _this._lastTime = performance.now();
            _this.dispatchEvent({
                type: $8ae143a90d3c4f75$export$5d78b97103c6f2c7.HOLD_END,
                deltaTime: _this._deltaTime,
                elapsedTime: _this._elapsedTime,
                originalEvent: event
            });
            window.clearInterval(_this._intervalId);
            _this._holding = false;
        };
        _this.holdIntervalDelay = holdIntervalDelay;
        return _this;
    }
    Object.defineProperty(Hold.prototype, "enabled", {
        get: function() {
            return this._enabled;
        },
        set: function(enabled) {
            if (this._enabled === enabled) return;
            this._enabled = enabled;
            if (!this._enabled) this._holdEnd();
        },
        enumerable: true,
        configurable: true
    });
    return Hold;
}($8ae143a90d3c4f75$var$EventDispatcher);
var $8ae143a90d3c4f75$export$6fc5dd9cc392d83f = function(_super) {
    $8ae143a90d3c4f75$var$__extends(ElementHold, _super);
    function ElementHold(element, holdIntervalDelay) {
        if (holdIntervalDelay === void 0) holdIntervalDelay = 100;
        var _this = _super.call(this, holdIntervalDelay) || this;
        _this._holdStart = _this._holdStart.bind(_this);
        _this._holdEnd = _this._holdEnd.bind(_this);
        var onPointerDown = _this._holdStart;
        var onPointerUp = _this._holdEnd;
        element.addEventListener('mousedown', onPointerDown);
        document.addEventListener('mouseup', onPointerUp);
        window.addEventListener('blur', _this._holdEnd);
        return _this;
    }
    return ElementHold;
}($8ae143a90d3c4f75$var$Hold);
var $8ae143a90d3c4f75$export$b930b29ba9cf39c9 = function(_super) {
    $8ae143a90d3c4f75$var$__extends(KeyboardKeyHold, _super);
    function KeyboardKeyHold(keyCode, holdIntervalDelay) {
        if (holdIntervalDelay === void 0) holdIntervalDelay = 100;
        var _this = _super.call(this, holdIntervalDelay) || this;
        _this._holdStart = _this._holdStart.bind(_this);
        _this._holdEnd = _this._holdEnd.bind(_this);
        var onKeydown = function(event) {
            if ($8ae143a90d3c4f75$var$isInputEvent(event)) return;
            if (event.keyCode !== keyCode) return;
            _this._holdStart(event);
        };
        var onKeyup = function(event) {
            if (event.keyCode !== keyCode) return;
            _this._holdEnd(event);
        };
        document.addEventListener('keydown', onKeydown);
        document.addEventListener('keyup', onKeyup);
        window.addEventListener('blur', _this._holdEnd);
        return _this;
    }
    return KeyboardKeyHold;
}($8ae143a90d3c4f75$var$Hold);
function $8ae143a90d3c4f75$var$isInputEvent(event) {
    var target = event.target;
    return target.tagName === 'INPUT' || target.tagName === 'SELECT' || target.tagName === 'TEXTAREA' || target.isContentEditable;
}


function $7f098f70bc341b4e$export$fc22e28a11679cb8(cameraControls) {
    const KEYCODE = {
        W: 87,
        A: 65,
        S: 83,
        D: 68,
        Q: 81,
        E: 69,
        ARROW_LEFT: 37,
        ARROW_UP: 38,
        ARROW_RIGHT: 39,
        ARROW_DOWN: 40
    };
    let baseTranslationSpeed = 0.0001;
    let rotSpeed = 1;
    let holdInterval = 0.1;
    let maxSpeedMultiplier = 50;
    let accelerationTime = 1500;
    const getSpeedMultiplier = (elapsedTime)=>{
        const t = Math.min(elapsedTime / accelerationTime, 1);
        return 1 + (maxSpeedMultiplier - 1) * t;
    };
    const wKey = new $8ae143a90d3c4f75$export$b930b29ba9cf39c9(KEYCODE.W, holdInterval);
    const aKey = new $8ae143a90d3c4f75$export$b930b29ba9cf39c9(KEYCODE.A, holdInterval);
    const sKey = new $8ae143a90d3c4f75$export$b930b29ba9cf39c9(KEYCODE.S, holdInterval);
    const dKey = new $8ae143a90d3c4f75$export$b930b29ba9cf39c9(KEYCODE.D, holdInterval);
    const qKey = new $8ae143a90d3c4f75$export$b930b29ba9cf39c9(KEYCODE.Q, holdInterval);
    const eKey = new $8ae143a90d3c4f75$export$b930b29ba9cf39c9(KEYCODE.E, holdInterval);
    aKey.addEventListener('holding', function(event) {
        const speed = baseTranslationSpeed * getSpeedMultiplier(event?.elapsedTime) * event?.deltaTime;
        cameraControls.truck(-speed, 0, true);
    });
    dKey.addEventListener('holding', function(event) {
        const speed = baseTranslationSpeed * getSpeedMultiplier(event?.elapsedTime) * event?.deltaTime;
        cameraControls.truck(speed, 0, true);
    });
    wKey.addEventListener('holding', function(event) {
        const speed = baseTranslationSpeed * getSpeedMultiplier(event?.elapsedTime) * event?.deltaTime;
        cameraControls.forward(speed, true);
    });
    sKey.addEventListener('holding', function(event) {
        const speed = baseTranslationSpeed * getSpeedMultiplier(event?.elapsedTime) * event?.deltaTime;
        cameraControls.forward(-speed, true);
    });
    qKey.addEventListener('holding', function(event) {
        const speed = baseTranslationSpeed * getSpeedMultiplier(event?.elapsedTime) * event?.deltaTime;
        cameraControls.truck(0, speed, true);
    });
    eKey.addEventListener('holding', function(event) {
        const speed = baseTranslationSpeed * getSpeedMultiplier(event?.elapsedTime) * event?.deltaTime;
        cameraControls.truck(0, -speed, true);
    });
    const leftKey = new $8ae143a90d3c4f75$export$b930b29ba9cf39c9(KEYCODE.ARROW_LEFT, holdInterval);
    const rightKey = new $8ae143a90d3c4f75$export$b930b29ba9cf39c9(KEYCODE.ARROW_RIGHT, holdInterval);
    const upKey = new $8ae143a90d3c4f75$export$b930b29ba9cf39c9(KEYCODE.ARROW_UP, holdInterval);
    const downKey = new $8ae143a90d3c4f75$export$b930b29ba9cf39c9(KEYCODE.ARROW_DOWN, holdInterval);
    leftKey.addEventListener('holding', function(event) {
        cameraControls.rotate(rotSpeed * (0, $hBQxr$MathUtils).DEG2RAD * event?.deltaTime, 0, true);
    });
    rightKey.addEventListener('holding', function(event) {
        cameraControls.rotate(-rotSpeed * (0, $hBQxr$MathUtils).DEG2RAD * event?.deltaTime, 0, true);
    });
    upKey.addEventListener('holding', function(event) {
        cameraControls.rotate(0, -rotSpeed * (0, $hBQxr$MathUtils).DEG2RAD * event?.deltaTime, true);
    });
    downKey.addEventListener('holding', function(event) {
        cameraControls.rotate(0, rotSpeed * (0, $hBQxr$MathUtils).DEG2RAD * event?.deltaTime, true);
    });
}


// Adapted from original GLTF 1.0 Loader in three.js r86
// https://github.com/mrdoob/three.js/blob/r86/examples/js/loaders/GLTFLoader.js

const $81e80e8b2d2d5e9f$var$FS_GLSL = "precision highp float; const float INV_PI = 0.31830988618; const float PI = 3.141592654; const float _RefractiveIndex = 1.2; const float environmentStrength = 1.5; varying vec3 v_normal; varying vec3 v_position; varying vec3 v_binormal; varying vec3 v_tangent; uniform vec3 u_color; uniform float u_metallic; uniform float u_roughness; uniform vec3 u_light0Pos; uniform vec3 u_light0Color; uniform vec3 u_light1Pos; uniform vec3 u_light1Color; uniform mat4 u_modelMatrix; uniform sampler2D u_reflectionCube; uniform sampler2D u_reflectionCubeBlur; const float u_noiseIntensity = 0.015; const float colorNoiseAmount = 0.015; const float noiseScale = 700.0; uniform vec3 cameraPosition; // Noise functions from https://github.com/ashima/webgl-noise // Used under the MIT license - license text in MITLICENSE // Copyright (C) 2011 by Ashima Arts (Simplex noise) // Copyright (C) 2011-2016 by Stefan Gustavson (Classic noise and others) vec3 mod289(vec3 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; } vec4 mod289(vec4 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; } vec4 permute(vec4 x) { return mod289(((x*34.0)+1.0)*x); } vec4 taylorInvSqrt(vec4 r) { return 1.79284291400159 - 0.85373472095314 * r; } float snoise(vec3 v, out vec3 gradient) { const vec2 C = vec2(1.0/6.0, 1.0/3.0) ; const vec4 D = vec4(0.0, 0.5, 1.0, 2.0); // First corner vec3 i = floor(v + dot(v, C.yyy) ); vec3 x0 = v - i + dot(i, C.xxx) ; // Other corners vec3 g = step(x0.yzx, x0.xyz); vec3 l = 1.0 - g; vec3 i1 = min( g.xyz, l.zxy ); vec3 i2 = max( g.xyz, l.zxy ); // x0 = x0 - 0.0 + 0.0 * C.xxx; // x1 = x0 - i1 + 1.0 * C.xxx; // x2 = x0 - i2 + 2.0 * C.xxx; // x3 = x0 - 1.0 + 3.0 * C.xxx; vec3 x1 = x0 - i1 + C.xxx; vec3 x2 = x0 - i2 + C.yyy; // 2.0*C.x = 1/3 = C.y vec3 x3 = x0 - D.yyy; // -1.0+3.0*C.x = -0.5 = -D.y // Permutations i = mod289(i); vec4 p = permute( permute( permute( i.z + vec4(0.0, i1.z, i2.z, 1.0 )) + i.y + vec4(0.0, i1.y, i2.y, 1.0 )) + i.x + vec4(0.0, i1.x, i2.x, 1.0 )); // Gradients: 7x7 points over a square, mapped onto an octahedron. // The ring size 17*17 = 289 is close to a multiple of 49 (49*6 = 294) float n_ = 0.142857142857; // 1.0/7.0 vec3 ns = n_ * D.wyz - D.xzx; vec4 j = p - 49.0 * floor(p * ns.z * ns.z); // mod(p,7*7) vec4 x_ = floor(j * ns.z); vec4 y_ = floor(j - 7.0 * x_ ); // mod(j,N) vec4 x = x_ *ns.x + ns.yyyy; vec4 y = y_ *ns.x + ns.yyyy; vec4 h = 1.0 - abs(x) - abs(y); vec4 b0 = vec4( x.xy, y.xy ); vec4 b1 = vec4( x.zw, y.zw ); //vec4 s0 = vec4(lessThan(b0,0.0))*2.0 - 1.0; //vec4 s1 = vec4(lessThan(b1,0.0))*2.0 - 1.0; vec4 s0 = floor(b0)*2.0 + 1.0; vec4 s1 = floor(b1)*2.0 + 1.0; vec4 sh = -step(h, vec4(0.0)); vec4 a0 = b0.xzyw + s0.xzyw*sh.xxyy ; vec4 a1 = b1.xzyw + s1.xzyw*sh.zzww ; vec3 p0 = vec3(a0.xy,h.x); vec3 p1 = vec3(a0.zw,h.y); vec3 p2 = vec3(a1.xy,h.z); vec3 p3 = vec3(a1.zw,h.w); //Normalise gradients vec4 norm = taylorInvSqrt(vec4(dot(p0,p0), dot(p1,p1), dot(p2, p2), dot(p3,p3))); p0 *= norm.x; p1 *= norm.y; p2 *= norm.z; p3 *= norm.w; // Mix final noise value vec4 m = max(0.6 - vec4(dot(x0,x0), dot(x1,x1), dot(x2,x2), dot(x3,x3)), 0.0); vec4 m2 = m * m; vec4 m4 = m2 * m2; vec4 pdotx = vec4(dot(p0,x0), dot(p1,x1), dot(p2,x2), dot(p3,x3)); // Determine noise gradient vec4 temp = m2 * m * pdotx; gradient = -8.0 * (temp.x * x0 + temp.y * x1 + temp.z * x2 + temp.w * x3); gradient += m4.x * p0 + m4.y * p1 + m4.z * p2 + m4.w * p3; gradient *= 42.0; return 42.0 * dot(m4, pdotx); } // End of noise code float GGX(float nDotH, float roughness2) { float nDotH2 = nDotH * nDotH; float alpha = nDotH2 * roughness2 + 1.0 - nDotH2; float denominator = PI * alpha * alpha; return (nDotH2 > 0.0 ? 1.0 : 0.0) * roughness2 / denominator; } float BlinnPhongNDF(float nDotH) { float exponent = (2.0 / (u_roughness * u_roughness) - 2.0); float coeff = 1.0 / (PI * u_roughness * u_roughness); return coeff * pow(nDotH, exponent); } float CT_GeoAtten(float nDotV, float nDotH, float vDotH, float nDotL, float lDotH) { float a = (2.0 * nDotH * nDotV) / vDotH; float b = (2.0 * nDotH * nDotL) / lDotH; return min(1.0, min(a, b)); } float GeoAtten(float nDotV) { float c = nDotV / (u_roughness * sqrt(1.0 - nDotV * nDotV)); return c >= 1.6 ? 1.0 : (3.535 * c + 2.181 * c * c) / (1.0 + 2.276 * c + 2.577 * c * c); } vec3 evaluateFresnelSchlick(float vDotH, vec3 f0) { return f0 + (1.0 - f0) * pow(1.0 - vDotH, 5.0); } float saturate(float value) { return clamp(value, 0.0, 1.0); } vec3 saturate(vec3 value) { return clamp(value, 0.0, 1.0); } mat3 transpose(mat3 inMat) { return mat3(inMat[0][0], inMat[0][1], inMat[0][2], inMat[1][0], inMat[1][1], inMat[1][2], inMat[2][0], inMat[2][1], inMat[2][2]); } void generatePapercraftColorNormal(vec3 normal, vec3 tangent, vec3 binormal, vec3 noisePos, inout vec4 outColorMult, inout vec3 outNormal) { mat3 tangentToObject; tangentToObject[0] = vec3(tangent.x, tangent.y, tangent.z); tangentToObject[1] = vec3(binormal.x, binormal.y, binormal.z); tangentToObject[2] = vec3(normal.x, normal.y, normal.z); mat3 objectToTangent = transpose(tangentToObject); vec3 intensificator = vec3(u_noiseIntensity, u_noiseIntensity, 1.0); vec3 tangentPos = objectToTangent * noisePos; vec3 gradient = vec3(0.0); float noiseOut = snoise(tangentPos * noiseScale, gradient); vec3 tangentSpaceNormal = normalize(intensificator * vec3(gradient.xy, 1.0)); outNormal = tangentToObject * tangentSpaceNormal; outColorMult = vec4(vec3(1.0 + noiseOut * colorNoiseAmount), 1.0); } void evaluatePBRLight( vec3 materialColor, vec3 lightColor, float nDotL, float nDotV, float nDotH, float vDotH, float lDotH, inout vec3 diffuseOut, inout vec3 specularOut, inout vec3 debug, float specAmount) { vec3 diffuse = INV_PI * nDotL * lightColor; vec3 d = vec3(GGX(nDotH, u_roughness * u_roughness)); vec3 g = vec3(CT_GeoAtten(nDotV, nDotH, vDotH, nDotL, lDotH)); vec3 f0 = vec3(abs((1.0 - _RefractiveIndex) / (1.0 + _RefractiveIndex))); f0 = f0 * f0; f0 = mix(f0, materialColor, u_metallic); vec3 f = evaluateFresnelSchlick(vDotH, f0); diffuseOut = diffuseOut + (1.0 - saturate(f)) * (1.0 - u_metallic) * lightColor * diffuse; specularOut = specularOut + specAmount * lightColor * saturate((d * g * f) / saturate(4.0 * saturate(nDotH) * nDotV)); debug = saturate(g); } void setParams(vec3 worldPosition, inout vec3 normal, inout vec3 view, inout float nDotV) { normal = normalize(normal); view = normalize(cameraPosition - worldPosition); nDotV = saturate(dot(normal, view)); } void setLightParams(vec3 lightPosition, vec3 worldPosition, vec3 V, vec3 N, inout vec3 L, inout vec3 H, inout float nDotL, inout float nDotH, inout float vDotH, inout float lDotH) { L = normalize(lightPosition - worldPosition); H = normalize(L + V); nDotL = saturate(dot(N, L)); nDotH = saturate(dot(N, H)); vDotH = saturate(dot(V, H)); lDotH = saturate(dot(L, H)); } void main() { vec3 materialColor = u_color; vec4 outColorMult; vec3 normalisedNormal = v_normal; vec3 normalisedView; float nDotV; generatePapercraftColorNormal(v_normal, v_tangent, v_binormal, v_position, outColorMult, normalisedNormal); setParams(v_position, normalisedNormal, normalisedView, nDotV); vec3 normalisedLight; vec3 normalisedHalf; float nDotL; float nDotH; float vDotH; float lDotH; setLightParams(u_light0Pos, v_position, normalisedView, normalisedNormal, normalisedLight, normalisedHalf, nDotL, nDotH, vDotH, lDotH); vec3 diffuse = vec3(0.0, 0.0, 0.0); vec3 specular = vec3(0.0, 0.0, 0.0); vec3 debug = vec3(0.0, 0.0, 0.0); evaluatePBRLight(materialColor * outColorMult.rgb, u_light0Color, nDotL, nDotV, nDotH, vDotH, lDotH, diffuse, specular, debug, 1.0); vec3 ambient = (1.0 - u_metallic) * materialColor * outColorMult.rgb * 0.0; setLightParams(u_light1Pos, v_position, normalisedView, normalisedNormal, normalisedLight, normalisedHalf, nDotL, nDotH, vDotH, lDotH); evaluatePBRLight(materialColor * outColorMult.rgb, u_light1Color, nDotL, nDotV, nDotH, vDotH, lDotH, diffuse, specular, debug, 1.0); vec3 R = -reflect(normalisedView, normalisedNormal); setLightParams(v_position + R, v_position, normalisedView, normalisedNormal, normalisedLight, normalisedHalf, nDotL, nDotH, vDotH, lDotH); vec3 envColor = mix(materialColor, vec3(1.0, 1.0, 1.0), 0.7); evaluatePBRLight(materialColor * outColorMult.rgb, envColor * environmentStrength, nDotL, nDotV, nDotH, vDotH, lDotH, diffuse, specular, debug, 0.25); gl_FragColor = vec4(specular + diffuse * materialColor, 1.0); }";
const $81e80e8b2d2d5e9f$var$VS_GLSL = "uniform mat4 u_modelViewMatrix; uniform mat4 u_projectionMatrix; uniform mat3 u_normalMatrix; attribute vec3 a_position; attribute vec3 a_normal; varying vec3 v_normal; varying vec3 v_position; varying vec3 v_binormal; varying vec3 v_tangent; void main() { vec3 objPosition = a_position; vec4 worldPosition = vec4(objPosition, 1.0); // Our object space has no rotation and no scale, so this is fine. v_normal = a_normal; v_position = worldPosition.xyz; // Looking for an arbitrary vector that isn't parallel to the normal. Avoiding axis directions should improve our chances. vec3 arbitraryVector = normalize(vec3(0.42, -0.21, 0.15)); vec3 alternateArbitraryVector = normalize(vec3(0.43, 1.5, 0.15)); // If arbitrary vector is parallel to the normal, choose a different one. v_tangent = normalize(abs(dot(v_normal, arbitraryVector)) < 1.0 ? cross(v_normal, arbitraryVector) : cross(v_normal, alternateArbitraryVector)); v_binormal = normalize(cross(v_normal, v_tangent)); gl_Position = u_projectionMatrix * u_modelViewMatrix * vec4(objPosition, 1.0); }";
const $81e80e8b2d2d5e9f$var$GEMFS_GLSL = "precision highp float; const float INV_PI = 0.31830988618; const float PI = 3.141592654; const float _RefractiveIndex = 1.2; const float _Metallic = 0.5; const float environmentStrength = 1.5; varying vec3 v_normal; varying vec3 v_position; varying float v_fresnel; uniform sampler2D u_gem; uniform vec4 u_color; uniform float u_metallic; uniform float u_roughness; uniform vec3 u_light0Pos; uniform vec3 u_light0Color; uniform vec3 u_light1Pos; uniform vec3 u_light1Color; uniform vec3 cameraPosition; float GGX(float nDotH, float roughness2) { float nDotH2 = nDotH * nDotH; float alpha = nDotH2 * roughness2 + 1.0 - nDotH2; float denominator = PI * alpha * alpha; return (nDotH2 > 0.0 ? 1.0 : 0.0) * roughness2 / denominator; } float BlinnPhongNDF(float nDotH) { float exponent = (2.0 / (u_roughness * u_roughness) - 2.0); float coeff = 1.0 / (PI * u_roughness * u_roughness); return coeff * pow(nDotH, exponent); } float CT_GeoAtten(float nDotV, float nDotH, float vDotH, float nDotL, float lDotH) { float a = (2.0 * nDotH * nDotV) / vDotH; float b = (2.0 * nDotH * nDotL) / lDotH; return min(1.0, min(a, b)); } float GeoAtten(float nDotV) { float c = nDotV / (u_roughness * sqrt(1.0 - nDotV * nDotV)); return c >= 1.6 ? 1.0 : (3.535 * c + 2.181 * c * c) / (1.0 + 2.276 * c + 2.577 * c * c); } vec3 evaluateFresnelSchlick(float vDotH, vec3 f0) { return f0 + (1.0 - f0) * pow(1.0 - vDotH, 5.0); } float saturate(float value) { return clamp(value, 0.0, 1.0); } vec3 saturate(vec3 value) { return clamp(value, 0.0, 1.0); } mat3 transpose(mat3 inMat) { return mat3(inMat[0][0], inMat[0][1], inMat[0][2], inMat[1][0], inMat[1][1], inMat[1][2], inMat[2][0], inMat[2][1], inMat[2][2]); } void evaluatePBRLight( vec3 materialColor, vec3 lightColor, float nDotL, float nDotV, float nDotH, float vDotH, float lDotH, inout vec3 diffuseOut, inout vec3 specularOut, inout vec3 debug, float specAmount) { vec3 diffuse = INV_PI * nDotL * lightColor; vec3 d = vec3(GGX(nDotH, u_roughness * u_roughness)); vec3 g = vec3(CT_GeoAtten(nDotV, nDotH, vDotH, nDotL, lDotH)); vec3 f0 = vec3(abs((1.0 - _RefractiveIndex) / (1.0 + _RefractiveIndex))); f0 = f0 * f0; f0 = mix(f0, materialColor, u_metallic); vec3 f = evaluateFresnelSchlick(vDotH, f0); diffuseOut = diffuseOut + (1.0 - saturate(f)) * (1.0 - u_metallic) * lightColor * diffuse; specularOut = specularOut + specAmount * lightColor * saturate((d * g * f) / saturate(4.0 * saturate(nDotH) * nDotV)); debug = saturate(g); } void setParams(vec3 worldPosition, inout vec3 normal, inout vec3 view, inout float nDotV) { normal = normalize(normal); view = normalize(cameraPosition - worldPosition); nDotV = saturate(dot(normal, view)); } void setLightParams(vec3 lightPosition, vec3 worldPosition, vec3 V, vec3 N, inout vec3 L, inout vec3 H, inout float nDotL, inout float nDotH, inout float vDotH, inout float lDotH) { L = normalize(lightPosition - worldPosition); H = normalize(L + V); nDotL = saturate(dot(N, L)); nDotH = saturate(dot(N, H)); vDotH = saturate(dot(V, H)); lDotH = saturate(dot(L, H)); } void main() { vec3 materialColor = u_color.rgb; vec3 normalisedNormal = v_normal; vec3 normalisedView = cameraPosition - v_position; float nDotV; setParams(v_position, normalisedNormal, normalisedView, nDotV); vec3 normalisedLight; vec3 normalisedHalf; float nDotL; float nDotH; float vDotH; float lDotH; setLightParams(u_light0Pos, v_position, normalisedView, normalisedNormal, normalisedLight, normalisedHalf, nDotL, nDotH, vDotH, lDotH); vec3 diffuse = vec3(0.0, 0.0, 0.0); vec3 specular = vec3(0.0, 0.0, 0.0); vec3 debug = vec3(0.0, 0.0, 0.0); evaluatePBRLight(materialColor, u_light0Color, nDotL, nDotV, nDotH, vDotH, lDotH, diffuse, specular, debug, 1.0); vec3 ambient = materialColor * 0.3; setLightParams(u_light1Pos, v_position, normalisedView, normalisedNormal, normalisedLight, normalisedHalf, nDotL, nDotH, vDotH, lDotH); evaluatePBRLight(materialColor, u_light1Color, nDotL, nDotV, nDotH, vDotH, lDotH, diffuse, specular, debug, 1.0); vec3 R = reflect(normalisedView, normalisedNormal); vec4 color = vec4(texture2D( u_gem, vec2(0.5*(INV_PI*atan(R.x, R.z)+1.0),0.5*(R.y+1.0)) ).rgb, u_color.a); setLightParams(v_position + R, v_position, normalisedView, normalisedNormal, normalisedLight, normalisedHalf, nDotL, nDotH, vDotH, lDotH); vec3 envColor = mix(materialColor, vec3(1.0, 1.0, 1.0), 0.5); evaluatePBRLight(materialColor, envColor * environmentStrength, nDotL, nDotV, nDotH, vDotH, lDotH, diffuse, specular, debug, 0.25); gl_FragColor = vec4(ambient + specular + diffuse * color.rgb, 1.0); } ";
const $81e80e8b2d2d5e9f$var$GEMVS_GLSL = "uniform mat4 u_modelViewMatrix; uniform mat4 u_projectionMatrix; uniform mat3 u_normalMatrix; attribute vec3 a_position; attribute vec3 a_normal; varying vec3 v_normal; varying vec3 v_position; void main() { vec4 worldPosition = vec4(a_position, 1.0); v_normal = a_normal; v_position = worldPosition.xyz; gl_Position = u_projectionMatrix * u_modelViewMatrix * vec4(a_position, 1.0); } ";
const $81e80e8b2d2d5e9f$var$GLASSFS_GLSL = "precision highp float; const float INV_PI = 0.31830988618; const float PI = 3.141592654; const float _RefractiveIndex = 1.2; // Always default to Olive Oil. const float _Metallic = 0.5; const float environmentStrength = 1.0; varying vec3 v_normal; varying vec3 v_position; uniform vec4 u_color; uniform float u_metallic; uniform float u_roughness; uniform vec3 u_light0Pos; uniform vec3 u_light0Color; uniform vec3 u_light1Pos; uniform vec3 u_light1Color; uniform vec3 cameraPosition; // camera position world float GGX(float nDotH, float roughness2) { float nDotH2 = nDotH * nDotH; float alpha = nDotH2 * roughness2 + 1.0 - nDotH2; float denominator = PI * alpha * alpha; return (nDotH2 > 0.0 ? 1.0 : 0.0) * roughness2 / denominator; } float BlinnPhongNDF(float nDotH) { float exponent = (2.0 / (u_roughness * u_roughness) - 2.0); float coeff = 1.0 / (PI * u_roughness * u_roughness); return coeff * pow(nDotH, exponent); } float CT_GeoAtten(float nDotV, float nDotH, float vDotH, float nDotL, float lDotH) { float a = (2.0 * nDotH * nDotV) / vDotH; float b = (2.0 * nDotH * nDotL) / lDotH; return min(1.0, min(a, b)); } float GeoAtten(float nDotV) { float c = nDotV / (u_roughness * sqrt(1.0 - nDotV * nDotV)); return c >= 1.6 ? 1.0 : (3.535 * c + 2.181 * c * c) / (1.0 + 2.276 * c + 2.577 * c * c); } vec3 evaluateFresnelSchlick(float vDotH, vec3 f0) { return f0 + (1.0 - f0) * pow(1.0 - vDotH, 5.0); } float saturate(float value) { return clamp(value, 0.0, 1.0); } vec3 saturate(vec3 value) { return clamp(value, 0.0, 1.0); } mat3 transpose(mat3 inMat) { return mat3(inMat[0][0], inMat[0][1], inMat[0][2], inMat[1][0], inMat[1][1], inMat[1][2], inMat[2][0], inMat[2][1], inMat[2][2]); } void evaluatePBRLight( vec3 materialColor, vec3 lightColor, float nDotL, float nDotV, float nDotH, float vDotH, float lDotH, inout vec3 diffuseOut, inout vec3 specularOut, inout vec3 debug, float specAmount) { vec3 diffuse = INV_PI * nDotL * lightColor; vec3 d = vec3(GGX(nDotH, u_roughness * u_roughness)); vec3 g = vec3(CT_GeoAtten(nDotV, nDotH, vDotH, nDotL, lDotH)); vec3 f0 = vec3(abs((1.0 - _RefractiveIndex) / (1.0 + _RefractiveIndex))); f0 = f0 * f0; f0 = mix(f0, materialColor, u_metallic); vec3 f = evaluateFresnelSchlick(vDotH, f0); diffuseOut = diffuseOut + (1.0 - saturate(f)) * (1.0 - u_metallic) * lightColor * diffuse; specularOut = specularOut + specAmount * lightColor * saturate((d * g * f) / saturate(4.0 * saturate(nDotH) * nDotV)); debug = saturate(g); } void setParams(vec3 worldPosition, inout vec3 normal, inout vec3 view, inout float nDotV) { normal = normalize(normal); view = normalize(cameraPosition - worldPosition); nDotV = saturate(dot(normal, view)); } void setLightParams(vec3 lightPosition, vec3 worldPosition, vec3 V, vec3 N, inout vec3 L, inout vec3 H, inout float nDotL, inout float nDotH, inout float vDotH, inout float lDotH) { L = normalize(lightPosition - worldPosition); H = normalize(L + V); nDotL = saturate(dot(N, L)); nDotH = saturate(dot(N, H)); vDotH = saturate(dot(V, H)); lDotH = saturate(dot(L, H)); } void main() { vec3 materialColor = u_color.rgb; vec4 outColorMult; vec3 normalisedNormal = v_normal; vec3 normalisedView; float nDotV; setParams(v_position, normalisedNormal, normalisedView, nDotV); vec3 normalisedLight; vec3 normalisedHalf; float nDotL; float nDotH; float vDotH; float lDotH; setLightParams(u_light0Pos, v_position, normalisedView, normalisedNormal, normalisedLight, normalisedHalf, nDotL, nDotH, vDotH, lDotH); vec3 diffuse = vec3(0.0, 0.0, 0.0); vec3 specular = vec3(0.0, 0.0, 0.0); vec3 debug = vec3(0.0, 0.0, 0.0); evaluatePBRLight(materialColor, u_light0Color, nDotL, nDotV, nDotH, vDotH, lDotH, diffuse, specular, debug, 1.0); vec3 ambient = materialColor * 0.3; setLightParams(u_light1Pos, v_position, normalisedView, normalisedNormal, normalisedLight, normalisedHalf, nDotL, nDotH, vDotH, lDotH); evaluatePBRLight(materialColor, u_light1Color, nDotL, nDotV, nDotH, vDotH, lDotH, diffuse, specular, debug, 1.0); vec3 R = -reflect(normalisedView, normalisedNormal); setLightParams(v_position + R, v_position, normalisedView, normalisedNormal, normalisedLight, normalisedHalf, nDotL, nDotH, vDotH, lDotH); vec3 envColor = mix(materialColor, vec3(1.0, 1.0, 1.0), 0.5); evaluatePBRLight(materialColor, envColor * environmentStrength, nDotL, nDotV, nDotH, vDotH, lDotH, diffuse, specular, debug, 0.2); gl_FragColor = vec4(ambient + specular + diffuse * materialColor, u_color.a); } ";
const $81e80e8b2d2d5e9f$var$GLASSVS_GLSL = "uniform mat4 u_modelViewMatrix; uniform mat4 u_projectionMatrix; uniform mat3 u_normalMatrix; attribute vec3 a_position; attribute vec3 a_normal; varying vec3 v_normal; varying vec3 v_position; void main() { vec4 worldPosition = vec4(a_position, 1.0); // Our object space has no rotation and no scale, so this is fine. v_normal = a_normal; v_position = worldPosition.xyz; gl_Position = u_projectionMatrix * u_modelViewMatrix * vec4(a_position, 1.0); } ";
const { Loader: $81e80e8b2d2d5e9f$var$ThreeLoader } = $hBQxr$three;
class $81e80e8b2d2d5e9f$export$9559c3115faeb0b0 extends $81e80e8b2d2d5e9f$var$ThreeLoader {
    constructor(manager, assetBaseUrl){
        super(manager);
        this.assetBaseUrl = assetBaseUrl;
    }
    load(url, onLoad, onProgress, onError) {
        var scope = this;
        var resourcePath;
        if (this.resourcePath !== '') resourcePath = this.resourcePath;
        else if (this.path !== '') resourcePath = this.path;
        else resourcePath = $hBQxr$three.LoaderUtils.extractUrlBase(url);
        var loader = new $hBQxr$three.FileLoader(scope.manager);
        loader.setPath(this.path);
        loader.setResponseType('arraybuffer');
        loader.load(url, function(data) {
            scope.parse(data, resourcePath, onLoad);
        }, onProgress, onError);
    }
    parse(data, path, callback) {
        var content;
        var extensions = {};
        var magic = new TextDecoder().decode(new Uint8Array(data, 0, 4));
        if (magic === $81e80e8b2d2d5e9f$var$BINARY_EXTENSION_HEADER_DEFAULTS.magic) {
            extensions[$81e80e8b2d2d5e9f$var$EXTENSIONS.KHR_BINARY_GLTF] = new $81e80e8b2d2d5e9f$var$GLTFBinaryExtension(data);
            content = extensions[$81e80e8b2d2d5e9f$var$EXTENSIONS.KHR_BINARY_GLTF].content;
        } else content = new TextDecoder().decode(new Uint8Array(data));
        var json = JSON.parse(content);
        if (json.extensionsUsed && json.extensionsUsed.indexOf($81e80e8b2d2d5e9f$var$EXTENSIONS.KHR_MATERIALS_COMMON) >= 0) extensions[$81e80e8b2d2d5e9f$var$EXTENSIONS.KHR_MATERIALS_COMMON] = new $81e80e8b2d2d5e9f$var$GLTFMaterialsCommonExtension(json);
        var parser = new $81e80e8b2d2d5e9f$var$GLTFParser(json, extensions, {
            crossOrigin: this.crossOrigin,
            manager: this.manager,
            path: path || this.resourcePath || '',
            assetBaseUrl: this.assetBaseUrl
        });
        parser.parse(function(scene, scenes, cameras, animations) {
            var glTF = {
                "scene": scene,
                "scenes": scenes,
                "cameras": cameras,
                "animations": animations
            };
            callback(glTF);
        });
    }
}
function $81e80e8b2d2d5e9f$var$GLTFRegistry() {
    var objects = {};
    return {
        get: function(key) {
            return objects[key];
        },
        add: function(key, object) {
            objects[key] = object;
        },
        remove: function(key) {
            delete objects[key];
        },
        removeAll: function() {
            objects = {};
        },
        update: function(scene, camera) {
            for(var name in objects){
                var object = objects[name];
                if (object.update) object.update(scene, camera);
            }
        }
    };
}
class $81e80e8b2d2d5e9f$var$GLTFShader {
    constructor(targetNode, allNodes){
        var boundUniforms = {};
        // bind each uniform to its source node
        var uniforms = targetNode.material.uniforms;
        for(var uniformId in uniforms){
            var uniform = uniforms[uniformId];
            if (uniform.semantic) {
                var sourceNodeRef = uniform.node;
                var sourceNode = targetNode;
                if (sourceNodeRef) sourceNode = allNodes[sourceNodeRef];
                boundUniforms[uniformId] = {
                    semantic: uniform.semantic,
                    sourceNode: sourceNode,
                    targetNode: targetNode,
                    uniform: uniform
                };
            }
        }
        this.boundUniforms = boundUniforms;
        this._m4 = new $hBQxr$three.Matrix4();
    }
    update(scene, camera) {
        var boundUniforms = this.boundUniforms;
        for(var name in boundUniforms){
            var boundUniform = boundUniforms[name];
            switch(boundUniform.semantic){
                case "MODELVIEW":
                    var m4 = boundUniform.uniform.value;
                    m4.multiplyMatrices(camera.matrixWorldInverse, boundUniform.sourceNode.matrixWorld);
                    break;
                case "MODELVIEWINVERSETRANSPOSE":
                    var m3 = boundUniform.uniform.value;
                    this._m4.multiplyMatrices(camera.matrixWorldInverse, boundUniform.sourceNode.matrixWorld);
                    m3.getNormalMatrix(this._m4);
                    break;
                case "PROJECTION":
                    var m4 = boundUniform.uniform.value;
                    m4.copy(camera.projectionMatrix);
                    break;
                case "JOINTMATRIX":
                    var m4v = boundUniform.uniform.value;
                    for(var mi = 0; mi < m4v.length; mi++)// So it goes like this:
                    // SkinnedMesh world matrix is already baked into MODELVIEW;
                    // transform joints to local space,
                    // then transform using joint's inverse
                    m4v[mi].getInverse(boundUniform.sourceNode.matrixWorld).multiply(boundUniform.targetNode.skeleton.bones[mi].matrixWorld).multiply(boundUniform.targetNode.skeleton.boneInverses[mi]).multiply(boundUniform.targetNode.bindMatrix);
                    break;
                default:
                    console.warn("Unhandled shader semantic: " + boundUniform.semantic);
                    break;
            }
        }
    }
}
var $81e80e8b2d2d5e9f$var$EXTENSIONS = {
    KHR_BINARY_GLTF: 'KHR_binary_glTF',
    KHR_MATERIALS_COMMON: 'KHR_materials_common'
};
function $81e80e8b2d2d5e9f$var$GLTFMaterialsCommonExtension(json) {
    this.name = $81e80e8b2d2d5e9f$var$EXTENSIONS.KHR_MATERIALS_COMMON;
    this.lights = {};
    var extension = json.extensions && json.extensions[$81e80e8b2d2d5e9f$var$EXTENSIONS.KHR_MATERIALS_COMMON] || {};
    var lights = extension.lights || {};
    for(var lightId in lights){
        var light = lights[lightId];
        var lightNode;
        var lightParams = light[light.type];
        var color = new $hBQxr$three.Color().fromArray(lightParams.color);
        switch(light.type){
            case "directional":
                lightNode = new $hBQxr$three.DirectionalLight(color);
                lightNode.position.set(0, 0, 1);
                break;
            case "point":
                lightNode = new $hBQxr$three.PointLight(color);
                break;
            case "spot":
                lightNode = new $hBQxr$three.SpotLight(color);
                lightNode.position.set(0, 0, 1);
                break;
            case "ambient":
                lightNode = new $hBQxr$three.AmbientLight(color);
                break;
        }
        if (lightNode) this.lights[lightId] = lightNode;
    }
}
var $81e80e8b2d2d5e9f$var$BINARY_EXTENSION_BUFFER_NAME = 'binary_glTF';
var $81e80e8b2d2d5e9f$var$BINARY_EXTENSION_HEADER_DEFAULTS = {
    magic: 'glTF',
    version: 1,
    contentFormat: 0
};
var $81e80e8b2d2d5e9f$var$BINARY_EXTENSION_HEADER_LENGTH = 20;
class $81e80e8b2d2d5e9f$var$GLTFBinaryExtension {
    constructor(data){
        this.name = $81e80e8b2d2d5e9f$var$EXTENSIONS.KHR_BINARY_GLTF;
        var headerView = new DataView(data, 0, $81e80e8b2d2d5e9f$var$BINARY_EXTENSION_HEADER_LENGTH);
        var header = {
            magic: new TextDecoder().decode(new Uint8Array(data.slice(0, 4))),
            version: headerView.getUint32(4, true),
            length: headerView.getUint32(8, true),
            contentLength: headerView.getUint32(12, true),
            contentFormat: headerView.getUint32(16, true)
        };
        for(var key in $81e80e8b2d2d5e9f$var$BINARY_EXTENSION_HEADER_DEFAULTS){
            var value = $81e80e8b2d2d5e9f$var$BINARY_EXTENSION_HEADER_DEFAULTS[key];
            if (header[key] !== value) throw new Error('Unsupported glTF-Binary header: Expected "%s" to be "%s".', key, value);
        }
        var contentArray = new Uint8Array(data, $81e80e8b2d2d5e9f$var$BINARY_EXTENSION_HEADER_LENGTH, header.contentLength);
        this.header = header;
        this.content = new TextDecoder().decode(contentArray);
        this.body = data.slice($81e80e8b2d2d5e9f$var$BINARY_EXTENSION_HEADER_LENGTH + header.contentLength, header.length);
    }
    loadShader(shader, bufferViews) {
        var bufferView = bufferViews[shader.extensions[$81e80e8b2d2d5e9f$var$EXTENSIONS.KHR_BINARY_GLTF].bufferView];
        var array = new Uint8Array(bufferView);
        return new TextDecoder().decode(array);
    }
}
var $81e80e8b2d2d5e9f$var$WEBGL_CONSTANTS = {
    FLOAT: 5126,
    //FLOAT_MAT2: 35674,
    FLOAT_MAT3: 35675,
    FLOAT_MAT4: 35676,
    FLOAT_VEC2: 35664,
    FLOAT_VEC3: 35665,
    FLOAT_VEC4: 35666,
    LINEAR: 9729,
    REPEAT: 10497,
    SAMPLER_2D: 35678,
    TRIANGLES: 4,
    LINES: 1,
    UNSIGNED_BYTE: 5121,
    UNSIGNED_SHORT: 5123,
    VERTEX_SHADER: 35633,
    FRAGMENT_SHADER: 35632
};
var $81e80e8b2d2d5e9f$var$WEBGL_TYPE = {
    5126: Number,
    //35674: Matrix2,
    35675: $hBQxr$three.Matrix3,
    35676: $hBQxr$three.Matrix4,
    35664: $hBQxr$three.Vector2,
    35665: $hBQxr$three.Vector3,
    35666: $hBQxr$three.Vector4,
    35678: $hBQxr$three.Texture
};
var $81e80e8b2d2d5e9f$var$WEBGL_COMPONENT_TYPES = {
    5120: Int8Array,
    5121: Uint8Array,
    5122: Int16Array,
    5123: Uint16Array,
    5125: Uint32Array,
    5126: Float32Array
};
var $81e80e8b2d2d5e9f$var$WEBGL_FILTERS = {
    9728: $hBQxr$three.NearestFilter,
    9729: $hBQxr$three.LinearFilter,
    9984: $hBQxr$three.NearestMipmapNearestFilter,
    9985: $hBQxr$three.LinearMipmapNearestFilter,
    9986: $hBQxr$three.NearestMipmapLinearFilter,
    9987: $hBQxr$three.LinearMipmapLinearFilter
};
var $81e80e8b2d2d5e9f$var$WEBGL_WRAPPINGS = {
    33071: $hBQxr$three.ClampToEdgeWrapping,
    33648: $hBQxr$three.MirroredRepeatWrapping,
    10497: $hBQxr$three.RepeatWrapping
};
var $81e80e8b2d2d5e9f$var$WEBGL_TEXTURE_FORMATS = {
    6406: $hBQxr$three.AlphaFormat,
    6407: $hBQxr$three.RGBFormat,
    6408: $hBQxr$three.RGBAFormat
};
var $81e80e8b2d2d5e9f$var$WEBGL_TEXTURE_DATATYPES = {
    5121: $hBQxr$three.UnsignedByteType,
    32819: $hBQxr$three.UnsignedShort4444Type,
    32820: $hBQxr$three.UnsignedShort5551Type
};
var $81e80e8b2d2d5e9f$var$WEBGL_SIDES = {
    1028: $hBQxr$three.BackSide,
    1029: $hBQxr$three.FrontSide // Culling back
};
var $81e80e8b2d2d5e9f$var$WEBGL_DEPTH_FUNCS = {
    512: $hBQxr$three.NeverDepth,
    513: $hBQxr$three.LessDepth,
    514: $hBQxr$three.EqualDepth,
    515: $hBQxr$three.LessEqualDepth,
    516: $hBQxr$three.GreaterEqualDepth,
    517: $hBQxr$three.NotEqualDepth,
    518: $hBQxr$three.GreaterEqualDepth,
    519: $hBQxr$three.AlwaysDepth
};
var $81e80e8b2d2d5e9f$var$WEBGL_BLEND_EQUATIONS = {
    32774: $hBQxr$three.AddEquation,
    32778: $hBQxr$three.SubtractEquation,
    32779: $hBQxr$three.ReverseSubtractEquation
};
var $81e80e8b2d2d5e9f$var$WEBGL_BLEND_FUNCS = {
    0: $hBQxr$three.ZeroFactor,
    1: $hBQxr$three.OneFactor,
    768: $hBQxr$three.SrcColorFactor,
    769: $hBQxr$three.OneMinusSrcColorFactor,
    770: $hBQxr$three.SrcAlphaFactor,
    771: $hBQxr$three.OneMinusSrcAlphaFactor,
    772: $hBQxr$three.DstAlphaFactor,
    773: $hBQxr$three.OneMinusDstAlphaFactor,
    774: $hBQxr$three.DstColorFactor,
    775: $hBQxr$three.OneMinusDstColorFactor,
    776: $hBQxr$three.SrcAlphaSaturateFactor
};
var $81e80e8b2d2d5e9f$var$WEBGL_TYPE_SIZES = {
    'SCALAR': 1,
    'VEC2': 2,
    'VEC3': 3,
    'VEC4': 4,
    'MAT2': 4,
    'MAT3': 9,
    'MAT4': 16
};
var $81e80e8b2d2d5e9f$var$PATH_PROPERTIES = {
    scale: 'scale',
    translation: 'position',
    rotation: 'quaternion'
};
var $81e80e8b2d2d5e9f$var$INTERPOLATION = {
    LINEAR: $hBQxr$three.InterpolateLinear,
    STEP: $hBQxr$three.InterpolateDiscrete
};
var $81e80e8b2d2d5e9f$var$STATES_ENABLES = {
    2884: 'CULL_FACE',
    2929: 'DEPTH_TEST',
    3042: 'BLEND',
    3089: 'SCISSOR_TEST',
    32823: 'POLYGON_OFFSET_FILL',
    32926: 'SAMPLE_ALPHA_TO_COVERAGE'
};
function $81e80e8b2d2d5e9f$var$_each(object, callback, thisObj) {
    if (!object) return Promise.resolve();
    var results;
    var fns = [];
    if (Object.prototype.toString.call(object) === '[object Array]') {
        results = [];
        var length = object.length;
        for(var idx = 0; idx < length; idx++){
            var value = callback.call(thisObj || this, object[idx], idx);
            if (value) {
                fns.push(value);
                if (value instanceof Promise) value.then((function(key, value) {
                    results[key] = value;
                }).bind(this, idx));
                else results[idx] = value;
            }
        }
    } else {
        results = {};
        for(var key in object)if (object.hasOwnProperty(key)) {
            var value = callback.call(thisObj || this, object[key], key);
            if (value) {
                fns.push(value);
                if (value instanceof Promise) value.then((function(key, value) {
                    results[key] = value;
                }).bind(this, key));
                else results[key] = value;
            }
        }
    }
    return Promise.all(fns).then(function() {
        return results;
    });
}
function $81e80e8b2d2d5e9f$var$resolveURL(url, path) {
    // Invalid URL
    if (typeof url !== 'string' || url === '') return '';
    // Absolute URL http://,https://,//
    if (/^(https?:)?\/\//i.test(url)) return url;
    // Data URI
    if (/^data:.*,.*$/i.test(url)) return url;
    // Blob URL
    if (/^blob:.*$/i.test(url)) return url;
    // Relative URL
    return (path || '') + url;
}
// js seems too dependent on attribute names so globally
// replace those in the shader code
function $81e80e8b2d2d5e9f$var$replaceTHREEShaderAttributes(shaderText, technique) {
    // Expected technique attributes
    var attributes = {};
    for(var attributeId in technique.attributes){
        var pname = technique.attributes[attributeId];
        var param = technique.parameters[pname];
        var atype = param.type;
        var semantic = param.semantic;
        attributes[attributeId] = {
            type: atype,
            semantic: semantic
        };
    }
    // Figure out which attributes to change in technique
    var shaderParams = technique.parameters;
    var shaderAttributes = technique.attributes;
    var params = {};
    for(var attributeId in attributes){
        var pname = shaderAttributes[attributeId];
        var shaderParam = shaderParams[pname];
        var semantic = shaderParam.semantic;
        if (semantic) params[attributeId] = shaderParam;
    }
    for(var pname in params){
        var param = params[pname];
        var semantic = param.semantic;
        var regEx = new RegExp("\\b" + pname + "\\b", "g");
        switch(semantic){
            case "POSITION":
                shaderText = shaderText.replace(regEx, 'position');
                break;
            case "NORMAL":
                shaderText = shaderText.replace(regEx, 'normal');
                break;
            case 'TEXCOORD_0':
            case 'TEXCOORD0':
            case 'TEXCOORD':
                shaderText = shaderText.replace(regEx, 'uv');
                break;
            case 'TEXCOORD_1':
                shaderText = shaderText.replace(regEx, 'uv2');
                break;
            case 'COLOR_0':
            case 'COLOR0':
            case 'COLOR':
                shaderText = shaderText.replace(regEx, 'color');
                break;
            case "WEIGHT":
                shaderText = shaderText.replace(regEx, 'skinWeight');
                break;
            case "JOINT":
                shaderText = shaderText.replace(regEx, 'skinIndex');
                break;
        }
    }
    return shaderText;
}
function $81e80e8b2d2d5e9f$var$createDefaultMaterial() {
    return new $hBQxr$three.MeshPhongMaterial({
        color: 0x00000,
        emissive: 0x888888,
        specular: 0x000000,
        shininess: 0,
        transparent: false,
        depthTest: true,
        side: $hBQxr$three.FrontSide
    });
}
class $81e80e8b2d2d5e9f$var$DeferredShaderMaterial {
    constructor(params){
        this.isDeferredShaderMaterial = true;
        this.params = params;
    }
    create() {
        var uniforms = $hBQxr$three.UniformsUtils.clone(this.params.uniforms);
        for(var uniformId in this.params.uniforms){
            var originalUniform = this.params.uniforms[uniformId];
            if (originalUniform.value instanceof $hBQxr$three.Texture) {
                uniforms[uniformId].value = originalUniform.value;
                uniforms[uniformId].value.needsUpdate = true;
            }
            uniforms[uniformId].semantic = originalUniform.semantic;
            uniforms[uniformId].node = originalUniform.node;
        }
        this.params.uniforms = uniforms;
        return new $hBQxr$three.RawShaderMaterial(this.params);
    }
}
class $81e80e8b2d2d5e9f$var$GLTFParser {
    constructor(json, extensions, options){
        this.json = json || {};
        this.extensions = extensions || {};
        this.options = options || {};
        // loader object cache
        this.cache = new $81e80e8b2d2d5e9f$var$GLTFRegistry();
    }
    _withDependencies(dependencies) {
        var _dependencies = {};
        for(var i = 0; i < dependencies.length; i++){
            var dependency = dependencies[i];
            var fnName = "load" + dependency.charAt(0).toUpperCase() + dependency.slice(1);
            var cached = this.cache.get(dependency);
            if (cached !== undefined) _dependencies[dependency] = cached;
            else if (this[fnName]) {
                var fn = this[fnName]();
                this.cache.add(dependency, fn);
                _dependencies[dependency] = fn;
            }
        }
        return $81e80e8b2d2d5e9f$var$_each(_dependencies, function(dependency) {
            return dependency;
        });
    }
    parse(callback) {
        var json = this.json;
        // Clear the loader cache
        this.cache.removeAll();
        // Fire the callback on complete
        this._withDependencies([
            "scenes",
            "cameras",
            "animations"
        ]).then(function(dependencies) {
            var scenes = [];
            for(var name in dependencies.scenes)scenes.push(dependencies.scenes[name]);
            var scene = json.scene !== undefined ? dependencies.scenes[json.scene] : scenes[0];
            var cameras = [];
            for(var name in dependencies.cameras){
                var camera = dependencies.cameras[name];
                cameras.push(camera);
            }
            var animations = [];
            for(var name in dependencies.animations)animations.push(dependencies.animations[name]);
            callback(scene, scenes, cameras, animations);
        });
    }
    loadShaders() {
        var json = this.json;
        // Skip shader loading entirely since materials get completely replaced by replaceBrushMaterials()
        // Just return empty shaders for all shader references to avoid breaking the material loading pipeline
        return Promise.resolve($81e80e8b2d2d5e9f$var$_each(json.shaders, function() {
            return ''; // Return empty string for each shader
        }));
    }
    loadBuffers() {
        var json = this.json;
        var extensions = this.extensions;
        var options = this.options;
        return $81e80e8b2d2d5e9f$var$_each(json.buffers, function(buffer, name) {
            if (name === $81e80e8b2d2d5e9f$var$BINARY_EXTENSION_BUFFER_NAME) return extensions[$81e80e8b2d2d5e9f$var$EXTENSIONS.KHR_BINARY_GLTF].body;
            if (buffer.type === 'arraybuffer' || buffer.type === undefined) return new Promise(function(resolve) {
                var loader = new $hBQxr$three.FileLoader(options.manager);
                loader.setResponseType('arraybuffer');
                loader.setCrossOrigin('no-cors');
                loader.load($81e80e8b2d2d5e9f$var$resolveURL(buffer.uri, options.path), function(buffer) {
                    resolve(buffer);
                });
            });
            else console.warn('THREE.LegacyGLTFLoader: ' + buffer.type + ' buffer type is not supported');
        });
    }
    loadBufferViews() {
        var json = this.json;
        return this._withDependencies([
            "buffers"
        ]).then(function(dependencies) {
            return $81e80e8b2d2d5e9f$var$_each(json.bufferViews, function(bufferView) {
                var arraybuffer = dependencies.buffers[bufferView.buffer];
                var byteLength = bufferView.byteLength !== undefined ? bufferView.byteLength : 0;
                return arraybuffer.slice(bufferView.byteOffset, bufferView.byteOffset + byteLength);
            });
        });
    }
    loadAccessors() {
        var json = this.json;
        return this._withDependencies([
            "bufferViews"
        ]).then(function(dependencies) {
            return $81e80e8b2d2d5e9f$var$_each(json.accessors, function(accessor) {
                var arraybuffer = dependencies.bufferViews[accessor.bufferView];
                var itemSize = $81e80e8b2d2d5e9f$var$WEBGL_TYPE_SIZES[accessor.type];
                var TypedArray = $81e80e8b2d2d5e9f$var$WEBGL_COMPONENT_TYPES[accessor.componentType];
                // For VEC3: itemSize is 3, elementBytes is 4, itemBytes is 12.
                var elementBytes = TypedArray.BYTES_PER_ELEMENT;
                var itemBytes = elementBytes * itemSize;
                // The buffer is not interleaved if the stride is the item size in bytes.
                if (accessor.byteStride && accessor.byteStride !== itemBytes) {
                    // Use the full buffer if it's interleaved.
                    var array = new TypedArray(arraybuffer);
                    // Integer parameters to IB/IBA are in array elements, not bytes.
                    var ib = new $hBQxr$three.InterleavedBuffer(array, accessor.byteStride / elementBytes);
                    return new $hBQxr$three.InterleavedBufferAttribute(ib, itemSize, accessor.byteOffset / elementBytes);
                } else {
                    array = new TypedArray(arraybuffer, accessor.byteOffset, accessor.count * itemSize);
                    return new $hBQxr$three.BufferAttribute(array, itemSize);
                }
            });
        });
    }
    loadTextures() {
        var json = this.json;
        // Skip texture loading entirely since materials get completely replaced by replaceBrushMaterials()
        // Just return null textures for all texture references to avoid breaking the material loading pipeline
        return Promise.resolve($81e80e8b2d2d5e9f$var$_each(json.textures, function() {
            return null; // Return null for each texture
        }));
    }
    loadMaterials() {
        var json = this.json;
        return this._withDependencies([
            "shaders",
            "textures"
        ]).then(function(dependencies) {
            return $81e80e8b2d2d5e9f$var$_each(json.materials, function(material) {
                var materialType;
                var materialValues = {};
                var materialParams = {};
                var khr_material;
                if (material.extensions && material.extensions[$81e80e8b2d2d5e9f$var$EXTENSIONS.KHR_MATERIALS_COMMON]) khr_material = material.extensions[$81e80e8b2d2d5e9f$var$EXTENSIONS.KHR_MATERIALS_COMMON];
                if (khr_material) {
                    // don't copy over unused values to avoid material warning spam
                    var keys = [
                        'ambient',
                        'emission',
                        'transparent',
                        'transparency',
                        'doubleSided'
                    ];
                    switch(khr_material.technique){
                        case 'BLINN':
                        case 'PHONG':
                            materialType = $hBQxr$three.MeshPhongMaterial;
                            keys.push('diffuse', 'specular', 'shininess');
                            break;
                        case 'LAMBERT':
                            materialType = $hBQxr$three.MeshLambertMaterial;
                            keys.push('diffuse');
                            break;
                        case 'CONSTANT':
                        default:
                            materialType = $hBQxr$three.MeshBasicMaterial;
                            break;
                    }
                    keys.forEach(function(v) {
                        if (khr_material.values[v] !== undefined) materialValues[v] = khr_material.values[v];
                    });
                    if (khr_material.doubleSided || materialValues.doubleSided) materialParams.side = $hBQxr$three.DoubleSide;
                    if (khr_material.transparent || materialValues.transparent) {
                        materialParams.transparent = true;
                        materialParams.opacity = materialValues.transparency !== undefined ? materialValues.transparency : 1;
                    }
                } else if (material.technique === undefined) {
                    materialType = $hBQxr$three.MeshPhongMaterial;
                    Object.assign(materialValues, material.values);
                } else {
                    materialType = $81e80e8b2d2d5e9f$var$DeferredShaderMaterial;
                    var technique = json.techniques[material.technique];
                    materialParams.uniforms = {};
                    var program = json.programs[technique.program];
                    if (program) {
                        materialParams.fragmentShader = dependencies.shaders[program.fragmentShader];
                        if (!materialParams.fragmentShader) // Shaders are intentionally skipped since materials get replaced by replaceBrushMaterials()
                        materialType = $hBQxr$three.MeshPhongMaterial;
                        var vertexShader = dependencies.shaders[program.vertexShader];
                        if (!vertexShader) // Shaders are intentionally skipped since materials get replaced by replaceBrushMaterials()
                        materialType = $hBQxr$three.MeshPhongMaterial;
                        // IMPORTANT: FIX VERTEX SHADER ATTRIBUTE DEFINITIONS
                        // I'm not sure we need to replace the param names any more.
                        // Not sure why it worked before!
                        //materialParams.vertexShader = replaceTHREEShaderAttributes( vertexShader, technique );
                        var uniforms = technique.uniforms;
                        for(var uniformId in uniforms){
                            var pname = uniforms[uniformId];
                            var shaderParam = technique.parameters[pname];
                            var ptype = shaderParam.type;
                            if ($81e80e8b2d2d5e9f$var$WEBGL_TYPE[ptype]) {
                                var pcount = shaderParam.count;
                                var value;
                                if (material.values !== undefined) value = material.values[pname];
                                var uvalue = new $81e80e8b2d2d5e9f$var$WEBGL_TYPE[ptype]();
                                var usemantic = shaderParam.semantic;
                                var unode = shaderParam.node;
                                switch(ptype){
                                    case $81e80e8b2d2d5e9f$var$WEBGL_CONSTANTS.FLOAT:
                                        uvalue = shaderParam.value;
                                        if (pname == "transparency") materialParams.transparent = true;
                                        if (value !== undefined) uvalue = value;
                                        break;
                                    case $81e80e8b2d2d5e9f$var$WEBGL_CONSTANTS.FLOAT_VEC2:
                                    case $81e80e8b2d2d5e9f$var$WEBGL_CONSTANTS.FLOAT_VEC3:
                                    case $81e80e8b2d2d5e9f$var$WEBGL_CONSTANTS.FLOAT_VEC4:
                                    case $81e80e8b2d2d5e9f$var$WEBGL_CONSTANTS.FLOAT_MAT3:
                                        if (shaderParam && shaderParam.value) uvalue.fromArray(shaderParam.value);
                                        if (value) uvalue.fromArray(value);
                                        break;
                                    case $81e80e8b2d2d5e9f$var$WEBGL_CONSTANTS.FLOAT_MAT2:
                                        // what to do?
                                        console.warn("FLOAT_MAT2 is not a supported uniform type");
                                        break;
                                    case $81e80e8b2d2d5e9f$var$WEBGL_CONSTANTS.FLOAT_MAT4:
                                        if (pcount) {
                                            uvalue = new Array(pcount);
                                            for(var mi = 0; mi < pcount; mi++)uvalue[mi] = new $81e80e8b2d2d5e9f$var$WEBGL_TYPE[ptype]();
                                            if (shaderParam && shaderParam.value) {
                                                var m4v = shaderParam.value;
                                                uvalue.fromArray(m4v);
                                            }
                                            if (value) uvalue.fromArray(value);
                                        } else {
                                            if (shaderParam && shaderParam.value) {
                                                var m4 = shaderParam.value;
                                                uvalue.fromArray(m4);
                                            }
                                            if (value) uvalue.fromArray(value);
                                        }
                                        break;
                                    case $81e80e8b2d2d5e9f$var$WEBGL_CONSTANTS.SAMPLER_2D:
                                        if (value !== undefined) uvalue = dependencies.textures[value];
                                        else if (shaderParam.value !== undefined) uvalue = dependencies.textures[shaderParam.value];
                                        else uvalue = null;
                                        break;
                                }
                                materialParams.uniforms[uniformId] = {
                                    value: uvalue,
                                    semantic: usemantic,
                                    node: unode
                                };
                            } else throw new Error("Unknown shader uniform param type: " + ptype);
                        }
                        var states = technique.states || {};
                        var enables = states.enable || [];
                        var functions = states.functions || {};
                        var enableCullFace = false;
                        var enableDepthTest = false;
                        var enableBlend = false;
                        for(var i = 0, il = enables.length; i < il; i++){
                            var enable = enables[i];
                            switch($81e80e8b2d2d5e9f$var$STATES_ENABLES[enable]){
                                case 'CULL_FACE':
                                    enableCullFace = true;
                                    break;
                                case 'DEPTH_TEST':
                                    enableDepthTest = true;
                                    break;
                                case 'BLEND':
                                    enableBlend = true;
                                    break;
                                // TODO: implement
                                case 'SCISSOR_TEST':
                                case 'POLYGON_OFFSET_FILL':
                                case 'SAMPLE_ALPHA_TO_COVERAGE':
                                    break;
                                default:
                                    throw new Error("Unknown technique.states.enable: " + enable);
                            }
                        }
                        if (enableCullFace) materialParams.side = functions.cullFace !== undefined ? $81e80e8b2d2d5e9f$var$WEBGL_SIDES[functions.cullFace] : $hBQxr$three.FrontSide;
                        else materialParams.side = $hBQxr$three.DoubleSide;
                        materialParams.depthTest = enableDepthTest;
                        materialParams.depthFunc = functions.depthFunc !== undefined ? $81e80e8b2d2d5e9f$var$WEBGL_DEPTH_FUNCS[functions.depthFunc] : $hBQxr$three.LessDepth;
                        materialParams.depthWrite = functions.depthMask !== undefined ? functions.depthMask[0] : true;
                        materialParams.blending = enableBlend ? $hBQxr$three.CustomBlending : $hBQxr$three.NoBlending;
                        materialParams.transparent = enableBlend;
                        var blendEquationSeparate = functions.blendEquationSeparate;
                        if (blendEquationSeparate !== undefined) {
                            materialParams.blendEquation = $81e80e8b2d2d5e9f$var$WEBGL_BLEND_EQUATIONS[blendEquationSeparate[0]];
                            materialParams.blendEquationAlpha = $81e80e8b2d2d5e9f$var$WEBGL_BLEND_EQUATIONS[blendEquationSeparate[1]];
                        } else {
                            materialParams.blendEquation = $hBQxr$three.AddEquation;
                            materialParams.blendEquationAlpha = $hBQxr$three.AddEquation;
                        }
                        var blendFuncSeparate = functions.blendFuncSeparate;
                        if (blendFuncSeparate !== undefined) {
                            materialParams.blendSrc = $81e80e8b2d2d5e9f$var$WEBGL_BLEND_FUNCS[blendFuncSeparate[0]];
                            materialParams.blendDst = $81e80e8b2d2d5e9f$var$WEBGL_BLEND_FUNCS[blendFuncSeparate[1]];
                            materialParams.blendSrcAlpha = $81e80e8b2d2d5e9f$var$WEBGL_BLEND_FUNCS[blendFuncSeparate[2]];
                            materialParams.blendDstAlpha = $81e80e8b2d2d5e9f$var$WEBGL_BLEND_FUNCS[blendFuncSeparate[3]];
                        } else {
                            materialParams.blendSrc = $hBQxr$three.OneFactor;
                            materialParams.blendDst = $hBQxr$three.ZeroFactor;
                            materialParams.blendSrcAlpha = $hBQxr$three.OneFactor;
                            materialParams.blendDstAlpha = $hBQxr$three.ZeroFactor;
                        }
                    }
                }
                if (Array.isArray(materialValues.diffuse)) materialParams.color = new $hBQxr$three.Color().fromArray(materialValues.diffuse);
                else if (typeof materialValues.diffuse === 'string') materialParams.map = dependencies.textures[materialValues.diffuse];
                delete materialParams.diffuse;
                if (typeof materialValues.reflective === 'string') materialParams.envMap = dependencies.textures[materialValues.reflective];
                if (typeof materialValues.bump === 'string') materialParams.bumpMap = dependencies.textures[materialValues.bump];
                if (Array.isArray(materialValues.emission)) {
                    if (materialType === $hBQxr$three.MeshBasicMaterial) materialParams.color = new $hBQxr$three.Color().fromArray(materialValues.emission);
                    else materialParams.emissive = new $hBQxr$three.Color().fromArray(materialValues.emission);
                } else if (typeof materialValues.emission === 'string') {
                    if (materialType === $hBQxr$three.MeshBasicMaterial) materialParams.map = dependencies.textures[materialValues.emission];
                    else materialParams.emissiveMap = dependencies.textures[materialValues.emission];
                }
                if (Array.isArray(materialValues.specular)) materialParams.specular = new $hBQxr$three.Color().fromArray(materialValues.specular);
                else if (typeof materialValues.specular === 'string') materialParams.specularMap = dependencies.textures[materialValues.specular];
                if (materialValues.shininess !== undefined) materialParams.shininess = materialValues.shininess;
                var _material = new materialType(materialParams);
                if (material.name !== undefined) _material.name = material.name;
                return _material;
            });
        });
    }
    loadMeshes() {
        var json = this.json;
        return this._withDependencies([
            "accessors",
            "materials"
        ]).then(function(dependencies) {
            return $81e80e8b2d2d5e9f$var$_each(json.meshes, function(mesh) {
                var group = new $hBQxr$three.Group();
                if (mesh.name !== undefined) group.name = mesh.name;
                if (mesh.extras) group.userData = mesh.extras;
                var primitives = mesh.primitives || [];
                for(var name in primitives){
                    var primitive = primitives[name];
                    if (primitive.mode === $81e80e8b2d2d5e9f$var$WEBGL_CONSTANTS.TRIANGLES || primitive.mode === undefined) {
                        var geometry = new $hBQxr$three.BufferGeometry();
                        var attributes = primitive.attributes;
                        for(var attributeId in attributes){
                            var attributeEntry = attributes[attributeId];
                            if (!attributeEntry) return;
                            var bufferAttribute = dependencies.accessors[attributeEntry];
                            switch(attributeId){
                                case 'POSITION':
                                    geometry.setAttribute('position', bufferAttribute);
                                    break;
                                case 'NORMAL':
                                    geometry.setAttribute('normal', bufferAttribute);
                                    break;
                                case 'TEXCOORD_0':
                                case 'TEXCOORD0':
                                case 'TEXCOORD':
                                    geometry.setAttribute('uv', bufferAttribute);
                                    break;
                                case 'TEXCOORD_1':
                                    geometry.setAttribute('uv2', bufferAttribute);
                                    break;
                                case 'COLOR_0':
                                case 'COLOR0':
                                case 'COLOR':
                                    geometry.setAttribute('color', bufferAttribute);
                                    break;
                                case 'WEIGHT':
                                    geometry.setAttribute('skinWeight', bufferAttribute);
                                    break;
                                case 'JOINT':
                                    geometry.setAttribute('skinIndex', bufferAttribute);
                                    break;
                                default:
                                    if (!primitive.material) break;
                                    var material = json.materials[primitive.material];
                                    if (!material.technique) break;
                                    var parameters = json.techniques[material.technique].parameters || {};
                                    for(var attributeName in parameters)if (parameters[attributeName]['semantic'] === attributeId) geometry.setAttribute(attributeName, bufferAttribute);
                            }
                        }
                        if (primitive.indices) geometry.setIndex(dependencies.accessors[primitive.indices]);
                        var material = dependencies.materials !== undefined ? dependencies.materials[primitive.material] : $81e80e8b2d2d5e9f$var$createDefaultMaterial();
                        var meshNode = new $hBQxr$three.Mesh(geometry, material);
                        meshNode.castShadow = true;
                        meshNode.name = name === "0" ? group.name : group.name + name;
                        if (primitive.extras) meshNode.userData = primitive.extras;
                        group.add(meshNode);
                    } else if (primitive.mode === $81e80e8b2d2d5e9f$var$WEBGL_CONSTANTS.LINES) {
                        var geometry = new $hBQxr$three.BufferGeometry();
                        var attributes = primitive.attributes;
                        for(var attributeId in attributes){
                            var attributeEntry = attributes[attributeId];
                            if (!attributeEntry) return;
                            var bufferAttribute = dependencies.accessors[attributeEntry];
                            switch(attributeId){
                                case 'POSITION':
                                    geometry.setAttribute('position', bufferAttribute);
                                    break;
                                case 'COLOR_0':
                                case 'COLOR0':
                                case 'COLOR':
                                    geometry.setAttribute('color', bufferAttribute);
                                    break;
                            }
                        }
                        var material = dependencies.materials[primitive.material];
                        var meshNode;
                        if (primitive.indices) {
                            geometry.setIndex(dependencies.accessors[primitive.indices]);
                            meshNode = new $hBQxr$three.LineSegments(geometry, material);
                        } else meshNode = new $hBQxr$three.Line(geometry, material);
                        meshNode.name = name === "0" ? group.name : group.name + name;
                        if (primitive.extras) meshNode.userData = primitive.extras;
                        group.add(meshNode);
                    } else console.warn("Only triangular and line primitives are supported");
                }
                return group;
            });
        });
    }
    loadCameras() {
        var json = this.json;
        return $81e80e8b2d2d5e9f$var$_each(json.cameras, function(camera) {
            if (camera.type == "perspective" && camera.perspective) {
                var yfov = camera.perspective.yfov;
                var aspectRatio = camera.perspective.aspectRatio !== undefined ? camera.perspective.aspectRatio : 1;
                // According to COLLADA spec...
                // aspectRatio = xfov / yfov
                var xfov = yfov * aspectRatio;
                var _camera = new $hBQxr$three.PerspectiveCamera($hBQxr$three.MathUtils.radToDeg(xfov), aspectRatio, camera.perspective.znear || 1, camera.perspective.zfar || 2e6);
                if (camera.name !== undefined) _camera.name = camera.name;
                if (camera.extras) _camera.userData = camera.extras;
                return _camera;
            } else if (camera.type == "orthographic" && camera.orthographic) {
                var _camera = new $hBQxr$three.OrthographicCamera(window.innerWidth / -2, window.innerWidth / 2, window.innerHeight / 2, window.innerHeight / -2, camera.orthographic.znear, camera.orthographic.zfar);
                if (camera.name !== undefined) _camera.name = camera.name;
                if (camera.extras) _camera.userData = camera.extras;
                return _camera;
            }
        });
    }
    loadSkins() {
        var json = this.json;
        return this._withDependencies([
            "accessors"
        ]).then(function(dependencies) {
            return $81e80e8b2d2d5e9f$var$_each(json.skins, function(skin) {
                var bindShapeMatrix = new $hBQxr$three.Matrix4();
                if (skin.bindShapeMatrix !== undefined) bindShapeMatrix.fromArray(skin.bindShapeMatrix);
                var _skin = {
                    bindShapeMatrix: bindShapeMatrix,
                    jointNames: skin.jointNames,
                    inverseBindMatrices: dependencies.accessors[skin.inverseBindMatrices]
                };
                return _skin;
            });
        });
    }
    loadAnimations() {
        var json = this.json;
        return this._withDependencies([
            "accessors",
            "nodes"
        ]).then(function(dependencies) {
            return $81e80e8b2d2d5e9f$var$_each(json.animations, function(animation, animationId) {
                var tracks = [];
                for(var channelId in animation.channels){
                    var channel = animation.channels[channelId];
                    var sampler = animation.samplers[channel.sampler];
                    if (sampler) {
                        var target = channel.target;
                        var name = target.id;
                        var input = animation.parameters !== undefined ? animation.parameters[sampler.input] : sampler.input;
                        var output = animation.parameters !== undefined ? animation.parameters[sampler.output] : sampler.output;
                        var inputAccessor = dependencies.accessors[input];
                        var outputAccessor = dependencies.accessors[output];
                        var node = dependencies.nodes[name];
                        if (node) {
                            node.updateMatrix();
                            node.matrixAutoUpdate = true;
                            var TypedKeyframeTrack = $81e80e8b2d2d5e9f$var$PATH_PROPERTIES[target.path] === $81e80e8b2d2d5e9f$var$PATH_PROPERTIES.rotation ? $hBQxr$three.QuaternionKeyframeTrack : $hBQxr$three.VectorKeyframeTrack;
                            var targetName = node.name ? node.name : node.uuid;
                            var interpolation = sampler.interpolation !== undefined ? $81e80e8b2d2d5e9f$var$INTERPOLATION[sampler.interpolation] : $hBQxr$three.InterpolateLinear;
                            // KeyframeTrack.optimize() will modify given 'times' and 'values'
                            // buffers before creating a truncated copy to keep. Because buffers may
                            // be reused by other tracks, make copies here.
                            tracks.push(new TypedKeyframeTrack(targetName + '.' + $81e80e8b2d2d5e9f$var$PATH_PROPERTIES[target.path], $hBQxr$three.AnimationUtils.arraySlice(inputAccessor.array, 0), $hBQxr$three.AnimationUtils.arraySlice(outputAccessor.array, 0), interpolation));
                        }
                    }
                }
                var name = animation.name !== undefined ? animation.name : "animation_" + animationId;
                return new $hBQxr$three.AnimationClip(name, undefined, tracks);
            });
        });
    }
    loadNodes() {
        var json = this.json;
        var extensions = this.extensions;
        var scope = this;
        return $81e80e8b2d2d5e9f$var$_each(json.nodes, function(node) {
            var matrix = new $hBQxr$three.Matrix4();
            var _node;
            if (node.jointName) {
                _node = new $hBQxr$three.Bone();
                _node.name = node.name !== undefined ? node.name : node.jointName;
                _node.jointName = node.jointName;
            } else {
                _node = new $hBQxr$three.Object3D();
                if (node.name !== undefined) _node.name = node.name;
            }
            if (node.extras) _node.userData = node.extras;
            if (node.matrix !== undefined) {
                matrix.fromArray(node.matrix);
                _node.applyMatrix4(matrix);
            } else {
                if (node.translation !== undefined) _node.position.fromArray(node.translation);
                if (node.rotation !== undefined) _node.quaternion.fromArray(node.rotation);
                if (node.scale !== undefined) _node.scale.fromArray(node.scale);
            }
            return _node;
        }).then(function(__nodes) {
            return scope._withDependencies([
                "meshes",
                "skins",
                "cameras"
            ]).then(function(dependencies) {
                return $81e80e8b2d2d5e9f$var$_each(__nodes, function(_node, nodeId) {
                    var node = json.nodes[nodeId];
                    if (node.meshes !== undefined) for(var meshId in node.meshes){
                        var mesh = node.meshes[meshId];
                        var group = dependencies.meshes[mesh];
                        if (group === undefined) {
                            console.warn('LegacyGLTFLoader: Couldn\'t find node "' + mesh + '".');
                            continue;
                        }
                        for(var childrenId in group.children){
                            var child = group.children[childrenId];
                            // clone Mesh to add to _node
                            var originalMaterial = child.material;
                            var originalGeometry = child.geometry;
                            var originalUserData = child.userData;
                            var originalName = child.name;
                            var material;
                            if (originalMaterial.isDeferredShaderMaterial) originalMaterial = material = originalMaterial.create();
                            else material = originalMaterial;
                            switch(child.type){
                                case 'LineSegments':
                                    child = new $hBQxr$three.LineSegments(originalGeometry, material);
                                    break;
                                case 'LineLoop':
                                    child = new $hBQxr$three.LineLoop(originalGeometry, material);
                                    break;
                                case 'Line':
                                    child = new $hBQxr$three.Line(originalGeometry, material);
                                    break;
                                default:
                                    child = new $hBQxr$three.Mesh(originalGeometry, material);
                            }
                            child.castShadow = true;
                            child.userData = originalUserData;
                            child.name = originalName;
                            var skinEntry;
                            if (node.skin) skinEntry = dependencies.skins[node.skin];
                            // Replace Mesh with SkinnedMesh in library
                            if (skinEntry) {
                                var getJointNode = function(jointId) {
                                    var keys = Object.keys(__nodes);
                                    for(var i = 0, il = keys.length; i < il; i++){
                                        var n = __nodes[keys[i]];
                                        if (n.jointName === jointId) return n;
                                    }
                                    return null;
                                };
                                var geometry = originalGeometry;
                                var material = originalMaterial;
                                material.skinning = true;
                                child = new $hBQxr$three.SkinnedMesh(geometry, material);
                                child.castShadow = true;
                                child.userData = originalUserData;
                                child.name = originalName;
                                var bones = [];
                                var boneInverses = [];
                                for(var i = 0, l = skinEntry.jointNames.length; i < l; i++){
                                    var jointId = skinEntry.jointNames[i];
                                    var jointNode = getJointNode(jointId);
                                    if (jointNode) {
                                        bones.push(jointNode);
                                        var m = skinEntry.inverseBindMatrices.array;
                                        var mat = new $hBQxr$three.Matrix4().fromArray(m, i * 16);
                                        boneInverses.push(mat);
                                    } else console.warn("WARNING: joint: '" + jointId + "' could not be found");
                                }
                                child.bind(new $hBQxr$three.Skeleton(bones, boneInverses), skinEntry.bindShapeMatrix);
                                var buildBoneGraph = function(parentJson, parentObject, property) {
                                    var children = parentJson[property];
                                    if (children === undefined) return;
                                    for(var i = 0, il = children.length; i < il; i++){
                                        var nodeId = children[i];
                                        var bone = __nodes[nodeId];
                                        var boneJson = json.nodes[nodeId];
                                        if (bone !== undefined && bone.isBone === true && boneJson !== undefined) {
                                            parentObject.add(bone);
                                            buildBoneGraph(boneJson, bone, 'children');
                                        }
                                    }
                                };
                                buildBoneGraph(node, child, 'skeletons');
                            }
                            _node.add(child);
                        }
                    }
                    if (node.camera !== undefined) {
                        var camera = dependencies.cameras[node.camera];
                        _node.add(camera);
                    }
                    if (node.extensions && node.extensions[$81e80e8b2d2d5e9f$var$EXTENSIONS.KHR_MATERIALS_COMMON] && node.extensions[$81e80e8b2d2d5e9f$var$EXTENSIONS.KHR_MATERIALS_COMMON].light) {
                        var extensionLights = extensions[$81e80e8b2d2d5e9f$var$EXTENSIONS.KHR_MATERIALS_COMMON].lights;
                        var light = extensionLights[node.extensions[$81e80e8b2d2d5e9f$var$EXTENSIONS.KHR_MATERIALS_COMMON].light];
                        _node.add(light);
                    }
                    return _node;
                });
            });
        });
    }
    loadScenes() {
        var json = this.json;
        // scene node hierachy builder
        function buildNodeHierachy(nodeId, parentObject, allNodes) {
            var _node = allNodes[nodeId];
            parentObject.add(_node);
            var node = json.nodes[nodeId];
            if (node.children) {
                var children = node.children;
                for(var i = 0, l = children.length; i < l; i++){
                    var child = children[i];
                    buildNodeHierachy(child, _node, allNodes);
                }
            }
        }
        return this._withDependencies([
            "nodes"
        ]).then(function(dependencies) {
            return $81e80e8b2d2d5e9f$var$_each(json.scenes, function(scene) {
                var _scene = new $hBQxr$three.Scene();
                if (scene.name !== undefined) _scene.name = scene.name;
                if (scene.extras) _scene.userData = scene.extras;
                var nodes = scene.nodes || [];
                for(var i = 0, l = nodes.length; i < l; i++){
                    var nodeId = nodes[i];
                    buildNodeHierachy(nodeId, _scene, dependencies.nodes);
                }
                _scene.traverse(function(child) {
                    // Register raw material meshes with LegacyGLTFLoader.Shaders
                    if (child.material && child.material.isRawShaderMaterial) {
                        child.gltfShader = new $81e80e8b2d2d5e9f$var$GLTFShader(child, dependencies.nodes);
                        child.onBeforeRender = function(renderer, scene, camera) {
                            this.gltfShader.update(scene, camera);
                        };
                    }
                });
                return _scene;
            });
        });
    }
}



class $677737c8a5cbea2f$var$SketchMetadata {
    constructor(scene, userData){
        // Traverse the scene and return all nodes with a name starting with "node_SceneLight_"
        let sceneLights = [];
        scene?.traverse((node)=>{
            if (node.name && node.name.startsWith("node_SceneLight_")) {
                sceneLights.push(node);
                if (sceneLights.length === 2) return false; // Bail out early
            }
            return true; // Continue traversal
        });
        this.EnvironmentGuid = userData['TB_EnvironmentGuid'] ?? '';
        this.Environment = userData['TB_Environment'] ?? '(None)';
        this.EnvironmentPreset = new $677737c8a5cbea2f$var$EnvironmentPreset($677737c8a5cbea2f$export$2ec4afd9b3c16a85.lookupEnvironment(this.EnvironmentGuid));
        if (userData && userData['TB_UseGradient'] === undefined) {
            // The sketch metadata doesn't specify whether to use a gradient or not,
            // so we'll use the environment preset value (assuming it's not a null preset)
            let isValidEnvironmentPreset = this.EnvironmentPreset.Guid !== null;
            this.UseGradient = isValidEnvironmentPreset && this.EnvironmentPreset.UseGradient;
        } else this.UseGradient = JSON.parse(userData['TB_UseGradient'].toLowerCase());
        this.SkyColorA = $677737c8a5cbea2f$export$2ec4afd9b3c16a85.parseTBColorString(userData['TB_SkyColorA'], this.EnvironmentPreset.SkyColorA);
        this.SkyColorB = $677737c8a5cbea2f$export$2ec4afd9b3c16a85.parseTBColorString(userData['TB_SkyColorB'], this.EnvironmentPreset.SkyColorB);
        this.SkyGradientDirection = $677737c8a5cbea2f$export$2ec4afd9b3c16a85.parseTBVector3(userData['TB_SkyGradientDirection'], new $hBQxr$three.Vector3(0, 1, 0));
        this.AmbientLightColor = $677737c8a5cbea2f$export$2ec4afd9b3c16a85.parseTBColorString(userData['TB_AmbientLightColor'], this.EnvironmentPreset.AmbientLightColor);
        this.FogColor = $677737c8a5cbea2f$export$2ec4afd9b3c16a85.parseTBColorString(userData['TB_FogColor'], this.EnvironmentPreset.FogColor);
        this.FogDensity = userData['TB_FogDensity'] ?? this.EnvironmentPreset.FogDensity;
        this.SkyTexture = userData['TB_SkyTexture'] ?? this.EnvironmentPreset.SkyTexture;
        this.ReflectionTexture = userData['TB_ReflectionTexture'] ?? this.EnvironmentPreset.ReflectionTexture;
        this.ReflectionIntensity = userData['TB_ReflectionIntensity'] ?? this.EnvironmentPreset.ReflectionIntensity;
        function radToDeg3(rot) {
            return {
                x: $hBQxr$three.MathUtils.radToDeg(rot.x),
                y: $hBQxr$three.MathUtils.radToDeg(rot.y),
                z: $hBQxr$three.MathUtils.radToDeg(rot.z)
            };
        }
        let light0rot = sceneLights.length >= 1 ? radToDeg3(sceneLights[0].rotation) : null;
        let light1rot = sceneLights.length >= 2 ? radToDeg3(sceneLights[1].rotation) : null;
        // Light 0 Rotation
        if (userData['TB_SceneLight0Rotation']) this.SceneLight0Rotation = $677737c8a5cbea2f$export$2ec4afd9b3c16a85.parseTBVector3(userData['TB_SceneLight0Rotation']);
        else if (light0rot) this.SceneLight0Rotation = new $hBQxr$three.Vector3(light0rot.x, light0rot.y, light0rot.z);
        else this.SceneLight0Rotation = this.EnvironmentPreset.SceneLight0Rotation;
        // Light 1 Rotation
        if (userData['TB_SceneLight1Rotation']) this.SceneLight1Rotation = $677737c8a5cbea2f$export$2ec4afd9b3c16a85.parseTBVector3(userData['TB_SceneLight1Rotation']);
        else if (light1rot) this.SceneLight1Rotation = new $hBQxr$three.Vector3(light1rot.x, light1rot.y, light1rot.z);
        else this.SceneLight1Rotation = this.EnvironmentPreset.SceneLight1Rotation;
        // Light 0 Color
        if (userData['TB_SceneLight0Color']) this.SceneLight0Color = $677737c8a5cbea2f$export$2ec4afd9b3c16a85.parseTBColorString(userData['TB_SceneLight0Color'], this.EnvironmentPreset.SceneLight0Color);
        else this.SceneLight0Color = $677737c8a5cbea2f$export$2ec4afd9b3c16a85.parseTBColorString(null, this.EnvironmentPreset.SceneLight0Color);
        // Light 1 Color
        if (userData['TB_SceneLight1Color']) this.SceneLight1Color = $677737c8a5cbea2f$export$2ec4afd9b3c16a85.parseTBColorString(userData['TB_SceneLight1Color'], this.EnvironmentPreset.SceneLight1Color);
        else this.SceneLight1Color = $677737c8a5cbea2f$export$2ec4afd9b3c16a85.parseTBColorString(null, this.EnvironmentPreset.SceneLight1Color);
        // Remove original GLTF lights since we'll create new ones from metadata
        sceneLights.forEach((light)=>{
            light.parent?.remove(light);
        });
        this.CameraTranslation = $677737c8a5cbea2f$export$2ec4afd9b3c16a85.parseTBVector3(userData['TB_CameraTranslation'], null);
        this.CameraRotation = $677737c8a5cbea2f$export$2ec4afd9b3c16a85.parseTBVector3(userData['TB_CameraRotation'], null);
        const parsed = parseFloat(userData['TB_CameraTargetDistance']);
        this.CameraTargetDistance = Number.isFinite(parsed) ? parsed : null;
        this.FlyMode = userData['TB_FlyMode'] ? JSON.parse(userData['TB_FlyMode'].toLowerCase()) : false;
    }
}
class $677737c8a5cbea2f$var$EnvironmentPreset {
    constructor(preset){
        let defaultColor = new $hBQxr$three.Color("#FFF");
        let defaultRotation = new $hBQxr$three.Vector3(0, 1, 0);
        this.Guid = preset?.guid ?? null;
        this.Name = preset?.name ?? "No preset";
        this.AmbientLightColor = preset?.renderSettings.ambientColor ?? defaultColor;
        this.SkyColorA = preset?.skyboxColorA ?? defaultColor;
        this.SkyColorB = preset?.skyboxColorB ?? defaultColor;
        this.SkyGradientDirection = new $hBQxr$three.Vector3(0, 1, 0);
        this.FogColor = preset?.renderSettings.fogColor ?? defaultColor;
        this.FogDensity = preset?.renderSettings.fogDensity ?? 0;
        this.SceneLight0Color = preset?.lights[0].color ?? defaultColor;
        this.SceneLight0Rotation = preset?.lights[0].rotation ?? defaultRotation;
        this.SceneLight1Color = preset?.lights[1].color ?? defaultColor;
        this.SceneLight1Rotation = preset?.lights[1].rotation ?? defaultRotation;
        this.SkyTexture = preset?.renderSettings.skyboxCubemap ?? null;
        this.UseGradient = this.SkyTexture === null;
        this.ReflectionTexture = preset?.renderSettings.reflectionCubemap ?? null;
        this.ReflectionIntensity = preset?.renderSettings.reflectionIntensity ?? 1;
    }
}
class $677737c8a5cbea2f$export$2ec4afd9b3c16a85 {
    constructor(assetBaseUrl, pre_render, frame){///
		this.pre_render  = pre_render ///
				
        this.loadingError = false;
        this.icosa_frame = frame;
        // Attempt to find viewer frame if not assigned
        if (!this.icosa_frame) this.icosa_frame = document.getElementById('icosa-viewer');
        // Create if still not assigned
        if (!this.icosa_frame) {
            this.icosa_frame = document.createElement('div');
            this.icosa_frame.id = 'icosa-viewer';
        }
		/* ///
        initCustomUi(this.icosa_frame);
        const controlPanel = document.createElement('div');
        controlPanel.classList.add('control-panel');
        const fullscreenButton = document.createElement('button');
        fullscreenButton.classList.add('panel-button', 'fullscreen-button');
        fullscreenButton.onclick = ()=>{
            this.toggleFullscreen(fullscreenButton);
        };
        controlPanel.appendChild(fullscreenButton);
        this.icosa_frame.appendChild(controlPanel);
		/// */
		
        //loadscreen
        const loadscreen = document.createElement('div');
        loadscreen.id = 'loadscreen';
        const loadanim = document.createElement('div');
        loadanim.classList.add('loadlogo');
        loadscreen.appendChild(loadanim);
        this.icosa_frame.appendChild(loadscreen);
        loadscreen.addEventListener('transitionend', function() {
            const opacity = window.getComputedStyle(loadscreen).opacity;
            if (parseFloat(opacity) < 0.2) loadscreen.classList.add('loaded');
        });
        this.showErrorIcon = ()=>{
            let loadscreen = document.getElementById('loadscreen');
            loadscreen?.classList.remove('fade-out');
            loadscreen?.classList.add('loaderror');
        };
        const clock = new $hBQxr$three.Clock();
        this.scene = new $hBQxr$three.Scene();
        this.three = $hBQxr$three;
        const viewer1 = this;
        const manager = new $hBQxr$three.LoadingManager();
        manager.onStart = function() {
            document.getElementById('loadscreen')?.classList.remove('fade-out');
            document.getElementById('loadscreen')?.classList.remove('loaded');
        };
        manager.onLoad = function() {
            let loadscreen = document.getElementById('loadscreen');
            if (!loadscreen?.classList.contains('loaderror')) loadscreen?.classList.add('fade-out');
        };
        this.brushPath = new URL('brushes/', assetBaseUrl);
        this.environmentPath = new URL('environments/', assetBaseUrl);
        this.texturePath = new URL('textures/', assetBaseUrl);
        this.defaultBackgroundColor = new $hBQxr$three.Color(0x000000);
        this.tiltLoader = new (0, $hBQxr$TiltLoader)(manager);
        this.tiltLoader.setBrushPath(this.brushPath.toString());
        this.objLoader = new (0, $hBQxr$OBJLoader)(manager);
        this.mtlLoader = new (0, $hBQxr$MTLLoader)(manager);
        this.fbxLoader = new (0, $hBQxr$FBXLoader)(manager);
        this.plyLoader = new (0, $hBQxr$PLYLoader)(manager);
        this.stlLoader = new (0, $hBQxr$STLLoader)(manager);
        this.usdzLoader = new (0, $hBQxr$USDZLoader)(manager);
        this.voxLoader = new (0, $hBQxr$VOXLoader)(manager);
        this.gltfLegacyLoader = new (0, $81e80e8b2d2d5e9f$export$9559c3115faeb0b0)(manager, assetBaseUrl);
        this.gltfLoader = new (0, $hBQxr$GLTFLoader)(manager);
        // this.gltfLoader.register(parser => new GLTFGoogleTiltBrushTechniquesExtension(parser, this.brushPath.toString()));
        this.gltfLoader.register((parser)=>new (0, $hBQxr$GLTFGoogleTiltBrushMaterialExtension)(parser, this.brushPath.toString()));
        const dracoLoader = new (0, $hBQxr$DRACOLoader)();
        dracoLoader.setDecoderPath('https://www.gstatic.com/draco/v1/decoders/');
        this.gltfLoader.setDRACOLoader(dracoLoader);
        this.canvas = document.createElement('canvas');
        this.canvas.id = 'c';
        this.icosa_frame.appendChild(this.canvas);
        this.canvas.onmousedown = ()=>{
            this.canvas.classList.add('grabbed');
        };
        this.canvas.onmouseup = ()=>{
            this.canvas.classList.remove('grabbed');
        };
        this.renderer = new $hBQxr$three.WebGLRenderer({
            canvas: this.canvas,
            antialias: true
        });
        this.renderer.setPixelRatio(window.devicePixelRatio);
        this.renderer.outputColorSpace = $hBQxr$three.SRGBColorSpace;
        this.renderer.xr.enabled = true;
        function handleController(inputSource) {
            const gamepad = inputSource.gamepad;
            if (gamepad) return {
                axes: gamepad.axes,
                buttons: gamepad.buttons
            };
            return null;
        }
        this.cameraRig = new $hBQxr$three.Group();
        this.selectedNode = null;
        this.treeViewRoot = null;
        let controller0;
        let controller1;
        let controllerGrip0;
        let controllerGrip1;
        let previousLeftThumbstickX = 0;
		
		viewer1.flying_value = 0 ///		
		
		try { ///
			controller0 = this.renderer.xr.getController(0);
			controller0.addEventListener( 'selectstart', _=>{ viewer1.flying_value = 1 })///
			controller0.addEventListener( 'selectend',   _=>{ viewer1.flying_value = 0 })///
			this.scene.add(controller0);
			controller1 = this.renderer.xr.getController(1);
			controller1.addEventListener( 'selectstart', _=>{ viewer1.flying_value = 1 })///
			controller1.addEventListener( 'selectend',   _=>{ viewer1.flying_value = 0 })///
			this.scene.add(controller1);
			const controllerModelFactory = new (0, $hBQxr$XRControllerModelFactory)();
			controllerGrip0 = this.renderer.xr.getControllerGrip(0);
			controllerGrip0.add(controllerModelFactory.createControllerModel(controllerGrip0));
			this.scene.add(controllerGrip0);
			controllerGrip1 = this.renderer.xr.getControllerGrip(1);
			controllerGrip1.add(controllerModelFactory.createControllerModel(controllerGrip1));
			this.scene.add(controllerGrip1);
		} catch( error ){} ///
		
        let xrButton = (0, $a681b8b24de9c7d6$export$d1c1e163c7960c6).createButton(this.renderer);
        this.xrButton = xrButton ///
		
		/* ///this.icosa_frame.appendChild(xrButton);
        function initCustomUi(viewerContainer) {
            const button = document.createElement('button');
            button.innerHTML = `<?xml version="1.0" encoding="utf-8"?>
<svg width="36" height="36" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
<path d="M4 7.5L11.6078 3.22062C11.7509 3.14014 11.8224 3.09991 11.8982 3.08414C11.9654 3.07019 12.0346 3.07019 12.1018 3.08414C12.1776 3.09991 12.2491 3.14014 12.3922 3.22062L20 7.5M4 7.5V16.0321C4 16.2025 4 16.2876 4.02499 16.3637C4.04711 16.431 4.08326 16.4928 4.13106 16.545C4.1851 16.6041 4.25933 16.6459 4.40779 16.7294L12 21M4 7.5L12 11.5M12 21L19.5922 16.7294C19.7407 16.6459 19.8149 16.6041 19.8689 16.545C19.9167 16.4928 19.9529 16.431 19.975 16.3637C20 16.2876 20 16.2025 20 16.0321V7.5M12 21V11.5M20 7.5L12 11.5" stroke="white" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
</svg>`;
            button.style.backgroundColor = 'transparent';
            button.style.position = 'absolute';
            button.style.bottom = '10px';
            button.style.left = '10px';
            button.style.padding = '0px 2px';
            button.style.border = 'none';
            button.style.color = 'white';
            button.style.cursor = 'pointer';
            button.style.zIndex = '20';
            button.title = 'Fit Scene to View';
            viewerContainer.appendChild(button);
            const svgPath = button.querySelector('path');
            button.addEventListener('click', ()=>{
                viewer1.frameScene();
            });
            button.addEventListener('mouseover', ()=>{
                svgPath.setAttribute('stroke', 'rgba(255, 255, 255, 0.7)');
            });
            button.addEventListener('mouseout', ()=>{
                svgPath.setAttribute('stroke', 'white');
            });
        }*/
		
        const animate = ()=>{
            this.renderer.setAnimationLoop(render);
        // requestAnimationFrame( animate );
        // composer.render();
        };
        const render = ()=>{
			
			this.pre_render() ///
			
            const delta = clock.getDelta();
            if (this.renderer.xr.isPresenting) {
                let session = this.renderer.xr.getSession();
                viewer1.activeCamera = viewer1?.xrCamera;
                const inputSources = Array.from(session.inputSources);
                const moveSpeed = 0.05;
                const snapAngle = 45; ///
                inputSources.forEach((inputSource)=>{
                    const controllerData = handleController(inputSource);
                    if (controllerData) {
                        const axes = controllerData.axes;
                        if (inputSource.handedness === 'left') // Movement (left thumbstick)
                        {
                            if (Math.abs(axes[2]) > 0.1 || Math.abs(axes[3]) > 0.1 || viewer1.flying_value ) {///
                                const moveX = axes[2] * moveSpeed;
                                const moveZ = -axes[3] * moveSpeed + viewer1.flying_value * 0.09;///
                                // Get the camera's forward and right vectors
                                const forward = new $hBQxr$three.Vector3();
                                viewer1.activeCamera.getWorldDirection(forward);
                                // TODO Make this an option
                                //forward.y = 0; // Ignore vertical movement
                                forward.normalize();
                                const right = new $hBQxr$three.Vector3();
                                right.crossVectors(forward, viewer1.activeCamera.up).normalize();
                                // Calculate the movement vector
                                const movement = new $hBQxr$three.Vector3();
                                movement.addScaledVector(forward, moveZ);
                                movement.addScaledVector(right, moveX);
                                viewer1.cameraRig.position.add(movement);
                            }
                        }
                        if (inputSource.handedness === 'right') {
                            // Rotation (right thumbstick x)
                            if (Math.abs(axes[2]) > 0.8 && Math.abs(previousLeftThumbstickX) <= 0.8) {
								if (axes[2] > 0) viewer1.cameraRig.rotateOnWorldAxis( viewer1.activeCamera.up,  $hBQxr$three.MathUtils.degToRad(snapAngle)) /// viewer1.cameraRig.rotation.y -= $hBQxr$three.MathUtils.degToRad(snapAngle);
								else             viewer1.cameraRig.rotateOnWorldAxis( viewer1.activeCamera.up, -$hBQxr$three.MathUtils.degToRad(snapAngle)) /// viewer1.cameraRig.rotation.y += $hBQxr$three.MathUtils.degToRad(snapAngle);
                            }
                            previousLeftThumbstickX = axes[2];
                            // Up/down position right thumbstick y)
                            if (Math.abs(axes[3]) > 0.5) viewer1.cameraRig.position.y -= axes[3] * moveSpeed; ///
                        }
                    }
                });
            } else {
                viewer1.activeCamera = viewer1?.flatCamera;
                const needResize = viewer1.canvas.width !== viewer1.canvas.clientWidth || viewer1.canvas.height !== viewer1.canvas.clientHeight;
                if (needResize && viewer1?.flatCamera) {
                    this.renderer.setSize(viewer1.canvas.clientWidth, viewer1.canvas.clientHeight, false);
                    viewer1.flatCamera.aspect = viewer1.canvas.clientWidth / viewer1.canvas.clientHeight;
                    viewer1.flatCamera.updateProjectionMatrix();
                }
                if (viewer1?.cameraControls) viewer1.cameraControls.update(delta);
                if (viewer1?.trackballControls) viewer1.trackballControls.update();
            }
            // SparkRenderer stochastic setup is now handled by GUI toggle
            if (viewer1?.activeCamera) this.renderer.render(viewer1.scene, viewer1.activeCamera);
        };
        this.dataURLtoBlob = (dataURL)=>{
            let arr = dataURL.split(',');
            let mimeMatch = arr[0].match(/:(.*?);/);
            let mime = mimeMatch ? mimeMatch[1] : 'image/png';
            let bstr = atob(arr[1]);
            let n = bstr.length;
            let u8arr = new Uint8Array(n);
            while(n--)u8arr[n] = bstr.charCodeAt(n);
            return new Blob([
                u8arr
            ], {
                type: mime
            });
        };
        this.captureThumbnail = (width, height)=>{
            // Store original renderer state
            const originalRenderTarget = this.renderer.getRenderTarget();
            const originalSize = this.renderer.getSize(new $hBQxr$three.Vector2());
            const originalPixelRatio = this.renderer.getPixelRatio();
            // Store original camera aspect ratio
            const originalAspect = this.activeCamera.aspect;
            // Create render target for offscreen rendering
            const renderTarget = new $hBQxr$three.WebGLRenderTarget(width, height, {
                format: $hBQxr$three.RGBAFormat,
                type: $hBQxr$three.UnsignedByteType,
                generateMipmaps: false,
                minFilter: $hBQxr$three.LinearFilter,
                magFilter: $hBQxr$three.LinearFilter
            });
            // Set render target and size
            this.renderer.setRenderTarget(renderTarget);
            this.renderer.setSize(width, height, false);
            this.renderer.setPixelRatio(1); // Use 1:1 pixel ratio for consistent output
            // Update camera aspect ratio to match thumbnail dimensions
            this.activeCamera.aspect = width / height;
            this.activeCamera.updateProjectionMatrix();
            // Render the scene
            this.renderer.render(this.scene, this.activeCamera);
            // Read pixels from render target
            const pixels = new Uint8Array(width * height * 4);
            this.renderer.readRenderTargetPixels(renderTarget, 0, 0, width, height, pixels);
            // Create canvas and draw pixels to it
            const canvas = document.createElement('canvas');
            canvas.width = width;
            canvas.height = height;
            const ctx = canvas.getContext('2d');
            const imageData = ctx.createImageData(width, height);
            // Copy pixels (note: WebGL coordinates are flipped compared to canvas)
            for(let y = 0; y < height; y++)for(let x = 0; x < width; x++){
                const srcIndex = ((height - y - 1) * width + x) * 4; // Flip Y
                const dstIndex = (y * width + x) * 4;
                imageData.data[dstIndex] = pixels[srcIndex]; // R
                imageData.data[dstIndex + 1] = pixels[srcIndex + 1]; // G
                imageData.data[dstIndex + 2] = pixels[srcIndex + 2]; // B
                imageData.data[dstIndex + 3] = pixels[srcIndex + 3]; // A
            }
            ctx.putImageData(imageData, 0, 0);
            const dataUrl = canvas.toDataURL('image/png');
            // Restore original renderer state
            this.renderer.setRenderTarget(originalRenderTarget);
            this.renderer.setSize(originalSize.x, originalSize.y, false);
            this.renderer.setPixelRatio(originalPixelRatio);
            // Restore original camera aspect ratio
            this.activeCamera.aspect = originalAspect;
            this.activeCamera.updateProjectionMatrix();
            // Clean up
            renderTarget.dispose();
            return dataUrl;
        };
        animate();
    }
    static parseTBVector3(vectorString, defaultValue) {
        // Return default value if explicitly null, else return a default vector3
        if (!vectorString) return defaultValue === undefined ? new $hBQxr$three.Vector3() : defaultValue;
        const [x, y, z] = vectorString.split(',').map((p)=>parseFloat(p.trim()));
        return new $hBQxr$three.Vector3(x, y, z);
    }
    static parseTBColorString(colorString, defaultValue) {
        let r, g, b;
        if (colorString) {
            [r, g, b] = colorString.split(',').map(parseFloat);
            return new $hBQxr$three.Color(r, g, b);
        } else {
            // Check if it's already a THREE.Color
            if (defaultValue instanceof $hBQxr$three.Color) return defaultValue;
            else return new $hBQxr$three.Color(defaultValue.r, defaultValue.g, defaultValue.b, defaultValue.a);
        }
    }
    toggleFullscreen(controlButton) {
        if (this.icosa_frame?.requestFullscreen) this.icosa_frame?.requestFullscreen();
        document.onfullscreenchange = ()=>{
            if (document.fullscreenElement == null) {
                controlButton.onclick = ()=>{
                    if (this.icosa_frame?.requestFullscreen) this.icosa_frame?.requestFullscreen();
                };
                controlButton.classList.remove('fullscreen');
            } else {
                controlButton.onclick = ()=>{
                    if (document.exitFullscreen) document.exitFullscreen();
                };
                controlButton.classList.add('fullscreen');
            }
        };
    }
    initializeScene() {
        let defaultBackgroundColor = this.overrides?.["defaultBackgroundColor"];
        if (!defaultBackgroundColor) defaultBackgroundColor = "#000000";
        this.defaultBackgroundColor = new $hBQxr$three.Color(defaultBackgroundColor);
        if (!this.loadedModel) return;
        this.scene.clear();
        this.initSceneBackground();
        this.initFog();
        this.initLights();
        this.initCameras();
        // Compensate for insanely large models
        const LIMIT = 100000;
        let radius = this.overrides?.geometryData?.stats?.radius;
        if (radius > LIMIT) {
            let excess = radius - LIMIT;
            let sceneNode = this.scene.add(this.loadedModel);
            sceneNode.scale.divideScalar(excess);
            // Reframe the scaled scene
            this.frameNode(sceneNode);
        } else {
            if (this.isNewTiltExporter(this.sceneGltf)) this.scene.scale.set(0.1, 0.1, 0.1);
            this.scene.add(this.loadedModel);
        }
    }
    toggleTreeView(root) {
        if (root.childElementCount == 0) {
            this.createTreeView(this.scene, root);
            root.style.display = 'none';
        }
        if (root.style.display === 'block') root.style.display = 'none';
        else if (root.style.display === 'none') root.style.display = 'block';
    }
    static lookupEnvironment(guid) {
        return ({
            "e38af599-4575-46ff-a040-459703dbcd36": {
                name: "Passthrough",
                guid: "e38af599-4575-46ff-a040-459703dbcd36",
                renderSettings: {
                    fogEnabled: false,
                    fogColor: {
                        r: 0.0,
                        g: 0.0,
                        b: 0.0,
                        a: 0.0
                    },
                    fogDensity: 0.0,
                    fogStartDistance: 0.0,
                    fogEndDistance: 0.0,
                    clearColor: {
                        r: 1.0,
                        g: 1.0,
                        b: 1.0,
                        a: 1.0
                    },
                    ambientColor: {
                        r: 0.5,
                        g: 0.5,
                        b: 0.5,
                        a: 1.0
                    },
                    skyboxExposure: 0.0,
                    skyboxTint: {
                        r: 0.0,
                        g: 0.0,
                        b: 0.0,
                        a: 0.0
                    },
                    environmentPrefab: "EnvironmentPrefabs/Passthrough",
                    environmentReverbZone: "EnvironmentAudio/ReverbZone_Room",
                    skyboxCubemap: null,
                    reflectionCubemap: "threelight_reflection",
                    reflectionIntensity: 0.3
                },
                lights: [
                    {
                        color: {
                            r: 1.16949809,
                            g: 1.19485855,
                            b: 1.31320751,
                            a: 1.0
                        },
                        position: {
                            x: 0.0,
                            y: 0.0,
                            z: 0.0
                        },
                        rotation: {
                            x: 60.0000038,
                            y: 0.0,
                            z: 25.9999962
                        },
                        type: "Directional",
                        range: 5.0,
                        spotAngle: 30.0,
                        shadowsEnabled: true
                    },
                    {
                        color: {
                            r: 0.428235322,
                            g: 0.4211765,
                            b: 0.3458824,
                            a: 1.0
                        },
                        position: {
                            x: 0.0,
                            y: 0.0,
                            z: 0.0
                        },
                        rotation: {
                            x: 40.0000038,
                            y: 180.0,
                            z: 220.0
                        },
                        type: "Directional",
                        range: 5.0,
                        spotAngle: 30.0,
                        shadowsEnabled: false
                    }
                ],
                teleportBoundsHalfWidth: 80.0,
                controllerXRayHeight: 0.0,
                widgetHome: {
                    x: 0.0,
                    y: 0.0,
                    z: 0.0
                },
                skyboxColorA: {
                    r: 0.0,
                    g: 0.0,
                    b: 0.0,
                    a: 0.0
                },
                skyboxColorB: {
                    r: 0.0,
                    g: 0.0,
                    b: 0.0,
                    a: 0.0
                }
            },
            "ab080599-e465-4a6d-8587-43bf495af68b": {
                name: "Standard",
                guid: "ab080599-e465-4a6d-8587-43bf495af68b",
                renderSettings: {
                    fogEnabled: true,
                    fogColor: {
                        r: 0.164705887,
                        g: 0.164705887,
                        b: 0.20784314,
                        a: 1.0
                    },
                    fogDensity: 0.0025,
                    fogStartDistance: 0.0,
                    fogEndDistance: 0.0,
                    clearColor: {
                        r: 0.156862751,
                        g: 0.156862751,
                        b: 0.203921571,
                        a: 1.0
                    },
                    ambientColor: {
                        r: 0.392156869,
                        g: 0.392156869,
                        b: 0.392156869,
                        a: 1.0
                    },
                    skyboxExposure: 0.9,
                    skyboxTint: {
                        r: 0.235294119,
                        g: 0.2509804,
                        b: 0.3529412,
                        a: 1.0
                    },
                    environmentPrefab: "EnvironmentPrefabs/Standard",
                    environmentReverbZone: "EnvironmentAudio/ReverbZone_CarpetedHallway",
                    skyboxCubemap: null,
                    reflectionCubemap: "threelight_reflection",
                    reflectionIntensity: 0.3
                },
                lights: [
                    {
                        color: {
                            r: 0.7780392,
                            g: 0.815686345,
                            b: 0.9913726,
                            a: 1.0
                        },
                        position: {
                            x: 0.0,
                            y: 0.0,
                            z: 0.0
                        },
                        rotation: {
                            x: 60.0000038,
                            y: 0.0,
                            z: 25.9999962
                        },
                        type: "Directional",
                        range: 5.0,
                        spotAngle: 30.0,
                        shadowsEnabled: true
                    },
                    {
                        color: {
                            r: 0.428235322,
                            g: 0.4211765,
                            b: 0.3458824,
                            a: 1.0
                        },
                        position: {
                            x: 0.0,
                            y: 0.0,
                            z: 0.0
                        },
                        rotation: {
                            x: 40.0000038,
                            y: 180.0,
                            z: 220.0
                        },
                        type: "Directional",
                        range: 5.0,
                        spotAngle: 30.0,
                        shadowsEnabled: false
                    }
                ],
                teleportBoundsHalfWidth: 80.0,
                controllerXRayHeight: 0.0,
                widgetHome: {
                    x: 0.0,
                    y: 0.0,
                    z: 0.0
                },
                skyboxColorA: {
                    r: 0.274509817,
                    g: 0.274509817,
                    b: 0.31764707,
                    a: 1.0
                },
                skyboxColorB: {
                    r: 0.03529412,
                    g: 0.03529412,
                    b: 0.08627451,
                    a: 1.0
                }
            },
            "c504347a-c96d-4505-853b-87b484acff9a": {
                name: "NightSky",
                guid: "c504347a-c96d-4505-853b-87b484acff9a",
                renderSettings: {
                    fogEnabled: true,
                    fogColor: {
                        r: 0.0196078438,
                        g: 0.0117647061,
                        b: 0.0431372561,
                        a: 1.0
                    },
                    fogDensity: 0.006,
                    fogStartDistance: 0.0,
                    fogEndDistance: 0.0,
                    clearColor: {
                        r: 0.0,
                        g: 0.0,
                        b: 0.0,
                        a: 1.0
                    },
                    ambientColor: {
                        r: 0.3019608,
                        g: 0.3019608,
                        b: 0.6039216,
                        a: 1.0
                    },
                    skyboxExposure: 1.0,
                    skyboxTint: {
                        r: 1.0,
                        g: 1.0,
                        b: 1.0,
                        a: 1.0
                    },
                    environmentPrefab: "EnvironmentPrefabs/NightSky",
                    environmentReverbZone: "EnvironmentAudio/ReverbZone_Mountains",
                    skyboxCubemap: "nightsky",
                    reflectionCubemap: "milkyway_reflection",
                    reflectionIntensity: 3.0
                },
                lights: [
                    {
                        color: {
                            r: 1.02352941,
                            g: 0.7647059,
                            b: 0.929411769,
                            a: 1.0
                        },
                        position: {
                            x: 0.0,
                            y: 0.0,
                            z: 8.0
                        },
                        rotation: {
                            x: 65.0,
                            y: 0.0,
                            z: 25.9999981
                        },
                        type: "Directional",
                        range: 5.0,
                        spotAngle: 30.0,
                        shadowsEnabled: true
                    },
                    {
                        color: {
                            r: 0.0,
                            g: 0.0,
                            b: 0.0,
                            a: 1.0
                        },
                        position: {
                            x: 0.0,
                            y: 0.0,
                            z: 8.0
                        },
                        rotation: {
                            x: 0.0,
                            y: 0.0,
                            z: 0.0
                        },
                        type: "Directional",
                        range: 5.0,
                        spotAngle: 30.0,
                        shadowsEnabled: false
                    }
                ],
                teleportBoundsHalfWidth: 80.0,
                controllerXRayHeight: 0.0,
                widgetHome: {
                    x: 0.0,
                    y: 0.0,
                    z: 0.0
                },
                skyboxColorA: {
                    r: 0.08235294,
                    g: 0.0470588244,
                    b: 0.184313729,
                    a: 1.0
                },
                skyboxColorB: {
                    r: 0.0,
                    g: 0.0,
                    b: 0.0,
                    a: 1.0
                }
            },
            "96cf6f36-47b6-44f4-bdbf-63be2ddac909": {
                name: "Space",
                guid: "96cf6f36-47b6-44f4-bdbf-63be2ddac909",
                renderSettings: {
                    fogEnabled: true,
                    fogColor: {
                        r: 0.0,
                        g: 0.0,
                        b: 0.0,
                        a: 1.0
                    },
                    fogDensity: 0.0,
                    fogStartDistance: 5.0,
                    fogEndDistance: 20.0,
                    clearColor: {
                        r: 0.0,
                        g: 0.0,
                        b: 0.0,
                        a: 1.0
                    },
                    ambientColor: {
                        r: 0.227450982,
                        g: 0.20784314,
                        b: 0.360784322,
                        a: 1.0
                    },
                    skyboxExposure: 1.0,
                    skyboxTint: {
                        r: 1.0,
                        g: 1.0,
                        b: 1.0,
                        a: 1.0
                    },
                    environmentPrefab: "EnvironmentPrefabs/Space",
                    environmentReverbZone: "EnvironmentAudio/ReverbZone_Arena",
                    skyboxCubemap: "milkyway_PNG",
                    reflectionCubemap: "milkyway_reflection",
                    reflectionIntensity: 3.0
                },
                lights: [
                    {
                        color: {
                            r: 1.16000009,
                            g: 0.866666734,
                            b: 0.866666734,
                            a: 1.0
                        },
                        position: {
                            x: 0.0,
                            y: 0.0,
                            z: 8.0
                        },
                        rotation: {
                            x: 30.0000019,
                            y: 39.9999962,
                            z: 50.0
                        },
                        type: "Directional",
                        range: 5.0,
                        spotAngle: 30.0,
                        shadowsEnabled: true
                    },
                    {
                        color: {
                            r: 0.0,
                            g: 0.0,
                            b: 0.0,
                            a: 1.0
                        },
                        position: {
                            x: 0.0,
                            y: 0.0,
                            z: 8.0
                        },
                        rotation: {
                            x: 0.0,
                            y: 0.0,
                            z: 0.0
                        },
                        type: "Directional",
                        range: 5.0,
                        spotAngle: 30.0,
                        shadowsEnabled: false
                    }
                ],
                teleportBoundsHalfWidth: 20.0,
                controllerXRayHeight: -1000000000,
                widgetHome: {
                    x: 0.0,
                    y: 0.0,
                    z: 0.0
                },
                skyboxColorA: {
                    r: 0.0,
                    g: 0.0,
                    b: 0.0,
                    a: 1.0
                },
                skyboxColorB: {
                    r: 0.121568628,
                    g: 0.03529412,
                    b: 0.172549024,
                    a: 1.0
                }
            },
            "e2e72b76-d443-4721-97e6-f3d49fe98dda": {
                name: "DressForm",
                guid: "e2e72b76-d443-4721-97e6-f3d49fe98dda",
                renderSettings: {
                    fogEnabled: true,
                    fogColor: {
                        r: 0.172549024,
                        g: 0.180392161,
                        b: 0.243137255,
                        a: 1.0
                    },
                    fogDensity: 0.007,
                    fogStartDistance: 0.0,
                    fogEndDistance: 0.0,
                    clearColor: {
                        r: 0.219607845,
                        g: 0.227450982,
                        b: 0.31764707,
                        a: 1.0
                    },
                    ambientColor: {
                        r: 0.4117647,
                        g: 0.3529412,
                        b: 0.596078455,
                        a: 1.0
                    },
                    skyboxExposure: 0.61,
                    skyboxTint: {
                        r: 0.458823532,
                        g: 0.5137255,
                        b: 0.7882353,
                        a: 1.0
                    },
                    environmentPrefab: "EnvironmentPrefabs/DressForm",
                    environmentReverbZone: "EnvironmentAudio/ReverbZone_LivingRoom",
                    skyboxCubemap: null,
                    reflectionCubemap: "threelight_reflection",
                    reflectionIntensity: 0.3
                },
                lights: [
                    {
                        color: {
                            r: 1.1152941,
                            g: 0.917647064,
                            b: 0.7764706,
                            a: 1.0
                        },
                        position: {
                            x: 0.0,
                            y: 0.0,
                            z: 7.0
                        },
                        rotation: {
                            x: 50.0,
                            y: 41.9999962,
                            z: 25.9999924
                        },
                        type: "Directional",
                        range: 5.0,
                        spotAngle: 30.0,
                        shadowsEnabled: true
                    },
                    {
                        color: {
                            r: 0.356862754,
                            g: 0.3509804,
                            b: 0.2882353,
                            a: 1.0
                        },
                        position: {
                            x: 0.0,
                            y: 0.0,
                            z: 8.0
                        },
                        rotation: {
                            x: 40.0000038,
                            y: 227.0,
                            z: 220.0
                        },
                        type: "Directional",
                        range: 5.0,
                        spotAngle: 30.0,
                        shadowsEnabled: false
                    }
                ],
                teleportBoundsHalfWidth: 80.0,
                controllerXRayHeight: 0.0,
                widgetHome: {
                    x: 0.0,
                    y: 0.0,
                    z: 0.0
                },
                skyboxColorA: {
                    r: 0.34117648,
                    g: 0.345098048,
                    b: 0.4509804,
                    a: 1.0
                },
                skyboxColorB: {
                    r: 0.09411765,
                    g: 0.105882354,
                    b: 0.1764706,
                    a: 1.0
                }
            },
            "ab080511-e465-4a6d-8587-53bf495af68b": {
                name: "Pedestal",
                guid: "ab080511-e465-4a6d-8587-53bf495af68b",
                renderSettings: {
                    fogEnabled: true,
                    fogColor: {
                        r: 0.172549024,
                        g: 0.180392161,
                        b: 0.243137255,
                        a: 1.0
                    },
                    fogDensity: 0.007,
                    fogStartDistance: 0.0,
                    fogEndDistance: 0.0,
                    clearColor: {
                        r: 0.219607845,
                        g: 0.227450982,
                        b: 0.31764707,
                        a: 1.0
                    },
                    ambientColor: {
                        r: 0.4117647,
                        g: 0.3529412,
                        b: 0.596078455,
                        a: 1.0
                    },
                    skyboxExposure: 0.61,
                    skyboxTint: {
                        r: 0.458823532,
                        g: 0.5137255,
                        b: 0.7882353,
                        a: 1.0
                    },
                    environmentPrefab: "EnvironmentPrefabs/Pedestal",
                    environmentReverbZone: "EnvironmentAudio/ReverbZone_LivingRoom",
                    skyboxCubemap: null,
                    reflectionCubemap: "threelight_reflection",
                    reflectionIntensity: 1.0
                },
                lights: [
                    {
                        color: {
                            r: 1.1152941,
                            g: 0.917647064,
                            b: 0.7764706,
                            a: 1.0
                        },
                        position: {
                            x: 0.0,
                            y: 0.0,
                            z: 7.0
                        },
                        rotation: {
                            x: 50.0,
                            y: 41.9999962,
                            z: 25.9999924
                        },
                        type: "Directional",
                        range: 5.0,
                        spotAngle: 30.0,
                        shadowsEnabled: true
                    },
                    {
                        color: {
                            r: 0.356862754,
                            g: 0.3509804,
                            b: 0.2882353,
                            a: 1.0
                        },
                        position: {
                            x: 0.0,
                            y: 0.0,
                            z: 8.0
                        },
                        rotation: {
                            x: 40.0000038,
                            y: 227.0,
                            z: 220.0
                        },
                        type: "Directional",
                        range: 5.0,
                        spotAngle: 30.0,
                        shadowsEnabled: false
                    }
                ],
                teleportBoundsHalfWidth: 80.0,
                controllerXRayHeight: 0.0,
                widgetHome: {
                    x: 0.0,
                    y: 9.675,
                    z: 5.0
                },
                skyboxColorA: {
                    r: 0.34117648,
                    g: 0.345098048,
                    b: 0.4509804,
                    a: 1.0
                },
                skyboxColorB: {
                    r: 0.09411765,
                    g: 0.105882354,
                    b: 0.1764706,
                    a: 1.0
                }
            },
            "ab080511-e565-4a6d-8587-53bf495af68b": {
                name: "Snowman",
                guid: "ab080511-e565-4a6d-8587-53bf495af68b",
                renderSettings: {
                    fogEnabled: true,
                    fogColor: {
                        r: 0.6509804,
                        g: 0.7254902,
                        b: 0.8745098,
                        a: 1.0
                    },
                    fogDensity: 0.005,
                    fogStartDistance: 0.0,
                    fogEndDistance: 0.0,
                    clearColor: {
                        r: 0.6509804,
                        g: 0.7019608,
                        b: 0.870588243,
                        a: 1.0
                    },
                    ambientColor: {
                        r: 0.7294118,
                        g: 0.7294118,
                        b: 0.7294118,
                        a: 1.0
                    },
                    skyboxExposure: 0.95,
                    skyboxTint: {
                        r: 0.75686276,
                        g: 0.819607854,
                        b: 1.0,
                        a: 1.0
                    },
                    environmentPrefab: "EnvironmentPrefabs/Snowman",
                    environmentReverbZone: "EnvironmentAudio/ReverbZone_PaddedCell",
                    skyboxCubemap: "snowysky",
                    reflectionCubemap: "threelight_reflection",
                    reflectionIntensity: 0.3
                },
                lights: [
                    {
                        color: {
                            r: 0.241451,
                            g: 0.234078437,
                            b: 0.3465098,
                            a: 1.0
                        },
                        position: {
                            x: 0.0,
                            y: 0.0,
                            z: 8.0
                        },
                        rotation: {
                            x: 58.0,
                            y: 315.999969,
                            z: 50.0000038
                        },
                        type: "Directional",
                        range: 5.0,
                        spotAngle: 30.0,
                        shadowsEnabled: true
                    },
                    {
                        color: {
                            r: 0.410980433,
                            g: 0.4956863,
                            b: 0.65882355,
                            a: 1.0
                        },
                        position: {
                            x: 0.0,
                            y: 0.0,
                            z: 8.0
                        },
                        rotation: {
                            x: 40.0,
                            y: 143.0,
                            z: 220.0
                        },
                        type: "Directional",
                        range: 5.0,
                        spotAngle: 30.0,
                        shadowsEnabled: false
                    }
                ],
                teleportBoundsHalfWidth: 80.0,
                controllerXRayHeight: 0.0,
                widgetHome: {
                    x: 0.0,
                    y: 0.0,
                    z: 0.0
                },
                skyboxColorA: {
                    r: 0.4627451,
                    g: 0.5647059,
                    b: 0.7058824,
                    a: 1.0
                },
                skyboxColorB: {
                    r: 0.7607843,
                    g: 0.8156863,
                    b: 0.972549,
                    a: 1.0
                }
            },
            "36e65e4f-17d7-41ef-834a-e525db0b9888": {
                name: "PinkLemonade",
                guid: "36e65e4f-17d7-41ef-834a-e525db0b9888",
                renderSettings: {
                    fogEnabled: true,
                    fogColor: {
                        r: 1.0,
                        g: 0.5514706,
                        b: 0.9319472,
                        a: 1.0
                    },
                    fogDensity: 0.025,
                    fogStartDistance: 0.0,
                    fogEndDistance: 0.0,
                    clearColor: {
                        r: 0.827451,
                        g: 0.368627459,
                        b: 0.34117648,
                        a: 1.0
                    },
                    ambientColor: {
                        r: 1.0,
                        g: 0.9019608,
                        b: 0.854901969,
                        a: 1.0
                    },
                    skyboxExposure: 1.0,
                    skyboxTint: {
                        r: 0.827451,
                        g: 0.368627459,
                        b: 0.34117648,
                        a: 1.0
                    },
                    environmentPrefab: "EnvironmentPrefabs/AmbientDustDim",
                    environmentReverbZone: "EnvironmentAudio/ReverbZone_Room",
                    skyboxCubemap: null,
                    reflectionCubemap: "gradientblue",
                    reflectionIntensity: 0.0
                },
                lights: [
                    {
                        color: {
                            r: 0.0,
                            g: 0.0,
                            b: 0.0,
                            a: 1.0
                        },
                        position: {
                            x: 0.0,
                            y: 0.0,
                            z: 0.0
                        },
                        rotation: {
                            x: 318.189667,
                            y: 116.565048,
                            z: 116.565048
                        },
                        type: "Directional",
                        range: 0.0,
                        spotAngle: 0.0,
                        shadowsEnabled: true
                    },
                    {
                        color: {
                            r: 0.5,
                            g: 0.28039217,
                            b: 0.3156863,
                            a: 1.0
                        },
                        position: {
                            x: 0.0,
                            y: 0.0,
                            z: 0.0
                        },
                        rotation: {
                            x: 0.0,
                            y: 0.0,
                            z: 0.0
                        },
                        type: "Directional",
                        range: 0.0,
                        spotAngle: 0.0,
                        shadowsEnabled: false
                    }
                ],
                teleportBoundsHalfWidth: 80.0,
                controllerXRayHeight: -1000000000,
                widgetHome: {
                    x: 0.0,
                    y: 0.0,
                    z: 0.0
                },
                skyboxColorA: {
                    r: 1.0,
                    g: 0.882352948,
                    b: 0.65882355,
                    a: 1.0
                },
                skyboxColorB: {
                    r: 0.858823538,
                    g: 0.294117659,
                    b: 0.3647059,
                    a: 1.0
                }
            },
            "a9bc2bc8-6d86-4cda-82a9-283e0f3977ac": {
                name: "Pistachio",
                guid: "a9bc2bc8-6d86-4cda-82a9-283e0f3977ac",
                renderSettings: {
                    fogEnabled: true,
                    fogColor: {
                        r: 0.2784314,
                        g: 0.5686275,
                        b: 0.458823532,
                        a: 1.0
                    },
                    fogDensity: 0.015,
                    fogStartDistance: 0.0,
                    fogEndDistance: 0.0,
                    clearColor: {
                        r: 0.9558824,
                        g: 0.6708847,
                        b: 0.513083935,
                        a: 1.0
                    },
                    ambientColor: {
                        r: 0.610186,
                        g: 0.838235259,
                        b: 0.75194633,
                        a: 1.0
                    },
                    skyboxExposure: 1.0,
                    skyboxTint: {
                        r: 0.797,
                        g: 0.616,
                        b: 0.755,
                        a: 1.0
                    },
                    environmentPrefab: "EnvironmentPrefabs/AmbientDustDim",
                    environmentReverbZone: "EnvironmentAudio/ReverbZone_Room",
                    skyboxCubemap: null,
                    reflectionCubemap: "gradientblue",
                    reflectionIntensity: 0.3
                },
                lights: [
                    {
                        color: {
                            r: 0.209818333,
                            g: 0.242647052,
                            b: 0.171280265,
                            a: 1.0
                        },
                        position: {
                            x: 0.0,
                            y: 0.0,
                            z: 0.0
                        },
                        rotation: {
                            x: 41.810318,
                            y: 116.565048,
                            z: 243.434937
                        },
                        type: "Directional",
                        range: 0.0,
                        spotAngle: 0.0,
                        shadowsEnabled: true
                    },
                    {
                        color: {
                            r: 0.977941155,
                            g: 0.506417,
                            b: 0.438635379,
                            a: 1.0
                        },
                        position: {
                            x: 0.0,
                            y: 0.0,
                            z: 0.0
                        },
                        rotation: {
                            x: 0.0,
                            y: 0.0,
                            z: 0.0
                        },
                        type: "Directional",
                        range: 0.0,
                        spotAngle: 0.0,
                        shadowsEnabled: false
                    }
                ],
                teleportBoundsHalfWidth: 50.0,
                controllerXRayHeight: -1000000000,
                widgetHome: {
                    x: 0.0,
                    y: 0.0,
                    z: 0.0
                },
                skyboxColorA: {
                    r: 1.0,
                    g: 0.5176471,
                    b: 0.3529412,
                    a: 1.0
                },
                skyboxColorB: {
                    r: 0.458823532,
                    g: 0.78039217,
                    b: 0.5529412,
                    a: 1.0
                }
            },
            "e65cde1a-a177-4bfb-b93f-f673c99a32bc": {
                name: "Illustrative",
                guid: "e65cde1a-a177-4bfb-b93f-f673c99a32bc",
                renderSettings: {
                    fogEnabled: true,
                    fogColor: {
                        r: 1.0,
                        g: 1.0,
                        b: 1.0,
                        a: 1.0
                    },
                    fogDensity: 0.0125,
                    fogStartDistance: 0.0,
                    fogEndDistance: 0.0,
                    clearColor: {
                        r: 0.7019608,
                        g: 0.7019608,
                        b: 0.7019608,
                        a: 1.0
                    },
                    ambientColor: {
                        r: 1.0,
                        g: 1.0,
                        b: 1.0,
                        a: 1.0
                    },
                    skyboxExposure: 0.0,
                    skyboxTint: {
                        r: 0.625,
                        g: 0.625,
                        b: 0.625,
                        a: 1.0
                    },
                    environmentPrefab: "EnvironmentPrefabs/AmbientDustDim",
                    environmentReverbZone: "EnvironmentAudio/ReverbZone_Room",
                    skyboxCubemap: null,
                    reflectionCubemap: null,
                    reflectionIntensity: 0.0
                },
                lights: [
                    {
                        color: {
                            r: 0.0,
                            g: 0.0,
                            b: 0.0,
                            a: 1.0
                        },
                        position: {
                            x: 0.0,
                            y: 0.0,
                            z: 0.0
                        },
                        rotation: {
                            x: 0.0,
                            y: 180.0,
                            z: 180.0
                        },
                        type: "Directional",
                        range: 0.0,
                        spotAngle: 0.0,
                        shadowsEnabled: true
                    },
                    {
                        color: {
                            r: 0.0,
                            g: 0.0,
                            b: 0.0,
                            a: 1.0
                        },
                        position: {
                            x: 0.0,
                            y: 0.0,
                            z: 0.0
                        },
                        rotation: {
                            x: 0.0,
                            y: 0.0,
                            z: 0.0
                        },
                        type: "Directional",
                        range: 0.0,
                        spotAngle: 0.0,
                        shadowsEnabled: false
                    }
                ],
                teleportBoundsHalfWidth: 100.0,
                controllerXRayHeight: -1000000000,
                widgetHome: {
                    x: 0.0,
                    y: 0.0,
                    z: 0.0
                },
                skyboxColorA: {
                    r: 0.7647059,
                    g: 0.7647059,
                    b: 0.7647059,
                    a: 1.0
                },
                skyboxColorB: {
                    r: 0.623529434,
                    g: 0.623529434,
                    b: 0.623529434,
                    a: 1.0
                }
            },
            "580b4529-ac50-4fe9-b8d2-635765a14893": {
                name: "Black",
                guid: "580b4529-ac50-4fe9-b8d2-635765a14893",
                renderSettings: {
                    fogEnabled: true,
                    fogColor: {
                        r: 0.0196078438,
                        g: 0.0196078438,
                        b: 0.0196078438,
                        a: 1.0
                    },
                    fogDensity: 0.0,
                    fogStartDistance: 0.0,
                    fogEndDistance: 0.0,
                    clearColor: {
                        r: 0.0,
                        g: 0.0,
                        b: 0.0,
                        a: 1.0
                    },
                    ambientColor: {
                        r: 0.392156869,
                        g: 0.392156869,
                        b: 0.392156869,
                        a: 1.0
                    },
                    skyboxExposure: 1.0,
                    skyboxTint: {
                        r: 0.0,
                        g: 0.0,
                        b: 0.0,
                        a: 1.0
                    },
                    environmentPrefab: "EnvironmentPrefabs/AmbientDust",
                    environmentReverbZone: "EnvironmentAudio/ReverbZone_Room",
                    skyboxCubemap: null,
                    reflectionCubemap: "threelight_reflection",
                    reflectionIntensity: 0.3
                },
                lights: [
                    {
                        color: {
                            r: 0.7780392,
                            g: 0.815686345,
                            b: 0.9913726,
                            a: 1.0
                        },
                        position: {
                            x: 0.0,
                            y: 0.0,
                            z: 0.0
                        },
                        rotation: {
                            x: 60.0000038,
                            y: 0.0,
                            z: 25.9999962
                        },
                        type: "Directional",
                        range: 5.0,
                        spotAngle: 30.0,
                        shadowsEnabled: true
                    },
                    {
                        color: {
                            r: 0.428235322,
                            g: 0.4211765,
                            b: 0.3458824,
                            a: 1.0
                        },
                        position: {
                            x: 0.0,
                            y: 0.0,
                            z: 0.0
                        },
                        rotation: {
                            x: 40.0000038,
                            y: 180.0,
                            z: 220.0
                        },
                        type: "Directional",
                        range: 5.0,
                        spotAngle: 30.0,
                        shadowsEnabled: false
                    }
                ],
                teleportBoundsHalfWidth: 100.0,
                controllerXRayHeight: -1000000000,
                widgetHome: {
                    x: 0.0,
                    y: 0.0,
                    z: 0.0
                },
                skyboxColorA: {
                    r: 0.0,
                    g: 0.0,
                    b: 0.0,
                    a: 1.0
                },
                skyboxColorB: {
                    r: 0.0,
                    g: 0.0,
                    b: 0.0,
                    a: 1.0
                }
            },
            "9b89b0a4-c41e-4b78-82a1-22f10a238357": {
                name: "White",
                guid: "9b89b0a4-c41e-4b78-82a1-22f10a238357",
                renderSettings: {
                    fogEnabled: true,
                    fogColor: {
                        r: 1.0,
                        g: 1.0,
                        b: 1.0,
                        a: 1.0
                    },
                    fogDensity: 0.0,
                    fogStartDistance: 0.0,
                    fogEndDistance: 0.0,
                    clearColor: {
                        r: 0.784313738,
                        g: 0.784313738,
                        b: 0.784313738,
                        a: 1.0
                    },
                    ambientColor: {
                        r: 0.392156869,
                        g: 0.392156869,
                        b: 0.392156869,
                        a: 1.0
                    },
                    skyboxExposure: 1.0,
                    skyboxTint: {
                        r: 0.0,
                        g: 0.0,
                        b: 0.0,
                        a: 1.0
                    },
                    environmentPrefab: "EnvironmentPrefabs/AmbientGrid",
                    environmentReverbZone: "EnvironmentAudio/ReverbZone_Room",
                    skyboxCubemap: null,
                    reflectionCubemap: "threelight_reflection",
                    reflectionIntensity: 0.4
                },
                lights: [
                    {
                        color: {
                            r: 0.7780392,
                            g: 0.815686345,
                            b: 0.9913726,
                            a: 1.0
                        },
                        position: {
                            x: 0.0,
                            y: 0.0,
                            z: 0.0
                        },
                        rotation: {
                            x: 60.0000038,
                            y: 0.0,
                            z: 25.9999962
                        },
                        type: "Directional",
                        range: 5.0,
                        spotAngle: 30.0,
                        shadowsEnabled: true
                    },
                    {
                        color: {
                            r: 0.428235322,
                            g: 0.4211765,
                            b: 0.3458824,
                            a: 1.0
                        },
                        position: {
                            x: 0.0,
                            y: 0.0,
                            z: 0.0
                        },
                        rotation: {
                            x: 40.0000038,
                            y: 180.0,
                            z: 220.0
                        },
                        type: "Directional",
                        range: 5.0,
                        spotAngle: 30.0,
                        shadowsEnabled: false
                    }
                ],
                teleportBoundsHalfWidth: 100.0,
                controllerXRayHeight: -1000000000,
                widgetHome: {
                    x: 0.0,
                    y: 0.0,
                    z: 0.0
                },
                skyboxColorA: {
                    r: 0.784313738,
                    g: 0.784313738,
                    b: 0.784313738,
                    a: 1.0
                },
                skyboxColorB: {
                    r: 0.784313738,
                    g: 0.784313738,
                    b: 0.784313738,
                    a: 1.0
                }
            },
            "0ca88298-e5e8-4e94-aad8-4b4f6c80ae52": {
                name: "Blue",
                guid: "0ca88298-e5e8-4e94-aad8-4b4f6c80ae52",
                renderSettings: {
                    fogEnabled: true,
                    fogColor: {
                        r: 0.6313726,
                        g: 0.7137255,
                        b: 0.894117653,
                        a: 1.0
                    },
                    fogDensity: 0.005,
                    fogStartDistance: 0.0,
                    fogEndDistance: 0.0,
                    clearColor: {
                        r: 0.270588249,
                        g: 0.309803933,
                        b: 0.470588237,
                        a: 1.0
                    },
                    ambientColor: {
                        r: 0.203921571,
                        g: 0.294117659,
                        b: 0.368627459,
                        a: 1.0
                    },
                    skyboxExposure: 1.46,
                    skyboxTint: {
                        r: 0.4627451,
                        g: 0.5294118,
                        b: 0.698039234,
                        a: 1.0
                    },
                    environmentPrefab: "EnvironmentPrefabs/AmbientGrid_Blue",
                    environmentReverbZone: "EnvironmentAudio/ReverbZone_Room",
                    skyboxCubemap: "gradientblue",
                    reflectionCubemap: "threelight_reflection",
                    reflectionIntensity: 0.8
                },
                lights: [
                    {
                        color: {
                            r: 1.5533334,
                            g: 1.40666676,
                            b: 1.77333343,
                            a: 1.0
                        },
                        position: {
                            x: 0.0,
                            y: 0.0,
                            z: 0.0
                        },
                        rotation: {
                            x: 60.0000038,
                            y: 0.0,
                            z: 25.9999962
                        },
                        type: "Directional",
                        range: 5.0,
                        spotAngle: 30.0,
                        shadowsEnabled: true
                    },
                    {
                        color: {
                            r: 0.271215677,
                            g: 0.2667451,
                            b: 0.219058827,
                            a: 1.0
                        },
                        position: {
                            x: 0.0,
                            y: 0.0,
                            z: 0.0
                        },
                        rotation: {
                            x: 40.0000038,
                            y: 180.0,
                            z: 220.0
                        },
                        type: "Directional",
                        range: 5.0,
                        spotAngle: 30.0,
                        shadowsEnabled: false
                    }
                ],
                teleportBoundsHalfWidth: 100.0,
                controllerXRayHeight: -1000000000,
                widgetHome: {
                    x: 0.0,
                    y: 0.0,
                    z: 0.0
                },
                skyboxColorA: {
                    r: 0.180392161,
                    g: 0.235294119,
                    b: 0.4117647,
                    a: 1.0
                },
                skyboxColorB: {
                    r: 0.356862754,
                    g: 0.392156869,
                    b: 0.6,
                    a: 1.0
                }
            }
        })[guid];
    }
    async loadGltf1(url, loadEnvironment, overrides) {
        try {
            await this._loadGltf(url, loadEnvironment, overrides, true);
        } catch (error) {
            this.showErrorIcon();
            console.error("Error loading glTFv1 model");
            this.loadingError = true;
        }
    }
    async loadGltf(url, loadEnvironment, overrides) {
        try {
            await this._loadGltf(url, loadEnvironment, overrides, false);
        } catch (error) {
            this.showErrorIcon();
            console.error("Error loading glTFv2 model");
            this.loadingError = true;
        }
    }
    async replaceGltf1Materials(model, brushPath) {
        // Create a minimal mock parser object with the required options.manager
        const mockParser = {
            options: {
                manager: $hBQxr$three.DefaultLoadingManager
            }
        };
        const extension = new (0, $hBQxr$GLTFGoogleTiltBrushMaterialExtension)(mockParser, brushPath, true);
        // Collect all meshes first, then process them with async/await
        const meshes = [];
        model.traverse((object)=>{
            if (object.type === "Mesh") meshes.push(object);
        });
        // Process all meshes asynchronously
        for (const mesh of meshes){
            // Use material name directly - strip "material_" prefix if present
            const materialName = mesh.material?.name;
            if (materialName) {
                // Strip "material_" prefix if present
                let brushId = materialName.startsWith('material_') ? materialName.substring(9) : materialName;
                // At this point we either have just a guid or brushName-guid
                // If we have brushName-guid, extract just the guid
                const guidMatch = brushId.match(/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}/);
                if (guidMatch) brushId = guidMatch[0];
                try {
                    await extension.replaceMaterial(mesh, brushId);
                } catch (error) {
                    console.warn(`Failed to replace material for ${brushId} on mesh ${mesh.name}:`, error);
                // Keep original material as fallback
                }
            }
        }
    }
    async _loadGltf(url, loadEnvironment, overrides, isV1) {
        let sceneGltf;
        this.overrides = overrides;
        this.isV1 = isV1;
        if (this.isV1) {
            sceneGltf = await this.gltfLegacyLoader.loadAsync(url);
            await this.replaceGltf1Materials(sceneGltf.scene, this.brushPath.toString());
        } else sceneGltf = await this.gltfLoader.loadAsync(url);
        // The legacy loader has the latter structure
        let userData = (Object.keys(sceneGltf.userData || {}).length > 0 ? sceneGltf.userData : null) ?? sceneGltf.scene.userData;
        if (!this.isNewTiltExporter(sceneGltf)) this.scaleScene(sceneGltf, userData, true);
        this.setupSketchMetaDataFromScene(sceneGltf.scene, userData);
        if (loadEnvironment) await this.assignEnvironment(sceneGltf);
        if (overrides?.tiltUrl) this.tiltData = await this.tiltLoader.loadAsync(tiltUrl);
        this.loadedModel = sceneGltf.scene;
        this.sceneGltf = sceneGltf;
        this.initializeScene();
    }
    isLegacyTiltExporter(sceneGltf) {
        const generator = sceneGltf.asset?.generator;
        return generator && !generator.includes('Tilt Brush');
    }
    isNewTiltExporter(sceneGltf) {
        return sceneGltf?.scene?.userData?.isNewTiltExporter ?? false;
    }
    isAnyTiltExporter(sceneGltf) {
        const generator = sceneGltf?.asset?.generator;
        return generator && (generator.includes('Tilt Brush') || generator.includes('Open Brush UnityGLTF Exporter'));
    }
    scaleScene(sceneGltf, negate) {
        const userData = sceneGltf.scene?.userData || sceneGltf.userData || {};
        let poseTranslation = $677737c8a5cbea2f$export$2ec4afd9b3c16a85.parseTBVector3(userData['TB_PoseTranslation'], new $hBQxr$three.Vector3(0, 0, 0));
        let poseRotation = $677737c8a5cbea2f$export$2ec4afd9b3c16a85.parseTBVector3(userData['TB_PoseRotation'], new $hBQxr$three.Vector3(0, 0, 0));
        let poseScale = userData['TB_PoseScale'] ?? 1;
        // Correct the scale for new exporter (handled automatically for the legacy exporter)
        if (this.isNewTiltExporter(sceneGltf)) poseScale *= negate ? 10 : 0.1;
        if (negate) {
            // Create inverse transformation matrix: (T * R * S)^-1 = S^-1 * R^-1 * T^-1
            const inverseScale = 1.0 / poseScale;
            const inverseRotation = new $hBQxr$three.Euler($hBQxr$three.MathUtils.degToRad(-poseRotation.x), $hBQxr$three.MathUtils.degToRad(-poseRotation.y), $hBQxr$three.MathUtils.degToRad(-poseRotation.z), 'ZYX' // Reverse order for inverse
            );
            const inverseTranslation = poseTranslation.clone().negate();
            // Apply inverse transforms in reverse order: S^-1 * R^-1 * T^-1
            sceneGltf.scene.scale.multiplyScalar(inverseScale);
            sceneGltf.scene.setRotationFromEuler(inverseRotation);
            // Transform the translation by the inverse rotation and scale
            const rotMatrix = new $hBQxr$three.Matrix4().makeRotationFromEuler(inverseRotation);
            inverseTranslation.applyMatrix4(rotMatrix);
            inverseTranslation.multiplyScalar(inverseScale);
            sceneGltf.scene.position.copy(inverseTranslation);
        } else {
            sceneGltf.scene.position.copy(poseTranslation);
            sceneGltf.scene.setRotationFromEuler(new $hBQxr$three.Euler($hBQxr$three.MathUtils.degToRad(poseRotation.x), $hBQxr$three.MathUtils.degToRad(poseRotation.y), $hBQxr$three.MathUtils.degToRad(poseRotation.z)));
            sceneGltf.scene.scale.multiplyScalar(poseScale);
        }
    }
    async loadTilt(url, overrides) {
        try {
            this.overrides = overrides;
            const tiltData = await this.tiltLoader.loadAsync(url);
            this.loadedModel = tiltData;
            this.setupSketchMetaData(tiltData);
            this.initializeScene();
        } catch (error) {
            this.showErrorIcon();
            console.error("Error loading Tilt model");
            this.loadingError = true;
        }
    }
    setAllVertexColors(model) {
        model.traverse((node)=>{
            if (node.material) {
                if (Array.isArray(node.material)) node.material.forEach((material)=>material.vertexColors = true);
                else node.material.vertexColors = true;
            }
        });
    }
    // Defaults to assuming materials are vertex colored
    async loadObj(url, overrides) {
        try {
            this.overrides = overrides;
            this.objLoader.loadAsync(url).then((objData)=>{
                this.loadedModel = objData;
                let defaultBackgroundColor = overrides?.["defaultBackgroundColor"];
                if (!defaultBackgroundColor) defaultBackgroundColor = "#000000";
                this.defaultBackgroundColor = new $hBQxr$three.Color(defaultBackgroundColor);
                let withVertexColors = overrides?.["withVertexColors"];
                if (withVertexColors) this.setAllVertexColors(this.loadedModel);
                this.setupSketchMetaData(this.loadedModel);
                this.initializeScene();
            });
        } catch (error) {
            this.showErrorIcon();
            console.error("Error loading Obj model");
            this.loadingError = true;
        }
    }
    async loadObjWithMtl(objUrl, mtlUrl, overrides) {
        try {
            this.overrides = overrides;
            this.mtlLoader.loadAsync(mtlUrl).then((materials)=>{
                materials.preload();
                this.objLoader.setMaterials(materials);
                this.objLoader.loadAsync(objUrl).then((objData)=>{
                    this.loadedModel = objData;
                    let defaultBackgroundColor = overrides?.["defaultBackgroundColor"];
                    if (!defaultBackgroundColor) defaultBackgroundColor = "#000000";
                    this.defaultBackgroundColor = new $hBQxr$three.Color(defaultBackgroundColor);
                    let withVertexColors = overrides?.["withVertexColors"];
                    if (withVertexColors) this.setAllVertexColors(this.loadedModel);
                    this.setupSketchMetaData(this.loadedModel);
                    this.initializeScene();
                    this.frameScene(); // Not sure why the standard viewpoint heuristic isn't working here
                });
            });
        } catch (error) {
            this.showErrorIcon();
            console.error("Error loading Obj/Mtl model");
            this.loadingError = true;
        }
    }
    async loadFbx(url, overrides) {
        try {
            this.overrides = overrides;
            const fbxData = await this.fbxLoader.loadAsync(url);
            this.loadedModel = fbxData;
            this.setupSketchMetaData(fbxData);
            this.initializeScene();
            this.frameScene();
        } catch (error) {
            this.showErrorIcon();
            console.error("Error loading Fbx model");
            this.loadingError = true;
        }
    }
    async loadPly(url, overrides) {
        try {
            this.overrides = overrides;
            const plyData = await this.plyLoader.loadAsync(url);
            plyData.computeVertexNormals();
            const material = new $hBQxr$three.MeshStandardMaterial({
                color: 0xffffff,
                metalness: 0
            });
            const plyModel = new $hBQxr$three.Mesh(plyData, material);
            this.loadedModel = plyModel;
            this.setupSketchMetaData(plyModel);
            this.initializeScene();
            this.frameScene();
        } catch (error) {
            this.showErrorIcon();
            console.error("Error loading Ply model");
            this.loadingError = true;
        }
    }
    async loadStl(url, overrides) {
        try {
            this.overrides = overrides;
            const stlData = await this.stlLoader.loadAsync(url);
            let material = new $hBQxr$three.MeshStandardMaterial({
                color: 0xffffff,
                metalness: 0
            });
            if (stlData.hasColors) material = new $hBQxr$three.MeshStandardMaterial({
                opacity: stlData.alpha,
                vertexColors: true
            });
            const stlModel = new $hBQxr$three.Mesh(stlData, material);
            this.loadedModel = stlModel;
            this.setupSketchMetaData(stlModel);
            this.initializeScene();
            this.frameScene();
        } catch (error) {
            this.showErrorIcon();
            console.error("Error loading Stl model");
            this.loadingError = true;
        }
    }
    async loadUsdz(url, overrides) {
        try {
            this.overrides = overrides;
            const usdzData = await this.usdzLoader.loadAsync(url);
            this.loadedModel = usdzData;
            this.setupSketchMetaData(usdzData);
            this.initializeScene();
            this.frameScene();
        } catch (error) {
            this.showErrorIcon();
            console.error("Error loading Usdz model");
            this.loadingError = true;
        }
    }
    async loadVox(url, overrides) {
        try {
            this.overrides = overrides;
            let voxModel = new $hBQxr$three.Group();
            let chunks = await this.voxLoader.loadAsync(url);
            for(let i = 0; i < chunks.length; i++){
                const chunk = chunks[i];
                const mesh = new (0, $hBQxr$VOXMesh)(chunk);
                mesh.scale.setScalar(0.15);
                voxModel.add(mesh);
            }
            this.loadedModel = voxModel;
            this.setupSketchMetaData(voxModel);
            this.initializeScene();
            this.frameScene();
        } catch (error) {
            this.showErrorIcon();
            console.error("Error loading Vox model");
            this.loadingError = true;
        }
    }
    async loadSparkModule() {
        try {
            // Construct module name at runtime to avoid bundler processing
            const moduleName = "@sparkjsdev/spark";
            const sparkModule = await import(/* webpackIgnore: true */ moduleName);
            if (!sparkModule.SplatMesh) throw new Error("SplatMesh not found in Spark module exports");
            return sparkModule;
        } catch (error) {
            throw new Error(`Spark (@sparkjsdev/spark) is not available: ${error.message}`);
        }
    }
    async loadSplat(url, overrides) {
        try {
            this.overrides = overrides;
            // Add default camera override for splat files if none supplied
            if (!this.overrides?.camera || Object.keys(this.overrides.camera).length === 0) {
                this.overrides = this.overrides || {};
                this.overrides.camera = {
                    translation: [
                        0,
                        0,
                        3
                    ],
                    rotation: [
                        0,
                        0,
                        0,
                        1
                    ] // Identity quaternion (no rotation, facing forward)
                };
            }
            // Dynamic import for optional Spark dependency
            let SparkModule;
            try {
                SparkModule = await this.loadSparkModule();
            } catch (importError) {
                console.error(importError.message);
                this.showErrorIcon();
                this.loadingError = true;
                return;
            }
            const splatModel = new SparkModule.SplatMesh({
                url: url
            });
            await splatModel.initialized;
            // Apply coordinate system correction - splat files are upside-down compared to other formats
            splatModel.rotation.x = Math.PI;
            this.loadedModel = splatModel;
            this.setupSketchMetaData(splatModel);
            this.modelBoundingBox = splatModel.getBoundingBox(false);
            this.scene.add(this.loadedModel);
            this.initializeScene();
            // Manually trigger loading screen fade-out since SplatMesh doesn't use LoadingManager
            let loadscreen = document.getElementById('loadscreen');
            if (loadscreen && !loadscreen.classList.contains('loaderror')) loadscreen.classList.add('fade-out');
        } catch (error) {
            this.showErrorIcon();
            console.error("Error loading Splat model:", error);
            this.loadingError = true;
        }
    }
    async assignEnvironment(sceneGltf) {
        let scene = sceneGltf.scene;
        const guid = this.sketchMetadata?.EnvironmentGuid;
        if (guid) {
            const envUrl = new URL(`${guid}/${guid}.glb`, this.environmentPath);
            try {
                // Use the standard GLTFLoader for environments
                const standardLoader = new (0, $hBQxr$GLTFLoader)();
                const envGltf = await standardLoader.loadAsync(envUrl.toString());
                if (this.isV1) envGltf.scene.setRotationFromEuler(new $hBQxr$three.Euler(0, Math.PI, 0));
                if (!this.isNewTiltExporter(sceneGltf)) envGltf.scene.scale.set(.1, .1, .1);
                scene.attach(envGltf.scene);
                this.environmentObject = envGltf.scene;
            } catch (error) {
                console.error(`Failed to load environment: ${error}`);
            }
        } else console.log(`No environment GUID found`);
    }
    generateGradientSky(colorA, colorB, direction) {
        const canvas = document.createElement('canvas');
        canvas.id = "skyCanvas";
        canvas.width = 1;
        canvas.height = 256;
        const context = canvas.getContext('2d');
        const gradient = context.createLinearGradient(0, 0, 0, 256);
        gradient.addColorStop(0, colorB.convertSRGBToLinear().getStyle());
        gradient.addColorStop(1, colorA.convertSRGBToLinear().getStyle());
        context.fillStyle = gradient;
        context.fillRect(0, 0, 1, 256);
        const texture = new $hBQxr$three.CanvasTexture(canvas);
        texture.wrapS = $hBQxr$three.RepeatWrapping;
        texture.wrapT = $hBQxr$three.ClampToEdgeWrapping;
        return this.generateSkyGeometry(texture, direction);
    }
    generateTextureSky(textureName) {
        const textureUrl = new URL(`skies/${textureName}.png`, this.texturePath);
        let texture = new $hBQxr$three.TextureLoader().load(textureUrl.toString());
        return this.generateSkyGeometry(texture, new $hBQxr$three.Vector3(0, 1, 0));
    }
    generateSkyGeometry(texture, direction) {
        texture.colorSpace = $hBQxr$three.SRGBColorSpace;
        const material = new $hBQxr$three.MeshBasicMaterial({
            map: texture,
            side: $hBQxr$three.BackSide
        });
        material.fog = false;
        material.toneMapped = false;
        const geometry = new $hBQxr$three.SphereGeometry(5000, 64, 64);
        const skysphere = new $hBQxr$three.Mesh(geometry, material);
        skysphere.name = "environmentSky";
        const defaultUp = new $hBQxr$three.Vector3(0, 1, 0);
        const quaternion = new $hBQxr$three.Quaternion().setFromUnitVectors(defaultUp, direction);
        skysphere.applyQuaternion(quaternion);
        return skysphere;
    }
    setupSketchMetaDataFromScene(scene, userData) {
        let sketchMetaData = new $677737c8a5cbea2f$var$SketchMetadata(scene, userData);
        this.modelBoundingBox = new $hBQxr$three.Box3().setFromObject(scene);
        this.sketchMetadata = sketchMetaData;
    }
    setupSketchMetaData(model) {
        let sketchMetaData = new $677737c8a5cbea2f$var$SketchMetadata(model, model.userData);
        this.modelBoundingBox = new $hBQxr$three.Box3().setFromObject(model);
        this.sketchMetadata = sketchMetaData;
    }
    initCameras() {
        let cameraOverrides = this.overrides?.camera;
        // Check if there's a GLTF camera in the scene
        let gltfCamera = null;
        this.loadedModel.traverse((object)=>{
            if (object instanceof $hBQxr$three.Camera && object.name === "TB_ThumbnailSaveCamera" && !gltfCamera) gltfCamera = object;
        });
        let sketchCam = this.sketchMetadata?.CameraTranslation?.toArray();
        if (sketchCam) {
            let poseScale = this.isAnyTiltExporter(this.sceneGltf) ? 0.1 : 1;
            sketchCam = [
                sketchCam[0] * poseScale,
                sketchCam[1] * poseScale,
                sketchCam[2] * poseScale
            ];
        }
        const fov = cameraOverrides?.perspective?.yfov / (Math.PI / 180) || 75;
        const aspect = 2;
        const near = cameraOverrides?.perspective?.znear || 0.1;
        const far = 6000;
        this.flatCamera = new $hBQxr$three.PerspectiveCamera(fov, aspect, near, far);
        let cameraPos = [];
        // Use GLTF camera transform if available AND we are in fly mode
        // (which currently indicates a recent Open Brush export)
        if (this.sketchMetadata.FlyMode && gltfCamera) {
            var worldPos = new $hBQxr$three.Vector3();
            gltfCamera.getWorldPosition(worldPos);
            worldPos.multiplyScalar(0.1);
            cameraPos[0] = worldPos.x;
            cameraPos[1] = worldPos.y;
            cameraPos[2] = worldPos.z;
            this.flatCamera.position.set(cameraPos[0], cameraPos[1], cameraPos[2]);
            var worldQuat = new $hBQxr$three.Quaternion();
            gltfCamera.getWorldQuaternion(worldQuat);
            // var yRotation = new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(0, 1, 0), Math.PI);
            // worldQuat.multiply(yRotation);
            this.flatCamera.quaternion.set(worldQuat.x, worldQuat.y, worldQuat.z, worldQuat.w);
        } else {
            cameraPos = cameraOverrides?.translation || sketchCam || [
                0,
                0.25,
                -3.5
            ];
            let cameraRot = cameraOverrides?.rotation || this.sketchMetadata?.CameraRotation?.toArray() || [
                0,
                0,
                0
            ]; // Could be euler angles or quaternion
            // Fix handedness between Unity and gltf/three.js
            // Should we fix this on export?
            if (cameraRot.length == 3) {
                // Assume euler angles in degrees
                cameraRot[0] += 0;
                cameraRot[1] += 180;
                cameraRot[2] += 0;
                cameraRot[0] = $hBQxr$three.MathUtils.degToRad(cameraRot[0]);
                cameraRot[1] = $hBQxr$three.MathUtils.degToRad(cameraRot[1]);
                cameraRot[2] = $hBQxr$three.MathUtils.degToRad(cameraRot[2]);
            }
            this.flatCamera.position.set(cameraPos[0], cameraPos[1], cameraPos[2]);
            if (cameraRot.length == 3) this.flatCamera.rotation.setFromVector3(new $hBQxr$three.Vector3(cameraRot[0], cameraRot[1], cameraRot[2]));
            else this.flatCamera.quaternion.set(cameraRot[0], cameraRot[1], cameraRot[2], cameraRot[3]);
        }
        this.flatCamera.updateProjectionMatrix();
        this.flatCamera.updateMatrixWorld();
        this.xrCamera = new $hBQxr$three.PerspectiveCamera(fov, aspect, near, far);
        this.cameraRig = new $hBQxr$three.Group();
        this.scene.add(this.cameraRig);
        this.cameraRig.add(this.xrCamera);
        this.activeCamera = this.flatCamera;
        let cameraTarget;
        if (this.sketchMetadata.FlyMode) {
            // Simulate fly mode by setting target point in front of camera
            const forward = new $hBQxr$three.Vector3();
            this.flatCamera.getWorldDirection(forward);
            cameraTarget = this.flatCamera.position.clone().add(forward.multiplyScalar(0.05));
            (0, $e1f901905a002d12$export$2e2bcd8739ae039).install({
                THREE: $hBQxr$three
            });
            this.cameraControls = new (0, $e1f901905a002d12$export$2e2bcd8739ae039)(this.flatCamera, viewer.canvas);
            this.cameraControls.smoothTime = 0.1;
            this.cameraControls.draggingSmoothTime = 0.1;
            this.cameraControls.polarRotateSpeed = this.cameraControls.azimuthRotateSpeed = 1.0;
            this.cameraControls.setPosition(cameraPos[0], cameraPos[1], cameraPos[2], false);
            this.cameraControls.setTarget(cameraTarget.x, cameraTarget.y, cameraTarget.z, false);
            (0, $7f098f70bc341b4e$export$fc22e28a11679cb8)(this.cameraControls);
        } else {
            let pivot = cameraOverrides?.GOOGLE_camera_settings?.pivot;
            if (pivot) // TODO this pivot should be recalculated to take into account
            //  any camera rotation adjustment applied above
            cameraTarget = new $hBQxr$three.Vector3(pivot[0], pivot[1], pivot[2]);
            else if (this.sketchMetadata.CameraTargetDistance) {
                // We do have a distance so can calculate target point
                // Capture camera direction BEFORE CameraControls modifies anything
                const forward = new $hBQxr$three.Vector3();
                this.flatCamera.getWorldDirection(forward);
                let cameraTargetDistance = this.sketchMetadata.CameraTargetDistance;
                cameraTarget = this.flatCamera.position.clone().add(forward.multiplyScalar(cameraTargetDistance));
            } else {
                let vp = this.overrides?.geometryData?.visualCenterPoint;
                if (!vp) {
                    const box = this.modelBoundingBox;
                    if (box != undefined) {
                        const boxCenter = box.getCenter(new $hBQxr$three.Vector3());
                        vp = [
                            boxCenter.x,
                            boxCenter.y,
                            boxCenter.z
                        ];
                    }
                }
                let visualCenterPoint = new $hBQxr$three.Vector3(vp[0], vp[1], vp[2]);
                cameraTarget = this.calculatePivot(this.flatCamera, visualCenterPoint);
                cameraTarget = cameraTarget || visualCenterPoint;
            }
            (0, $e1f901905a002d12$export$2e2bcd8739ae039).install({
                THREE: $hBQxr$three
            });
            this.cameraControls = new (0, $e1f901905a002d12$export$2e2bcd8739ae039)(this.flatCamera, viewer.canvas);
            this.cameraControls.smoothTime = 0.1;
            this.cameraControls.draggingSmoothTime = 0.1;
            this.cameraControls.polarRotateSpeed = this.cameraControls.azimuthRotateSpeed = 1.0;
            this.cameraControls.setPosition(cameraPos[0], cameraPos[1], cameraPos[2], false);
            this.cameraControls.setTarget(cameraTarget.x, cameraTarget.y, cameraTarget.z, false);
            (0, $7f098f70bc341b4e$export$fc22e28a11679cb8)(this.cameraControls);
        }
        // Position and orient the cameraRig to match flatCamera AFTER camera controls are set up
        // The flatCamera is independent of scene scale, but cameraRig is a child of the scene.
        // For new Tilt exporters, the scene will be scaled to 0.1, so we need to compensate.
        // We scale BOTH the position and the rig scale to counteract the scene scale.
        const sceneScaleFactor = this.isNewTiltExporter(this.sceneGltf) ? 10 : 1;
        this.cameraRig.position.copy(this.flatCamera.position).multiplyScalar(sceneScaleFactor);
        this.cameraRig.scale.set(sceneScaleFactor, sceneScaleFactor, sceneScaleFactor);
        // Calculate world position after setup
        this.cameraRig.updateMatrixWorld(true);
        // VR cameras should never be tilted - only copy Y-axis rotation (yaw)
        // Calculate Y rotation from camera position to target (ignoring vertical component)
        const flatCameraWorldDir = new $hBQxr$three.Vector3();
        this.flatCamera.getWorldDirection(flatCameraWorldDir);
        const directionToTarget = new $hBQxr$three.Vector3().subVectors(cameraTarget, this.flatCamera.position);
        directionToTarget.y = 0; // Project onto XZ plane
        directionToTarget.normalize();
        const yaw = Math.atan2(directionToTarget.x, directionToTarget.z);
        // Add 180 degrees because camera's default forward is -Z, not +Z
        this.cameraRig.rotation.y = yaw + Math.PI;
        this.xrCamera.updateProjectionMatrix();
    }
    calculatePivot(camera, centroid) {
        // 1. Get the camera's forward vector
        const forward = new $hBQxr$three.Vector3();
        camera.getWorldDirection(forward); // This gives the forward vector in world space.
        // 2. Define a plane based on the centroid and facing the camera
        const planeNormal = forward.clone().negate(); // Plane facing the camera
        const plane = new $hBQxr$three.Plane().setFromNormalAndCoplanarPoint(planeNormal, centroid);
        // 3. Calculate the intersection point of the forward vector with the plane
        const cameraPosition = camera.position.clone();
        const ray = new $hBQxr$three.Ray(cameraPosition, forward);
        const intersectionPoint = new $hBQxr$three.Vector3();
        if (ray.intersectPlane(plane, intersectionPoint)) return intersectionPoint; // This is your calculated pivot point.
        else {
            console.error("No intersection between camera forward vector and plane.");
            return null; // Handle the error case gracefully.
        }
    }
    initLights() {
        // Logic for scene light creation:
        // 1. Are there explicit GLTF scene lights? If so use them and skip the rest
        // 2. Are there dummy transforms in the GLTF that represent scene lights? If so use them in preference.
        // 3. Does the GLTF have custom metadata for light transform and color?
        // 4. Does the GLTF have an environment preset guid? If so use the light transform and colors from that
        // 5. If there's neither custom metadata, an environment guid or explicit GLTF lights - create some default lighting.
        function convertTBEuler(rot) {
            return new $hBQxr$three.Euler($hBQxr$three.MathUtils.degToRad(rot.x), $hBQxr$three.MathUtils.degToRad(rot.y), $hBQxr$three.MathUtils.degToRad(rot.z));
        }
        if (this.sketchMetadata == undefined || this.sketchMetadata == null) {
            const light = new $hBQxr$three.DirectionalLight(0xffffff, 1);
            light.position.set(10, 10, 10).normalize();
            this.loadedModel.add(light);
            return;
        }
        let l0 = new $hBQxr$three.DirectionalLight(this.sketchMetadata.SceneLight0Color, 1.0);
        let l1 = new $hBQxr$three.DirectionalLight(this.sketchMetadata.SceneLight1Color, 1.0);
        let light0Euler = convertTBEuler(this.sketchMetadata.SceneLight0Rotation);
        let light1Euler = convertTBEuler(this.sketchMetadata.SceneLight1Rotation);
        // Same rotation adjustment we apply to scene and environment
        if (this.isNewTiltExporter(this.sceneGltf) || this.isV1) {
            light0Euler.y += Math.PI;
            light1Euler.y += Math.PI;
        }
        const light0Direction = new $hBQxr$three.Vector3(0, 0, 1).applyEuler(light0Euler);
        l0.position.copy(light0Direction.multiplyScalar(10));
        const light1Direction = new $hBQxr$three.Vector3(0, 0, 1).applyEuler(light1Euler);
        l1.position.copy(light1Direction.multiplyScalar(10));
        l0.castShadow = true;
        l1.castShadow = false;
        this.loadedModel?.add(l0);
        this.loadedModel?.add(l1);
        const ambientLight = new $hBQxr$three.AmbientLight();
        ambientLight.color = this.sketchMetadata.AmbientLightColor;
        this.scene.add(ambientLight);
    }
    initFog() {
        if (this.sketchMetadata == undefined || this.sketchMetadata == null) return;
        this.scene.fog = new $hBQxr$three.FogExp2(this.sketchMetadata.FogColor, this.sketchMetadata.FogDensity);
    }
    initSceneBackground() {
        // OBJ and FBX models don't have metadata
        if (!this.sketchMetadata == undefined) {
            this.scene.background = this.defaultBackgroundColor;
            return;
        }
        let sky = null;
        if (this.sketchMetadata.UseGradient) sky = this.generateGradientSky(this.sketchMetadata.SkyColorA, this.sketchMetadata.SkyColorB, this.sketchMetadata.SkyGradientDirection);
        else if (this.sketchMetadata.SkyTexture) sky = this.generateTextureSky(this.sketchMetadata.SkyTexture);
        if (sky !== null) {
            this.scene?.add(sky);
            this.skyObject = sky;
        } else // Use the default background color if there's no sky
        this.scene.background = this.defaultBackgroundColor;
    }
    frameScene() {
        if (this.selectedNode != null) // If a node is selected in the treeview, frame that
        this.frameNode(this.selectedNode);
        else if (this.modelBoundingBox != null) // This should be the bounding box of the loaded model itself
        this.frameBox(this.modelBoundingBox);
        else {
            // Fall back to framing the whole scene
            let box = new $hBQxr$three.Box3().setFromObject(this.scene);
            this.frameBox(box);
        }
    }
    frameNode(node) {
        this.frameBox(new $hBQxr$three.Box3().setFromObject(node));
    }
    frameBox(box) {
        const boxSize = box.getSize(new $hBQxr$three.Vector3()).length();
        const boxCenter = box.getCenter(new $hBQxr$three.Vector3());
        this.cameraControls.minDistance = boxSize * 0.01;
        this.cameraControls.maxDistance = boxSize * 10;
        const midDistance = this.cameraControls.minDistance + (boxSize - this.cameraControls.minDistance) / 2;
        this.cameraControls.setTarget(boxCenter.x, boxCenter.y, boxCenter.z);
        let sphere = new $hBQxr$three.Sphere();
        box.getBoundingSphere(sphere);
        let fullDistance = sphere.radius * 1.75;
        this.cameraControls.dollyTo(fullDistance, true);
        this.cameraControls.saveState();
    }
    levelCamera() {
        // Sets the camera target so that the camera is looking forward and level
        let cameraPos = new $hBQxr$three.Vector3();
        this.cameraControls.getPosition(cameraPos);
        let cameraDir = new $hBQxr$three.Vector3();
        this.cameraControls.camera.getWorldDirection(cameraDir);
        cameraDir.y = 0; // Ensure the direction is level
        cameraDir.normalize();
        let newTarget = cameraPos.clone().add(cameraDir);
        this.cameraControls.setTarget(newTarget.x, newTarget.y, newTarget.z, true);
    }
    createTreeView(model, root) {
        const treeView = root;
        if (!treeView) {
            console.error('Tree view container not found');
            return;
        }
        this.treeViewRoot = treeView;
        treeView.innerHTML = '';
        if (model) this.createTreeViewNode(model, treeView);
        else console.error('Model not loaded');
    }
    createTreeViewNode(object, parentElement) {
        const nodeElement = document.createElement('div');
        nodeElement.classList.add('tree-node');
        nodeElement.style.marginLeft = '5px';
        const contentElement = document.createElement('div');
        contentElement.classList.add('tree-content');
        const toggleButton = document.createElement('span');
        toggleButton.classList.add('toggle-btn');
        toggleButton.textContent = object.children && object.children.length > 0 ? "\u25B6" : ' ';
        toggleButton.addEventListener('click', ()=>{
            nodeElement.classList.toggle('expanded');
            toggleButton.textContent = nodeElement.classList.contains('expanded') ? "\u25BC" : "\u25B6";
        });
        const visibilityCheckbox = document.createElement('input');
        visibilityCheckbox.type = 'checkbox';
        visibilityCheckbox.checked = object.visible;
        visibilityCheckbox.addEventListener('change', ()=>{
            object.visible = visibilityCheckbox.checked;
        });
        const label = document.createElement('span');
        label.classList.add('label');
        label.textContent = object.name || object.type;
        label.style.marginLeft = '5px';
        contentElement.appendChild(toggleButton);
        contentElement.appendChild(visibilityCheckbox);
        contentElement.appendChild(label);
        label.addEventListener('click', ()=>{
            let wasSelected = label.classList.contains('selected');
            document.querySelectorAll('.tree-node').forEach((node)=>{
                const label = node.querySelector('.label');
                if (label) label.classList.remove('selected');
            });
            if (wasSelected) {
                label.classList.remove('selected');
                this.selectedNode = null;
            } else {
                label.classList.add('selected');
                this.selectedNode = object;
            }
            console.log(object);
        });
        nodeElement.appendChild(contentElement);
        if (object.children && object.children.length > 0) {
            const childrenContainer = document.createElement('div');
            childrenContainer.classList.add('children');
            nodeElement.appendChild(childrenContainer);
            object.children.forEach((child)=>{
                this.createTreeViewNode(child, childrenContainer);
            });
        }
        parentElement.appendChild(nodeElement);
    }
    /**
     * Generate a consistent path for an object in the scene hierarchy.
     * Uses object names and child indices to create a unique, reproducible identifier.
     */ getObjectPath(object, currentPath = '') {
        if (!object.parent) return currentPath || '/';
        const parent = object.parent;
        const childIndex = parent.children.indexOf(object);
        const nodeName = object.name || `${object.type}_${childIndex}`;
        const newPath = `/${nodeName}${currentPath}`;
        return this.getObjectPath(parent, newPath);
    }
    /**
     * Get the current visibility state of all nodes in the scene hierarchy.
     * Returns a map of object paths to visibility state.
     * Object paths are consistent across sessions when loading the same file.
     * @returns An object mapping paths to state information
     */ getTreeViewState() {
        const state = {};
        const collectState = (object)=>{
            const path = this.getObjectPath(object);
            state[path] = {
                visible: object.visible
            };
            if (object.children && object.children.length > 0) object.children.forEach((child)=>collectState(child));
        };
        if (this.scene) collectState(this.scene);
        return state;
    }
    /**
     * Restore the visibility state of nodes from a previously saved state.
     * @param state - The state object returned from getTreeViewState()
     */ setTreeViewState(state) {
        const applyState = (object)=>{
            const path = this.getObjectPath(object);
            const savedState = state[path];
            if (savedState !== undefined) object.visible = savedState.visible;
            if (object.children && object.children.length > 0) object.children.forEach((child)=>applyState(child));
        };
        if (this.scene) applyState(this.scene);
        // Refresh the tree view UI if it exists
        this.refreshTreeView();
    }
    /**
     * Refresh the tree view UI to match the current visibility state of objects.
     * This updates all checkboxes in the tree view to reflect the actual visibility of objects.
     */ refreshTreeView() {
        if (!this.treeViewRoot || !this.scene) return;
        // Recreate the tree view to reflect the current state
        this.createTreeView(this.scene, this.treeViewRoot);
    }
}


export {$677737c8a5cbea2f$export$2ec4afd9b3c16a85 as Viewer};
//# sourceMappingURL=icosa-viewer.module.js.map
