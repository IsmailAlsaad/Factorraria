using Luminance.Common.Easings;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Terraria;

namespace Factorraria.Content.Particles
{
    public sealed class PuffExplosion : Particle
    {
        public override string AtlasTextureName => "Factorraria.Puff_Explosion.png";

        public PuffExplosion(Vector2 position, Vector2 velocity, Color color, int lifetime)
        {
            Position = position;
            Velocity = velocity;
            DrawColor = color;
            Lifetime = lifetime;
            Scale = Vector2.One;
        }

        public override void Update()
        {
            Velocity *= 0.95f;

            DrawColor = Color.Lerp(DrawColor, new Color(0, 0, 0, 0), EasingCurves.Exp.Evaluate(EasingType.In,LifetimeRatio));
        }
    }
}
