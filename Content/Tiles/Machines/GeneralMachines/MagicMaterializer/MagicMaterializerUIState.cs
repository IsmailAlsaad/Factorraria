using Factorraria.Common.UI;
using Factorraria.Content.Configs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace Factorraria.Content.Tiles.Machines.GeneralMachines.MagicMaterializer
{
    public class MagicMaterializerUIState : MachineUIStateBase
    {
        MagicMaterializerTileEntity Entity => (MagicMaterializerTileEntity)CurrentEntity;

        public override Vector2 BasePanelOffset => new Vector2(-30, -60);

        protected override List<MachineUIElementEntry> BuildElements()
        {
            var list = new List<MachineUIElementEntry>();

            list.Add(FuelTankEntry(0, new Vector2(0, 0)));
            list.Add(new MachineUIElementEntry(new MaterializerStatusUIElement(() => Entity), new Vector2(30, 0), new Vector2(150, 84)));

            return list;
        }
    }

    // Three text lines next to the tank: Active/Idle, awakened statue count, live enemy count.
    public class MaterializerStatusUIElement : UIElement, IZoomScalable
    {
        readonly Func<MagicMaterializerTileEntity> getEntity;
        float zoom = 1f;

        public MaterializerStatusUIElement(Func<MagicMaterializerTileEntity> getEntity)
        {
            this.getEntity = getEntity;
        }

        public void SetZoomScale(float zoomScale) => zoom = zoomScale;

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            MagicMaterializerTileEntity entity = getEntity();
            if (entity == null) return;

            CalculatedStyle dimensions = GetDimensions();
            Vector2 position = new Vector2(dimensions.X, dimensions.Y);
            float scale = 0.9f * zoom;
            float lineHeight = 26f * zoom;

            Color stateColor = entity.IsAwakening ? new Color(190, 150, 255) : Color.Gray;
            Utils.DrawBorderString(spriteBatch, entity.IsAwakening ? "Active" : "Idle", position, stateColor, scale);
            Utils.DrawBorderString(spriteBatch, "Statues: " + entity.AwakenedStatueCount, position + new Vector2(0f, lineHeight), Color.White, scale);
            Utils.DrawBorderString(spriteBatch, "Enemies: " + entity.AwakenedEnemyCount, position + new Vector2(0f, lineHeight * 2f), Color.White, scale);
        }
    }
}