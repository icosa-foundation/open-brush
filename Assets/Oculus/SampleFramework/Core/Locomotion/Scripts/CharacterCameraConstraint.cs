/************************************************************************************

See SampleFramework license.txt for license terms.  Unless required by applicable law 
or agreed to in writing, the sample code is provided “AS IS” WITHOUT WARRANTIES OR 
CONDITIONS OF ANY KIND, either express or implied.  See the license for specific 
language governing permissions and limitations under the license.

************************************************************************************/

using System;
using UnityEngine;

/// <summary>
/// This component is responsible for moving the character capsule to match the HMD, fading out the camera or blocking movement when 
/// collisions occur, and adjusting the character capsule height to match the HMD's offset from the ground.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(OVRPlayerController))]
public class CharacterCameraConstraint : MonoBehaviour
{
	/// <summary>
	/// This should be a reference to the OVRCameraRig that is usually a child of the PlayerController.
	/// </summary>
	[Tooltip("This should be a reference to the OVRCameraRig that is usually a child of the PlayerController.")]
	public OVRCameraRig CameraRig;

	/// <summary>
	/// This value represents the character capsule's distance from the HMD's position. When the player is moving in legal space without collisions, this will be zero.
	/// </summary>
	[Tooltip("This value represents the character capsule's distance from the HMD's position. When the player is moving in legal space without collisions, this will be zero.")]
	public float CurrentDistance;

	/// <summary>
	/// When true, the camera will fade to black when the HMD is moved into collidable geometry.
	/// </summary>
	[Tooltip("When true, the camera will fade to black when the HMD is moved into collidable geometry.")]
	public bool EnableFadeout;

	/// <summary>
	/// When true, the camera will be prevented from passing through collidable geometry. This is usually considered uncomfortable for users.
	/// </summary>
	[Tooltip("When true, the camera will be prevented from passing through collidable geometry. This is usually considered uncomfortable for users.")]
	public bool EnableCollision;

	/// <summary>
	/// When true, adjust the character controller height on the fly to match the HMD's offset from the ground which will allow ducking to go through smaller spaces.
	/// </summary>
	[Tooltip("When true, adjust the character controller height on the fly to match the HMD's offset from the ground which will allow ducking to go through smaller spaces.")]
	public bool DynamicHeight;

	/// <summary>
	/// This should be set to 1 to make the screen completely fade out when the HMD is inside world geometry. Lesser values can be useful for testing.
	/// </summary>
	[Tooltip("This should be set to 1 to make the screen completely fade out when the HMD is inside world geometry. Lesser values can be useful for testing.")]
	public float MaxFade = 1;

	/// <summary>
	/// This value is used to control how far from the character capsule the HMD must be before the fade to black begins.
	/// </summary>
	[Tooltip("This value is used to control how far from the character capsule the HMD must be before the fade to black begins.")]
	public float FadeMinDistance = 0.25f;

	/// <summary>
	/// This value is used to control how far from the character capsule the HMD must be before the fade to black is complete. 
	/// This should be tuned so that it is fully faded in before the camera will clip geometry that the player should not be able see beyond.
	/// </summary>
	[Tooltip("This value is used to control how far from the character capsule the HMD must be before the fade to black is complete. This should be tuned so that it is fully faded in before the camera will clip geometry that the player should not be able see beyond.")]
	public float FadeMaxDistance = 0.35f;

	private readonly Action _cameraUpdateAction;
	private readonly Action _preCharacterMovementAction;
	private CharacterController _character;
	private OVRPlayerController _playerController;

	CharacterCameraConstraint()
	{
		_cameraUpdateAction = CameraUpdate;
		_preCharacterMovementAction = PreCharacterMovement;
	}

	void Awake ()
	{
		_character = GetComponent<CharacterController>();
		_playerController = GetComponent<OVRPlayerController>();
	}

	void OnEnable()
	{
		_playerController.CameraUpdated += _cameraUpdateAction;
		_playerController.PreCharacterMove += _preCharacterMovementAction;
	}

	void OnDisable()
	{
		_playerController.PreCharacterMove -= _preCharacterMovementAction;
		_playerController.CameraUpdated -= _cameraUpdateAction;
	}

	/// <summary>
	/// This method is the handler for the PlayerController.CameraUpdated event, which is used
	/// to update the character height based on camera position.
	/// </summary>
	private void CameraUpdate()
	{
		// If dynamic height is enabled, try to adjust the controller height to the height of the camera.
		if (DynamicHeight)
		{
			var cameraHeight = _playerController.CameraHeight;

			// If the new height is less than before, just accept the reduced height.
			if (cameraHeight <= _character.height)
			{
				_character.height = cameraHeight - _character.skinWidth;
			}
			else
			{
				// Attempt to increase the controller height to the height of the camera.
				// It is important to understand that this will prevent the character from growing into colliding 
				// geometry, and that the camera might go above the character controller. For instance, ducking through
				// a low tunnel and then standing up in the middle would allow the player to see outside the world.
				// The CharacterCameraConstraint is designed to detect this problem and provide feedback to the user,
				// however it is useful to keep the character controller at a size that fits the space because this would allow
				// the player to move to a taller space. If the character controller was simply made as tall as the camera wanted,
				// the player would then be stuck and unable to move at all until the player ducked back down to the 
				// necessary elevation.
				var bottom = _character.transform.position;
				bottom += _character.center;
				bottom.y -= _character.height / 2.0f + _character.radius;
				RaycastHit info;
				var pad = _character.radius - _character.skinWidth;
				if (EnableCollision && Physics.SphereCast(bottom, _character.radius, Vector3.up, out info, cameraHeight + pad,
					_character.gameObject.layer, QueryTriggerInteraction.Ignore))
				{
					_character.height = info.distance - _character.radius - _character.skinWidth;
					var t = _character.transform;
					var p = t.position;
					p.y -= (cameraHeight - info.distance + pad); 
					t.position = p;
				}
				else
				{
					_character.height = cameraHeight - _character.skinWidth;
				}
			}
		}
	}

	/// <summary>
	/// This method is the handler for the PlayerController.PreCharacterMove event, which is used
	/// to do the work of fading out the camera or adjust the position depending on the 
	/// settings and the relationship of where the camera is and where the character is.
	/// </summary>
	void PreCharacterMovement()
	{
		if (_playerController.Teleported)
			return;
		
		// First, determine if the lateral movement will collide with the scene geometry.
		var oldCameraPos = CameraRig.transform.position;
		var wpos = CameraRig.centerEyeAnchor.position;
		var delta = wpos - transform.position;
		delta.y = 0;
		var len = delta.magnitude;
		if (len > 0.0f)
		{
			_character.Move(delta);
			var currentDelta = transform.position - wpos;
			currentDelta.y = 0;
			CurrentDistance = currentDelta.magnitude;
			CameraRig.transform.position = oldCameraPos;
			if (EnableCollision)
			{
				if (CurrentDistance > 0)
				{
					CameraRig.transform.position = oldCameraPos - delta;
				}
				//OVRInspector.instance.fader.SetFadeLevel(0);
				return;
			}
		}
		else
		{
			CurrentDistance = 0;
		}

		// Next, determine if the player camera is colliding with something above the player by doing a sphere test from the feet to the head.
		var bottom = transform.position;
		bottom += _character.center;
		bottom.y -= _character.height / 2.0f;

		RaycastHit info;
		var max = _playerController.CameraHeight;
		if (Physics.SphereCast(bottom, _character.radius, Vector3.up, out info, max,
			gameObject.layer, QueryTriggerInteraction.Ignore))
		{
			// It hit something. Use the fade distance min/max to determine how much to fade.
			var dist = info.distance;
			dist = max - dist;
			if (dist > CurrentDistance)
			{
				CurrentDistance = dist;
			}
		}

		//if (EnableFadeout)
		//{
			//float fadeLevel = Mathf.Clamp01((CurrentDistance - FadeMinDistance)/ (FadeMaxDistance - FadeMinDistance));
			//OVRInspector.instance.fader.SetFadeLevel(fadeLevel * MaxFade);
		//}
	}
}
