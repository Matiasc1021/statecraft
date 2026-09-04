using System.Collections.Generic;
using UnityEngine;
using Statecraft.Core;
using Statecraft.Data;
using Statecraft.Economy;
using Statecraft.Entities;
using Statecraft.Factions;

namespace Statecraft.Player
{
    /// <summary>
    /// Mouse control: click to select, drag to box-select, right-click to order,
    /// and a placement mode driven by the build menu.
    ///
    /// Kept on the legacy Input API on purpose — it is two lines instead of an
    /// InputActions asset, and swapping to the Input System later touches only this
    /// file and the camera. Project settings ship with "Both" enabled.
    /// </summary>
    public class SelectionController : MonoBehaviour
    {
        public static SelectionController Instance { get; private set; }

        [Header("Box select")]
        public float dragThresholdPixels = 8f;

        public readonly List<Entity> selected = new List<Entity>();

        /// <summary>Non-null while the player is placing a building from the build menu.</summary>
        public string pendingBuildingId;

        Camera _cam;
        Vector2 _dragStart;
        bool    _dragging;
        GameObject _ghost;

        public bool IsPlacing => !string.IsNullOrEmpty(pendingBuildingId);

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            if (IsPlacing) { UpdatePlacement(); return; }

            if (Input.GetKeyDown(KeyCode.Escape)) ClearSelection();

            HandleLeftMouse();
            HandleRightMouse();
        }

        // ── Placement ─────────────────────────────────────────────────────

        public void BeginPlacement(string buildingId)
        {
            CancelPlacement();
            pendingBuildingId = buildingId;

            var def = Content.Building(buildingId);
            if (def == null) { pendingBuildingId = null; return; }

            _ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ghost.name = "BuildGhost";
            Destroy(_ghost.GetComponent<Collider>());
            _ghost.transform.localScale = new Vector3(def.footprint.x, 3f, def.footprint.y);
            MaterialFactory.Paint(_ghost, new Color(0.4f, 1f, 0.6f, 0.5f), unlit: true);
        }

        public void CancelPlacement()
        {
            pendingBuildingId = null;
            if (_ghost != null) Destroy(_ghost);
            _ghost = null;
        }

        void UpdatePlacement()
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)) { CancelPlacement(); return; }
            if (!TryGroundPoint(Input.mousePosition, out var point)) return;

            if (_ghost != null) _ghost.transform.position = point + Vector3.up * 1.5f;

            if (Input.GetMouseButtonDown(0))
            {
                var reg = FactionRegistry.Instance;
                var f   = reg != null ? reg.Local : null;
                if (f != null) EconomySystem.Instance?.TryPlaceBuilding(f, pendingBuildingId, point);
                if (!Input.GetKey(KeyCode.LeftShift)) CancelPlacement();
            }
        }

        // ── Selection ─────────────────────────────────────────────────────

        void HandleLeftMouse()
        {
            if (Input.GetMouseButtonDown(0))
            {
                _dragStart = Input.mousePosition;
                _dragging  = false;
            }

            if (Input.GetMouseButton(0) && !_dragging)
            {
                if (((Vector2)Input.mousePosition - _dragStart).magnitude > dragThresholdPixels)
                    _dragging = true;
            }

            if (!Input.GetMouseButtonUp(0)) return;

            bool additive = Input.GetKey(KeyCode.LeftShift);
            if (!additive) ClearSelection();

            if (_dragging) BoxSelect(_dragStart, Input.mousePosition);
            else           ClickSelect(Input.mousePosition);

            _dragging = false;
        }

        void ClickSelect(Vector2 screenPos)
        {
            var ray = _cam.ScreenPointToRay(screenPos);
            if (!Physics.Raycast(ray, out var hit, 5000f)) return;

            var e = hit.collider.GetComponentInParent<Entity>();
            if (e != null && e.selectable) { Add(e); return; }

            var node = hit.collider.GetComponentInParent<ResourceNode>();
            if (node != null) HudSelection.selectedNode = node;
        }

        void BoxSelect(Vector2 a, Vector2 b)
        {
            var reg = FactionRegistry.Instance;
            if (reg == null) return;

            var rect = ScreenRect(a, b);
            var units = reg.UnitsOf(reg.localFactionId);
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u == null || !u.IsAlive || !u.selectable) continue;
                var sp = _cam.WorldToScreenPoint(u.transform.position);
                if (sp.z < 0f) continue;
                if (rect.Contains(new Vector2(sp.x, sp.y))) Add(u);
            }
        }

        public static Rect ScreenRect(Vector2 a, Vector2 b)
            => new Rect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

        void Add(Entity e)
        {
            if (e == null || selected.Contains(e)) return;
            selected.Add(e);
            e.SetSelected(true);
            HudSelection.selectedNode = null;
        }

        public void ClearSelection()
        {
            for (int i = 0; i < selected.Count; i++) if (selected[i] != null) selected[i].SetSelected(false);
            selected.Clear();
        }

        public void SelectOnly(Entity e)
        {
            ClearSelection();
            Add(e);
        }

        // ── Orders ────────────────────────────────────────────────────────

        void HandleRightMouse()
        {
            if (!Input.GetMouseButtonDown(1) || selected.Count == 0) return;

            var ray = _cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit, 5000f)) return;

            var node = hit.collider.GetComponentInParent<ResourceNode>();
            var reg  = FactionRegistry.Instance;

            for (int i = 0; i < selected.Count; i++)
            {
                var e = selected[i];
                if (e == null || reg == null || e.factionId != reg.localFactionId) continue;

                if (e is WorkerUnit w)
                {
                    if (node != null && !node.Depleted) w.AssignTo(node);
                    else                                w.ManualMove(ScatterAround(hit.point, i));
                }
                else if (e is UnitEntity u)
                {
                    u.MoveTo(ScatterAround(hit.point, i));
                }
                else if (e is BuildingEntity b)
                {
                    b.rallyPoint = hit.point;
                    EventLog.Post(LogChannel.System, $"{b.DisplayName} rally point set.", b.factionId);
                }
            }
        }

        static Vector3 ScatterAround(Vector3 centre, int index)
        {
            if (index == 0) return centre;
            float ring   = 1.6f + (index / 8) * 1.8f;
            float angle  = (index % 8) * (Mathf.PI * 2f / 8f);
            return centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ring;
        }

        bool TryGroundPoint(Vector2 screenPos, out Vector3 point)
        {
            point = Vector3.zero;
            var ray = _cam.ScreenPointToRay(screenPos);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out float enter)) return false;
            point = ray.GetPoint(enter);
            return true;
        }

        // ── Drag rectangle ────────────────────────────────────────────────

        void OnGUI()
        {
            if (!_dragging) return;

            var a = _dragStart;
            var b = (Vector2)Input.mousePosition;
            var rect = new Rect(Mathf.Min(a.x, b.x), Screen.height - Mathf.Max(a.y, b.y),
                                Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

            var fill = new Color(0.35f, 1f, 0.55f, 0.12f);
            var line = new Color(0.35f, 1f, 0.55f, 0.9f);
            HudPaint.Fill(rect, fill);
            HudPaint.Frame(rect, line, 1f);
        }
    }

    /// <summary>Anything the HUD needs to know about the current click that is not an Entity.</summary>
    public static class HudSelection
    {
        public static ResourceNode selectedNode;
    }
}
