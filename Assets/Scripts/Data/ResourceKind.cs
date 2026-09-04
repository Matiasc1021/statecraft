using System;
using UnityEngine;

namespace Statecraft.Data
{
    /// <summary>
    /// The five tradable resources of the modern economy.
    ///
    /// Capital    — money. Earned by selling goods and running trade routes. Can go negative (debt, accrues interest).
    /// Energy     — a FLOW, not a stockpile in practice. Demand above supply causes brownouts (global output penalty).
    /// Materials  — raw extraction. The base of every physical thing.
    /// Components — manufactured goods. Materials + Energy -> Components. The main export good.
    /// Data       — the strategic resource. Feeds research, espionage ops, and automation. Scarce and highly tradable.
    /// </summary>
    public enum ResourceKind
    {
        Capital    = 0,
        Energy     = 1,
        Materials  = 2,
        Components = 3,
        Data       = 4,
    }

    /// <summary>
    /// A five-slot resource vector. Used for stockpiles, costs, incomes and trades alike.
    /// Value type on purpose — cheap to copy, no aliasing bugs when passing costs around.
    /// </summary>
    [Serializable]
    public struct ResourceBundle
    {
        public float capital;
        public float energy;
        public float materials;
        public float components;
        public float data;

        public const int KindCount = 5;

        public static readonly ResourceKind[] AllKinds =
        {
            ResourceKind.Capital,
            ResourceKind.Energy,
            ResourceKind.Materials,
            ResourceKind.Components,
            ResourceKind.Data,
        };

        public ResourceBundle(float capital, float energy, float materials, float components, float data)
        {
            this.capital    = capital;
            this.energy     = energy;
            this.materials  = materials;
            this.components = components;
            this.data       = data;
        }

        // ── Indexed access ────────────────────────────────────────────

        public float Get(ResourceKind k)
        {
            switch (k)
            {
                case ResourceKind.Capital:    return capital;
                case ResourceKind.Energy:     return energy;
                case ResourceKind.Materials:  return materials;
                case ResourceKind.Components: return components;
                case ResourceKind.Data:       return data;
                default:                      return 0f;
            }
        }

        public void Set(ResourceKind k, float v)
        {
            switch (k)
            {
                case ResourceKind.Capital:    capital    = v; break;
                case ResourceKind.Energy:     energy     = v; break;
                case ResourceKind.Materials:  materials  = v; break;
                case ResourceKind.Components: components = v; break;
                case ResourceKind.Data:       data       = v; break;
            }
        }

        public void Add(ResourceKind k, float v) => Set(k, Get(k) + v);

        // ── Arithmetic ────────────────────────────────────────────────

        public static ResourceBundle operator +(ResourceBundle a, ResourceBundle b)
            => new ResourceBundle(a.capital + b.capital, a.energy + b.energy, a.materials + b.materials,
                                  a.components + b.components, a.data + b.data);

        public static ResourceBundle operator -(ResourceBundle a, ResourceBundle b)
            => new ResourceBundle(a.capital - b.capital, a.energy - b.energy, a.materials - b.materials,
                                  a.components - b.components, a.data - b.data);

        public static ResourceBundle operator *(ResourceBundle a, float s)
            => new ResourceBundle(a.capital * s, a.energy * s, a.materials * s, a.components * s, a.data * s);

        /// <summary>True when this bundle can pay <paramref name="cost"/> in full.</summary>
        public bool Covers(ResourceBundle cost)
            => capital    >= cost.capital
            && energy     >= cost.energy
            && materials  >= cost.materials
            && components >= cost.components
            && data       >= cost.data;

        /// <summary>The single resource this bundle is shortest on relative to a cost, or null if it covers it.</summary>
        public ResourceKind? FirstShortfall(ResourceBundle cost)
        {
            for (int i = 0; i < AllKinds.Length; i++)
            {
                var k = AllKinds[i];
                if (Get(k) < cost.Get(k)) return k;
            }
            return null;
        }

        public static ResourceBundle Zero => new ResourceBundle(0, 0, 0, 0, 0);

        public override string ToString()
            => $"C{capital:0} E{energy:0} M{materials:0} P{components:0} D{data:0}";

        public static string Label(ResourceKind k)
        {
            switch (k)
            {
                case ResourceKind.Capital:    return "Capital";
                case ResourceKind.Energy:     return "Energy";
                case ResourceKind.Materials:  return "Materials";
                case ResourceKind.Components: return "Components";
                case ResourceKind.Data:       return "Data";
                default:                      return k.ToString();
            }
        }

        public static Color TintOf(ResourceKind k)
        {
            switch (k)
            {
                case ResourceKind.Capital:    return new Color(0.95f, 0.82f, 0.35f);
                case ResourceKind.Energy:     return new Color(0.40f, 0.80f, 1.00f);
                case ResourceKind.Materials:  return new Color(0.78f, 0.55f, 0.38f);
                case ResourceKind.Components: return new Color(0.70f, 0.75f, 0.82f);
                case ResourceKind.Data:       return new Color(0.55f, 0.95f, 0.60f);
                default:                      return Color.white;
            }
        }
    }
}
