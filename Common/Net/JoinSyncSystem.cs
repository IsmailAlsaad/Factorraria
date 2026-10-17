using System.IO;
using Terraria.ModLoader;

namespace Factorraria.Common.Net
{
    // Registers the join-sync packets, runs the server-side pump and carries the two TEMPORARY test providers used to prove the
    // mechanism (a counter and a 20000-byte blob that needs several chunks). Real providers arrive with patches 3.1, 3.2, 3.4,
    // 7.2 and 9.2; remove the test providers in Phase 10.
    public class JoinSyncSystem : ModSystem
    {
        public static bool TestProvidersEnabled = true;
        public static int TestBlobSize = 20000;

        static int testCounter;

        /// <summary>Client: the counter value the server sent in the last full-state sync (0 = none yet).</summary>
        public static int LastTestCounter;

        public override void Load()
        {
            FactorrariaNet.Register(FactorrariaPacket.JoinRequest, PacketDirection.ToServer, JoinSync.HandleRequest);
            FactorrariaNet.Register(FactorrariaPacket.JoinChunk, PacketDirection.ToClient, JoinSync.HandleChunk);
            FactorrariaNet.Register(FactorrariaPacket.JoinDone, PacketDirection.ToClient, JoinSync.HandleDone);

            if (TestProvidersEnabled)
            {
                JoinSync.Register("test-counter", WriteTestCounter, ReadTestCounter);
                JoinSync.Register("test-blob", WriteTestBlob, ReadTestBlob);
            }
        }

        public override void PostUpdateEverything()
        {
            JoinSync.ServerTick();
        }

        public override void Unload()
        {
            JoinSync.Clear();
            testCounter = 0;
            LastTestCounter = 0;
        }

        // ---------------------------------------------------------------- test providers

        static void WriteTestCounter(BinaryWriter writer)
        {
            writer.Write(++testCounter);
        }

        static void ReadTestCounter(BinaryReader reader)
        {
            LastTestCounter = reader.ReadInt32();
            FactorrariaNet.Log("join test provider: counter = " + LastTestCounter);
        }

        static byte BlobByte(int index) => (byte)((index * 31 + 7) % 251);

        static void WriteTestBlob(BinaryWriter writer)
        {
            writer.Write(TestBlobSize);
            int sum = 0;
            for (int i = 0; i < TestBlobSize; i++)
            {
                byte b = BlobByte(i);
                writer.Write(b);
                sum = unchecked(sum * 31 + b);
            }
            writer.Write(sum);
        }

        static void ReadTestBlob(BinaryReader reader)
        {
            int size = reader.ReadInt32();
            if (size < 0 || size > 1000000)
                throw new InvalidDataException("test blob size " + size);

            bool ok = true;
            int sum = 0;
            for (int i = 0; i < size; i++)
            {
                byte b = reader.ReadByte();
                if (b != BlobByte(i))
                    ok = false;
                sum = unchecked(sum * 31 + b);
            }

            if (reader.ReadInt32() != sum)
                ok = false;

            FactorrariaNet.Log("join test provider: blob " + size + " bytes " + (ok ? "OK" : "MISMATCH"));
        }
    }
}