using Microsoft.Xna.Framework;
using System;
using Terraria.UI;

namespace Factorraria.Common.UI
{
    // MachineUIElementEntry.cs
    // A bundle: one UI element + where it sits + how big it is, both measured at zoom = 1 (normal size).
    // Nothing about "how to resize" lives here anymore — resizing is 100% generic now (see MachineUIStateBase).
    public class MachineUIElementEntry
    {
        public UIElement Element;
        public Vector2 BasePosition; // Left/Top at zoom = 1
        public Vector2 BaseSize;     // Width/Height at zoom = 1

        public MachineUIElementEntry(UIElement element, Vector2 basePosition, Vector2 baseSize)
        {
            Element = element;
            BasePosition = basePosition;
            BaseSize = baseSize;
        }
    }
}
