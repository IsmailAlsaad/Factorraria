using Factorraria.Common.Net;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Factorraria.Common.Carts
{
    /// <summary>
    /// Empty carts carry one NPC or enemy. Capture, carry, release and saving run on the server and in single-player only (CartCargo.Update
    /// and CartCargo.OnBump already return on a multiplayer client). A client only snaps the riding NPC to its cart replica every tick
    /// (ClientTick), using the carrier id the NPC's sync carries (CartPassengerGlobalNPC). Tuning numbers live in CartPhysics.
    /// </summary>
    public static class CartPassengers
    {
        private static int WormAiStyle = 6; // vanilla worm AI (NPCAIStyleID.Worm)

        // NPCs a cart may take, filtered once per game tick and shared by every cart (same idea as CartCargo's world item candidates).
        private static readonly List<NPC> candidates = new List<NPC>();
        private static uint candidatesTick = uint.MaxValue;

        /// <summary>Bosses, segmented / worm NPCs (taking a head would leave the body behind), invulnerable NPCs and projectile-NPCs never ride.</summary>
        public static bool CanCarry(NPC npc)
        {
            if (!npc.active || npc.life <= 0 || npc.type <= 0)
            {
                return false;
            }

            if (npc.boss || NPCID.Sets.ShouldBeCountedAsBoss[npc.type])
            {
                return false;
            }

            if (npc.realLife >= 0 || npc.aiStyle == WormAiStyle)
            {
                return false;
            }

            if (npc.immortal || npc.dontTakeDamage || NPCID.Sets.ProjectileNPC[npc.type])
            {
                return false;
            }

            return true;
        }

        /// <summary>The NPC this cart really carries right now (it is alive and its tag points back at this cart), or null.</summary>
        public static NPC GetPassenger(Cart cart)
        {
            if (cart.PassengerNpc < 0 || cart.PassengerNpc >= Main.maxNPCs)
            {
                return null;
            }

            NPC npc = Main.npc[cart.PassengerNpc];

            if (!npc.active || npc.GetGlobalNPC<CartPassengerGlobalNPC>().CarrierCartId != cart.Id)
            {
                return null; // died, despawned, or the slot now holds another NPC (its tag was reset by SetDefaults)
            }

            return npc;
        }

        /// <summary>Server / single-player, every tick for every cart (called from CartCargo.Update).</summary>
        public static void Update(Cart cart)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                return;
            }

            if (cart.Module != CartModule.Empty)
            {
                // A chest, terrarium or motor was installed: the passenger gets out.
                if (cart.PassengerNpc >= 0)
                {
                    Release(cart, false);
                }

                cart.PendingPassenger = null;
                return;
            }

            if (cart.PassengerNpc >= 0)
            {
                NPC passenger = GetPassenger(cart);

                if (passenger != null)
                {
                    Pin(cart, passenger);
                    return;
                }

                cart.PassengerNpc = -1;
            }

            if (cart.PendingPassenger != null)
            {
                TryRestore(cart);
                return;
            }

            TryCapture(cart);
        }

        /// <summary>The cart bounced off a bumper (CartCargo.OnBump): the passenger is thrown out.</summary>
        public static void OnBump(Cart cart)
        {
            if (cart.PassengerNpc >= 0)
            {
                Release(cart, true);
            }
        }

        private static List<NPC> GetCandidates()
        {
            if (candidatesTick == Main.GameUpdateCount)
            {
                return candidates;
            }

            candidatesTick = Main.GameUpdateCount;
            candidates.Clear();

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];

                if (!npc.active || !CanCarry(npc))
                {
                    continue;
                }

                CartPassengerGlobalNPC tag = npc.GetGlobalNPC<CartPassengerGlobalNPC>();

                if (tag.CarrierCartId > 0 || tag.PickupCooldown > 0)
                {
                    continue;
                }

                candidates.Add(npc);
            }

            return candidates;
        }

        private static void TryCapture(Cart cart)
        {
            List<NPC> list = GetCandidates();

            if (list.Count == 0)
            {
                return;
            }

            float pad = CartPhysics.PassengerPickupPadding;
            float maxDistance = CartPhysics.PassengerPickupRange;

            for (int i = 0; i < list.Count; i++)
            {
                NPC npc = list[i];

                if (!npc.active)
                {
                    continue;
                }

                CartPassengerGlobalNPC tag = npc.GetGlobalNPC<CartPassengerGlobalNPC>();

                if (tag.CarrierCartId > 0)
                {
                    continue; // another cart took it earlier this tick
                }

                float reach = maxDistance + npc.height; // a tall NPC's centre is far above the rail point even when it stands on the cart

                if (Vector2.DistanceSquared(cart.Position, npc.Center) > reach * reach)
                {
                    continue;
                }

                if (!Touches(cart, npc, pad))
                {
                    continue;
                }

                Capture(cart, npc, tag);
                return;
            }
        }

        /// <summary>
        /// The NPC is taken when its hitbox reaches the cart's box (grown by pad) and its centre is over the cart along the floor. A hitbox test
        /// is needed because a tall NPC's centre stands well above the cart box (35 px tall): the old centre-only test could only take an Ogre
        /// when its walk bob happened to put the centre inside, which is why pickups were hit and miss.
        /// </summary>
        private static bool Touches(Cart cart, NPC npc, float pad)
        {
            Rectangle box = cart.Hitbox;
            box.Inflate((int)pad, (int)pad);

            if (!box.Intersects(npc.Hitbox))
            {
                return false;
            }

            float along = Vector2.Dot(npc.Center - CartGeometry.BoxCenter(cart), CartGeometry.Right(cart));

            return Math.Abs(along) <= CartPhysics.HitboxWidth * 0.5f + pad;
        }

        /// <summary>announce = false for a passenger that is put back after a world load (no pickup sound then).</summary>
        private static void Capture(Cart cart, NPC npc, CartPassengerGlobalNPC tag, bool announce = true)
        {
            tag.CarrierCartId = cart.Id;
            tag.GraceTicks = 0;
            cart.PassengerNpc = npc.whoAmI;

            Pin(cart, npc);

            if (announce)
            {
                CartSound.Play(CartSoundKind.PassengerPickup, npc.Center); // after the pin, so the puff is where it now sits
            }

            SyncNpc(npc);
        }

        /// <summary>
        /// Lets the passenger go. With launch (a bump) it is thrown the way the cart was travelling; otherwise it just drops out where it is.
        /// Either way it gets the pickup cooldown and, unless it is a town NPC, the despawn grace period.
        /// </summary>
        public static void Release(Cart cart, bool launch)
        {
            NPC npc = GetPassenger(cart);
            cart.PassengerNpc = -1;

            if (npc == null)
            {
                return;
            }

            CartPassengerGlobalNPC tag = npc.GetGlobalNPC<CartPassengerGlobalNPC>();
            tag.CarrierCartId = 0;
            tag.PickupCooldown = CartPhysics.PassengerPickupCooldown;
            tag.GraceTicks = npc.townNPC ? 0 : CartPhysics.PassengerGraceTicks;
            npc.rotation = 0f; // it was tilted with the cart; the hitbox does not turn, so nothing jumps

            CartSound.Play(CartSoundKind.PassengerRelease, npc.Center);

            if (launch)
            {
                npc.velocity = new Vector2(0f, -CartPhysics.PassengerLaunchLift);
            }
            else
            {
                npc.velocity = Vector2.Zero;
            }

            SyncNpc(npc);
        }

        /// <summary>
        /// How far (px) a passenger is lifted above the cart floor, on top of PassengerFloorLift, so that a short one still shows above the
        /// cart's front wall: the shorter the NPC, the higher it sits, and a tall one is not lifted at all. It only uses fixed tuning numbers
        /// and npc.height, so the server and every client work out the same value.
        /// </summary>
        public static float GetExtraLift(NPC npc)
        {
            float floor = CartPhysics.PassengerFloorLift;
            float wanted = CartPhysics.PassengerWallHeight + CartPhysics.PassengerMinVisible - floor - npc.height;
            float highest = Math.Max(0f, CartPhysics.PassengerWallHeight - CartPhysics.PassengerMinInside - floor);

            return MathHelper.Clamp(wanted, 0f, highest);
        }

        /// <summary>
        /// Puts the passenger in the cart: its hitbox CENTRE on the cart's up axis (floor lift + extra lift + half its height), tilted with the
        /// cart and facing the way the cart faces. The centre is what is placed because a sprite turns about its centre while the cart turns
        /// about its feet. Server and clients both use this, and the draw calls it once more (DrawRider) so the picture is exact whatever
        /// vanilla did to the NPC in between.
        /// </summary>
        public static void Pin(Cart cart, NPC npc)
        {
            npc.Center = cart.Position + CartGeometry.Up(cart) * (CartPhysics.PassengerFloorLift + GetExtraLift(npc) + npc.height * 0.5f);
            npc.velocity = Vector2.Zero;
            npc.oldPosition = npc.position;
            npc.netOffset = Vector2.Zero;
            npc.rotation = cart.Rotation;

            int facing = cart.Facing < 0 ? -1 : 1;
            npc.direction = facing;
            npc.spriteDirection = facing;
        }

        /// <summary>Server: sends the NPC now, so clients learn at once that it was picked up or released (the carrier id rides in the sync).</summary>
        private static void SyncNpc(NPC npc)
        {
            if (Main.netMode == NetmodeID.Server)
            {
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
            }
        }

        /// <summary>Client, every tick after the cart replicas moved (CartNet.ClientTick): snaps every riding NPC to its cart so it does not lag behind.</summary>
        public static void ClientTick()
        {
            if (CartSystem.Carts.Count == 0)
            {
                return;
            }

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];

                if (!npc.active)
                {
                    continue;
                }

                int carrierId = npc.GetGlobalNPC<CartPassengerGlobalNPC>().CarrierCartId;

                if (carrierId <= 0)
                {
                    continue;
                }

                Cart cart = CartSystem.FindById(carrierId);

                if (cart != null)
                {
                    Pin(cart, npc);
                }
            }
        }

        #region Drawing (clients and single-player only; a dedicated server never draws)

        /// <summary>True only while DrawRider draws a passenger: CartPassengerGlobalNPC.PreDraw lets exactly that draw through and hides vanilla's own.</summary>
        public static bool DrawingPassenger;

        // cart id -> the NPC riding it, rebuilt once per frame by CollectRiders from the carrier ids every copy of the game has
        private static readonly Dictionary<int, NPC> riders = new Dictionary<int, NPC>();

        /// <summary>Once per frame before the carts are drawn (CartSystem.PostDrawTiles).</summary>
        public static void CollectRiders()
        {
            riders.Clear();

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];

                if (!npc.active)
                {
                    continue;
                }

                int carrierId = npc.GetGlobalNPC<CartPassengerGlobalNPC>().CarrierCartId;

                if (carrierId > 0)
                {
                    riders[carrierId] = npc;
                }
            }
        }

        /// <summary>
        /// Draws this cart's passenger. Call it right BEFORE the cart's own sprite: the cart wall then covers the NPC's lower part, so it looks
        /// like it sits inside, instead of depending on where vanilla's NPC pass lands relative to PostDrawTiles. It is drawn behind every
        /// other NPC and the players. The pin repeats here so the NPC is exactly on the cart as it is drawn this frame.
        /// </summary>
        public static void DrawRider(Cart cart)
        {
            if (!riders.TryGetValue(cart.Id, out NPC npc) || !npc.active || npc.GetGlobalNPC<CartPassengerGlobalNPC>().CarrierCartId != cart.Id)
            {
                return;
            }

            Pin(cart, npc);
            DrawingPassenger = true;

            try
            {
                Main.instance.DrawNPCCheckAlt(npc); // town NPC texture variants (party hats, shimmer...), same call vanilla's NPC pass makes
                Main.instance.DrawNPC(npc.whoAmI, false);
            }
            finally
            {
                DrawingPassenger = false;
            }
        }

        #endregion

        #region Saving: passengers stay in their carts across exit and rejoin

        /// <summary>
        /// Town NPCs (and the few others the game saves itself) are kept by the game, so the cart only remembers which one rode it. Enemies are
        /// never saved by Terraria, so the cart stores enough to spawn the same kind of enemy again with the same share of its health.
        /// </summary>
        private static bool IsSavedByTheGame(NPC npc)
        {
            return NPCLoader.SavesAndLoads(npc); // town NPCs, bound NPCs and whatever mods save
        }

        /// <summary>The "passenger" entry for Cart.Save, or null. A passenger that is still waiting to be restored is saved unchanged.</summary>
        public static TagCompound Save(Cart cart)
        {
            NPC npc = GetPassenger(cart);

            if (npc == null)
            {
                return cart.PendingPassenger;
            }

            TagCompound tag = new TagCompound();
            tag["type"] = NPCID.Search.GetName(npc.type);
            tag["netId"] = npc.netID;
            tag["life"] = npc.life;
            tag["lifeMax"] = npc.lifeMax;
            tag["persisted"] = IsSavedByTheGame(npc);

            if (!string.IsNullOrEmpty(npc.GivenName))
            {
                tag["name"] = npc.GivenName;
            }

            return tag;
        }

        /// <summary>
        /// Server, every tick while the cart has a saved passenger (set by Cart.Load). Runs after the world finished loading, so the game's own
        /// NPCs exist. Retries for a while (no free NPC slot yet, town NPC not loaded yet), then gives up.
        /// </summary>
        private static void TryRestore(Cart cart)
        {
            TagCompound saved = cart.PendingPassenger;
            cart.PassengerRestoreTries++;

            if (cart.PassengerRestoreTries > CartPhysics.PassengerRestoreTries)
            {
                LogInfo("[Carts] gave up restoring the passenger of cart " + cart.Id + " (" + saved.GetString("type") + ")");
                cart.PendingPassenger = null;
                return;
            }

            if (!NPCID.Search.TryGetId(saved.GetString("type"), out int type))
            {
                LogInfo("[Carts] cart " + cart.Id + " carried an NPC that no longer exists (" + saved.GetString("type") + "), dropped");
                cart.PendingPassenger = null;
                return;
            }

            Vector2 bottom = cart.Position + CartGeometry.Up(cart) * CartPhysics.PassengerFloorLift;
            NPC npc = saved.GetBool("persisted") ? FindSavedNpc(type, saved.GetString("name"), bottom) : SpawnNpc(type, saved, bottom);

            if (npc == null)
            {
                return; // try again next tick
            }

            cart.PendingPassenger = null;
            Capture(cart, npc, npc.GetGlobalNPC<CartPassengerGlobalNPC>(), false);
        }

        private static NPC FindSavedNpc(int type, string givenName, Vector2 near)
        {
            NPC best = null;
            float bestDistance = CartPhysics.PassengerRestoreRange * CartPhysics.PassengerRestoreRange;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];

                if (!npc.active || npc.type != type || npc.GetGlobalNPC<CartPassengerGlobalNPC>().CarrierCartId > 0)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(givenName) && npc.GivenName != givenName)
                {
                    continue;
                }

                float distance = Vector2.DistanceSquared(npc.Center, near);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = npc;
                }
            }

            return best;
        }

        private static NPC SpawnNpc(int type, TagCompound saved, Vector2 bottom)
        {
            int index = NPC.NewNPC(new EntitySource_Misc("FactorrariaCartPassenger"), (int)bottom.X, (int)bottom.Y, type);

            if (index < 0 || index >= Main.maxNPCs)
            {
                return null;
            }

            NPC npc = Main.npc[index];

            int netId = saved.GetInt("netId");

            if (netId < 0 && npc.netID != netId)
            {
                npc.SetDefaults(netId); // a vanilla variant such as the coloured slimes
            }

            int savedMax = saved.GetInt("lifeMax");

            if (savedMax > 0)
            {
                float fraction = MathHelper.Clamp(saved.GetInt("life") / (float)savedMax, 0f, 1f);
                npc.life = Math.Clamp((int)Math.Ceiling(npc.lifeMax * fraction), 1, npc.lifeMax);
            }

            return npc;
        }

        private static void LogInfo(string message)
        {
            ModContent.GetInstance<CartSystem>().Mod.Logger.Info(message);
        }

        #endregion
    }
}