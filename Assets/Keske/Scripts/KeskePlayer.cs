using UnityEngine;

namespace Keske
{
    [DefaultExecutionOrder(100)]
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
        readonly RaycastHit[] aimHits = new RaycastHit[32];
        static readonly Vector2[] aimOffsets = {
            Vector2.zero, Vector2.left, Vector2.right, Vector2.up, Vector2.down,
            new Vector2(-0.707f, -0.707f), new Vector2(0.707f, -0.707f),
            new Vector2(-0.707f, 0.707f), new Vector2(0.707f, 0.707f)
        };

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
            if (Cursor.lockState != CursorLockMode.Locked) { target = null; return; }
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
            if (transform.position.y < -5) Teleport(game.spawnPosition, game.spawnYaw);
            if (input.sqrMagnitude > 0.1f && controller.isGrounded)
            {
                footstep -= Time.deltaTime;
                if (footstep <= 0)
                {
                    game.sound.Play("step", 0.23f);
                    footstep = KeskeInput.Sprint ? 0.31f : 0.46f;
                }
            }
            if (KeskeInput.Pressed(KeyCode.F) && torch != null) torch.enabled = !torch.enabled;
        }

        void LateUpdate()
        {
            if (game == null || !game.CanMove || Cursor.lockState != CursorLockMode.Locked || view == null)
            { target = null; return; }
            // Doors and risk poses may have moved since the last physics step.
            Physics.SyncTransforms();
            target = FindTarget();
            if (target != null && KeskeInput.InteractPressed) UseTarget();
        }

        public void UseTarget()
        {
            // Notes, plans and completed risks change the screen and clear target inside Use.
            // Pickups can deactivate themselves. Revalidate before reading the shared field.
            var selected = target;
            if (game == null || selected == null || !selected.isActiveAndEnabled || !game.CanMove)
            { target = null; return; }
            selected.Use(game);
            if (target == null || !target.isActiveAndEnabled || !game.CanMove) target = null;
        }

        public KeskeInteractable FindTarget()
        {
            if (view == null) return null;
            KeskeInteractable candidate = null;
            // A five-pixel radius at 720p helps with small objects without a sticky timer.
            float tolerance = 5f * view.pixelHeight / 720f;
            Vector3 center = new Vector3(view.pixelRect.center.x, view.pixelRect.center.y, 0);
            for (int i = 0; i < aimOffsets.Length; i++)
            {
                Vector2 offset = aimOffsets[i] * tolerance;
                Ray screenRay = view.ScreenPointToRay(center + new Vector3(offset.x, offset.y, 0));
                Ray ray = new Ray(view.transform.position, screenRay.direction);
                int count = Physics.RaycastNonAlloc(ray, aimHits, reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                RaycastHit[] hits = aimHits;
                if (count == aimHits.Length)
                {
                    hits = Physics.RaycastAll(ray, reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    count = hits.Length;
                }
                Collider nearest = null;
                float distance = float.PositiveInfinity;
                for (int j = 0; j < count; j++)
                {
                    var hit = hits[j];
                    if (hit.collider.transform.IsChildOf(transform) || hit.distance >= distance) continue;
                    nearest = hit.collider;
                    distance = hit.distance;
                }
                if (nearest == null) continue;
                var item = nearest.GetComponentInParent<KeskeInteractable>();
                if (item == null || !item.isActiveAndEnabled) continue;
                if (i == 0) return item; // The exact crosshair always wins.
                if (item == target) return item; // Keep only a still-visible nearby target.
                if (candidate == null) candidate = item;
            }
            return candidate;
        }

    }
}
