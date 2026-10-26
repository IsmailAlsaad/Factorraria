using Factorraria.Common.Carts;
using Factorraria.Common.Machines;
using Factorraria.Common.Net;
using Factorraria.Content.Tiles.Machines.ElectricalConsumers.PowerPoles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
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
    //   - first right click on a pole's footprint or on a crossboard's hitbox: Pending = (that pole, its free point closest to the player)
    //   - while pending, a hologram runs from the start point to the cursor, or to the hovered second pole's free point closest to the start
    //   - second right click on a different pole: RequestLink (only when the hologram is not red). The wire is taken when the server answers.
    // The click is consumed (On_Player.TileInteractionsUse is skipped for it), so the pole's own UI does not open while wire is held.
    //
    // The checks run in PostUpdateInput, before the players update, once per tick. Press edge = the button is down now and was not down on the
    // previous tick, tracked here (Main.mouseRightRelease can be cleared by other code), so holding the button never fires twice.
    public class PoleLinkInputSystem : ModSystem
    {
        // Tunables (plain instance fields so hot reload can change them).
        public float HighlightRangeTiles = 24f;     // connection points of poles this close to the player are marked while wire is held
        public float MarkerSize = 6f;               // pixels
        public int NoticeTicks = 150;               // how long a client-side message stays next to the cursor
        public Color FreePointColor = new Color(120, 255, 120);
        public Color TakenPointColor = new Color(255, 90, 90);
        public Color StartPointColor = new Color(90, 220, 255);

        // TEMPORARY (patch P6, remove in Phase 10): draws the tile coordinates under the cursor. Toggle with /ptiles.
        public bool ShowTileDebug = true;

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
        string noticeText;
        int noticeLeft;

        struct Preview
        {
            public bool Active;
            public Vector2 From;
            public Vector2 To;
            public CustomWireType Wire;
            public bool Invalid;
            public bool CanCommit;
            public int EndSlot;
            public float Span;
            public float MaxSpan;
            public int Cost;
            public int Have;
            public string Reason;
        }

        Preview preview;

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
            preview = default;
            noticeText = null;
            noticeLeft = 0;
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

        // The connection point closest to a position whether it is free or not (used only to show where a link WOULD attach to a full pole).
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
            if (noticeLeft > 0)
                noticeLeft--;

            bool right = Main.mouseRight;
            bool pressed = right && !rightWasDown;
            rightWasDown = right;

            bool escape = Main.keyState.IsKeyDown(Keys.Escape);
            bool escapePressed = escape && !escapeWasDown;
            escapeWasDown = escape;

            holdingWire = TryGetHeldWire(player, out heldWire, out heldItem);

            if (player.dead)
            {
                Cancel(null);
                return;
            }

            PoleLinkSystem links = PoleLinkSystem.Instance;
            Vector2 mouse = CartSystem.GetZoomCorrectedMouseWorld();
            int tileX = Math.Clamp((int)(mouse.X / 16f), 0, Main.maxTilesX - 1);
            int tileY = Math.Clamp((int)(mouse.Y / 16f), 0, Main.maxTilesY - 1);

            if (Pending)
            {
                if (!holdingWire)
                    Cancel("Pole link cancelled.");
                else if (escapePressed)
                    Cancel("Pole link cancelled.");
                else if (!PoleLinkSystem.TryGetPole(pendingPole, out PowerPoleTileEntityBase startPole) || !Framing.GetTileSafely(pendingPole.X, pendingPole.Y).HasTile
                    || Framing.GetTileSafely(pendingPole.X, pendingPole.Y).TileType != startPole.ValidTileType)
                    Cancel("Pole link cancelled: the first pole is gone.");
                else if (pendingSlot >= links.SlotCount(startPole) || !links.IsSlotFree(pendingPole, pendingSlot))
                    Cancel("Pole link cancelled: that connection point was just used.");
                else
                    BuildPreview(player, startPole, links, mouse, tileX, tileY);
            }

            bool overUi = player.mouseInterface || player.lastMouseInterface;
            if (!pressed || overUi || !holdingWire)
                return;

            HandlePress(player, links, tileX, tileY);
        }

        void Cancel(string message)
        {
            bool wasPending = Pending;
            Pending = false;
            preview = default;

            if (wasPending && message != null)
                Notify(message);
        }

        void Notify(string message)
        {
            noticeText = message;
            noticeLeft = NoticeTicks;
        }

        // Fills the hologram: start point to the cursor, or to the hovered second pole's free point closest to the start point.
        void BuildPreview(Player player, PowerPoleTileEntityBase startPole, PoleLinkSystem links, Vector2 mouse, int tileX, int tileY)
        {
            PowerPoleCrossboardSystem sway = ModContent.GetInstance<PowerPoleCrossboardSystem>();
            Vector2 startRest = startPole.GetLinkPointWorld(pendingSlot);
            Vector2 from = PoleWireDrawSystem.GetSwayedPoint(sway, startPole, pendingSlot);

            PowerPoleTileEntityBase endPole = null;
            int endSlot = -1;
            bool noFreePoint = false;

            if (TryFindPoleAtTile(tileX, tileY, out PowerPoleTileEntityBase hovered) && hovered.Position != pendingPole)
            {
                endPole = hovered;
                endSlot = links.ChooseFreePoint(hovered, startRest);
                if (endSlot < 0)
                {
                    noFreePoint = true;
                    endSlot = ClosestPoint(links, hovered, startRest);   // still attaches to the closest point, drawn red
                }
            }

            Vector2 endRest = endPole != null ? endPole.GetLinkPointWorld(endSlot) : mouse;
            Vector2 to = endPole != null ? PoleWireDrawSystem.GetSwayedPoint(sway, endPole, endSlot) : mouse;

            float span = Vector2.Distance(startRest, endRest) / 16f;
            float max = links.MaxSpan(heldWire);

            if (span >= max * links.BreakSpanMultiplier)
            {
                Cancel("Pole link cancelled: the wire would be too long.");
                return;
            }

            int cost = links.CostFor(heldWire, span);
            int have = PoleLinkNet.CountWire(player, heldItem.type);

            string reason = null;
            if (noFreePoint)
                reason = "That pole has no free connection point.";
            else if (span > max)
                reason = "Too long: " + span.ToString("0.0") + " tiles, this wire reaches " + max.ToString("0") + ".";
            else if (have < cost)
                reason = "Not enough wire: needs " + cost + ", you have " + have + ".";

            preview = new Preview
            {
                Active = true,
                From = from,
                To = to,
                Wire = heldWire,
                Invalid = reason != null,
                CanCommit = endPole != null && reason == null,
                EndSlot = endSlot,
                Span = span,
                MaxSpan = max,
                Cost = cost,
                Have = have,
                Reason = reason
            };
        }

        // The right-click press edge, with wire held and the cursor not on a UI element.
        void HandlePress(Player player, PoleLinkSystem links, int tileX, int tileY)
        {
            if (!TryFindPoleAtTile(tileX, tileY, out PowerPoleTileEntityBase pole))
                return;   // not on a pole: does nothing (a pending link stays, and vanilla handles the click as usual)

            ConsumeThisTick = true;   // the pole's UI must not open

            if (!Pending)
            {
                if (!player.IsTargetTileInItemRange(heldItem))
                {
                    Notify("Out of reach.");
                    return;
                }

                int slot = links.ChooseFreePoint(pole, player.Center);
                if (slot < 0)
                {
                    Notify("That pole has no free connection point.");
                    return;
                }

                Pending = true;
                pendingPole = pole.Position;
                pendingSlot = slot;
                return;
            }

            if (pole.Position == pendingPole)
            {
                Notify("Pick a different pole.");
                return;
            }

            if (!preview.Active || !preview.CanCommit)
            {
                Notify(preview.Reason ?? "That link is not possible.");
                return;
            }

            if (!player.IsTargetTileInItemRange(heldItem))
            {
                Notify("Out of reach.");
                return;
            }

            PoleLinkNet.RequestLink(pendingPole.X, pendingPole.Y, pole.Position.X, pole.Position.Y, heldWire, pendingSlot, preview.EndSlot, tileX, tileY);
            Pending = false;
            preview = default;
        }

        // ---------------------------------------------------------------- drawing: world (called by PowerPoleCrossboardSystem.PostDrawTiles, after the links)

        public void DrawWorld(SpriteBatch spriteBatch)
        {
            if (Main.dedServ || Main.gameMenu || !holdingWire)
                return;

            Player player = Main.LocalPlayer;
            PoleLinkSystem links = PoleLinkSystem.Instance;
            PowerPoleCrossboardSystem sway = ModContent.GetInstance<PowerPoleCrossboardSystem>();
            float range = HighlightRangeTiles * 16f;
            bool begun = false;

            foreach (TileEntity entity in TileEntity.ByPosition.Values)
            {
                if (entity is not PowerPoleTileEntityBase pole)
                    continue;

                Point16 corner = pole.Position;
                Vector2 middle = new Vector2(corner.X * 16f + 8f, (corner.Y + FootprintMid(pole)) * 16f);
                if (Vector2.Distance(middle, player.Center) > range + FootprintMid(pole) * 16f || !BaseMachine.IsOnScreen(middle, FootprintMid(pole) * 16f + 64f))
                    continue;

                Tile tile = Framing.GetTileSafely(corner.X, corner.Y);
                if (!tile.HasTile || tile.TileType != pole.ValidTileType)
                    continue;

                int count = links.SlotCount(pole);
                for (int i = 0; i < count; i++)
                {
                    if (!begun)
                    {
                        PoleWireDrawSystem.BeginBatch(spriteBatch);
                        begun = true;
                    }

                    bool isStart = Pending && corner == pendingPole && i == pendingSlot;
                    Color color = isStart ? StartPointColor : links.IsSlotFree(corner, i) ? FreePointColor : TakenPointColor;
                    Vector2 point = PoleWireDrawSystem.GetSwayedPoint(sway, pole, i);
                    float size = isStart ? MarkerSize + 2f : MarkerSize;
                    spriteBatch.Draw(TextureAssets.MagicPixel.Value, point - Main.screenPosition, new Rectangle(0, 0, 1, 1), color * 0.85f, 0f, new Vector2(0.5f, 0.5f), size, SpriteEffects.None, 0f);
                }
            }

            if (preview.Active)
            {
                if (!begun)
                {
                    PoleWireDrawSystem.BeginBatch(spriteBatch);
                    begun = true;
                }

                ModContent.GetInstance<PoleWireDrawSystem>().DrawWire(spriteBatch, preview.From, preview.To, preview.Wire, preview.Invalid);
            }

            if (begun)
                spriteBatch.End();
        }

        // ---------------------------------------------------------------- drawing: text next to the cursor (an interface layer)

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
            if (index < 0)
                return;

            layers.Insert(index, new LegacyGameInterfaceLayer("Factorraria: Pole Link Text", () =>
            {
                DrawCursorText(Main.spriteBatch);
                return true;
            }, InterfaceScaleType.UI));
        }

        void DrawCursorText(SpriteBatch spriteBatch)
        {
            if (Main.dedServ || Main.gameMenu)
                return;

            Vector2 pos = Main.MouseScreen + new Vector2(18f, 18f);

            if (preview.Active)
            {
                string line = preview.Span.ToString("0.0") + " / " + preview.MaxSpan.ToString("0") + " tiles, " + preview.Cost + " wire";
                Utils.DrawBorderString(spriteBatch, line, pos, preview.Invalid ? new Color(255, 120, 120) : Color.White, 0.9f);
                pos.Y += 22f;

                if (preview.Reason != null)
                {
                    Utils.DrawBorderString(spriteBatch, preview.Reason, pos, new Color(255, 120, 120), 0.8f);
                    pos.Y += 20f;
                }
            }

            if (noticeLeft > 0 && noticeText != null)
            {
                float fade = Math.Min(1f, noticeLeft / 30f);
                Utils.DrawBorderString(spriteBatch, noticeText, pos, Color.Orange * fade, 0.85f);
                pos.Y += 22f;
            }

            if (ShowTileDebug)
                DrawTileDebug(spriteBatch, pos);
        }

        // TEMPORARY (patch P6, remove in Phase 10 with ShowTileDebug and the /ptiles command): the tile coordinates under the cursor.
        //   tile   = the tile under the zoom-corrected mouse (what the hit-testing uses)
        //   target = Player.tileTargetX/Y (what vanilla and the wire items use). The two should always be the same; if not, say so.
        //   pole   = the pole whose click zone contains the tile (its entity top-left), and whether the tile is in its footprint column
        //   reach  = IsTargetTileInItemRange for the held wire (only shown while wire is held)
        void DrawTileDebug(SpriteBatch spriteBatch, Vector2 pos)
        {
            Vector2 mouse = CartSystem.GetZoomCorrectedMouseWorld();
            int tx = (int)(mouse.X / 16f);
            int ty = (int)(mouse.Y / 16f);

            string line1 = "tile (" + tx + ", " + ty + ")  target (" + Player.tileTargetX + ", " + Player.tileTargetY + ")" + (tx != Player.tileTargetX || ty != Player.tileTargetY ? "  MISMATCH" : "");
            Utils.DrawBorderString(spriteBatch, line1, pos, Color.Yellow, 0.8f);
            pos.Y += 20f;

            string line2;
            if (TryFindPoleAtTile(tx, ty, out PowerPoleTileEntityBase pole))
            {
                bool inFootprint = tx == pole.Position.X && ty >= pole.Position.Y && ty < pole.Position.Y + PoleLinkSystem.FootprintHeight(pole);
                line2 = "pole (" + pole.Position.X + ", " + pole.Position.Y + ")  " + (inFootprint ? "footprint" : "crossboard/slack") + "  links " + PoleLinkSystem.Instance.LinksOf(pole.Position).Count + "/" + PoleLinkSystem.Instance.SlotCount(pole);
            }
            else
                line2 = "no pole here";

            if (Pending)
                line2 += "  pending from (" + pendingPole.X + ", " + pendingPole.Y + ")#" + pendingSlot;

            Utils.DrawBorderString(spriteBatch, line2, pos, Color.Yellow, 0.8f);
            pos.Y += 20f;

            if (holdingWire)
                Utils.DrawBorderString(spriteBatch, "wire " + heldWire + " x" + PoleLinkNet.CountWire(Main.LocalPlayer, heldItem.type) + "  reach " + (Main.LocalPlayer.IsTargetTileInItemRange(heldItem) ? "ok" : "OUT"), pos, Color.Yellow, 0.8f);
        }
    }
}