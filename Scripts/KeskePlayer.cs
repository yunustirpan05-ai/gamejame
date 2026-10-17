using UnityEngine;

namespace Keske
{
    [RequireComponent(typeof(CharacterController))]
    public class KeskePlayer : MonoBehaviour
    {
        public KeskeGame game;
        public Camera view;
        public Light torch;
        public float speed = 2.8f;
        public float sprintSpeed = 4.3f;
        public float sensitivity = 1.6f;
        public float reach = 2.8f;
        public KeskeInteractable target;
        CharacterController controller;
        float pitch;
        float fall;
        float footstep;

        void Awake() { controller = GetComponent<CharacterController>(); }

        public void Teleport(Vector3 position, float yaw)
        {
            if (controller == null) controller = GetComponent<CharacterController>();
            controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            pitch = 0;
            fall = -2;
            if (view != null) view.transform.localRotation = Quaternion.identity;
            controller.enabled = true;
            Physics.SyncTransforms();
            target = null;
        }

        void Update()
        {
            if (game == null || !game.CanMove) { target = null; return; }
            if (Cursor.lockState != CursorLockMode.Locked) return;
            Vector2 look = KeskeInput.Look * sensitivity;
            transform.Rotate(0, look.x, 0);
            pitch = Mathf.Clamp(pitch - look.y, -78, 78);
            view.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            Vector2 input = KeskeInput.Move;
            Vector3 motion = transform.right * input.x + transform.forward * input.y;
            float movementSpeed = KeskeInput.Sprint ? sprintSpeed : speed;
            if (controller.isGrounded && fall < 0) fall = -2;
            fall += -18 * Time.deltaTime;
            controller.Move((motion * movementSpeed + Vector3.up * fall) * Time.deltaTime);
            if (transform.position.y < -5) Teleport(new Vector3(0, 0.1f, -8), 0);
            if (input.sqrMagnitude > 0.1f && controller.isGrounded)
            {
                footstep -= Time.deltaTime;
                if (footstep <= 0)
                {
                    game.sound.Play("step", 0.23f);
                    footstep = KeskeInput.Sprint ? 0.31f : 0.46f;
                }
            }
            target = null;
            RaycastHit hit;
            if (Physics.Raycast(view.transform.position, view.transform.forward, out hit, reach, ~0, QueryTriggerInteraction.Ignore))
                target = hit.collider.GetComponentInParent<KeskeInteractable>();
            if (target != null && KeskeInput.Pressed(KeyCode.E)) target.Use(game);
            if (KeskeInput.Pressed(KeyCode.F) && torch != null) torch.enabled = !torch.enabled;
            if (game.CanMove && game.preview && transform.position.y > 3 && !game.progress.scareSeen) game.StartScare();
        }
    }
}
