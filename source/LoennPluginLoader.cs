﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Celeste;
using Celeste.Mod;
using Monocle;
using MonoMod.Utils;
using NLua;
using NLua.Exceptions;
using Snowberry.Editor;

namespace Snowberry;

public static class LoennPluginLoader {

    private static readonly Dictionary<string, LuaTable> reqCache = new();
    private static string curMod = null;

    public static Dictionary<string, KeyValuePair<string, string>> LoennText = new();

    internal static void LoadPlugins() {
        Snowberry.LogInfo("Loading Selene for Loenn plugins");
        // note: we don't load in live mode because it breaks everything, instead we have to pass files through selene
        // but we do make it a global
        // also setup other globals needed by plugins
        Everest.LuaLoader.Context.DoString("""
            selene = require("Selene/selene/lib/selene/init")
            selene.load(nil, false)
            selene.parser = require("Selene/selene/lib/selene/parser")

            celesteRender = {}
            unpack = table.unpack

            math = math or {}
            math.atan2 = math.atan2 or require("#Snowberry.LoennPluginLoader").atan2

            _MAP_VIEWER = {
                name = "snowberry"
            }
            """);

        Snowberry.LogInfo("Trying to load Loenn plugins");

        Dictionary<string, LuaTable> plugins = new();
        HashSet<string> triggers = new();

        if(!Everest.Content.Mods.SelectMany(x => x.List).Any(asset => asset.PathVirtual.Replace('\\', '/').StartsWith("Loenn/")))
            ReCrawlForLua();

        foreach(IGrouping<ModContent, ModAsset> modAssets in Everest.Content.Mods
                    .SelectMany(mod => mod.List.Select(asset => (mod, asset)))
                    .Where(pair => pair.asset.PathVirtual.StartsWith("Loenn"))
                    .GroupBy(pair => pair.mod, pair => pair.asset)) {
            curMod = modAssets.Key.Name;
            foreach(var asset in modAssets) {
                var path = asset.PathVirtual.Replace('\\', '/');
                if(path.StartsWith("Loenn/entities/") || path.StartsWith("Loenn/triggers/")) {
                    try {
                        var pluginTables = RunAsset(asset, path);
                        bool any = false;
                        if (pluginTables != null)
                            foreach (var p in pluginTables) {
                                if (p is LuaTable pluginTable) {
                                    List<LuaTable> pluginsFromScript = new() { pluginTable };
                                    // returning multiple plugins at once
                                    if (pluginTable["name"] == null)
                                        pluginsFromScript = pluginTable.Values.OfType<LuaTable>().ToList();

                                    foreach (var table in pluginsFromScript) {
                                        if (table["name"] is string name) {
                                            plugins[name] = table;
                                            if (path.StartsWith("Loenn/triggers/"))
                                                triggers.Add(name);
                                            Snowberry.LogInfo($"Loaded Loenn plugin for \"{name}\"");
                                            any = true;
                                        } else {
                                            Snowberry.Log(LogLevel.Warn, $"A nameless entity was found at \"{path}\"");
                                        }
                                    }
                                }
                            }

                        if (!any) {
                            Snowberry.LogInfo($"No plugins were loaded from \"{curMod}: {path}\"");
                        }
                    } catch (Exception e) {
                        string ex = e.ToString();
                        if (ex.Contains("error in error handling")) {
                            Snowberry.Log(LogLevel.Error, $"Could not load Loenn plugin at \"{path}\" because of internal Lua errors. No more Lua entities will be loaded. Try restarting the game.");
                            break;
                        }

                        Snowberry.Log(LogLevel.Warn, $"Failed to load Loenn plugin at \"{path}\"");
                        Snowberry.Log(LogLevel.Warn, $"Reason: {ex}");
                    }
                } else if (path.StartsWith("Loenn/lang/") && path.EndsWith("/en_gb.lang")) {
                    string text;
                    using(var reader = new StreamReader(asset.Stream))
                        text = reader.ReadToEnd();

                    foreach(var entry in text.Split('\n').Select(k => k.Split('#')[0])) {
                        if(!string.IsNullOrWhiteSpace(entry)) {
                            var split = entry.Split('=');
                            if(split.Length == 2 && !string.IsNullOrWhiteSpace(split[0]) && !string.IsNullOrWhiteSpace(split[1])) {
                                LoennText[split[0]] = new KeyValuePair<string, string>(split[1].Trim(), asset.Source.Mod.Name);
                            }
                        }
                    }
                }
            }
        }

        curMod = null;

        Snowberry.LogInfo($"Found {plugins.Count} Loenn plugins");
        Snowberry.Log(LogLevel.Info, $"Loaded {LoennText.Count} dialog entries from language files for Loenn plugins.");

        foreach(var plugin in plugins) {
            bool isTrigger = triggers.Contains(plugin.Key);
            LoennPluginInfo info = new LoennPluginInfo(plugin.Key, plugin.Value, isTrigger);
            PluginInfo.Entities[plugin.Key] = info;

            if (plugin.Value["placements"] is LuaTable placements)
                if (placements.Keys.OfType<string>().Contains("name")) {
                    Dictionary<string, object> options = new();
                    if(placements["data"] is LuaTable data)
                        foreach (var item in data.Keys.OfType<string>())
                            options[item] = data[item];

                    string placementName = placements["name"] as string ?? "";
                    placementName = placementName.Replace(" ", ""); // thank you Flaglines and Such. very cool
                    placementName = LoennText.TryGetValue($"{(isTrigger ? "triggers" : "entities")}.{plugin.Key}.placements.name.{placementName}", out var name) ? $"{name.Key} [{name.Value}]" : $"{plugin.Key}.{placements["name"]}";
                    Placements.Create(placementName, plugin.Key, options, isTrigger);
                } else if (placements.Keys.Count >= 1) {
                    foreach (var i in placements.Keys) {
                        Dictionary<string, object> options = new();
                        // thank you Eevee Helper, very cool
                        if (placements[i] is LuaTable ptable && (i is "default" || ptable.Keys.OfType<string>().Contains("name"))) {
                            if (ptable["data"] is LuaTable data)
                                foreach (var item in data.Keys.OfType<string>())
                                    options[item] = data[item];

                            string placementName = ptable["name"] as string ?? "default";
                            placementName = placementName.Replace(" ", ""); // lol
                            placementName = LoennText.TryGetValue($"{(isTrigger ? "triggers" : "entities")}.{plugin.Key}.placements.name.{placementName}", out var name) ? $"{name.Key} [{name.Value}]" : $"{plugin.Key}.{ptable["name"] ?? "default"}";
                            Placements.Create(placementName, plugin.Key, options, isTrigger);
                        }
                    }
                }
        }
    }

    private static object[] RunAsset(ModAsset asset, string path){
        string text;
        using(var reader = new StreamReader(asset.Stream))
            text = reader.ReadToEnd();

        // `require` searchers are broken, yaaaaaaay
        text = $"""
                    local snowberry_orig_require = require
                    local require = function(name)
                        return snowberry_orig_require("#Snowberry.LoennPluginLoader").EverestRequire(name)
                    end
                    {text}
                    """;

        if (Everest.LuaLoader.Context.GetFunction("selene.parse")?.Call(text)?.FirstOrDefault() is string proc)
            return Everest.LuaLoader.Context.DoString(proc, asset.Source.Name + ":" + path);

        Snowberry.Log(LogLevel.Error, $"Failed to parse Selene syntax in {path}");
        return null;
    }

    private static void ReCrawlForLua() {
        new DynamicData(typeof(Everest.Content)).Get<HashSet<string>>("BlacklistRootFolders").Remove("Loenn");
        foreach (var mod in Everest.Content.Mods)
            DynamicData.For(mod).Invoke("Crawl");
    }

    // invoked via lua
    public static object EverestRequire(string name) {
        // name could be "mods", "structs.rectangle", etc

        // if you want something, check LoennHelpers/
        try {
            var h = Everest.LuaLoader.Context.DoString($"return require(\"LoennHelpers/{name.Replace(".", "/")}\")").FirstOrDefault();
            if (h != null)
                return h;
        } catch (LuaScriptException e) {
            if(!e.ToString().Contains("not found:")) {
                Snowberry.Log(LogLevel.Verbose, $"Failed to load at {name}");
                Snowberry.Log(LogLevel.Verbose, $"Reason: {e}");
            }
        }

        return "\n\tCould not find Loenn library: " + name;
    }

    private static LuaTable EmptyTable() {
        return Everest.LuaLoader.Context.DoString("return {}").FirstOrDefault() as LuaTable;
    }

    // invoked via lua
    public static object LuaGetImage(string textureName, string atlasName) {
        atlasName ??= "Gameplay";
        Atlas atlas = atlasName.ToLowerInvariant().Equals("gui") ? GFX.Gui : atlasName.ToLowerInvariant().Equals("misc") ? GFX.Misc : GFX.Game;

        if (textureName.StartsWith("@Internal@/"))
            textureName = "plugins/Snowberry/" + textureName.Substring("@Internal@/".Length);

        if (!atlas.Has(textureName))
            return null;

        var meta = EmptyTable();

        // We render these so we can pick whatever format we like
        meta["image"] = textureName;
        meta["atlas"] = atlasName;

        MTexture texture = atlas[textureName];
        meta["width"] = meta["realWidth"] = texture.Width;
        meta["height"] = meta["realHeight"] = texture.Height;
        meta["offsetX"] = meta["offsetY"] = 0;

        return meta;
    }

    private static long toLong(object o) {
        return o switch {
            long l => l,
            int i => i,
            float f => (long)f,
            double d => (long)d,
            short s => s,
            byte b => b,
            _ => 0
        };
    }

    private static double toDouble(object o) {
        return o switch {
            long l => l,
            int i => i,
            float f => f,
            double d => d,
            short s => s,
            byte b => b,
            _ => 0
        };
    }

    // invoked via lua
    public static object lshift(object o, object by) {
        return toLong(o) << (int)toLong(by);
    }
    public static double atan2(object l, object r) {
        return Math.Atan2(toDouble(l), toDouble(r));
    }

    // invoked via lua
    public static LuaTable RequireFromMods(string filename, string modName) {
        string curModName = string.IsNullOrEmpty(modName) ? curMod : modName;
        if (curModName == null || filename == null)
            return null;

        string targetFile = $"Loenn/{filename.Replace('.', '/')}";
        string targetId = $"{curModName}::{targetFile}";

        try {
            if (reqCache.TryGetValue(targetId, out var library))
                return library;

            foreach (var asset in Everest.Content.Mods
                         .Where(mod => mod.Name == curModName)
                         .SelectMany(mod => mod.List)
                         .Where(asset => asset.Type == typeof(AssetTypeLua))
                         .Where(asset => asset.PathVirtual.Replace('\\', '/') == targetFile))
                return reqCache[targetId] = RunAsset(asset, targetFile)?.FirstOrDefault() as LuaTable;

            return reqCache[targetId] = null;
        } catch (Exception e) {
            Snowberry.Log(LogLevel.Error, $"Error running Loenn library {modName}/{filename}: {e}");
            return reqCache[targetId] = null;
        }
    }

    // invoked via lua
    public static LuaTable FindLoadedMod(string modName) {
        foreach (var module in Everest.Modules) {
            if (module.Metadata.Name == modName) {
                var ret = EmptyTable();
                ret["Name"] = modName;
                ret["Version"] = module.Metadata.VersionString;

                return ret;
            }
        }

        return null;
    }

    // invoked via lua
    public static VirtualMap<MTexture> Autotile(string layer, object key, float width, float height) {
        bool fg = layer.Equals("tilesFg", StringComparison.InvariantCultureIgnoreCase);
        char keyC = key.ToString()[0];
        return (fg ? GFX.FGAutotiler : GFX.BGAutotiler).GenerateBoxStable(keyC, (int)(width / 8f), (int)(height / 8f)).TileGrid.Tiles;
    }
}