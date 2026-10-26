using Factorraria.Common.Machines;
using Factorraria.Common.Net;
using Factorraria.Content.Tiles.Machines.ElectricalConsumers.PowerPoles;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;

namespace Factorraria.Common.Systems
{
    // One hanging wire between two power poles. Poles are identified by their entity's top-left tile (Point16), not by entity ID, so a
    // link survives the entity bootstrap scan and entity re-sharing; the entity may be missing on a process that has not seen it yet.
    public class PoleLink
    {
        public Point16 A;             // entity top-left of pole A
        public Point16 B;             // entity top-left of pole B
        public byte SlotA;            // connection point index on pole A (PowerPoleTileEntityBase.GetLinkPointWorld)
        public byte SlotB;            // connection point index on pole B
        public CustomWireType Wire;   // one single type: Copper, Tin or Gold
        public ushort Cost;           // wire items paid, refunded when the link goes away

        public bool Touches(Point16 pole) => A == pole || B == pole;
        public Point16 Other(Point16 pole) => A == pole ? B : A;
        public byte SlotOn(Point16 pole) => A == pole ? SlotA : SlotB;
    }

    // The link graph (power poles patch P3). Authority: the server (and single-player) owns it, saves it and changes it; a multiplayer
    // client only holds a replica that PoleLinkNet fills from the server (join sync, PoleLinkChanged). Nothing here sends packets.
    // Every change sets PowerGridSystem.gridNeedsRebuilding (the server's flood fill learns about links in patch P4).
    public class PoleLinkSystem : ModSystem
    {
        // ---- Tunables. Plain instance fields (not const or static) so hot reload can tweak them; patch P8 retunes them.
        public float CopperSpan = 20f;             // longest link in tiles, measured between the two connection points
        public float TinSpan = 20f;
        public float GoldSpan = 40f;
        public float CopperTilesPerItem = 5f;      // a link costs ceil(span / tilesPerItem) wire items
        public float TinTilesPerItem = 5f;
        public float GoldTilesPerItem = 8f;
        public float BreakSpanMultiplier = 2f;     // used by the pending link in patch P6: it turns red above the span and is dropped at span x this
        public float ClickSlackPixels = 16f;       // how far outside a pole's footprint or crossboard a click still counts as "on the pole"
        public int MaxSlotsPerPole = 8;            // sanity bound for save and network data (the real count is PowerPoleTileEntityBase.LinkPointCount)

        // Raised on every change, so drawing code (patch P5) can cache.
        public int Version;

        readonly List<PoleLink> links = new List<PoleLink>();
        readonly Dictionary<Point16, PoleLink[]> bySlot = new Dictionary<Point16, PoleLink[]>();

        public static PoleLinkSystem Instance => ModContent.GetInstance<PoleLinkSystem>();

        public IReadOnlyList<PoleLink> Links => links;

        // One used connection point of a pole as it goes over the wire.
        public struct SlotState
        {
            public byte Slot;
            public Point16 Other;
            public byte OtherSlot;
            public CustomWireType Wire;
            public ushort Cost;
        }

        void Changed()
        {
            Version++;
            PowerGridSystem.gridNeedsRebuilding = true;
        }

        // ---------------------------------------------------------------- lookups

        public PoleLink GetLink(Point16 pole, int slot)
        {
            return bySlot.TryGetValue(pole, out PoleLink[] slots) && slot >= 0 && slot < slots.Length ? slots[slot] : null;
        }

        public bool IsSlotFree(Point16 pole, int slot) => GetLink(pole, slot) == null;

        /// <summary>Every link of one pole (a new list).</summary>
        public List<PoleLink> LinksOf(Point16 pole)
        {
            var result = new List<PoleLink>();
            if (bySlot.TryGetValue(pole, out PoleLink[] slots))
            {
                foreach (PoleLink link in slots)
                {
                    if (link != null)
                        result.Add(link);
                }
            }

            return result;
        }

        /// <summary>
        /// The poles at the other end of this pole's links (distinct, a new list). It only returns positions: the entity of a returned pole
        /// may not exist (yet) on this process, so callers (the flood fill, patch P4) must tolerate a missing entity.
        /// </summary>
        public List<Point16> GetLinkedPoles(Point16 pole)
        {
            var result = new List<Point16>();
            if (bySlot.TryGetValue(pole, out PoleLink[] slots))
            {
                foreach (PoleLink link in slots)
                {
                    if (link == null)
                        continue;

                    Point16 other = link.Other(pole);
                    if (!result.Contains(other))
                        result.Add(other);
                }
            }

            return result;
        }

        /// <summary>The complete state of one pole: every used slot.</summary>
        public List<SlotState> GetPoleState(Point16 pole)
        {
            var result = new List<SlotState>();
            if (!bySlot.TryGetValue(pole, out PoleLink[] slots))
                return result;

            for (int i = 0; i < slots.Length; i++)
            {
                PoleLink link = slots[i];
                if (link == null)
                    continue;

                Point16 other = link.Other(pole);
                result.Add(new SlotState { Slot = (byte)i, Other = other, OtherSlot = link.SlotOn(other), Wire = link.Wire, Cost = link.Cost });
            }

            return result;
        }

        // ---------------------------------------------------------------- changes

        bool CanPlace(PoleLink link)
        {
            return link != null && link.A != link.B && link.SlotA < MaxSlotsPerPole && link.SlotB < MaxSlotsPerPole
                && IsSlotFree(link.A, link.SlotA) && IsSlotFree(link.B, link.SlotB);
        }

        PoleLink[] GetSlots(Point16 pole, int minLength)
        {
            if (!bySlot.TryGetValue(pole, out PoleLink[] slots))
            {
                slots = new PoleLink[Math.Max(minLength, 1)];
                bySlot[pole] = slots;
            }
            else if (slots.Length < minLength)
            {
                Array.Resize(ref slots, minLength);
                bySlot[pole] = slots;
            }

            return slots;
        }

        void Place(PoleLink link)
        {
            links.Add(link);
            GetSlots(link.A, link.SlotA + 1)[link.SlotA] = link;
            GetSlots(link.B, link.SlotB + 1)[link.SlotB] = link;
        }

        void ClearSlot(Point16 pole, int slot, PoleLink link)
        {
            if (!bySlot.TryGetValue(pole, out PoleLink[] slots) || slot >= slots.Length || !ReferenceEquals(slots[slot], link))
                return;

            slots[slot] = null;

            foreach (PoleLink other in slots)
            {
                if (other != null)
                    return;
            }

            bySlot.Remove(pole);
        }

        bool Unplace(PoleLink link)
        {
            if (!links.Remove(link))
                return false;

            ClearSlot(link.A, link.SlotA, link);
            ClearSlot(link.B, link.SlotB, link);
            return true;
        }

        /// <summary>Adds a link when both poles differ and both connection points are free. Returns false and changes nothing otherwise.</summary>
        public bool TryAdd(PoleLink link)
        {
            if (!CanPlace(link))
                return false;

            Place(link);
            Changed();
            return true;
        }

        public bool Remove(PoleLink link)
        {
            if (!Unplace(link))
                return false;

            Changed();
            return true;
        }

        /// <summary>Removes every link of a pole (a pole was killed) and returns them, so the caller can refund and broadcast.</summary>
        public List<PoleLink> RemoveAllFor(Point16 pole)
        {
            List<PoleLink> removed = LinksOf(pole);
            foreach (PoleLink link in removed)
                Unplace(link);

            if (removed.Count > 0)
                Changed();

            return removed;
        }

        public void Clear()
        {
            links.Clear();
            bySlot.Clear();
            Changed();
        }

        /// <summary>Client: replaces the whole graph (join sync).</summary>
        public void ReplaceAll(List<PoleLink> all)
        {
            links.Clear();
            bySlot.Clear();

            foreach (PoleLink link in all)
            {
                if (CanPlace(link))
                    Place(link);
            }

            Changed();
        }

        /// <summary>
        /// Client: the server sent the complete slot list of each of these poles. Every link that touches one of them is dropped first, then the
        /// listed ones are put back (a link between two poles of the same packet is listed twice and placed once), so a repeated or
        /// out-of-order packet cannot drift.
        /// </summary>
        public void ApplyPoleStates(List<KeyValuePair<Point16, List<SlotState>>> states)
        {
            var affected = new HashSet<Point16>();
            foreach (var state in states)
                affected.Add(state.Key);

            foreach (PoleLink link in new List<PoleLink>(links))
            {
                if (affected.Contains(link.A) || affected.Contains(link.B))
                    Unplace(link);
            }

            foreach (var state in states)
            {
                foreach (SlotState slot in state.Value)
                {
                    if (GetLink(state.Key, slot.Slot) != null)
                        continue;   // already placed from the other end's list in this packet

                    var link = new PoleLink { A = state.Key, SlotA = slot.Slot, B = slot.Other, SlotB = slot.OtherSlot, Wire = slot.Wire, Cost = slot.Cost };
                    if (CanPlace(link))
                        Place(link);
                }
            }

            Changed();
        }

        // ---------------------------------------------------------------- poles (entities and geometry)

        public static bool TryGetPole(Point16 corner, out PowerPoleTileEntityBase pole)
        {
            if (TileEntity.ByPosition.TryGetValue(corner, out TileEntity entity) && entity is PowerPoleTileEntityBase found)
            {
                pole = found;
                return true;
            }

            pole = null;
            return false;
        }

        /// <summary>Any footprint tile of a pole to its entity (false when that tile is not a pole with an entity on this process).</summary>
        public static bool TryGetPoleAtTile(int x, int y, out PowerPoleTileEntityBase pole, out Point16 corner)
        {
            pole = null;
            corner = default;

            if (!WorldGen.InWorld(x, y, 1))
                return false;

            if (!TileEntityHelper.TryGetEntityFromTile(x, y, out TileEntity entity, out corner) || entity is not PowerPoleTileEntityBase found)
                return false;

            pole = found;
            return true;
        }

        /// <summary>Footprint height in tiles, from the tile's object data (14 wooden, 16 platinum).</summary>
        public static int FootprintHeight(PowerPoleTileEntityBase pole)
        {
            TileObjectData data = TileObjectData.GetTileData(pole.ValidTileType, 0);
            return data != null ? data.Height : 1;
        }

        /// <summary>World position of the top of a pole's footprint column.</summary>
        public static Vector2 FootprintTopWorld(Point16 corner) => new Vector2(corner.X * 16f + 8f, corner.Y * 16f);

        /// <summary>True when a tile is on the pole: its footprint column or one of its crossboards (resting position), grown by ClickSlackPixels.</summary>
        public bool IsClickZone(PowerPoleTileEntityBase pole, int tileX, int tileY)
        {
            int slack = (int)Math.Ceiling(ClickSlackPixels);
            var tile = new Rectangle(tileX * 16, tileY * 16, 16, 16);

            var footprint = new Rectangle(pole.Position.X * 16, pole.Position.Y * 16, 16, FootprintHeight(pole) * 16);
            footprint.Inflate(slack, slack);
            if (footprint.Intersects(tile))
                return true;

            foreach (PoleBoard board in pole.Boards)
            {
                Vector2 center = pole.Position.ToVector2() * 16f + board.AttachOffset;
                Vector2 boardTopLeft = center - board.Pivot;
                var rect = new Rectangle((int)boardTopLeft.X, (int)boardTopLeft.Y, (int)board.Size.X, (int)board.Size.Y);
                rect.Inflate(slack, slack);
                if (rect.Intersects(tile))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Server-side plausibility test for a cut: the clicked tile is on one of the two poles, or inside the box around the two connection
        /// points (the wire lies inside it). A pole whose entity is missing here is skipped; with both missing nothing is plausible.
        /// </summary>
        public bool IsClickNearLink(PoleLink link, int tileX, int tileY)
        {
            bool hasA = TryGetPole(link.A, out PowerPoleTileEntityBase poleA);
            bool hasB = TryGetPole(link.B, out PowerPoleTileEntityBase poleB);

            if (hasA && IsClickZone(poleA, tileX, tileY))
                return true;
            if (hasB && IsClickZone(poleB, tileX, tileY))
                return true;
            if (!hasA || !hasB)
                return false;

            Vector2 p = poleA.GetLinkPointWorld(link.SlotA);
            Vector2 q = poleB.GetLinkPointWorld(link.SlotB);
            float slack = ClickSlackPixels;
            float x = tileX * 16f + 8f;
            float y = tileY * 16f + 8f;

            return x >= Math.Min(p.X, q.X) - slack && x <= Math.Max(p.X, q.X) + slack
                && y >= Math.Min(p.Y, q.Y) - slack && y <= Math.Max(p.Y, q.Y) + slack;
        }

        // ---------------------------------------------------------------- span, cost, point choice

        public float MaxSpan(CustomWireType wire)
        {
            if (wire == CustomWireType.Gold)
                return GoldSpan;

            return wire == CustomWireType.Tin ? TinSpan : CopperSpan;
        }

        public float TilesPerItem(CustomWireType wire)
        {
            if (wire == CustomWireType.Gold)
                return GoldTilesPerItem;

            return wire == CustomWireType.Tin ? TinTilesPerItem : CopperTilesPerItem;
        }

        /// <summary>Wire items one link of this length costs: ceil(span / tiles per item), at least 1.</summary>
        public int CostFor(CustomWireType wire, float spanTiles)
        {
            float perItem = Math.Max(0.01f, TilesPerItem(wire));
            return Math.Max(1, (int)Math.Ceiling(spanTiles / perItem - 0.0001f));
        }

        /// <summary>Distance in tiles between two connection points at their resting positions (the same on every process).</summary>
        public static float SpanTiles(PowerPoleTileEntityBase a, int slotA, PowerPoleTileEntityBase b, int slotB)
        {
            return Vector2.Distance(a.GetLinkPointWorld(slotA), b.GetLinkPointWorld(slotB)) / 16f;
        }

        /// <summary>How many connection points of this pole can hold a link (its points, capped by MaxSlotsPerPole).</summary>
        public int SlotCount(PowerPoleTileEntityBase pole) => Math.Min(pole.LinkPointCount, MaxSlotsPerPole);

        /// <summary>The free connection point of a pole closest to a world position, or -1 when every point is taken.</summary>
        public int ChooseFreePoint(PowerPoleTileEntityBase pole, Vector2 target)
        {
            int best = -1;
            float bestDistance = float.MaxValue;
            int count = SlotCount(pole);

            for (int i = 0; i < count; i++)
            {
                if (!IsSlotFree(pole.Position, i))
                    continue;

                float distance = Vector2.DistanceSquared(pole.GetLinkPointWorld(i), target);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return best;
        }

        // ---------------------------------------------------------------- save and load (server and single-player)

        public override void SaveWorldData(TagCompound tag)
        {
            if (links.Count == 0)
                return;   // a world without links keeps none of these keys

            var a = new List<Point16>();
            var b = new List<Point16>();
            var slotA = new List<byte>();
            var slotB = new List<byte>();
            var wire = new List<byte>();
            var cost = new List<int>();

            foreach (PoleLink link in links)
            {
                a.Add(link.A);
                b.Add(link.B);
                slotA.Add(link.SlotA);
                slotB.Add(link.SlotB);
                wire.Add((byte)link.Wire);
                cost.Add(link.Cost);
            }

            tag["PoleLinkA"] = a;
            tag["PoleLinkB"] = b;
            tag["PoleLinkSlotA"] = slotA;
            tag["PoleLinkSlotB"] = slotB;
            tag["PoleLinkWire"] = wire;
            tag["PoleLinkCost"] = cost;
        }

        public override void LoadWorldData(TagCompound tag)
        {
            links.Clear();
            bySlot.Clear();

            if (!tag.ContainsKey("PoleLinkA") || !tag.ContainsKey("PoleLinkB") || !tag.ContainsKey("PoleLinkSlotA") || !tag.ContainsKey("PoleLinkSlotB")
                || !tag.ContainsKey("PoleLinkWire") || !tag.ContainsKey("PoleLinkCost"))
            {
                return;
            }

            List<Point16> a = tag.Get<List<Point16>>("PoleLinkA");
            List<Point16> b = tag.Get<List<Point16>>("PoleLinkB");
            List<byte> slotA = tag.Get<List<byte>>("PoleLinkSlotA");
            List<byte> slotB = tag.Get<List<byte>>("PoleLinkSlotB");
            List<byte> wire = tag.Get<List<byte>>("PoleLinkWire");
            List<int> cost = tag.Get<List<int>>("PoleLinkCost");

            int count = Math.Min(a.Count, Math.Min(b.Count, Math.Min(slotA.Count, Math.Min(slotB.Count, Math.Min(wire.Count, cost.Count)))));

            // Poles are never checked against the world here: the entity bootstrap scan may not have created them yet, and links are only
            // removed when a pole is killed (MachineRemoval), never by a scan.
            for (int i = 0; i < count; i++)
            {
                var type = (CustomWireType)wire[i];
                if (!CustomWireNet.IsSingleKnownType(type))
                    continue;

                var link = new PoleLink { A = a[i], B = b[i], SlotA = slotA[i], SlotB = slotB[i], Wire = type, Cost = (ushort)Math.Clamp(cost[i], 0, ushort.MaxValue) };
                if (CanPlace(link))
                    Place(link);
            }

            Changed();
            PoleLinkNet.Trace("loaded " + links.Count + " pole link(s) from the world file");
        }

        public override void OnWorldUnload()
        {
            // Links of the world that was just left must not leak into the next one.
            links.Clear();
            bySlot.Clear();
            Changed();
        }

        public override void Unload()
        {
            links.Clear();
            bySlot.Clear();
        }
    }
}