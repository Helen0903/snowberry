﻿using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Monocle;

namespace Snowberry.UI;

// Evil element that controls its own layouting
public class UITree : UIElement {
    public float PadLeft = 5, PadRight = 5, PadUp = 5, PadDown = 5;
    public float Spacing = 5;

    // extremely deep trees cause a lot of lag looking for nested textboxes. if those definitely don't exist, use this
    public bool NoKb = false;

    public bool Collapsed = false;
    protected UIElement Header;
    protected UIButton ToggleButton;

    public UITree(UIElement header, bool collapsed = false) : this(header, new(0, 3), new(5, 0), collapsed) {}

    public UITree(UIElement header, Vector2 headerOffset, Vector2 buttonOffset, bool collapsed = false) {
        RenderChildren = false;
        Collapsed = collapsed;

        Header = new UIElement();
        Header.AddRight(header, headerOffset);
        Header.AddRight(ToggleButton = new UIButton(Collapsed ? "\u2190" : "\u2193", Fonts.Regular, 2, 2) {
            OnPress = () => {
                Collapsed = !Collapsed;
                ToggleButton.SetText(Collapsed ? "\u2190" : "\u2193");
                LayoutUp();
            }
        }, buttonOffset);
        Header.CalculateBounds();
        Add(Header);
    }

    public override void Update(Vector2 position = default) {
        ToggleButton.Active = Active;

        if (UIScene.Instance is not { /* non-null */ } scene) {
            base.Update(position);
            return;
        }

        // relies on Relayout sending hidden entities to 9999,9999 but that's like fine yknow
        bool mouseClicked = scene.MouseClicked;
        scene.MouseClicked = mouseClicked || !new Rectangle((int)position.X, (int)position.Y, Width, Height).Contains(Mouse.Screen.ToPoint());
        base.Update(position);
        scene.MouseClicked = mouseClicked;
    }

    public override void Render(Vector2 position = default) {
        // note that we're responsible for drawing *all* children, but not e.g. backgrounds or bounds
        base.Render(position);

        if (Collapsed)
            Header.Render(position + Header.Position);
        else {
            var screenBounds = UIScene.Instance?.UI?.Bounds ?? Rectangle.Empty;
            foreach (var element in Children.Where(element => element.Visible && element.Bounds.Intersects(screenBounds)))
                element.Render(position + element.Position);
        }
        Draw.Rect(position + new Vector2(0, PadUp), 1, Height - PadUp - PadDown, Color.White);
        Draw.Rect(position + new Vector2(1, PadUp + 1), 1, Height - PadUp - PadDown - 2, Color.Gray);
    }

    public void Layout() {
        if (Collapsed) {
            foreach (UIElement e in Children) // just make sure they're not inside our bounds during Update
                e.Position = new(9999);
            Header.Position = new(PadLeft, PadUp);
            Width = (int)((Header.Position.X + Header.Width));
            Height = (int)((Header.Position.Y + Header.Height));
        } else {
            int i = 0;
            foreach (UIElement e in Children.Except(toRemove)) {
                if (e.Active) {
                    e.Position = new(PadLeft, PadUp + i);
                    i += (int)(e.Height + Spacing);
                } else
                    e.Position = new(0);
            }
            CalculateBounds();
        }
        Width += (int)PadRight;
        Height += (int)PadDown;
    }

    public void ApplyUp(Action<UITree> action) {
        action(this);
        (Parent as UITree)?.ApplyUp(action);
    }

    public void ApplyDown(Action<UITree> action) {
        foreach(UITree tree in Children.OfType<UITree>())
            tree.ApplyDown(action);
        action(this);
    }

    public void LayoutUp() => ApplyUp(x => x.Layout());
    public void LayoutDown() => ApplyDown(x => x.Layout());

    public override bool NestedGrabsKeyboard() => !NoKb && base.NestedGrabsKeyboard();
}