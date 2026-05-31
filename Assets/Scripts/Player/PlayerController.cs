using UnityEngine;
using UnityEngine.InputSystem;
using Galatea.Systems;

namespace Galatea.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 3.5f;

        [Header("Look")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private float mouseSensitivity = 0.1f;

        [Header("Input")]
        [SerializeField] private InputAction moveAction;
        [SerializeField] private InputAction lookAction;

        private const float PitchLimit = 89f;
        private const float MovingThreshold = 0.05f;

        private CharacterController _controller;
        private float _pitch;
        private bool _isWalking;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();

            if (moveAction.bindings.Count == 0)
            {
                moveAction.AddCompositeBinding("2DVector")
                    .With("Up",    "<Keyboard>/w")
                    .With("Down",  "<Keyboard>/s")
                    .With("Left",  "<Keyboard>/a")
                    .With("Right", "<Keyboard>/d");
            }
            if (lookAction.bindings.Count == 0)
                lookAction.AddBinding("<Mouse>/delta");
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnEnable()
        {
            moveAction.Enable();
            lookAction.Enable();
        }

        private void OnDisable()
        {
            moveAction.Disable();
            lookAction.Disable();
            if (_isWalking)
            {
                SoundManager.StopWalkLoop();
                _isWalking = false;
            }
        }

        private void Update()
        {
            Vector2 moveInput = moveAction.ReadValue<Vector2>();
            Vector3 worldMove = transform.TransformDirection(new Vector3(moveInput.x, 0f, moveInput.y)) * moveSpeed;
            _controller.SimpleMove(worldMove);

            UpdateWalkLoop(moveInput);

            if (cameraTransform == null) return;

            Vector2 lookInput = lookAction.ReadValue<Vector2>();

            transform.Rotate(0f, lookInput.x * mouseSensitivity, 0f, Space.Self);

            // Cumulative pitch via Quaternion.Euler — avoids localEulerAngles' 0/360 wrap-around glitch.
            _pitch -= lookInput.y * mouseSensitivity;
            _pitch = Mathf.Clamp(_pitch, -PitchLimit, PitchLimit);
            cameraTransform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private void UpdateWalkLoop(Vector2 moveInput)
        {
            bool shouldWalk = moveInput.sqrMagnitude >= MovingThreshold * MovingThreshold;
            if (shouldWalk == _isWalking) return;

            _isWalking = shouldWalk;
            if (_isWalking) SoundManager.StartWalkLoop();
            else            SoundManager.StopWalkLoop();
        }
    }
}
