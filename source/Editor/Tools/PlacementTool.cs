﻿using System;
using System.Collections.Generic;
using System.Linq;
using Celeste;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using Snowberry.UI;
using Placement = Snowberry.Editor.Placements.Placement;

namespace Snowberry.Editor.Tools;

public class PlacementTool : Tool {

    private const int width = 240;

    private Placement curLeftSelection, curRightSelection;
    private Dictionary<Placement, UIButton> placementButtons = new();
    private Entity preview;
    private Vector2? lastPress;
    private bool startedDrag;
    private Placement lastPlacement;
    private UISearchBar<Placement> searchBar;
    private UIScrollPane buttonPane;

    private static string search = "";

    public override UIElement CreatePanel(int height) {
        placementButtons.Clear();

        UIElement panel = new(){
            Width = width,
            Background = Calc.HexToColor("202929") * (185 / 255f),
            GrabsClick = true,
            GrabsScroll = true,
            Height = height
        };

        buttonPane = new UIScrollPane{
            Width = width,
            Background = null,
            Height = height - 30
        };
        var buttonTree = new UITree(new UILabel("placements"));

        buttonTree.Add(CreateEntitiesTree());
        buttonTree.AddBelow(CreateEntitiesTree(true));
        //buttonTree.AddBelow(CreateDecalsTree());

        buttonTree.Layout();

        buttonPane.Add(buttonTree);
        panel.Add(buttonPane);

        static bool entityMatcher(Placement entry, string term) => entry.Name.ToLower().Contains(term.ToLower());
        static bool modMatcher(Placement entry, string term) {
            var split = entry.EntityName.Split('/');
            return (split.Length >= 2 ? split[0] : "Celeste").Contains(term);
        }

        panel.Add(searchBar = new UISearchBar<Placement>(width - 10, entityMatcher) {
            Position = new Vector2(5, height - 20),
            Entries = Placements.All.ToArray(),
            InfoText = Dialog.Clean("SNOWBERRY_MAINMENU_LOADSEARCH"),
            OnInputChange = s => {
                search = s;
                buttonTree.ApplyDown(x => x.Active = x.Visible = false);
                buttonPane.Scroll = 0;
                foreach (var b in placementButtons) {
                    var button = b.Value;
                    bool active = searchBar.Found == null || searchBar.Found.Contains(b.Key);
                    if (button.Parent is UITree p) { // not a header button, need to set Active to get hidden
                        button.Visible = button.Active = active;
                        p.ApplyUp(x => x.Active |= x.Visible |= active);
                    } else // header button, get hidden by hiding the entire tree, don't set Active so sub-buttons can force this visible
                        (button.Parent?.Parent as UITree)?.ApplyUp(x => x.Active |= x.Visible |= active); // weh
                }
                buttonTree.LayoutDown();
            }
        });
        searchBar.AddSpecialMatcher('@', modMatcher, Calc.HexToColor("1b6dcc"));
        searchBar.UpdateInput(search);

        return panel;
    }

    public override string GetName() => Dialog.Clean("SNOWBERRY_EDITOR_TOOL_ENTITIES");

    public override void Update(bool canClick) {
        bool middlePan = Snowberry.Settings.MiddleClickPan;

        Placement selection = (middlePan && (MInput.Mouse.CheckRightButton || (middlePan && MInput.Mouse.ReleasedRightButton)) || !middlePan && MInput.Keyboard.Check(Keys.LeftAlt, Keys.RightAlt)) ? curRightSelection : curLeftSelection;
        if ((MInput.Mouse.ReleasedLeftButton || (middlePan && MInput.Mouse.ReleasedRightButton)) && canClick && selection != null && Editor.SelectedRoom != null) {
            Entity toAdd = selection.Build(Editor.SelectedRoom);
            UpdateEntity(toAdd);
            if (toAdd.Name != "player")
                toAdd.EntityID = AllocateId();
            Editor.SelectedRoom.AddEntity(toAdd);
        }

        RefreshPreview(lastPlacement != selection);
        lastPlacement = selection;
        if (preview != null)
            UpdateEntity(preview);

        if (MInput.Mouse.PressedLeftButton || (middlePan && MInput.Mouse.PressedRightButton))
            lastPress = Mouse.World;
        else if (!MInput.Mouse.CheckLeftButton && !(middlePan && MInput.Mouse.CheckRightButton)) {
            lastPress = null;
            startedDrag = false;
        }

        foreach (var item in placementButtons) {
            var button = item.Value;
            if (item.Key.Equals(curLeftSelection) && item.Key.Equals(curRightSelection))
                button.BG = button.PressedBG = button.HoveredBG = BothSelectedBtnBg;
            else if (item.Key.Equals(curLeftSelection))
                button.BG = button.PressedBG = button.HoveredBG = LeftSelectedBtnBg;
            else if (item.Key.Equals(curRightSelection))
                button.BG = button.PressedBG = button.HoveredBG = RightSelectedBtnBg;
            else {
                button.BG = UIButton.DefaultBG;
                button.HoveredBG = UIButton.DefaultHoveredBG;
                button.PressedBG = UIButton.DefaultPressedBG;
            }
        }
    }

    public static int AllocateId() =>
        // TODO: find lowest unoccupied ID
        Editor.Instance.Map.Rooms.SelectMany(k => k.AllEntities)
            .Select(item => item.EntityID)
            .Concat(new[]{ 0 }).Max() + 1;

    private void RefreshPreview(bool changedPlacement) {
        bool middlePan = Snowberry.Settings.MiddleClickPan;

        Placement selection = (middlePan && (MInput.Mouse.CheckRightButton || MInput.Mouse.ReleasedRightButton) || !middlePan && MInput.Keyboard.Check(Keys.LeftAlt, Keys.RightAlt)) ? curRightSelection : curLeftSelection;
        if ((preview == null || changedPlacement) && selection != null) {
            preview = selection.Build(Editor.SelectedRoom);
        } else if (selection == null)
            preview = null;
    }

    private void UpdateEntity(Entity e) {
        var ctrl = MInput.Keyboard.Check(Keys.LeftControl, Keys.RightControl);
        Vector2 mpos = ctrl ? Mouse.World : Mouse.World.RoundTo(8);
        UpdateSize(e, mpos);

        if (lastPress != null) {
            Vector2 cPress = ctrl ? lastPress.Value : lastPress.Value.RoundTo(8);
            // moved >=16 pixels -> start dragging nodes
            if ((mpos - cPress).LengthSquared() >= 16 * 16) {
                startedDrag = true;
            }

            float newX = mpos.X, newY = mpos.Y;
            // resizable entities should never move down/right of their original spot
            if (e.MinWidth != -1) newX = Math.Min(newX, cPress.X);
            if (e.MinHeight != -1) newY = Math.Min(newY, cPress.Y);
            // nodes entities should never move, their node does
            if (e.MinWidth == -1 && e.MinHeight == -1 && e.MinNodes > 0) {
                newX = cPress.X;
                newY = cPress.Y;
            }

            e.SetPosition(new Vector2(newX, newY));
        } else
            e.SetPosition(mpos);

        e.ResetNodes();
        while (e.Nodes.Count < e.MinNodes) {
            Vector2 ePosition;
            if (e.MinWidth == -1 && e.MinHeight == -1 && lastPress != null && startedDrag) {
                Vector2 cPress = ctrl ? lastPress.Value : lastPress.Value.RoundTo(8);
                // distribute nodes along line
                float fraction = (e.Nodes.Count + 1) / (float)e.MinNodes;
                ePosition = cPress + (mpos - cPress) * fraction;
            } else {
                ePosition = (e.Nodes.Count > 0 ? e.Nodes.Last() : e.Position) + Vector2.UnitX * 24;
            }

            e.AddNode(ePosition);
        }

        e.ApplyDefaults();
        e.Initialize();
    }

    private void UpdateSize(Entity e, Vector2 mpos) {
        if (lastPress != null && (MInput.Mouse.CheckLeftButton || MInput.Mouse.CheckRightButton || MInput.Mouse.ReleasedLeftButton || MInput.Mouse.ReleasedRightButton)) {
            Vector2 cPress = lastPress.Value.RoundTo(8);
            if (e.MinWidth > -1) {
                if (mpos.X < cPress.X) {
                    e.SetWidth((int)Math.Round((cPress.X - mpos.X) / 8f) * 8 + e.MinWidth);
                } else {
                    e.SetWidth(Math.Max((int)Math.Round((mpos.X - cPress.X) / 8f) * 8, e.MinWidth));
                }
            }
            if (e.MinHeight > -1) {
                if (mpos.Y < cPress.Y) {
                    e.SetHeight((int)Math.Round((cPress.Y - mpos.Y) / 8f) * 8 + e.MinHeight);
                } else {
                    e.SetHeight(Math.Max((int)Math.Round((mpos.Y - cPress.Y) / 8f) * 8, e.MinHeight));
                }
            }
        } else {
            e.SetWidth(e.MinWidth != -1 ? e.MinWidth : 0);
            e.SetHeight(e.MinHeight != -1 ? e.MinHeight : 0);
        }
    }

    public override void RenderWorldSpace() {
        base.RenderWorldSpace();
        if (preview != null) {
            Calc.PushRandom(preview.GetHashCode());
            preview.Render();
            if (lastPress != null)
                DrawUtil.DrawGuidelines(preview.Bounds, Color.White);
            Calc.PopRandom();
        }
    }

    private UITree CreateEntitiesTree(bool triggers = false) {
        UITree entities = new(new UILabel(triggers ? "triggers" : "entities")) {
            NoKb = true,
            PadUp = 2,
            PadDown = 2
        };
        foreach (var group in Placements.All.Where(x => x.IsTrigger == triggers).OrderBy(x => x.Name).GroupBy(x => x.EntityName)) {
            if (group.Count() == 1)
                entities.Add(CreatePlacementButton(group.First(), width - entities.PadLeft));
            else {
                UITree subtree = new UITree(CreatePlacementButton(group.First(), width - entities.PadLeft * 2 - 20), new(), new(5, 2), collapsed: true) {
                    PadUp = 2,
                    PadDown = 2
                };
                foreach (Placement p in group.Skip(1))
                    subtree.Add(CreatePlacementButton(p, width - entities.PadLeft * 2));
                subtree.Layout();
                entities.Add(subtree);
            }
        }
        entities.Layout();
        return entities;
    }

    private UITree CreateDecalsTree() {
        List<List<string>> decalPaths = GFX.Game.Textures.Keys
            .Select(x => x.Split('/').ToList())
            .Where(x => x.Count > 0 && x[0] == "decals")
            .ToList();
        Tree<string> decalTree = Tree<string>.FromPrefixes(decalPaths, "").Children[0];
        decalTree.Parent = null; // get rid of the empty parent for AggregateUp

        UITree RenderPart(Tree<string> part){
            UITree tree = new(new UILabel(part.Value + "/", Fonts.Regular), collapsed: true){
                NoKb = true,
                PadUp = 2,
                PadDown = 2
            };
            foreach(Tree<string> c in part.Children)
                if(c.Children.Any())
                    tree.Add(RenderPart(c));
                else {
                    UIElement group = new();
                    group.Add(new UIButton(c.Value, Fonts.Regular, 3, 3));
                    group.AddRight(new UIImage(GFX.Game[c.AggregateUp((l, r) => l + "/" + r)]));
                    group.CalculateBounds();
                    tree.Add(group);
                }

            tree.Layout();
            return tree;
        }

        return RenderPart(decalTree);
    }

    private UIButton CreatePlacementButton(Placement item, float maxWidth) {
        UIButton b = new UIButton(Fonts.Regular.FitWithSuffix(item.Name, maxWidth - 15), Fonts.Regular, 4, 4) {
            OnPress = () => curLeftSelection = curLeftSelection != item ? item : null,
            OnRightPress = () => curRightSelection = curRightSelection != item ? item : null
        };
        placementButtons[item] = b;
        return b;
    }

    public override void ResizePanel(int height) {
        if (buttonPane != null)
            buttonPane.Height = height - 30;
        if (searchBar != null)
            searchBar.Position.Y = height - 20;
    }
}