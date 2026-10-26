using Factorraria.Common.Carts;
using Factorraria.Common.Machines;
using Factorraria.Common.Net;
using Factorraria.Content.Items.Wires;
using Factorraria.Content.Tiles.Machines.ElectricalConsumers.PowerPoles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace Factorraria.Common.Systems
{
    // Power poles patch P6: the player's input for linking two poles. OWNER CLIENT ONLY (the local player on a client, or in single-player);
    // a dedicated server never runs any of it. Nothing here is saved, and nothing is sent until the finishing click: the pending link, the
    // hologram, the highlights and the hit-testing are all local. The finishing click calls PoleLinkNet.RequestLink and the server (P3)
    // validates it in full.
    //
    // Input: a RIGHT click while a wire item is held (the cursor item first, otherwise the selected item). Left click still places ground wire.
    //   - hovering a pole (its footprint or a crossboard's hitbox) selects the connection point closest to the MOUSE, and only that one is highlighted
    //   - first right click on a pole: Pending = (that pole, that point). A taken point does nothing (its highlight is already red)
    //   - while pending, a hologram runs from the start point to the cursor, or to the hovered second pole's point closest to the mouse
    //   - second right click on a different pole: RequestLink (only when the hologram is not red). The wire is taken when the server answers.
    //   - a right click that is not on a pole (mid air) while pending cancels the pending link (P7b)
    // There is no hover or cursor text for any of this (removed after the P6 test); the server's refusals still show as chat lines (PoleLinkNet).
    // P7 (cutting links with the Iron and Lead cutters) lives here too: see TryRequestCutAtCursor at the end of the class.
    // The click is consumed (On_Player.TileInteractionsUse is skipped for it), so the pole's own UI does not open while wire is held.
    //
    // The checks run in PostUpdateInput, before the players update, once per tick. Press edge = the button is down now and was not down on the
    // previous tick, tracked here (Main.mouseRightRelease can be cleared by other code), so holding the button never fires twice.
    public class PoleLinkInputSystem : ModSystem
    {
        // Tunables (plain instance fields so hot reload can change them).
        public float CutLinePixels = 6f;            // P7: how close (world pixels) the cursor must be to a hanging wire to cut it
        public float CutPointPixels = 8f;           // P7: radius of a connection point's cut zone (a point that carries a link)
        public int CutRepeatTicks = 20;             // P7: the same link is not requested again for this many ticks (the request is still in flight)

        // ---- pending link (owner client only, never saved)
        public bool Pending;
        Point16 pendingPole;
        int pendingSlot;

        // ---- set by UpdateInput, read by the hook and the drawing
        public bool ConsumeThisTick;                 // the right click was used: skip vanilla's TileInteractionsUse for it
        bool holdingWire;
        CustomWireType heldWire;
        Item heldItem;
        bool rightWasDown;
        bool escapeWasDown;

        struct Preview
        {
            public bool Active;
            public Vector2 From;
            public Vector2 To;
            public CustomWireType Wire;
            public bool Invalid;
            public bool CanCommit;
            public int EndSlot;
        }

        Preview preview;

        // ---- hover (P6b): which pole the cursor is on while wire is held, read by PowerPoleCrossboardSystem for the highlight cells
        bool hoverActive;
        Point16 hoverPole;
        public PoleLink CutHoverLink;                // P7b: with a cutter in hand, the link under the cursor (drawn in the invalid colour); null otherwise
        int hoverSlot;                               // that pole's connection point closest to the mouse (free or not): the only one highlighted, and the one a click uses
        int heldCount;                               // how many of the held wire's item the player has (only while hoverActive)

        public enum PointHighlight { None, Valid, Invalid }

        public override void Load()
        {
            On_Player.TileInteractionsUse += SkipConsumedClick;
        }

        public override void Unload()
        {
            On_Player.TileInteractionsUse -= SkipConsumedClick;
        }

        public override void OnWorldLoad()
        {
            Reset();
        }

        public override void OnWorldUnload()
        {
            Reset();
        }

        void Reset()
        {
            Pending = false;
            ConsumeThisTick = false;
            holdingWire = false;
            hoverActive = false;
            CutHoverLink = null;
            preview = default;
            rightWasDown = false;
            escapeWasDown = false;
        }

        // The same pattern as MaterializerRightClickGuard: skip vanilla's interaction, but mark the press as used.
        static void SkipConsumedClick(On_Player.orig_TileInteractionsUse orig, Player self, int myX, int myY)
        {
            PoleLinkInputSystem input = ModContent.GetInstance<PoleLinkInputSystem>();
            if (self.whoAmI == Main.myPlayer && input.ConsumeThisTick)
            {
                input.ConsumeThisTick = false;
                self.tileInteractionHappened = true;
                self.tileInteractAttempted = true;
                self.releaseUseTile = false;
                return;
            }

            orig(self, myX, myY);
        }

        // ---------------------------------------------------------------- held wire

        /// <summary>The held wire: the item on the cursor when there is one, otherwise the selected item. False when that is not a copper, tin or gold wire.</summary>
        public static bool TryGetHeldWire(Player player, out CustomWireType wire, out Item item)
        {
            wire = default;
            item = null;

            Item candidate = Main.mouseItem != null && !Main.mouseItem.IsAir ? Main.mouseItem : player.HeldItem;
            if (candidate == null || candidate.IsAir)
                return false;

            foreach (CustomWireType known in CustomWireSystem.AllWireTypes)
            {
                if (candidate.type == CustomWireNet.WireItemType(known))
                {
                    wire = known;
                    item = candidate;
                    return true;
                }
            }

            return false;
        }

        // ---------------------------------------------------------------- hit-testing

        /// <summary>The pole whose footprint or crossboard hitbox (PoleLinkSystem.IsClickZone) contains this tile; the nearest one when several do.</summary>
        public static bool TryFindPoleAtTile(int tileX, int tileY, out PowerPoleTileEntityBase pole)
        {
            pole = null;
            float best = float.MaxValue;
            PoleLinkSystem links = PoleLinkSystem.Instance;

            foreach (TileEntity entity in TileEntity.ByPosition.Values)
            {
                if (entity is not PowerPoleTileEntityBase candidate)
                    continue;

                Point16 corner = candidate.Position;
                if (Math.Abs(corner.X - tileX) > 8 || tileY < corner.Y - 6 || tileY > corner.Y + PoleLinkSystem.FootprintHeight(candidate) + 6)
                    continue;

                Tile tile = Framing.GetTileSafely(corner.X, corner.Y);
                if (!tile.HasTile || tile.TileType != candidate.ValidTileType)
                    continue;

                if (!links.IsClickZone(candidate, tileX, tileY))
                    continue;

                float distance = Math.Abs(corner.X - tileX) + Math.Abs(corner.Y + FootprintMid(candidate) - tileY) * 0.01f;
                if (distance < best)
                {
                    best = distance;
                    pole = candidate;
                }
            }

            return pole != null;
        }

        static float FootprintMid(PowerPoleTileEntityBase pole) => PoleLinkSystem.FootprintHeight(pole) * 0.5f;

        // The connection point of a pole closest to a world position (resting positions), whether it is free or not. Since P6c this is THE point
        // choice of the client: hover highlight, start of a link and end of a link all use the point closest to the MOUSE, and a taken point is
        // simply invalid. Nothing is chosen for the player.
        static int ClosestPoint(PoleLinkSystem links, PowerPoleTileEntityBase pole, Vector2 target)
        {
            int best = 0;
            float bestDistance = float.MaxValue;
            int count = links.SlotCount(pole);

            for (int i = 0; i < count; i++)
            {
                float distance = Vector2.DistanceSquared(pole.GetLinkPointWorld(i), target);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return best;
        }

        // What the board's highlight layer shows for one connection point of a pole right now (the slot is the pole's flat link point index).
        // None unless wire is held, the cursor is on THIS pole, and this is the point closest to the mouse (hoverSlot): only that one is lit.
        // Valid = the point is free and the link would be possible. Invalid = the point is already used by a wire, or the link cannot be made.
        // With no pending link the span is not known yet, so only free or used counts. With a pending link, hovering a SECOND pole checks the
        // same things the hologram does (span within the held wire's range, enough wire items), so the point is green exactly when the hologram
        // is; hovering the pole the link started on is invalid (a pole cannot link to itself).
        public PointHighlight GetPointHighlight(PowerPoleTileEntityBase pole, int slot)
        {
            if (!holdingWire || !hoverActive || pole.Position != hoverPole || slot != hoverSlot)
                return PointHighlight.None;

            PoleLinkSystem links = PoleLinkSystem.Instance;
            if (!links.IsSlotFree(pole.Position, slot))
                return PointHighlight.Invalid;

            if (!Pending)
                return PointHighlight.Valid;

            if (pole.Position == pendingPole || !PoleLinkSystem.TryGetPole(pendingPole, out PowerPoleTileEntityBase startPole) || pendingSlot >= startPole.LinkPointCount)
                return PointHighlight.Invalid;

            float span = PoleLinkSystem.SpanTiles(startPole, pendingSlot, pole, slot);
            if (span > links.MaxSpan(heldWire) || heldCount < links.CostFor(heldWire, span))
                return PointHighlight.Invalid;

            return PointHighlight.Valid;
        }

        // ---------------------------------------------------------------- the per-tick update

        public override void PostUpdateInput()
        {
            if (Main.dedServ || Main.gameMenu)
                return;

            Player player = Main.LocalPlayer;
            if (player == null || !player.active)
                return;

            ConsumeThisTick = false;
            preview = default;
            hoverActive = false;
            CutHoverLink = null;

            bool right = Main.mouseRight;
            bool pressed = right && !rightWasDown;
            rightWasDown = right;

            bool escape = Main.keyState.IsKeyDown(Keys.Escape);
            bool escapePressed = escape && !escapeWasDown;
            escapeWasDown = escape;

            holdingWire = TryGetHeldWire(player, out heldWire, out heldItem);

            if (player.dead)
            {
                Cancel();
                return;
            }

            PoleLinkSystem links = PoleLinkSystem.Instance;
            Vector2 mouse = CartSystem.GetZoomCorrectedMouseWorld();
            int tileX = Math.Clamp((int)(mouse.X / 16f), 0, Main.maxTilesX - 1);
            int tileY = Math.Clamp((int)(mouse.Y / 16f), 0, Main.maxTilesY - 1);

            // The pole under the cursor (the same hit-test as the click), only while wire is held and the cursor is not on a UI element.
            if (holdingWire && !player.mouseInterface && !player.lastMouseInterface && TryFindPoleAtTile(tileX, tileY, out PowerPoleTileEntityBase hoverCandidate))
            {
                hoverActive = true;
                hoverPole = hoverCandidate.Position;
                hoverSlot = ClosestPoint(links, hoverCandidate, mouse);
                heldCount = PoleLinkNet.CountWire(player, heldItem.type);
            }

            // A cutter in hand and the cursor on a link or a link's connection point: that wire is drawn in the invalid colour (PoleWireDrawSystem), so
            // the player sees it can be cut. Same target rule as the cut itself (TryFindCutTarget). PostUpdateInput sees the unscaled mouse, so the
            // zoom matrix helper is right here (the cut itself runs inside the player update, see RequestCutAtCursor).
            if (!player.mouseInterface && !player.lastMouseInterface && Main.mouseItem.IsAir && IsCutter(player.HeldItem)
                && TryFindCutTarget(links, mouse, tileX, tileY, out _, out _, out PoleLink cutLink))
                CutHoverLink = cutLink;

            if (Pending)
            {
                if (!holdingWire)
                    Cancel();
                else if (escapePressed)
                    Cancel();
                else if (!PoleLinkSystem.TryGetPole(pendingPole, out PowerPoleTileEntityBase startPole) || !Framing.GetTileSafely(pendingPole.X, pendingPole.Y).HasTile
                    || Framing.GetTileSafely(pendingPole.X, pendingPole.Y).TileType != startPole.ValidTileType)
                    Cancel();   // the first pole is gone
                else if (pendingSlot >= links.SlotCount(startPole) || !links.IsSlotFree(pendingPole, pendingSlot))
                    Cancel();   // that connection point was just used
                else
                    BuildPreview(player, startPole, links, mouse, tileX, tileY);
            }

            bool overUi = player.mouseInterface || player.lastMouseInterface;
            if (!pressed || overUi || !holdingWire)
                return;

            HandlePress(player, links, mouse, tileX, tileY);
        }

        void Cancel()
        {
            Pending = false;
            preview = default;
        }

        // Fills the hologram: start point to the cursor, or to the hovered second pole's free point closest to the start point.
        void BuildPreview(Player player, PowerPoleTileEntityBase startPole, PoleLinkSystem links, Vector2 mouse, int tileX, int tileY)
        {
            PowerPoleCrossboardSystem sway = ModContent.GetInstance<PowerPoleCrossboardSystem>();
            Vector2 startRest = startPole.GetLinkPointWorld(pendingSlot);
            Vector2 from = PoleWireDrawSystem.GetSwayedPoint(sway, startPole, pendingSlot);

            PowerPoleTileEntityBase endPole = null;
            int endSlot = -1;
            bool endTaken = false;

            // The end is the second pole's connection point closest to the MOUSE (free or not). A taken point is drawn red and cannot be
            // committed; nothing else is chosen for the player.
            if (TryFindPoleAtTile(tileX, tileY, out PowerPoleTileEntityBase hovered) && hovered.Position != pendingPole)
            {
                endPole = hovered;
                endSlot = ClosestPoint(links, hovered, mouse);
                endTaken = !links.IsSlotFree(hovered.Position, endSlot);
            }

            Vector2 endRest = endPole != null ? endPole.GetLinkPointWorld(endSlot) : mouse;
            Vector2 to = endPole != null ? PoleWireDrawSystem.GetSwayedPoint(sway, endPole, endSlot) : mouse;

            float span = Vector2.Distance(startRest, endRest) / 16f;
            float max = links.MaxSpan(heldWire);

            if (span >= max * links.BreakSpanMultiplier)
            {
                Cancel();   // the wire would be too long
                return;
            }

            int cost = links.CostFor(heldWire, span);
            int have = PoleLinkNet.CountWire(player, heldItem.type);
            bool invalid = endTaken || span > max || have < cost;

            preview = new Preview
            {
                Active = true,
                From = from,
                To = to,
                Wire = heldWire,
                Invalid = invalid,
                CanCommit = endPole != null && !invalid,
                EndSlot = endSlot
            };
        }

        // The right-click press edge, with wire held and the cursor not on a UI element.
        void HandlePress(Player player, PoleLinkSystem links, Vector2 mouse, int tileX, int tileY)
        {
            if (!TryFindPoleAtTile(tileX, tileY, out PowerPoleTileEntityBase pole))
            {
                // Not on a pole (mid air): a pending link is cancelled. The click is not consumed, vanilla handles it as usual.
                if (Pending)
                    Cancel();

                return;
            }

            ConsumeThisTick = true;   // the pole's UI must not open

            if (!Pending)
            {
                // The start is the connection point closest to the mouse. A taken point does nothing (its highlight is already red).
                int slot = ClosestPoint(links, pole, mouse);
                if (!links.IsSlotFree(pole.Position, slot))
                    return;

                Pending = true;
                pendingPole = pole.Position;
                pendingSlot = slot;
                return;
            }

            // The pole the link started on, or a red hologram (taken point, too long, not enough wire): nothing happens.
            if (pole.Position == pendingPole || !preview.Active || !preview.CanCommit)
                return;

            PoleLinkNet.RequestLink(pendingPole.X, pendingPole.Y, pole.Position.X, pole.Position.Y, heldWire, pendingSlot, preview.EndSlot, tileX, tileY);
            Pending = false;
            preview = default;
        }

        // ---------------------------------------------------------------- drawing: world (called by PowerPoleCrossboardSystem.PostDrawTiles, after the boards)
        // Only the pending-link hologram. The connection point highlights are cells of the board sheets, drawn with the boards (GetPointHighlight).

        public void DrawWorld(SpriteBatch spriteBatch)
        {
            if (Main.dedServ || Main.gameMenu || !holdingWire || !preview.Active)
                return;

            PoleWireDrawSystem.BeginBatch(spriteBatch);
            ModContent.GetInstance<PoleWireDrawSystem>().DrawWire(spriteBatch, preview.From, preview.To, preview.Wire, preview.Invalid);
            spriteBatch.End();
        }

        // ---------------------------------------------------------------- cutting links (P7)
        // The Iron and Lead cutters call TryRequestCutAtCursor from UseItem (owner only, right after their IsOwner check), BEFORE their tile-wire
        // branch. It finds the link under the mouse on this client's replica and sends PoleLinkNet.RequestCut; the server validates, removes the
        // link, drops the refund once and tells everyone (P3). Nothing is predicted here: the wire vanishes when the server's answer arrives.
        // Reach is the server's call (FactorrariaNet.InReach on the clicked tile): the cutters have no client reach check, tile wires included.

        Point16 lastCutPole;
        int lastCutSlot = -1;
        uint lastCutTick;

        /// <summary>Owner only. True when the click was a pole link cut (a request was sent, or one for the same link is still in flight), so the cutter must not also cut a ground wire.</summary>
        public static bool TryRequestCutAtCursor(Player player)
        {
            return ModContent.GetInstance<PoleLinkInputSystem>().RequestCutAtCursor(player);
        }

        bool RequestCutAtCursor(Player player)
        {
            if (Main.dedServ || Main.gameMenu || player.dead)
                return false;

            PoleLinkSystem links = PoleLinkSystem.Instance;
            if (links.Links.Count == 0)
                return false;

            // UseItem runs INSIDE the player update, where vanilla has already zoom-corrected Main.mouseX/Y (the values Player.tileTargetX comes from),
            // so the mouse is Main.MouseWorld here. The zoom matrix helper used by PostUpdateInput would correct it a second time (P7b zoom fix).
            Vector2 mouse = Main.MouseWorld;
            int tileX = Player.tileTargetX;
            int tileY = Player.tileTargetY;

            if (!TryFindCutTarget(links, mouse, tileX, tileY, out Point16 pole, out int slot, out _))
                return false;

            // autoReuse fires every few ticks and the answer takes a round trip: the same link is not requested again until it has had time to arrive.
            if (pole == lastCutPole && slot == lastCutSlot && Main.GameUpdateCount - lastCutTick < (uint)CutRepeatTicks)
                return true;

            lastCutPole = pole;
            lastCutSlot = slot;
            lastCutTick = Main.GameUpdateCount;

            PoleLinkNet.Trace("cut click: mouse " + mouse + ", tile (" + tileX + ", " + tileY + "), zoom " + Main.GameZoomTarget + ", matrix-helper mouse " + CartSystem.GetZoomCorrectedMouseWorld() + ", link (" + pole.X + ", " + pole.Y + ")#" + slot);
            PoleLinkNet.RequestCut(pole.X, pole.Y, slot, tileX, tileY);
            SoundEngine.PlaySound(SoundID.Dig, new Vector2(tileX * 16f, tileY * 16f));
            return true;
        }

        // The link whose line (resting points, so every process agrees) passes within CutLinePixels of the mouse, or one of whose connection points
        // is within CutPointPixels of it; the nearest wins. pole/slot name the end the request is sent for: the hit end for a point, end A for a line.
        // A link whose pole entities are missing on this client is skipped. P9 keeps this straight resting-point hit-test by decision:
        // the swayed rope is visual only, so cutting, span checks and the server all keep the same geometry.
        bool TryFindLinkAtMouse(PoleLinkSystem links, Vector2 mouse, out Point16 pole, out int slot, out float distance)
        {
            pole = default;
            slot = -1;
            distance = float.MaxValue;
            float reach = Math.Max(CutLinePixels, CutPointPixels);

            foreach (PoleLink link in links.Links)
            {
                if (!PoleLinkSystem.TryGetPole(link.A, out PowerPoleTileEntityBase poleA) || !PoleLinkSystem.TryGetPole(link.B, out PowerPoleTileEntityBase poleB)
                    || link.SlotA >= poleA.LinkPointCount || link.SlotB >= poleB.LinkPointCount)
                    continue;

                Vector2 a = poleA.GetLinkPointWorld(link.SlotA);
                Vector2 b = poleB.GetLinkPointWorld(link.SlotB);

                if (mouse.X < Math.Min(a.X, b.X) - reach || mouse.X > Math.Max(a.X, b.X) + reach
                    || mouse.Y < Math.Min(a.Y, b.Y) - reach || mouse.Y > Math.Max(a.Y, b.Y) + reach)
                    continue;

                float line = DistanceToSegment(mouse, a, b);
                if (line <= CutLinePixels && line < distance)
                {
                    distance = line;
                    pole = link.A;
                    slot = link.SlotA;
                }

                float toA = Vector2.Distance(mouse, a);
                if (toA <= CutPointPixels && toA < distance)
                {
                    distance = toA;
                    pole = link.A;
                    slot = link.SlotA;
                }

                float toB = Vector2.Distance(mouse, b);
                if (toB <= CutPointPixels && toB < distance)
                {
                    distance = toB;
                    pole = link.B;
                    slot = link.SlotB;
                }
            }

            return slot >= 0;
        }

        // What a cutter would cut with the mouse here: TryFindLinkAtMouse plus the ground-wire rule (a ground wire on the cursor's tile wins unless
        // the cursor is closer to the line or point than to that tile's centre). Used by the cut itself and by the cut hover colour, so they agree.
        bool TryFindCutTarget(PoleLinkSystem links, Vector2 mouse, int tileX, int tileY, out Point16 pole, out int slot, out PoleLink link)
        {
            link = null;
            if (!TryFindLinkAtMouse(links, mouse, out pole, out slot, out float distance))
                return false;

            if (CustomWireSystem.WireAt(tileX, tileY) != CustomWireType.None)
            {
                float tileDistance = Vector2.Distance(mouse, new Vector2(tileX * 16f + 8f, tileY * 16f + 8f));
                if (distance >= tileDistance)
                    return false;
            }

            link = links.GetLink(pole, slot);
            return link != null;
        }

        static bool IsCutter(Item item)
        {
            return item != null && !item.IsAir && (item.type == ModContent.ItemType<IronCutterItem>() || item.type == ModContent.ItemType<LeadCutterItem>());
        }

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 edge = b - a;
            float lengthSquared = edge.LengthSquared();
            if (lengthSquared < 0.0001f)
                return Vector2.Distance(p, a);

            float t = MathHelper.Clamp(Vector2.Dot(p - a, edge) / lengthSquared, 0f, 1f);
            return Vector2.Distance(p, a + edge * t);
        }
    }
}