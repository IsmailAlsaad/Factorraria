using Factorraria.Content.Tiles.Machines.ElectricalConsumers.RoboticArm;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Factorraria.Common.Mounts
{
    // The two builder arms of a Mech Suit that is running a build job (patch M3). Plain objects, no tile entity, no projectile.
    // Patch M5b: they are not extra arms any more. When a job starts, two of the three anchoring arms hand over (MechSuitArmProjectile.HandOff ->
    // RegisterHandoff): each builder arm starts at the claw position of the anchoring arm it replaces, and that projectile vanishes. One anchoring
    // arm stays. Arm 0 always takes the closest valid block, arm 1 the furthest valid one (MechSuitBuildSystem.FindTarget).
    // Each arm is a RoboticArmMotion (the hand model: reach, strike timing) plus, on a machine with a screen, a RoboticArmVisual (beams,
    // elbow, claw, held block), both reused from the Robotic Arm with the player's shoulder as the pivot.
    //   Authority: the build job (MechSuitBuildSystem, server or single-player) drives the motions. A multiplayer client never runs the
    //   job; it receives MechBuildArmState packets (MechSuitBuildNet), applies them to its copy of the set and ticks the same motion model.
    //   Dedicated server: only the motion half exists (no textures are requested).
    public class MechSuitBuilderArm
    {
        public readonly RoboticArmMotion Motion = new RoboticArmMotion();
        public RoboticArmVisual Visual;      // client side only, created on first draw
        public Item Held = new Item();
        public ushort HeldType;

        public void SetHeld(int type)
        {
            HeldType = (ushort)type;
            if (type <= 0)
            {
                Held.TurnToAir();
                return;
            }

            if (Held.type != type)
                Held.SetDefaults(type);
        }
    }

    public class MechSuitBuilderArmSet
    {
        public readonly MechSuitBuilderArm[] Arms = { new MechSuitBuilderArm(), new MechSuitBuilderArm() };
        public bool Building;
        public int RetireTimer;
        public int HandoffsApplied;   // anchoring arms that already handed their claw position to one of the two builder arms
    }

    public class MechSuitBuilderArms : ModSystem
    {
        // --- TUNABLES (plain fields so hot reload can change them) ---
        public float UpperArmLength = 100f;  // same as MechanicalArmProjectile (the anchoring arms)
        public float ForearmLength = 100f;
        public float BeamThickness = 0.7f;   // same as the anchoring arms
        public float FoldBeamLength = 20f;   // each beam shrinks to this length when the hand is at the shoulder (grabbing an item, being put away)
        public float FoldDistance = 120f;     // hand-to-shoulder distance below which the beams start to shrink (full length at and beyond it)
        public float ShoulderOffsetX = 7f;   // the two arms sit this far left and right of the player's centre
        public float ShoulderOffsetY = -2f;
        public int RetireTicks = 30;         // after a job ends the arms fold back this long, then disappear

        readonly Dictionary<int, MechSuitBuilderArmSet> sets = new();
        readonly List<int> scratch = new();

        public override void OnWorldUnload()
        {
            sets.Clear();
            pendingHandoffs.Clear();
        }

        public MechSuitBuilderArmSet GetOrCreate(int player)
        {
            if (!sets.TryGetValue(player, out MechSuitBuilderArmSet set))
            {
                set = new MechSuitBuilderArmSet();
                sets[player] = set;
            }

            return set;
        }

        public bool TryGet(int player, out MechSuitBuilderArmSet set) => sets.TryGetValue(player, out set);

        readonly Dictionary<int, List<Vector2>> pendingHandoffs = new();

        /// <summary>A job starts for this player (server job start, or the first arm state a client receives): the builder arms become active and take over the claws waiting in the handoff list.</summary>
        public MechSuitBuilderArmSet BeginBuilding(int player)
        {
            MechSuitBuilderArmSet set = GetOrCreate(player);
            set.Building = true;
            set.HandoffsApplied = 0;
            if (pendingHandoffs.TryGetValue(player, out List<Vector2> waiting))
            {
                foreach (Vector2 claw in waiting)
                    ApplyHandoff(player, set, claw);

                pendingHandoffs.Remove(player);
            }

            return set;
        }

        /// <summary>An anchoring arm of this player turned into a builder arm (every machine calls this from the projectile). Applied at once when the job is already running here, otherwise kept until it starts.</summary>
        public void RegisterHandoff(int player, Vector2 clawPosition)
        {
            if (sets.TryGetValue(player, out MechSuitBuilderArmSet set) && set.Building)
            {
                ApplyHandoff(player, set, clawPosition);
                return;
            }

            if (!pendingHandoffs.TryGetValue(player, out List<Vector2> waiting))
                pendingHandoffs[player] = waiting = new List<Vector2>();

            if (waiting.Count < 2)
                waiting.Add(clawPosition);
        }

        void ApplyHandoff(int player, MechSuitBuilderArmSet set, Vector2 clawPosition)
        {
            if (set.HandoffsApplied >= set.Arms.Length)
                return;

            int index = set.HandoffsApplied++;
            set.Arms[index].Motion.StartFrom(clawPosition, Shoulder(Main.player[player], index));
        }

        public Vector2 Shoulder(Player player, int arm)
        {
            return player.Center + new Vector2(arm == 0 ? -ShoulderOffsetX : ShoulderOffsetX, ShoulderOffsetY);
        }

        // The job ended (or the server told us so): the arms stop, drop what they hold and fold back before they vanish.
        // A dedicated server has nothing to draw, so it just forgets the set.
        public void MarkEnded(int player)
        {
            if (!sets.TryGetValue(player, out MechSuitBuilderArmSet set))
                return;

            if (Main.dedServ)
            {
                sets.Remove(player);
                return;
            }

            pendingHandoffs.Remove(player);
            set.Building = false;
            set.RetireTimer = RetireTicks;
            foreach (MechSuitBuilderArm arm in set.Arms)
            {
                arm.SetHeld(0);
                arm.Motion.SetTarget(null);
            }
        }

        // Client machines (single-player and multiplayer): the multiplayer client ticks the replica's motion, a retiring set is ticked
        // here in every mode (the job that drove it is gone), and every set gets its visual updated.
        public override void PostUpdateEverything()
        {
            if (Main.dedServ || sets.Count == 0)
                return;

            scratch.Clear();
            foreach (KeyValuePair<int, MechSuitBuilderArmSet> pair in sets)
            {
                Player player = Main.player[pair.Key];
                MechSuitBuilderArmSet set = pair.Value;
                if (player == null || !player.active || (!set.Building && --set.RetireTimer <= 0))
                {
                    scratch.Add(pair.Key);
                    continue;
                }

                for (int i = 0; i < set.Arms.Length; i++)
                {
                    MechSuitBuilderArm arm = set.Arms[i];
                    Vector2 shoulder = Shoulder(player, i);
                    if (!set.Building)
                        arm.Motion.SetTarget(shoulder);

                    if (!set.Building || Main.netMode == NetmodeID.MultiplayerClient)
                        arm.Motion.Update(shoulder, UpperArmLength, ForearmLength, arm.Held, false);

                    arm.Visual ??= new RoboticArmVisual(true) { BeamThickness = BeamThickness };
                    // The logic and the reach keep the full lengths; only the drawn beams telescope as the hand nears the shoulder.
                    float fold = MathHelper.Clamp(Vector2.Distance(arm.Motion.Head, shoulder) / System.MathF.Max(FoldDistance, 1f), 0f, 1f);
                    float upperShown = MathHelper.Lerp(System.MathF.Min(FoldBeamLength, UpperArmLength), UpperArmLength, fold);
                    float foreShown = MathHelper.Lerp(System.MathF.Min(FoldBeamLength, ForearmLength), ForearmLength, fold);
                    arm.Visual.Update(arm.Motion, shoulder, upperShown, foreShown, arm.Held, false);
                }
            }

            foreach (int key in scratch)
                sets.Remove(key);
        }

        // Same pattern as MachineDynamicDrawSystem.PostDrawTiles: the arms are drawn over the tiles and under the players.
        public override void PostDrawTiles()
        {
            if (Main.dedServ || sets.Count == 0)
                return;

            SpriteBatch spriteBatch = Main.spriteBatch;
            bool begun = false;
            Rectangle view = new Rectangle((int)Main.screenPosition.X - 300, (int)Main.screenPosition.Y - 300, (int)Main.ViewSize.X + 600, (int)Main.ViewSize.Y + 600);

            foreach (KeyValuePair<int, MechSuitBuilderArmSet> pair in sets)
            {
                Player player = Main.player[pair.Key];
                if (player == null || !player.active || player.dead || !view.Contains(player.Center.ToPoint()))
                    continue;

                if (!begun)
                {
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                    begun = true;
                }

                for (int i = 0; i < pair.Value.Arms.Length; i++)
                {
                    MechSuitBuilderArm arm = pair.Value.Arms[i];
                    arm.Visual?.DrawBehind(spriteBatch, Shoulder(player, i), arm.Held, false);
                }
            }

            if (begun)
                spriteBatch.End();
        }
    }
}