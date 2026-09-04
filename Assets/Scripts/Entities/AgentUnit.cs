using UnityEngine;
using Statecraft.Data;

namespace Statecraft.Entities
{
    /// <summary>
    /// A field agent. Unlike every other unit, an agent's important state is not on
    /// the map — it is COVER. Cover is the resource espionage actually spends.
    ///
    ///   • Every operation burns cover.
    ///   • Cover regenerates slowly while the agent is idle at home.
    ///   • Low cover raises the chance the target detects the next op AND the chance
    ///     the agent is burned outright.
    ///
    /// So the espionage economy is: convert Capital + Data into operations, and
    /// manage a pool of agents whose usefulness degrades the more you use them.
    /// </summary>
    public class AgentUnit : UnitEntity
    {
        public enum AgentState { Idle, Deployed, Running, Burned }

        [Header("Tradecraft")]
        public float skill = 50f;
        public float cover = 75f;          // 0..100
        public float coverMax = 100f;
        public float coverRegenPerSecond = 1.1f;

        public AgentState state = AgentState.Idle;
        /// <summary>Faction this agent is currently posted against. -1 = at home.</summary>
        public int deployedAgainst = -1;
        public string runningOperationId;
        public float  operationSecondsLeft;

        public bool IsAvailable => state == AgentState.Idle || state == AgentState.Deployed;

        public override string DisplayName => "Field Agent";

        public override string StatusLine
        {
            get
            {
                switch (state)
                {
                    case AgentState.Running:  return $"Running {Content.Operation(runningOperationId)?.displayName} — {operationSecondsLeft:0}s";
                    case AgentState.Deployed: return $"Posted abroad — cover {cover:0}";
                    case AgentState.Burned:   return "BURNED";
                    default:                  return $"Idle — cover {cover:0}/{coverMax:0}";
                }
            }
        }

        public override void Configure(UnitDef d, int owningFactionId)
        {
            base.Configure(d, owningFactionId);
            skill = d.agentSkill;
            cover = d.agentCoverBase;
            var owner = Owner;
            if (owner != null) skill += owner.AgentSkillBonus;
        }

        /// <summary>Cover regeneration. Called from the economy tick, not Update.</summary>
        public void TickCover(float dt)
        {
            if (state == AgentState.Running || state == AgentState.Burned) return;
            float rate = coverRegenPerSecond * (state == AgentState.Deployed ? 0.45f : 1f);
            cover = Mathf.Min(coverMax, cover + rate * dt);
        }

        public void BurnCover(float amount)
        {
            var owner = Owner;
            float mult = owner != null ? owner.CoverDecayMultiplier : 1f;
            cover = Mathf.Max(0f, cover - amount * mult);
        }
    }
}
