using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Utility;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;
using DisPlacePlugin.Objects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using static DisPlacePlugin.DisPlacePlugin;
using Dalamud.Interface.Textures;

namespace DisPlacePlugin.Gui
{
    public class ConfigurationWindow : Window<DisPlacePlugin>
    {
        public Configuration Config => Plugin.Config;

        private string CustomTag = string.Empty;
        private readonly Dictionary<uint, uint> iconToFurniture = new() { };

        private readonly Vector4 PURPLE = new(0.26275f, 0.21569f, 0.56863f, 1f);
        private readonly Vector4 PURPLE_ALPHA = new(0.26275f, 0.21569f, 0.56863f, 0.5f);

        private FileDialogManager FileDialogManager { get; }

        public ConfigurationWindow(DisPlacePlugin plugin) : base(plugin)
        {
            this.FileDialogManager = new FileDialogManager
            {
                AddedWindowFlags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking,
            };
        }

        protected void DrawAllUi()
        {
            if (!ImGui.Begin($"{Plugin.Name}", ref WindowVisible, ImGuiWindowFlags.NoScrollWithMouse))
            {
                return;
            }
            if (ImGui.BeginChild("##SettingsRegion"))
            {
                DrawGeneralSettings();
                if (ImGui.BeginChild("##ItemListRegion"))
                {
                    ImGui.PushStyleColor(ImGuiCol.Header, PURPLE_ALPHA);
                    ImGui.PushStyleColor(ImGuiCol.HeaderHovered, PURPLE);
                    ImGui.PushStyleColor(ImGuiCol.HeaderActive, PURPLE);

                    if (ImGui.CollapsingHeader("室内家具", ImGuiTreeNodeFlags.DefaultOpen))
                    {
                        ImGui.PushID("interior");
                        DrawItemList(Plugin.InteriorItemList);
                        ImGui.PopID();
                    }
                    if (ImGui.CollapsingHeader("室外家具", ImGuiTreeNodeFlags.DefaultOpen))
                    {
                        ImGui.PushID("exterior");
                        DrawItemList(Plugin.ExteriorItemList);
                        ImGui.PopID();
                    }

                    if (ImGui.CollapsingHeader("室内固定装置", ImGuiTreeNodeFlags.DefaultOpen))
                    {
                        ImGui.PushID("interiorFixture");
                        DrawFixtureList(Plugin.Layout.interiorFixture);
                        ImGui.PopID();
                    }

                    if (ImGui.CollapsingHeader("室外固定装置", ImGuiTreeNodeFlags.DefaultOpen))
                    {
                        ImGui.PushID("exteriorFixture");
                        DrawFixtureList(Plugin.Layout.exteriorFixture);
                        ImGui.PopID();
                    }
                    if (ImGui.CollapsingHeader("未使用的家具", ImGuiTreeNodeFlags.DefaultOpen))
                    {
                        ImGui.PushID("unused");
                        DrawItemList(Plugin.UnusedItemList, true);
                        ImGui.PopID();
                    }

                    ImGui.PopStyleColor(3);
                    ImGui.EndChild();
                }
                ImGui.EndChild();
            }

            this.FileDialogManager.Draw();
        }

        protected override void DrawUi()
        {
            ImGui.PushStyleColor(ImGuiCol.TitleBgActive, PURPLE);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, PURPLE_ALPHA);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, PURPLE_ALPHA);
            ImGui.SetNextWindowSize(new Vector2(530, 450), ImGuiCond.FirstUseEver);

            DrawAllUi();

            ImGui.PopStyleColor(3);
            ImGui.End();
        }

        #region Helper Functions
        public static void DrawIcon(ushort icon, Vector2 size)
        {
            if (icon < 65000)
            {
                var iconTexture = DalamudApi.TextureProvider.GetFromGameIcon(new GameIconLookup(icon));
                ImGui.Image(iconTexture.GetWrapOrEmpty().Handle, size);
            }
        }
        #endregion

        #region Basic UI

        private void LogLayoutMode()
        {
            if (Memory.Instance.GetCurrentTerritory() == Memory.HousingArea.Island)
            {
                LogError("（管理家具 → 放置家具外观）");
            }
            else
            {
                LogError("（住宅 → 室内/室外家具）");
            }
        }

        private bool CheckModeForSave()
        {
            return true;
        }

        private bool CheckModeForLoad()
        {
            if (Config.ApplyLayout && !Memory.Instance.CanEditItem())
            {
                LogError("未处于旋转布局模式，无法加载并应用布局");
                return false;
            }

            if (!Config.ApplyLayout && !Memory.Instance.IsHousingMode())
            {
                LogError("未处于布局模式，无法加载布局");
                LogLayoutMode();
                return false;
            }

            return true;
        }

        private void SaveLayoutToFile()
        {
            if (!CheckModeForSave())
            {
                return;
            }

            try
            {
                Plugin.GetGameLayout();
                DisPlacePlugin.LayoutManager.ExportLayout();
            }
            catch (Exception e)
            {
                LogError($"保存错误：{e.Message}", e.StackTrace);
            }
        }

        private void LoadLayoutFromFile()
        {
            if (!CheckModeForLoad()) return;

            try
            {
                SaveLayoutManager.ImportLayout(Config.SaveLocation);
                Log($"已导入 {Plugin.InteriorItemList.Count + Plugin.ExteriorItemList.Count} 个物品");

                Plugin.MatchLayout();
                Config.ResetRecord();

                if (Config.ApplyLayout)
                {
                    Plugin.ApplyLayout();
                }
            }
            catch (Exception e)
            {
                LogError($"加载错误：{e.Message}", e.StackTrace);
            }
        }

        unsafe private void DrawGeneralSettings()
        {
            if (ImGui.Checkbox("显示家具标签", ref Config.DrawScreen)) Config.Save();
            if (Config.ShowTooltips && ImGui.IsItemHovered())
                ImGui.SetTooltip("在屏幕上显示家具名称");

            ImGui.SameLine();
            ImGui.Dummy(new Vector2(10, 0));
            ImGui.SameLine();
            if (ImGui.Checkbox("##hideTooltipsOnOff", ref Config.ShowTooltips)) Config.Save();
            ImGui.SameLine();
            ImGui.TextUnformatted("显示提示信息");

            ImGui.Dummy(new Vector2(0, 10));

            ImGui.Text("布局");

            if (!Config.SaveLocation.IsNullOrEmpty())
            {
                ImGui.Text($"当前文件路径：{Config.SaveLocation}");

                if (ImGui.Button("保存"))
                {
                    SaveLayoutToFile();
                }
                if (Config.ShowTooltips && ImGui.IsItemHovered()) ImGui.SetTooltip("保存布局到当前文件");
                ImGui.SameLine();
            }

            if (ImGui.Button("另存为"))
            {
                if (CheckModeForSave())
                {
                    string saveName = "save";
                    if (!Config.SaveLocation.IsNullOrEmpty())
                        saveName = Path.GetFileNameWithoutExtension(Config.SaveLocation);

                    FileDialogManager.SaveFileDialog(
                        "选择保存位置",
                        ".json",
                        saveName,
                        "json",
                        (bool ok, string res) =>
                        {
                            if (!ok) return;
                            Config.SaveLocation = res;
                            Config.Save();
                            SaveLayoutToFile();
                        },
                        Path.GetDirectoryName(Config.SaveLocation)
                    );
                }
            }
            if (Config.ShowTooltips && ImGui.IsItemHovered()) ImGui.SetTooltip("保存布局到文件");

            ImGui.SameLine();
            ImGui.Dummy(new Vector2(20, 0));
            ImGui.SameLine();

            if (!Config.SaveLocation.IsNullOrEmpty())
            {
                if (ImGui.Button("加载"))
                {
                    LoadLayoutFromFile();
                }
                if (Config.ShowTooltips && ImGui.IsItemHovered()) ImGui.SetTooltip("从当前文件加载布局");
                ImGui.SameLine();
            }

            if (ImGui.Button("从文件加载"))
            {
                if (CheckModeForLoad())
                {
                    FileDialogManager.OpenFileDialog(
                        "选择布局文件",
                        ".json",
                        (bool ok, List<string> res) =>
                        {
                            if (!ok) return;
                            Config.SaveLocation = res.FirstOrDefault("");
                            Config.Save();
                            LoadLayoutFromFile();
                        },
                        1,
                        Path.GetDirectoryName(Config.SaveLocation)
                    );
                }
            }
            if (Config.ShowTooltips && ImGui.IsItemHovered()) ImGui.SetTooltip("从文件加载布局");

            ImGui.SameLine();
            ImGui.Dummy(new Vector2(10, 0));
            ImGui.SameLine();

            if (ImGui.Checkbox("应用布局", ref Config.ApplyLayout))
            {
                Config.Save();
            }

            ImGui.SameLine();
            ImGui.Dummy(new Vector2(10, 0));
            ImGui.SameLine();

            ImGui.PushItemWidth(100);
            if (ImGui.InputInt("放置间隔（毫秒）", ref Config.LoadInterval))
            {
                Config.Save();
            }
            ImGui.PopItemWidth();
            if (Config.ShowTooltips && ImGui.IsItemHovered())
                ImGui.SetTooltip("应用布局时家具放置的时间间隔，数值过低可能会跳过部分家具");

            ImGui.Dummy(new Vector2(0, 15));

            bool hasFloors =
                Memory.Instance.GetCurrentTerritory() == Memory.HousingArea.Indoors &&
                !Memory.Instance.GetIndoorHouseSize().Equals("Apartment");

            if (hasFloors)
            {
                ImGui.Text("选择的楼层");

                if (ImGui.Checkbox("地下室", ref Config.Basement)) Config.Save();
                ImGui.SameLine(); ImGui.Dummy(new Vector2(10, 0)); ImGui.SameLine();

                if (ImGui.Checkbox("一层", ref Config.GroundFloor)) Config.Save();
                ImGui.SameLine(); ImGui.Dummy(new Vector2(10, 0)); ImGui.SameLine();

                if (Memory.Instance.HasUpperFloor() &&
                    ImGui.Checkbox("二层", ref Config.UpperFloor))
                {
                    Config.Save();
                }

                ImGui.Dummy(new Vector2(0, 15));
            }

            ImGui.Dummy(new Vector2(0, 15));
        }

        private void DrawRow(int i, HousingItem housingItem, bool showSetPosition = true, int childIndex = -1)
        {
            if (!housingItem.CorrectLocation)
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.5f, 1));

            ImGui.Text($"{housingItem.X:N4}, {housingItem.Y:N4}, {housingItem.Z:N4}");

            if (!housingItem.CorrectLocation)
                ImGui.PopStyleColor();

            ImGui.NextColumn();

            if (!housingItem.CorrectRotation)
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.5f, 1));

            ImGui.Text($"{housingItem.Rotate:N3}");
            ImGui.NextColumn();

            if (!housingItem.CorrectRotation)
                ImGui.PopStyleColor();

            var stain = DalamudApi.DataManager.GetExcelSheet<Stain>().GetRow(housingItem.Stain);
            var colorName = stain.Name;

            if (housingItem.Stain != 0)
            {
                Utils.StainButton("dye_" + i, stain, new Vector2(20));
                ImGui.SameLine();

                if (!housingItem.DyeMatch)
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.5f, 1));

                ImGui.Text($"{colorName}");

                if (!housingItem.DyeMatch)
                    ImGui.PopStyleColor();
            }
            else if (housingItem.MaterialItemKey != 0)
            {
                var item = DalamudApi.DataManager.GetExcelSheet<Item>().GetRow(housingItem.MaterialItemKey);
                if (!item.Equals(null))
                {
                    if (!housingItem.DyeMatch)
                        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.5f, 1));

                    DrawIcon(item.Icon, new Vector2(20, 20));
                    ImGui.SameLine();
                    ImGui.Text(item.Name.ToString());

                    if (!housingItem.DyeMatch)
                        ImGui.PopStyleColor();
                }
            }
            ImGui.NextColumn();

            if (showSetPosition)
            {
                string uniqueID = childIndex == -1 ? i.ToString() : i + "_" + childIndex;

                bool noMatch = housingItem.ItemStruct == IntPtr.Zero;

                if (!noMatch)
                {
                    if (ImGui.Button("设置##" + uniqueID))
                    {
                        Plugin.MatchLayout();

                        if (housingItem.ItemStruct != IntPtr.Zero)
                        {
                            SetItemPosition(housingItem);
                        }
                        else
                        {
                            LogError($"无法设置 {housingItem.Name} 的位置");
                        }
                    }
                }

                ImGui.NextColumn();
            }
        }

        private void DrawFixtureList(List<Fixture> fixtureList)
        {
            try
            {
                if (ImGui.Button("清空"))
                {
                    fixtureList.Clear();
                    Config.Save();
                }

                ImGui.Columns(3, "FixtureList", true);
                ImGui.Separator();

                ImGui.Text("楼层"); ImGui.NextColumn();
                ImGui.Text("固定装置"); ImGui.NextColumn();
                ImGui.Text("物品"); ImGui.NextColumn();

                ImGui.Separator();

                foreach (var fixture in fixtureList)
                {
                    ImGui.Text(fixture.level); ImGui.NextColumn();
                    ImGui.Text(fixture.type); ImGui.NextColumn();

                    var item = DalamudApi.DataManager.GetExcelSheet<Item>().GetRow(fixture.itemId);
                    if (!item.Equals(null))
                    {
                        DrawIcon(item.Icon, new Vector2(20, 20));
                        ImGui.SameLine();
                    }
                    ImGui.Text(fixture.name); ImGui.NextColumn();

                    ImGui.Separator();
                }

                ImGui.Columns(1);
            }
            catch (Exception e)
            {
                LogError(e.Message, e.StackTrace);
            }
        }

        private void DrawItemList(List<HousingItem> itemList, bool isUnused = false)
        {
            if (ImGui.Button("排序"))
            {
                itemList.Sort((x, y) =>
                {
                    if (x.Name.CompareTo(y.Name) != 0)
                        return x.Name.CompareTo(y.Name);
                    if (x.X.CompareTo(y.X) != 0)
                        return x.X.CompareTo(y.X);
                    if (x.Y.CompareTo(y.Y) != 0)
                        return x.Y.CompareTo(y.Y);
                    if (x.Z.CompareTo(y.Z) != 0)
                        return x.Z.CompareTo(y.Z);
                    if (x.Rotate.CompareTo(y.Rotate) != 0)
                        return x.Rotate.CompareTo(y.Rotate);
                    return 0;
                });
                Config.Save();
            }

            ImGui.SameLine();
            if (ImGui.Button("清空"))
            {
                itemList.Clear();
                Config.Save();
            }

            if (!isUnused)
            {
                ImGui.SameLine();
                ImGui.Text("注：缺失的物品、染色不匹配的物品，以及不在所选楼层的物品将显示为灰色");
            }

            int columns = isUnused ? 4 : 5;

            ImGui.Columns(columns, "ItemList", true);
            ImGui.Separator();

            ImGui.Text("物品"); ImGui.NextColumn();
            ImGui.Text("坐标 (X,Y,Z)"); ImGui.NextColumn();
            ImGui.Text("旋转"); ImGui.NextColumn();
            ImGui.Text("染色 / 材质"); ImGui.NextColumn();

            if (!isUnused)
            {
                ImGui.Text("设置位置");
                ImGui.NextColumn();
            }

            ImGui.Separator();

            for (int i = 0; i < itemList.Count(); i++)
            {
                var housingItem = itemList[i];
                var displayName = housingItem.Name;

                var item = DalamudApi.DataManager.GetExcelSheet<Item>().GetRow(housingItem.ItemKey);
                if (!item.Equals(null))
                {
                    DrawIcon(item.Icon, new Vector2(20, 20));
                    ImGui.SameLine();
                }

                if (housingItem.ItemStruct == IntPtr.Zero)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.5f, 1));
                }

                ImGui.Text(displayName);

                ImGui.NextColumn();
                DrawRow(i, housingItem, !isUnused);

                if (housingItem.ItemStruct == IntPtr.Zero)
                {
                    ImGui.PopStyleColor();
                }

                ImGui.Separator();
            }

            ImGui.Columns(1);
        }

        #endregion

        #region Draw Screen

        protected override void DrawScreen()
        {
            if (Config.DrawScreen)
            {
                DrawItemOnScreen();
            }
        }

        private unsafe void DrawItemOnScreen()
        {
            if (Memory.Instance == null) return;

            var itemList =
                Memory.Instance.GetCurrentTerritory() == Memory.HousingArea.Indoors
                    ? Plugin.InteriorItemList
                    : Plugin.ExteriorItemList;

            for (int i = 0; i < itemList.Count(); i++)
            {
                var playerPos = DalamudApi.ClientState.LocalPlayer.Position;
                var housingItem = itemList[i];

                if (housingItem.ItemStruct == IntPtr.Zero) continue;

                var itemStruct = (HousingItemStruct*)housingItem.ItemStruct;
                var itemPos = new Vector3(
                    itemStruct->Position.X,
                    itemStruct->Position.Y,
                    itemStruct->Position.Z
                );

                if (Config.HiddenScreenItemHistory.IndexOf(i) >= 0) continue;
                if (Config.DrawDistance > 0 &&
                    (playerPos - itemPos).Length() > Config.DrawDistance)
                    continue;

                var displayName = housingItem.Name;

                if (DalamudApi.GameGui.WorldToScreen(itemPos, out var screenCoords))
                {
                    ImGui.PushID("HousingItemWindow" + i);
                    ImGui.SetNextWindowPos(new Vector2(screenCoords.X, screenCoords.Y));
                    ImGui.SetNextWindowBgAlpha(0.8f);

                    if (ImGui.Begin(
                        "HousingItem" + i,
                        ImGuiWindowFlags.NoDecoration |
                        ImGuiWindowFlags.AlwaysAutoResize |
                        ImGuiWindowFlags.NoSavedSettings |
                        ImGuiWindowFlags.NoMove |
                        ImGuiWindowFlags.NoFocusOnAppearing |
                        ImGuiWindowFlags.NoNav))
                    {
                        ImGui.Text(displayName);
                        ImGui.SameLine();

                        if (ImGui.Button("设置##ScreenItem" + i))
                        {
                            if (!Memory.Instance.CanEditItem())
                            {
                                LogError("未处于旋转布局模式，无法设置位置");
                                ImGui.End();
                                ImGui.PopID();
                                continue;
                            }

                            SetItemPosition(housingItem);
                            Config.HiddenScreenItemHistory.Add(i);
                            Config.Save();
                        }

                        ImGui.End();
                    }

                    ImGui.PopID();
                }
            }
        }

        #endregion
    }
}
