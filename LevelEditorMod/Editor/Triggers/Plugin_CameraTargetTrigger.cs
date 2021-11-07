﻿using Celeste;

namespace LevelEditorMod.Editor.Triggers {
    [Plugin("cameraTargetTrigger")]
    public class Plugin_CameraTargetTrigger : Plugin_Trigger {
        [Option("deleteFlag")] public string DeleteFlag = "";
        [Option("lerpStrength")] public float LerpStrength = 1;
        [Option("positionMode")] public Trigger.PositionModes PositionMode = Trigger.PositionModes.NoEffect;
        [Option("xOnly")] public bool XOnly = false;
        [Option("yOnly")] public bool YOnly = false;
    }
}
