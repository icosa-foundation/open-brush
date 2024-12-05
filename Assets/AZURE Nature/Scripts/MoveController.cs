using UnityEngine;

namespace AzureNature
{
    public class MoveController : MonoBehaviour
    {
        public float movementSpeed;
        public float jumpSpeed;
        public float runMultiplier;
        public float gravity = -9.81f;
        Vector3 velocity;
        private CharacterController characterController;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
        }

        void Update()
        {
            if (characterController.isGrounded && velocity.y < 0)
            {
                velocity.y = -2f;
            }

            float x = Input.GetAxis("Horizontal");
            float z = Input.GetAxis("Vertical");

            Vector3 movement = transform.right * x + transform.forward * z;

            characterController.Move(movement * movementSpeed * Time.deltaTime);

            velocity.y += gravity * Time.deltaTime;

            characterController.Move(velocity * Time.deltaTime);

            if (Input.GetButton("Jump") && characterController.isGrounded)
            {
                velocity.y = Mathf.Sqrt(jumpSpeed * -2f * gravity);
            }

            if (Input.GetKey(KeyCode.LeftShift))
            {
                characterController.Move(movement * Time.deltaTime * runMultiplier);
            }

        }
    }
}