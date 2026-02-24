using System;
using Godot;

namespace ChaosArcadeTower.Presentation.Shared
{
    /// <summary>
    /// A Button that supports Godot drag-and-drop for piece slot swapping.
    /// SlotCode encoding: active slots = index (0..4), reserve slots = -(reserveIndex+1).
    /// </summary>
    public partial class DraggableSlot : Button
    {
        public int SlotCode { get; set; }
        public Action<int, int>? OnSwapRequested { get; set; }

        public static int ActiveCode(int index) => index;
        public static int ReserveCode(int reserveIndex) => -(reserveIndex + 1);
        public static bool IsReserve(int code) => code < 0;
        public static int ToIndex(int code) => code >= 0 ? code : -(code + 1);

        public override Variant _GetDragData(Vector2 atPosition)
        {
            if (string.IsNullOrEmpty(Text) || Text.Contains("[Empty]") || Text == "Empty")
                return default;

            var preview = new Label
            {
                Text = Text,
                Modulate = new Color(1f, 1f, 1f, 0.7f)
            };
            preview.AddThemeFontSizeOverride("font_size", 12);
            SetDragPreview(preview);

            return SlotCode;
        }

        public override bool _CanDropData(Vector2 atPosition, Variant data)
        {
            if (data.VariantType != Variant.Type.Int) return false;
            int source = data.AsInt32();
            return source != SlotCode;
        }

        public override void _DropData(Vector2 atPosition, Variant data)
        {
            int source = data.AsInt32();
            OnSwapRequested?.Invoke(source, SlotCode);
        }
    }
}
