using UnityEngine;
using Statecraft.Factions;

namespace Statecraft.Entities
{
    /// <summary>
    /// Base for anything on the map that belongs to a faction and can be selected,
    /// damaged or destroyed. Registers itself with the FactionRegistry so systems
    /// never have to call FindObjectsOfType in a hot loop.
    /// </summary>
    public abstract class Entity : MonoBehaviour
    {
        [Header("Ownership")]
        public int factionId;

        [Header("Health")]
        public float maxHp = 100f;
        public float hp    = 100f;

        public bool selectable = true;
        public bool IsAlive => hp > 0f;

        public Faction Owner => FactionRegistry.Instance != null ? FactionRegistry.Instance.Get(factionId) : null;

        public abstract string DisplayName { get; }
        public virtual string StatusLine => $"{hp:0}/{maxHp:0} HP";

        Renderer[] _renderers;
        GameObject _selectionRing;

        protected virtual void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>();
            if (hp <= 0f) hp = maxHp;
        }

        protected virtual void OnEnable()
        {
            FactionRegistry.Instance?.Register(this);
        }

        protected virtual void OnDisable()
        {
            FactionRegistry.Instance?.Unregister(this);
        }

        // ── Damage ────────────────────────────────────────────────────────

        public void Damage(float amount, int sourceFactionId = -1)
        {
            if (!IsAlive) return;
            hp -= amount;
            if (hp <= 0f)
            {
                hp = 0f;
                Die(sourceFactionId);
            }
        }

        protected virtual void Die(int killerFactionId)
        {
            FactionRegistry.Instance?.Unregister(this);
            Destroy(gameObject);
        }

        // ── Selection visuals ─────────────────────────────────────────────

        public virtual void SetSelected(bool on)
        {
            if (on && _selectionRing == null) _selectionRing = BuildSelectionRing();
            if (_selectionRing != null) _selectionRing.SetActive(on);
        }

        GameObject BuildSelectionRing()
        {
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(ring.GetComponent<Collider>());
            ring.name = "SelectionRing";
            ring.transform.SetParent(transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.05f, 0f);

            var b = LocalBounds();
            float r = Mathf.Max(b.extents.x, b.extents.z) * 1.25f + 0.4f;
            ring.transform.localScale = new Vector3(r * 2f, 0.02f, r * 2f);

            var mat = new Material(MaterialFactory.UnlitShader) { color = new Color(0.35f, 1f, 0.55f, 1f) };
            ring.GetComponent<Renderer>().sharedMaterial = mat;
            return ring;
        }

        Bounds LocalBounds()
        {
            if (_renderers == null || _renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.one);
            var b = new Bounds(transform.position, Vector3.zero);
            foreach (var r in _renderers) if (r) b.Encapsulate(r.bounds);
            b.center -= transform.position;
            return b;
        }

        /// <summary>Paint every renderer with the owning faction's colour.</summary>
        public void ApplyFactionColour(Color c)
        {
            if (_renderers == null) _renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in _renderers)
            {
                if (r == null || r.gameObject.name == "SelectionRing") continue;
                var m = new Material(r.sharedMaterial);
                m.color = c;
                r.sharedMaterial = m;
            }
        }
    }
}
