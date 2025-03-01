﻿using Microsoft.Xna.Framework;
using static Celeste.Trigger;

namespace Snowberry.Editor.Triggers;

[Plugin("lightFadeTrigger")]
public class Plugin_LightFadeTrigger : Trigger {
    [Option("positionMode")] public PositionModes PositionMode = PositionModes.NoEffect;
    [Option("lightAddFrom")] public float From = 0;
    [Option("lightAddTo")] public float To = 0;

    public override void Render() {
        base.Render();
        var str = (PositionMode == PositionModes.NoEffect || From == To) ? $"(light = {To})" : $"(light: {From} -> {To})";
        Fonts.Pico8.Draw(str, Center + Vector2.UnitY * 6, Vector2.One, new(0.5f), Color.Black);
    }

    public new static void AddPlacements() {
        Placements.Create("Light Fade Trigger", "lightParamTrigger", trigger: true);
    }
}