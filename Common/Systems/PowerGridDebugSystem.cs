using Factorraria.Common.Liquids;
using Factorraria.Common.Machines;
using Factorraria.Common.Systems;
using Factorraria.Content.Configs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Text;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.ObjectData;

public class PowerGridDebugSystem : ModSystem
{
    // Toggle this on/off whenever you want to enable/disable debug views
    public static bool EnableDebugView = true;

    // Palette used to give each separate network a distinct color
    private static readonly Color[] NetworkColors = new Color[]
    {
        Color.Cyan,
        Color.Lime,
        Color.Orange,
        Color.Magenta,
        Color.Yellow,
        Color.DeepSkyBlue,
        Color.Violet
    };

    public override void PostDrawTiles()
    {
        FurnaceOffsetConfig config = ModContent.GetInstance<FurnaceOffsetConfig>();

        EnableDebugView = config.EnableDebugs;

        if (!EnableDebugView)
            return;

        SpriteBatch spriteBatch = Main.spriteBatch;

        // Begin sprite batch with world camera transformation applied
        spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone,
            null,
            Main.GameViewMatrix.TransformationMatrix
        );

        // 1. Draw wire squares for every position in CustomWireSystem.WireGrid
        if (CustomWireSystem.WireGrid != null)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Color wireFill = Color.Gold * 0.35f;
            Color wireBorder = Color.Gold;

            foreach (var pos in CustomWireSystem.WireGrid.Keys)
            {
                Vector2 screenPos = new Vector2(pos.X, pos.Y) * 16f - Main.screenPosition;
                Rectangle wireRect = new Rectangle((int)screenPos.X, (int)screenPos.Y, 16, 16);

                spriteBatch.Draw(pixel, wireRect, wireFill);
                Utils.DrawRect(spriteBatch, wireRect, wireBorder);
            }
        }

        // 2. Draw active power networks and machines
        if (PowerGridSystem.ActivePowerNetworks != null)
        {
            for (int i = 0; i < PowerGridSystem.ActivePowerNetworks.Count; i++)
            {
                var network = PowerGridSystem.ActivePowerNetworks[i];
                Color gridColor = NetworkColors[i % NetworkColors.Length];

                // Calculate totals for current grid readout
                float totalSupply = 0f;
                float totalDemand = 0f;

                foreach (var producer in network.electricProducers)
                    if (producer.isWorking) totalSupply += producer.PowerSupply;

                foreach (var consumer in network.electricConsumers)
                    if (consumer.isWorking) totalDemand += consumer.PowerDemand;

                bool isStable = totalSupply >= totalDemand;
                string gridText = $"[Grid #{i + 1}]\nSupply: {totalSupply}/{totalDemand}\nStatus: {(isStable ? "STABLE" : "UNPOWERED")}";

                HashSet<TileEntity> drawnEntities = new HashSet<TileEntity>();

                // Draw Producer machine overlays
                foreach (var producer in network.electricProducers)
                {
                    if (producer is TileEntity te && drawnEntities.Add(te))
                    {
                        DrawEntityOverlay(spriteBatch, te, gridColor * 0.35f, gridColor);
                        DrawEntityText(spriteBatch, te, gridText, isStable ? Color.Lime : Color.Red);
                    }
                }

                // Draw Consumer machine overlays
                foreach (var consumer in network.electricConsumers)
                {
                    if (consumer is TileEntity te && drawnEntities.Add(te))
                    {
                        DrawEntityOverlay(spriteBatch, te, gridColor * 0.35f, gridColor);
                    }
                }
            }
        }

        // 3. Draw item/liquid slot readout over every machine tile entity, regardless
        // of whether it participates in a power network (covers e.g. FurnaceTileEntity,
        // which is a plain BaseMachine with no electric interface).
        foreach (var te in TileEntity.ByPosition.Values)
        {
            if (te is BaseMachine machine)
            {
                DrawMachineSlotText(spriteBatch, machine);
            }
        }

        spriteBatch.End();
    }

    private void DrawMachineSlotText(SpriteBatch sb, BaseMachine machine)
    {
        if (machine.InputSlots.Length == 0 && machine.OutputSlots.Length == 0 &&
            machine.InputLiquids.Length == 0 && machine.OutputLiquids.Length == 0)
        {
            return;
        }

        StringBuilder text = new StringBuilder();

        for (int i = 0; i < machine.InputSlots.Length; i++)
        {
            Item item = machine.InputSlots[i];
            text.AppendLine(item.IsAir ? $"In{i}: -" : $"In{i}: {item.type} x{item.stack}");
        }

        for (int i = 0; i < machine.OutputSlots.Length; i++)
        {
            Item item = machine.OutputSlots[i];
            text.AppendLine(item.IsAir ? $"Out{i}: -" : $"Out{i}: {item.type} x{item.stack}");
        }

        for (int i = 0; i < machine.InputLiquids.Length; i++)
        {
            LiquidStack liquid = machine.InputLiquids[i];
            text.AppendLine(liquid.IsEmpty ? $"InLiquid{i}: -" : $"InLiquid{i}: type {liquid.LiquidType} amt {liquid.Amount:F1}");
        }

        for (int i = 0; i < machine.OutputLiquids.Length; i++)
        {
            LiquidStack liquid = machine.OutputLiquids[i];
            text.AppendLine(liquid.IsEmpty ? $"OutLiquid{i}: -" : $"OutLiquid{i}: type {liquid.LiquidType} amt {liquid.Amount:F1}");
        }

        Vector2 screenPos = machine.Position.ToVector2() * 16f - Main.screenPosition;
        Vector2 labelPos = screenPos + new Vector2(20f, -10f); // offset right of the tile so it doesn't collide with the grid text on the left

        Utils.DrawBorderString(sb, text.ToString(), labelPos, Color.White, 0.7f);
    }

    private void DrawEntityOverlay(SpriteBatch sb, TileEntity te, Color fillColor, Color borderColor)
    {
        Tile tile = Main.tile[te.Position.X, te.Position.Y];
        TileObjectData data = TileObjectData.GetTileData(tile.TileType, 0);

        int width = data != null ? data.Width : 1;
        int height = data != null ? data.Height : 1;

        // Convert tile coordinate to world screen position
        Vector2 screenPos = te.Position.ToVector2() * 16f - Main.screenPosition;
        Rectangle rect = new Rectangle((int)screenPos.X, (int)screenPos.Y, width * 16, height * 16);

        // Draw filled semi-transparent box with an outline
        Texture2D pixel = TextureAssets.MagicPixel.Value;
        sb.Draw(pixel, rect, fillColor);
        Utils.DrawRect(sb, rect, borderColor);
    }

    private void DrawEntityText(SpriteBatch sb, TileEntity te, string text, Color textColor)
    {
        Vector2 screenPos = te.Position.ToVector2() * 16f - Main.screenPosition;
        Vector2 labelPos = screenPos - new Vector2(0, 45f);

        Utils.DrawBorderString(sb, text, labelPos, textColor, 0.8f);
    }
}