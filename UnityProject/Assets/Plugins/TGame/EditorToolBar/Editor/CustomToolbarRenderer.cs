// CustomToolbarRenderer.cs
// 在 Host 提供的 LeftRoot / RightRoot 容器里,按 5 槽位分桶,把各组件 Build 出来的 VisualElement Add 进去。
// 整棵树一次性建好,后续只在 Settings 改动时整体重建。组件内部用 UIElements 事件(clicked / change 等),
// 不要再走 IMGUI。
//
// 重建策略:每帧 OnGUI 检查时间戳(0.5s 一次)+ Settings 资产 Undo/保存后 ForceRebuild。

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TGame.EditorToolBar
{
    public static class CustomToolbarRenderer
    {
        private static CustomToolbarSettings _settings;
        private static CustomToolbarContext _context;

        // 缓存每个槽位下挂的"组件包"VisualElement
        private static readonly Dictionary<ToolbarSlot, VisualElement> _slotContainers
            = new Dictionary<ToolbarSlot, VisualElement>();

        /// <summary>
        /// 由 Host (Attach/Reattach) 或 SettingsWindow (ApplyModifiedProperties) 主动调用,
        /// 立即重建整棵 UI 树。组件 Build 内部用 UIElements 事件,本方法只在配置变化时调用一次。
        /// </summary>
        public static void ForceRebuild()
        {
            RebuildTree();
        }

        private static void RebuildTree()
        {
            if (_settings == null) _settings = CustomToolbarSettings.GetOrCreate();
            if (_context == null) _context = new CustomToolbarContext { Settings = _settings };

            // Registry 还没初始化
            if (CustomToolbarRegistry.Items.Count == 0) CustomToolbarRegistry.Scan();

            // 自动补全缺失配置
            foreach (var item in CustomToolbarRegistry.Items)
            {
                if (!_settings.Items.Exists(c => c.Id == item.Id))
                {
                    _settings.Items.Add(new ToolbarItemConfig
                    {
                        Id = item.Id,
                        Enabled = true,
                        Slot = item.DefaultSlot,
                        Order = item.DefaultOrder,
                        Width = item.DefaultWidth,
                    });
                }
            }

            // 按槽位分组
            var buckets = new Dictionary<ToolbarSlot, List<(ICustomToolbarItem item, ToolbarItemConfig cfg)>>();
            foreach (var slot in System.Enum.GetValues(typeof(ToolbarSlot)))
                buckets[(ToolbarSlot)slot] = new List<(ICustomToolbarItem, ToolbarItemConfig)>();

            for (int i = 0; i < _settings.Items.Count; i++)
            {
                var cfg = _settings.Items[i];
                if (!cfg.Enabled) continue;
                var item = CustomToolbarRegistry.FindById(cfg.Id);
                if (item == null) continue;
                buckets[cfg.Slot].Add((item, cfg));
            }
            foreach (var kv in buckets) kv.Value.Sort((a, b) => a.cfg.Order.CompareTo(b.cfg.Order));

            // 等 Host 把容器挂上再画
            var leftRoot = CustomToolbarHost.LeftRoot;
            var rightRoot = CustomToolbarHost.RightRoot;
            if (leftRoot == null || rightRoot == null)
            {
                return; // Host 还没初始化,下次 EnsureReady 再试
            }

            // 5 槽位实际归属:LeftStart/LeftEnd/Center → leftRoot,RightStart/RightEnd → rightRoot
            leftRoot.Clear();
            rightRoot.Clear();
            _slotContainers.Clear();

            AddSlot(leftRoot, ToolbarSlot.LeftStart, buckets[ToolbarSlot.LeftStart]);
            AddSlot(leftRoot, ToolbarSlot.LeftEnd, buckets[ToolbarSlot.LeftEnd]);
            AddSlot(leftRoot, ToolbarSlot.Center, buckets[ToolbarSlot.Center]);
            AddSlot(rightRoot, ToolbarSlot.RightStart, buckets[ToolbarSlot.RightStart]);
            AddSlot(rightRoot, ToolbarSlot.RightEnd, buckets[ToolbarSlot.RightEnd]);
        }

        private static void AddSlot(VisualElement parent, ToolbarSlot slot, List<(ICustomToolbarItem item, ToolbarItemConfig cfg)> entries)
        {
            if (entries.Count == 0) return;

            var container = new VisualElement
            {
                name = $"Slot.{slot}",
                pickingMode = PickingMode.Position,
                style =
                {
                    flexDirection = FlexDirection.Row,
                    flexShrink = 0,
                    height = 18,
                }
            };

            for (int i = 0; i < entries.Count; i++)
            {
                var (item, cfg) = entries[i];
                if (cfg.SpaceBefore > 0) container.Add(MakeSpace(cfg.SpaceBefore));
                VisualElement element = null;
                try
                {
                    element = item.Build(_context, cfg);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[EditorToolBar] Build failed for '{item.Id}': {ex}");
                    continue;
                }
                if (element == null) continue;
                if (cfg.Width > 0) element.style.width = cfg.Width;
                if (!string.IsNullOrEmpty(cfg.TooltipOverride)) element.tooltip = cfg.TooltipOverride;
                container.Add(element);
                if (cfg.SpaceAfter > 0) container.Add(MakeSpace(cfg.SpaceAfter));
            }

            parent.Add(container);
            _slotContainers[slot] = container;
        }

        private static VisualElement MakeSpace(float px)
        {
            return new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style = { width = px, flexShrink = 0 }
            };
        }
    }
}
