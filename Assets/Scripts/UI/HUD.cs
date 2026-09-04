using System.Collections.Generic;
using UnityEngine;
using Statecraft.Core;
using Statecraft.Data;
using Statecraft.Economy;
using Statecraft.Entities;
using Statecraft.Espionage;
using Statecraft.Factions;

namespace Statecraft.Player
{
    /// <summary>
    /// ── HUD ─────────────────────────────────────────────────────────────
    /// Top strip     — stockpiles, net rate per second, labour, energy, age, security.
    /// Left panel    — what is selected and what it can do.
    /// Right panel   — the four screens that make this game what it is:
    ///                 MARKET (prices you can move), INTEL (what you think you know),
    ///                 OPERATIONS (what you can do about it), LOG.
    ///
    /// The rule this HUD follows: never show the player a number they could not have
    /// earned. Rival figures come from IntelSystem, complete with a confidence label,
    /// and they are wrong in proportion to how old they are.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        enum Screen { Market, Intel, Operations, Log }
        Screen _screen = Screen.Market;

        const float TopBarHeight  = 74f;
        const float LeftWidth     = 300f;
        const float RightWidth    = 380f;

        Vector2 _leftScroll, _rightScroll;

        // Operations screen selection
        AgentUnit _opAgent;
        string    _opId = "steal_data";
        int       _opTargetId = -1;

        // Market screen input
        float _tradeSize = 100f;

        FactionRegistry Reg => FactionRegistry.Instance;
        Faction Me => Reg != null ? Reg.Local : null;

        void OnGUI()
        {
            var me = Me;
            if (me == null) return;

            DrawTopBar(me);
            DrawLeftPanel(me);
            DrawRightPanel(me);
            DrawHints();
        }

        // ── Top bar ───────────────────────────────────────────────────────

        void DrawTopBar(Faction me)
        {
            var r = new Rect(0f, 0f, UnityEngine.Screen.width, TopBarHeight);
            HudPaint.Panel(r);
            GUILayout.BeginArea(new Rect(10f, 6f, UnityEngine.Screen.width - 20f, TopBarHeight - 10f));

            GUILayout.BeginHorizontal();

            // Resources
            for (int i = 0; i < ResourceBundle.AllKinds.Length; i++)
            {
                var k    = ResourceBundle.AllKinds[i];
                var tint = ResourceBundle.TintOf(k);
                float amount = me.stock.Get(k);
                float rate   = me.netPerSecond.Get(k);

                GUILayout.BeginVertical(GUILayout.Width(118f));
                GUILayout.Label(HudPaint.Tinted(ResourceBundle.Label(k), tint), HudPaint.Small);

                string rateText = HudPaint.Tinted(HudPaint.Signed(rate) + "/s",
                    rate >= 0f ? new Color(0.55f, 0.95f, 0.6f) : new Color(1f, 0.5f, 0.45f));
                GUILayout.Label($"<b>{amount:0}</b>  {rateText}", HudPaint.Label);
                GUILayout.EndVertical();
            }

            GUILayout.Space(12f);

            // Labour & housing — the two constraints that actually bite.
            GUILayout.BeginVertical(GUILayout.Width(150f));
            float eff = me.LabourEfficiency;
            var effTint = eff > 0.95f ? new Color(0.55f, 0.95f, 0.6f)
                        : eff > 0.7f  ? new Color(0.95f, 0.85f, 0.45f)
                                      : new Color(1f, 0.5f, 0.45f);
            GUILayout.Label($"Labour {HudPaint.Tinted($"{eff * 100f:0}%", effTint)}", HudPaint.Label);
            GUILayout.Label($"Pop {me.population}/{me.housingCapacity}   Jobs {me.labourDemand}", HudPaint.Small);
            GUILayout.EndVertical();

            // Age
            GUILayout.BeginVertical(GUILayout.Width(210f));
            GUILayout.Label($"<b>{me.Age.displayName}</b>", HudPaint.Label);
            if (me.advancingAge)
            {
                var bar = GUILayoutUtility.GetRect(190f, 8f);
                HudPaint.Bar(bar, me.ageProgress, new Color(0.95f, 0.82f, 0.35f));
            }
            else
            {
                GUILayout.Label(me.Age.flavour, HudPaint.Small);
            }
            GUILayout.EndVertical();

            // Security & ops pressure
            GUILayout.BeginVertical(GUILayout.Width(150f));
            GUILayout.Label($"Security <b>{me.Security:0}</b>", HudPaint.Label);
            GUILayout.Label($"Ops against us {me.opsRunAgainstMe}  ·  caught {me.opsCaught}", HudPaint.Small);
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            // Clock
            GUILayout.BeginVertical(GUILayout.Width(160f));
            var clock = SimClock.Instance;
            if (clock != null)
            {
                int mins = Mathf.FloorToInt(clock.Elapsed / 60f);
                int secs = Mathf.FloorToInt(clock.Elapsed % 60f);
                GUILayout.Label($"<b>{mins:00}:{secs:00}</b>   x{clock.speedMultiplier:0.##}", HudPaint.Label);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(clock.paused ? "Play" : "Pause", HudPaint.Button, GUILayout.Width(52f)))
                    clock.paused = !clock.paused;
                if (GUILayout.Button("-", HudPaint.Button, GUILayout.Width(24f)))
                    clock.speedMultiplier = Mathf.Max(0.25f, clock.speedMultiplier - 0.25f);
                if (GUILayout.Button("+", HudPaint.Button, GUILayout.Width(24f)))
                    clock.speedMultiplier = Mathf.Min(4f, clock.speedMultiplier + 0.25f);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        // ── Left panel: selection ─────────────────────────────────────────

        void DrawLeftPanel(Faction me)
        {
            var r = new Rect(0f, TopBarHeight, LeftWidth, UnityEngine.Screen.height - TopBarHeight);
            HudPaint.Panel(r);
            GUILayout.BeginArea(new Rect(10f, TopBarHeight + 8f, LeftWidth - 20f, UnityEngine.Screen.height - TopBarHeight - 16f));
            _leftScroll = GUILayout.BeginScrollView(_leftScroll);

            var sel = SelectionController.Instance;

            if (sel != null && sel.IsPlacing)
            {
                GUILayout.Label(HudPaint.Tinted("PLACING", new Color(0.4f, 1f, 0.6f)), HudPaint.Header);
                GUILayout.Label($"{Content.Building(sel.pendingBuildingId)?.displayName}\nLeft click to place, Esc to cancel.\nHold Shift to place several.", HudPaint.Small);
                GUILayout.Space(8f);
            }

            if (HudSelection.selectedNode != null)
            {
                var n = HudSelection.selectedNode;
                GUILayout.Label(n.DisplayName, HudPaint.Header);
                GUILayout.Label(n.StatusLine, HudPaint.Small);
                var bar = GUILayoutUtility.GetRect(240f, 8f);
                HudPaint.Bar(bar, n.initialAmount <= 0f ? 0f : n.amountRemaining / n.initialAmount, ResourceBundle.TintOf(n.kind));
                GUILayout.Space(10f);
            }

            if (sel != null && sel.selected.Count > 0) DrawSelectionActions(me, sel);
            else                                       DrawIdleActions(me);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawSelectionActions(Faction me, SelectionController sel)
        {
            // Clean out anything that died since last frame.
            sel.selected.RemoveAll(e => e == null || !e.IsAlive);
            if (sel.selected.Count == 0) { DrawIdleActions(me); return; }

            if (sel.selected.Count > 1)
            {
                GUILayout.Label($"{sel.selected.Count} selected", HudPaint.Header);
                int workers = 0, agents = 0, others = 0;
                foreach (var e in sel.selected)
                {
                    if (e is WorkerUnit) workers++;
                    else if (e is AgentUnit) agents++;
                    else others++;
                }
                GUILayout.Label($"{workers} workers · {agents} agents · {others} other", HudPaint.Small);
                GUILayout.Space(8f);
            }

            var primary = sel.selected[0];
            GUILayout.Label(primary.DisplayName, HudPaint.Header);
            GUILayout.Label(primary.StatusLine, HudPaint.Small);

            var hpBar = GUILayoutUtility.GetRect(240f, 6f);
            HudPaint.Bar(hpBar, primary.maxHp <= 0f ? 0f : primary.hp / primary.maxHp, new Color(0.85f, 0.35f, 0.35f));
            GUILayout.Space(8f);

            if (primary.factionId != me.id)
            {
                GUILayout.Label("Not yours. You can see it, not command it.", HudPaint.Small);
                return;
            }

            if (primary is BuildingEntity b) DrawBuildingActions(me, b);
            if (primary is WorkerUnit)       DrawBuildMenu(me);
            if (primary is AgentUnit agent)
            {
                GUILayout.Space(6f);
                if (GUILayout.Button("Send to Operations screen", HudPaint.Button))
                {
                    _opAgent = agent;
                    _screen  = Screen.Operations;
                }
            }
        }

        void DrawBuildingActions(Faction me, BuildingEntity b)
        {
            if (b.underConstruction)
            {
                var bar = GUILayoutUtility.GetRect(240f, 8f);
                HudPaint.Bar(bar, b.buildProgress, new Color(0.4f, 0.8f, 1f));
                return;
            }

            if (b.HasRecipe)
            {
                GUILayout.Label("Conversion", HudPaint.Header);
                GUILayout.Label(DescribeRecipe(b.def.recipe), HudPaint.Small);
                var bar = GUILayoutUtility.GetRect(240f, 8f);
                HudPaint.Bar(bar, b.lastEfficiency, b.IsSabotaged ? new Color(1f, 0.4f, 0.4f) : new Color(0.5f, 0.9f, 0.6f));
                GUILayout.Label(b.IsSabotaged ? "OFFLINE — sabotage" : $"Running at {b.lastEfficiency * 100f:0}%", HudPaint.Small);
                GUILayout.Space(8f);
            }

            if (b.def.trains != null && b.def.trains.Count > 0)
            {
                GUILayout.Label("Train", HudPaint.Header);
                foreach (var unitId in b.def.trains)
                {
                    var u = Content.Unit(unitId);
                    if (u == null) continue;
                    bool locked = me.ageIndex < u.minAge;
                    GUI.enabled = !locked;
                    if (GUILayout.Button($"{u.displayName}   <i>{CostText(u.cost)}</i>" + (locked ? "  (locked)" : ""), HudPaint.Button))
                        b.TryQueue(unitId);
                    GUI.enabled = true;
                }

                if (b.queue.Count > 0)
                {
                    GUILayout.Space(4f);
                    var front = b.queue[0];
                    GUILayout.Label($"Queue: {b.queue.Count}", HudPaint.Small);
                    var bar = GUILayoutUtility.GetRect(240f, 8f);
                    HudPaint.Bar(bar, front.progress, new Color(0.95f, 0.82f, 0.35f));
                    if (GUILayout.Button("Cancel front (full refund)", HudPaint.Button)) b.CancelFront();
                }
                GUILayout.Space(8f);
            }

            if (b.def.role == BuildingRole.Capitol) DrawAgeAndResearch(me);
            if (b.def.role == BuildingRole.Academy) DrawResearchList(me);
            if (b.def.role == BuildingRole.Exchange) DrawTradeRoutes(me);
        }

        void DrawIdleActions(Faction me)
        {
            GUILayout.Label(me.displayName, HudPaint.Header);
            GUILayout.Label(me.civ != null ? me.civ.doctrine : "", HudPaint.Small);
            GUILayout.Space(8f);
            DrawAgeAndResearch(me);
            GUILayout.Space(8f);
            DrawBuildMenu(me);
        }

        void DrawAgeAndResearch(Faction me)
        {
            var eco = EconomySystem.Instance;
            if (eco == null) return;

            GUILayout.Label("Age", HudPaint.Header);
            if (me.ageIndex >= Content.MaxAge)
            {
                GUILayout.Label("Final age reached.", HudPaint.Small);
            }
            else if (me.advancingAge)
            {
                var bar = GUILayoutUtility.GetRect(240f, 8f);
                HudPaint.Bar(bar, me.ageProgress, new Color(0.95f, 0.82f, 0.35f));
                GUILayout.Label($"Advancing to {Content.Age(me.ageIndex + 1).displayName}", HudPaint.Small);
            }
            else
            {
                var next = Content.Age(me.ageIndex + 1);
                string why;
                bool can = eco.CanAdvanceAge(me, out why);
                GUI.enabled = can;
                if (GUILayout.Button($"Advance to {next.displayName}\n<i>{CostText(next.advanceCost)}</i>", HudPaint.Button, GUILayout.Height(36f)))
                    eco.TryAdvanceAge(me);
                GUI.enabled = true;
                if (!can) GUILayout.Label(why, HudPaint.Small);
            }

            if (!string.IsNullOrEmpty(me.researchingTechId))
            {
                GUILayout.Space(4f);
                GUILayout.Label($"Researching {Content.Tech(me.researchingTechId)?.displayName}", HudPaint.Small);
                var bar = GUILayoutUtility.GetRect(240f, 8f);
                HudPaint.Bar(bar, me.researchProgress, new Color(0.55f, 0.95f, 0.6f));
            }
        }

        void DrawResearchList(Faction me)
        {
            var eco = EconomySystem.Instance;
            if (eco == null) return;

            GUILayout.Label("Research", HudPaint.Header);
            foreach (var kv in Content.Techs)
            {
                var t = kv.Value;
                if (me.HasTech(t.id))
                {
                    GUILayout.Label(HudPaint.Tinted("✓ " + t.displayName, new Color(0.55f, 0.95f, 0.6f)), HudPaint.Small);
                    continue;
                }
                string why;
                bool can = eco.CanResearch(me, t, out why);
                GUI.enabled = can;
                if (GUILayout.Button($"{t.displayName}\n<i>{t.description}</i>\n<i>{CostText(t.cost)}</i>", HudPaint.Button, GUILayout.Height(48f)))
                    eco.TryResearch(me, t.id);
                GUI.enabled = true;
            }
        }

        void DrawBuildMenu(Faction me)
        {
            GUILayout.Label("Build", HudPaint.Header);
            var sel = SelectionController.Instance;

            foreach (var kv in Content.Buildings)
            {
                var d = kv.Value;
                if (d.role == BuildingRole.Capitol) continue;         // one per player, placed at start

                bool ageLocked = me.ageIndex < d.minAge;
                bool tooPoor   = !me.CanAfford(d.cost);

                GUI.enabled = !ageLocked;
                string label = $"{d.displayName}   <i>{CostText(d.cost)}</i>";
                if (ageLocked) label += $"\n<i>needs {Content.Age(d.minAge).displayName}</i>";
                else if (tooPoor) label = HudPaint.Tinted(label, new Color(0.9f, 0.6f, 0.55f));

                if (GUILayout.Button(label, HudPaint.Button, GUILayout.Height(ageLocked ? 34f : 24f)))
                    sel?.BeginPlacement(d.id);
                GUI.enabled = true;
            }
        }

        void DrawTradeRoutes(Faction me)
        {
            var routes = TradeRouteSystem.Instance;
            if (routes == null) return;

            GUILayout.Label($"Trade routes  {routes.UsedSlots(me.id)}/{routes.SlotsFor(me.id)}", HudPaint.Header);
            GUILayout.Label("A route sells a resource continuously at the market price. Rivals can cut them.", HudPaint.Small);

            foreach (var r in routes.RoutesOf(me.id))
            {
                var tint = r.IsDisrupted ? new Color(1f, 0.45f, 0.4f) : ResourceBundle.TintOf(r.export);
                GUILayout.Label(HudPaint.Tinted(
                    $"{ResourceBundle.Label(r.export)} · {r.throughputPerSecond * r.efficiency:0.0}/s · earned {r.capitalEarnedTotal:0}"
                    + (r.IsDisrupted ? $"  DISRUPTED {r.disruptedSeconds:0}s" : ""), tint), HudPaint.Small);
            }

            GUILayout.Space(4f);
            if (routes.UsedSlots(me.id) < routes.SlotsFor(me.id))
            {
                for (int i = 0; i < ResourceBundle.AllKinds.Length; i++)
                {
                    var k = ResourceBundle.AllKinds[i];
                    if (k == ResourceKind.Capital) continue;
                    if (GUILayout.Button($"Open route: export {ResourceBundle.Label(k)}", HudPaint.Button))
                        routes.TryOpenRoute(me, k, 1.5f);
                }
            }
        }

        // ── Right panel ───────────────────────────────────────────────────

        void DrawRightPanel(Faction me)
        {
            float x = UnityEngine.Screen.width - RightWidth;
            var r = new Rect(x, TopBarHeight, RightWidth, UnityEngine.Screen.height - TopBarHeight);
            HudPaint.Panel(r);

            GUILayout.BeginArea(new Rect(x + 10f, TopBarHeight + 8f, RightWidth - 20f, UnityEngine.Screen.height - TopBarHeight - 16f));

            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(_screen == Screen.Market,     "Market",     HudPaint.Button)) _screen = Screen.Market;
            if (GUILayout.Toggle(_screen == Screen.Intel,      "Intel",      HudPaint.Button)) _screen = Screen.Intel;
            if (GUILayout.Toggle(_screen == Screen.Operations, "Operations", HudPaint.Button)) _screen = Screen.Operations;
            if (GUILayout.Toggle(_screen == Screen.Log,        "Log",        HudPaint.Button)) _screen = Screen.Log;
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);

            _rightScroll = GUILayout.BeginScrollView(_rightScroll);
            switch (_screen)
            {
                case Screen.Market:     DrawMarket(me);     break;
                case Screen.Intel:      DrawIntel(me);      break;
                case Screen.Operations: DrawOperations(me); break;
                case Screen.Log:        DrawLog(me);        break;
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawMarket(Faction me)
        {
            var market = MarketSystem.Instance;
            if (market == null) return;

            GUILayout.Label("Commodity market", HudPaint.Header);
            GUILayout.Label($"Your spread: {me.MarketSpread * 100f:0.0}% per trade. Large orders move the price against you.", HudPaint.Small);
            GUILayout.Space(4f);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Order size", HudPaint.Small, GUILayout.Width(64f));
            _tradeSize = GUILayout.HorizontalSlider(_tradeSize, 20f, 500f, GUILayout.Width(160f));
            GUILayout.Label($"{_tradeSize:0}", HudPaint.Small, GUILayout.Width(40f));
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);

            for (int i = 0; i < ResourceBundle.AllKinds.Length; i++)
            {
                var k = ResourceBundle.AllKinds[i];
                if (!market.IsTradable(k)) continue;

                float price = market.PriceOf(k);
                float dev   = market.DeviationOf(k);
                var tint    = ResourceBundle.TintOf(k);
                var devTint = dev > 0.03f ? new Color(0.55f, 0.95f, 0.6f)
                            : dev < -0.03f ? new Color(1f, 0.5f, 0.45f)
                                           : new Color(0.75f, 0.75f, 0.78f);

                GUILayout.Label($"{HudPaint.Tinted(ResourceBundle.Label(k), tint)}   <b>{price:0.00}</b>   {HudPaint.Tinted(HudPaint.Signed(dev * 100f, "0") + "%", devTint)}", HudPaint.Label);

                var spark = GUILayoutUtility.GetRect(RightWidth - 40f, 26f);
                HudPaint.Sparkline(spark, market.HistoryOf(k), tint, market.BasePriceOf(k));

                GUILayout.BeginHorizontal();
                if (GUILayout.Button($"Buy {_tradeSize:0}\n<i>{market.QuoteBuy(me, k, _tradeSize):0} C</i>", HudPaint.Button, GUILayout.Height(30f)))
                    market.Buy(me, k, _tradeSize);
                if (GUILayout.Button($"Sell {_tradeSize:0}\n<i>{market.QuoteSell(me, k, _tradeSize):0} C</i>", HudPaint.Button, GUILayout.Height(30f)))
                    market.Sell(me, k, _tradeSize);
                GUILayout.EndHorizontal();
                GUILayout.Space(8f);
            }
        }

        void DrawIntel(Faction me)
        {
            var intel = IntelSystem.Instance;
            var reg   = Reg;
            if (intel == null || reg == null) return;

            GUILayout.Label("Intelligence", HudPaint.Header);
            GUILayout.Label("Rival figures are estimates. They decay. Run an operation to refresh them.", HudPaint.Small);
            GUILayout.Space(6f);

            foreach (var other in reg.Others(me.id))
            {
                var e = intel.Get(me.id, other);
                var stance = DiplomacySystem.Instance != null
                    ? DiplomacySystem.Instance.StanceBetween(me.id, other.id) : Stance.Correct;

                GUILayout.Label(HudPaint.Tinted($"■ {other.displayName}", other.colour) + $"   <i>{DiplomacySystem.Label(stance)}</i>", HudPaint.Label);
                GUILayout.Label(other.civ != null ? other.civ.displayName : "", HudPaint.Small);

                if (!e.hasAny)
                {
                    GUILayout.Label(HudPaint.Tinted("NO CURRENT INTEL", new Color(0.7f, 0.5f, 0.5f)), HudPaint.Small);
                    GUILayout.Space(10f);
                    continue;
                }

                var confTint = e.confidence > 0.6f ? new Color(0.55f, 0.95f, 0.6f)
                             : e.confidence > 0.3f ? new Color(0.95f, 0.85f, 0.45f)
                                                   : new Color(1f, 0.55f, 0.45f);
                GUILayout.Label($"{HudPaint.Tinted(IntelSystem.ConfidenceLabel(e.confidence), confTint)}  ·  {e.ageSeconds:0}s old", HudPaint.Small);
                var bar = GUILayoutUtility.GetRect(RightWidth - 40f, 6f);
                HudPaint.Bar(bar, e.confidence, confTint);

                GUILayout.Label($"Age: ~{Content.Age(e.ageIndex).displayName}   Security ~{e.security:0}", HudPaint.Small);
                GUILayout.Label($"Buildings ~{e.buildingCount}   Units ~{e.unitCount}", HudPaint.Small);

                string stocks = "";
                for (int i = 0; i < ResourceBundle.AllKinds.Length; i++)
                {
                    var k = ResourceBundle.AllKinds[i];
                    stocks += HudPaint.Tinted($"{ResourceBundle.Label(k).Substring(0, 3)} ~{e.stock.Get(k):0}", ResourceBundle.TintOf(k)) + "   ";
                }
                GUILayout.Label(stocks, HudPaint.Small);

                if (e.techs != null && e.techs.Count > 0)
                {
                    string techs = "";
                    foreach (var t in e.techs) techs += (Content.Tech(t)?.displayName ?? t) + ", ";
                    GUILayout.Label("Known tech: " + techs.TrimEnd(',', ' '), HudPaint.Small);
                }

                GUILayout.Space(10f);
            }
        }

        void DrawOperations(Faction me)
        {
            var esp = EspionageSystem.Instance;
            var reg = Reg;
            if (esp == null || reg == null) return;

            GUILayout.Label("Operations", HudPaint.Header);

            var agents = reg.AgentsOf(me.id);
            if (agents.Count == 0)
            {
                GUILayout.Label("No agents. Build a Directorate and train one.", HudPaint.Small);
                return;
            }

            // Agent picker
            GUILayout.Label("Agent", HudPaint.Header);
            foreach (var a in agents)
            {
                bool on = _opAgent == a;
                var tint = a.cover > 60f ? new Color(0.55f, 0.95f, 0.6f)
                         : a.cover > 30f ? new Color(0.95f, 0.85f, 0.45f)
                                         : new Color(1f, 0.5f, 0.45f);
                if (GUILayout.Toggle(on, $"{a.DisplayName}   cover {HudPaint.Tinted($"{a.cover:0}", tint)}   <i>{a.StatusLine}</i>", HudPaint.Button))
                    _opAgent = a;
            }
            if (_opAgent == null || !_opAgent.IsAlive) _opAgent = agents[0];

            // Target picker
            GUILayout.Space(6f);
            GUILayout.Label("Target", HudPaint.Header);
            foreach (var other in reg.Others(me.id))
            {
                if (GUILayout.Toggle(_opTargetId == other.id, HudPaint.Tinted("■ ", other.colour) + other.displayName, HudPaint.Button))
                    _opTargetId = other.id;
            }

            // Operation picker
            GUILayout.Space(6f);
            GUILayout.Label("Operation", HudPaint.Header);
            foreach (var kv in Content.Operations)
            {
                var op = kv.Value;
                bool locked = me.ageIndex < op.minAge;
                GUI.enabled = !locked;
                string label = $"{op.displayName}   <i>{CostText(op.cost * me.OperationCostMultiplier)}</i>";
                if (locked) label += $"   <i>needs {Content.Age(op.minAge).displayName}</i>";
                if (GUILayout.Toggle(_opId == op.id, label, HudPaint.Button)) _opId = op.id;
                GUI.enabled = true;
            }

            // Odds and launch
            var chosen = Content.Operation(_opId);
            var target = chosen != null && chosen.selfTargeted ? me : reg.Get(_opTargetId);

            GUILayout.Space(8f);
            if (chosen != null)
            {
                GUILayout.Label(chosen.description, HudPaint.Small);

                float ps = esp.PreviewSuccess(me, _opAgent, chosen, target);
                float pd = esp.PreviewDetection(me, _opAgent, chosen, target);

                GUILayout.Label($"Success {HudPaint.Tinted($"{ps * 100f:0}%", new Color(0.55f, 0.95f, 0.6f))}   ·   Detection {HudPaint.Tinted($"{pd * 100f:0}%", new Color(1f, 0.55f, 0.45f))}", HudPaint.Label);
                var b1 = GUILayoutUtility.GetRect(RightWidth - 40f, 6f); HudPaint.Bar(b1, ps, new Color(0.4f, 0.85f, 0.55f));
                var b2 = GUILayoutUtility.GetRect(RightWidth - 40f, 6f); HudPaint.Bar(b2, pd, new Color(0.9f, 0.45f, 0.4f));
                GUILayout.Label($"Cover cost {chosen.coverCost:0}  ·  duration {chosen.durationSeconds:0}s", HudPaint.Small);

                string why;
                bool can = esp.CanLaunch(me, _opAgent, chosen, target, out why);
                GUI.enabled = can;
                if (GUILayout.Button($"<b>LAUNCH {chosen.displayName.ToUpperInvariant()}</b>", HudPaint.Button, GUILayout.Height(32f)))
                    esp.TryLaunch(me, _opAgent, chosen.id, target);
                GUI.enabled = true;
                if (!can) GUILayout.Label(HudPaint.Tinted(why, new Color(1f, 0.6f, 0.5f)), HudPaint.Small);
            }
        }

        void DrawLog(Faction me)
        {
            GUILayout.Label("Log", HudPaint.Header);
            var log = EventLog.Instance;
            if (log == null) return;

            var entries = log.For(me.id, 60);
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var e = entries[i];
                int mins = Mathf.FloorToInt(e.time / 60f);
                int secs = Mathf.FloorToInt(e.time % 60f);
                GUILayout.Label($"<i>{mins:00}:{secs:00}</i>  " + HudPaint.Tinted(e.text, e.tint), HudPaint.Small);
            }
        }

        // ── Bottom hints ──────────────────────────────────────────────────

        void DrawHints()
        {
            var r = new Rect(LeftWidth + 12f, UnityEngine.Screen.height - 26f, UnityEngine.Screen.width - LeftWidth - RightWidth - 24f, 20f);
            GUI.Label(r, HudPaint.Tinted(
                "WASD / edge pan · Q,E rotate · wheel zoom · drag to box-select · right-click to order · Esc to clear",
                new Color(0.65f, 0.68f, 0.72f)), HudPaint.Small);
        }

        // ── Helpers ───────────────────────────────────────────────────────

        static string CostText(ResourceBundle cost)
        {
            var parts = new List<string>();
            for (int i = 0; i < ResourceBundle.AllKinds.Length; i++)
            {
                var k = ResourceBundle.AllKinds[i];
                float v = cost.Get(k);
                if (v <= 0.01f) continue;
                parts.Add(HudPaint.Tinted($"{v:0} {ResourceBundle.Label(k).Substring(0, 3)}", ResourceBundle.TintOf(k)));
            }
            return parts.Count == 0 ? "free" : string.Join("  ", parts);
        }

        static string DescribeRecipe(ConversionRecipe r)
        {
            string inputs = "", outputs = "";
            for (int i = 0; i < ResourceBundle.AllKinds.Length; i++)
            {
                var k = ResourceBundle.AllKinds[i];
                float vi = r.inputPerSecond.Get(k);
                float vo = r.outputPerSecond.Get(k);
                if (vi > 0.001f) inputs  += HudPaint.Tinted($"{vi:0.0} {ResourceBundle.Label(k).Substring(0, 3)}", ResourceBundle.TintOf(k)) + " ";
                if (vo > 0.001f) outputs += HudPaint.Tinted($"{vo:0.0} {ResourceBundle.Label(k).Substring(0, 3)}", ResourceBundle.TintOf(k)) + " ";
            }
            if (string.IsNullOrEmpty(inputs)) inputs = "—";
            return $"{inputs}→ {outputs}per second\nNeeds {r.labourSlots} workers of labour";
        }
    }
}
