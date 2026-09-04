using UnityEngine;

namespace Statecraft.Player
{
    /// <summary>
    /// Standard RTS camera: WASD / edge pan, wheel zoom along the view axis,
    /// Q and E to rotate around the focus point. Clamped to the map so you cannot
    /// lose yourself in the void, which is the single most common prototype annoyance.
    /// </summary>
    public class RTSCameraController : MonoBehaviour
    {
        [Header("Pan")]
        public float panSpeed        = 40f;
        public float fastPanMultiplier = 2.2f;
        public float edgePanBorder   = 8f;
        public bool  edgePanEnabled  = true;

        [Header("Zoom")]
        public float zoomSpeed  = 55f;
        public float minHeight  = 18f;
        public float maxHeight  = 130f;

        [Header("Rotate")]
        public float rotateSpeed = 90f;

        [Header("Bounds")]
        public float mapExtent = 160f;

        [Header("Framing")]
        public float pitchAtMinHeight = 42f;
        public float pitchAtMaxHeight = 68f;

        float _yaw;
        float _height = 55f;

        void Start()
        {
            _yaw    = transform.eulerAngles.y;
            _height = Mathf.Clamp(transform.position.y, minHeight, maxHeight);
            ApplyPitch();
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float speed = panSpeed * (Input.GetKey(KeyCode.LeftShift) ? fastPanMultiplier : 1f);

            // Zoom scales pan speed — otherwise zoomed out feels like wading.
            speed *= Mathf.Lerp(0.55f, 1.6f, Mathf.InverseLerp(minHeight, maxHeight, _height));

            Vector3 move = Vector3.zero;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))    move.z += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))  move.z -= 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  move.x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) move.x += 1f;

            if (edgePanEnabled && Application.isFocused)
            {
                Vector3 m = Input.mousePosition;
                if (m.x >= 0f && m.x <= Screen.width && m.y >= 0f && m.y <= Screen.height)
                {
                    if (m.x < edgePanBorder)                 move.x -= 1f;
                    if (m.x > Screen.width  - edgePanBorder) move.x += 1f;
                    if (m.y < edgePanBorder)                 move.z -= 1f;
                    if (m.y > Screen.height - edgePanBorder) move.z += 1f;
                }
            }

            if (Input.GetKey(KeyCode.Q)) _yaw -= rotateSpeed * dt;
            if (Input.GetKey(KeyCode.E)) _yaw += rotateSpeed * dt;

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                _height = Mathf.Clamp(_height - scroll * zoomSpeed * 0.1f * Mathf.Max(1f, _height * 0.06f),
                                      minHeight, maxHeight);
            }

            if (move.sqrMagnitude > 0.001f)
            {
                var flatRot = Quaternion.Euler(0f, _yaw, 0f);
                Vector3 delta = flatRot * move.normalized * speed * dt;
                var p = transform.position + delta;
                p.x = Mathf.Clamp(p.x, -mapExtent, mapExtent);
                p.z = Mathf.Clamp(p.z, -mapExtent, mapExtent);
                transform.position = p;
            }

            var pos = transform.position;
            pos.y = Mathf.Lerp(pos.y, _height, 1f - Mathf.Exp(-12f * dt));
            transform.position = pos;

            ApplyPitch();
        }

        void ApplyPitch()
        {
            float t = Mathf.InverseLerp(minHeight, maxHeight, transform.position.y);
            float pitch = Mathf.Lerp(pitchAtMinHeight, pitchAtMaxHeight, t);
            transform.rotation = Quaternion.Euler(pitch, _yaw, 0f);
        }

        /// <summary>Snap the camera above a world position, keeping the current framing.</summary>
        public void FocusOn(Vector3 worldPos)
        {
            float back = _height / Mathf.Tan(transform.eulerAngles.x * Mathf.Deg2Rad);
            var flatRot = Quaternion.Euler(0f, _yaw, 0f);
            var target = worldPos - flatRot * Vector3.forward * back;
            transform.position = new Vector3(target.x, _height, target.z);
        }
    }
}
