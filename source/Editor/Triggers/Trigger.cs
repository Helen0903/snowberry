﻿using Microsoft.Xna.Framework;
using Monocle;
using System.Text.RegularExpressions;

namespace Snowberry.Editor.Triggers;

[Plugin("checkpointBlockerTrigger")]
[Plugin("goldenBerryCollectTrigger")]
[Plugin("lookoutBlocker")]
[Plugin("stopBoostTrigger")]
[Plugin("windAttackTrigger")]
[Plugin("birdPathTrigger")]
[Plugin("everest/completeAreaTrigger")]
public class Trigger : Entity {
    protected virtual Color Color { get; } = Calc.HexToColor("0c5f7a");
    protected string Text { get; private set; }

    public override int MinWidth => 8;
    public override int MinHeight => 8;

    public override bool IsTrigger => true;

    public override void Initialize() {
        base.Initialize();
        Text = string.Join(" ", Regex.Split(char.ToUpper(Name[0]) + Name.Substring(1), @"(?=[A-Z])")).Trim();
    }

    public override void Render() {
        base.Render();

        Rectangle rect = new Rectangle((int)Position.X, (int)Position.Y, Width, Height);
        Draw.Rect(rect, Color * 0.3f);
        Draw.HollowRect(rect, Color);

        Fonts.Pico8.Draw(Text, new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f), Vector2.One, Vector2.One * 0.5f, Color.Black);
    }

    public static void AddPlacements() {
        Placements.Create("Checkpoint Blocker Trigger", "checkpointBlockerTrigger", trigger: true);
        Placements.Create("Golden Berry Collect Trigger", "goldenBerryCollectTrigger", trigger: true);
        Placements.Create("Lookout Blocker", "lookoutBlocker", trigger: true);
        Placements.Create("Stop Boost Trigger", "stopBoostTrigger", trigger: true);
        Placements.Create("Wind Attack Trigger", "windAttackTrigger", trigger: true);
        Placements.Create("Bird Path Trigger", "birdPathTrigger", trigger: true);
        Placements.Create("Complete Area Trigger (Everest)", "everest/completeAreaTrigger", trigger: true);
    }
}