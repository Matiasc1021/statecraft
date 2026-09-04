using UnityEngine;
using Statecraft.Data;

namespace Statecraft.Entities
{
    /// <summary>
    /// A finite deposit on the map. Workers walk to it, gather, and carry back.
    /// Materials deposits are common; Data sites are rare and are what the mid-game
    /// map fight is actually about.
    /// </summary>
    public class ResourceNode : MonoBehaviour
    {
        public ResourceKind kind = ResourceKind.Materials;
        public float amountRemaining = 1500f;
        public float initialAmount   = 1500f;

        /// <summary>How many workers can gather here at once.</summary>
        public int gatherSlots = 4;
        int _claimed;

        public bool Depleted => amountRemaining <= 0.01f;
        public bool HasFreeSlot => _claimed < gatherSlots;

        public void Claim()   { _claimed++; }
        public void Release() { _claimed = Mathf.Max(0, _claimed - 1); }

        void Awake()
        {
            if (initialAmount <= 0f) initialAmount = amountRemaining;
        }

        /// <summary>Take up to <paramref name="amount"/> out of the deposit.</summary>
        public float Extract(float amount)
        {
            float taken = Mathf.Min(amountRemaining, amount);
            amountRemaining -= taken;
            if (Depleted) OnDepleted();
            return taken;
        }

        void OnDepleted()
        {
            var r = GetComponentInChildren<Renderer>();
            if (r != null)
            {
                var m = new Material(r.sharedMaterial);
                m.color = new Color(0.25f, 0.25f, 0.27f);
                r.sharedMaterial = m;
            }
        }

        public string DisplayName => $"{ResourceBundle.Label(kind)} Deposit";
        public string StatusLine  => $"{amountRemaining:0} / {initialAmount:0} remaining";
    }
}
