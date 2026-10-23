using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Factorraria.Common.Carts
{
    /// <summary>
    /// Per-NPC state for cart passengers. While CarrierCartId is above 0 the NPC rides that cart: its AI is off, it cannot hurt
    /// players and it cannot despawn (the "don't despawn" flag). After a bump releases it, GraceTicks keeps vanilla from despawning
    /// it for a while, then vanilla rules apply again. The carrier id reaches clients through the NPC's own sync (SendExtraAI), so
    /// no packet of ours is needed; the logic itself (CartPassengers) only runs on the server and in single-player.
    /// </summary>
    public class CartPassengerGlobalNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>Id of the cart carrying this NPC, 0 = not carried. Set by the server, copied to clients by SendExtraAI / ReceiveExtraAI.</summary>
        public int CarrierCartId;

        /// <summary>Server only: ticks before a released NPC can be picked up again.</summary>
        public int PickupCooldown;

        /// <summary>Server only: ticks after a release during which vanilla may not despawn this NPC.</summary>
        public int GraceTicks;

        public bool IsCarried
        {
            get { return CarrierCartId > 0; }
        }

        public override void SetDefaults(NPC npc)
        {
            CarrierCartId = 0;
            PickupCooldown = 0;
            GraceTicks = 0;
        }

        public override bool PreAI(NPC npc)
        {
            return CarrierCartId <= 0; // a passenger does nothing; CartPassengers.Pin places it every tick
        }

        public override void PostAI(NPC npc)
        {
            if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient)
            {
                return;
            }

            if (PickupCooldown > 0)
            {
                PickupCooldown--;
            }

            if (GraceTicks > 0 && CarrierCartId <= 0)
            {
                GraceTicks--;
            }
        }

        // A passenger is drawn by CartSystem (CartPassengers.DrawRider) inside its cart, behind the front wall; vanilla's own NPC pass skips it.
        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            return CarrierCartId <= 0 || CartPassengers.DrawingPassenger;
        }

        public override bool CheckActive(NPC npc)
        {
            return CarrierCartId <= 0 && GraceTicks <= 0;
        }

        public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot)
        {
            return CarrierCartId <= 0;
        }

        // Always written (one bit, plus the id while carried). tModLoader shares one bit stream between every GlobalNPC, so a mod that
        // only writes sometimes would shift the bits of the others when the reader does not know whether anything was written.
        public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter)
        {
            bool carried = CarrierCartId > 0;
            bitWriter.WriteBit(carried);

            if (carried)
            {
                binaryWriter.Write7BitEncodedInt(CarrierCartId);
            }
        }

        public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader)
        {
            bool wasCarried = CarrierCartId > 0;

            if (bitReader.ReadBit())
            {
                CarrierCartId = binaryReader.Read7BitEncodedInt();
            }
            else
            {
                CarrierCartId = 0;

                if (wasCarried)
                {
                    npc.rotation = 0f; // it got out: stop tilting it with the cart (the server resets its own copy in Release)
                }
            }
        }
    }
}