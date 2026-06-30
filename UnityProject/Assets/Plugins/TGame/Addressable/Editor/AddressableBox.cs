using System;
using System.Collections.Generic;
using System.Linq;
using TGame.ToolBox;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.UIElements;

namespace TGame.ToolBox
{
    public class AddressableBox : IToolBoxContentVisualElement
    {
        public static BoxRegistration Registration => new()
        {
            Name = "Addressable",
            Group = "程序",
            Icon = "AssetBundle Icon",
            Factory = () => new AddressableBox().CreateContent()
        };

        private readonly List<ResourceLocationEntry> _results = new();

        private VisualElement _root;
        private TextField _labelsField;
        private Button _queryButton;
        private Label _summaryLabel;
        private HelpBox _statusBox;
        private ListView _resultList;

        public VisualElement CreateContent()
        {
            _root = new VisualElement();
            _root.style.flexGrow = 1;
            _root.style.paddingLeft = 12;
            _root.style.paddingRight = 12;
            _root.style.paddingTop = 12;
            _root.style.paddingBottom = 12;

            var title = new Label("Addressables Label 查询");
            title.AddToClassList("tbx-section-title");
            _root.Add(title);

            var inputRow = new VisualElement();
            inputRow.style.flexDirection = FlexDirection.Row;
            inputRow.style.alignItems = Align.Center;
            inputRow.style.marginBottom = 8;
            _root.Add(inputRow);

            _labelsField = new TextField("Labels");
            _labelsField.tooltip = "输入一个或多个 label, 用逗号、分号、空格或换行分隔。";
            _labelsField.style.flexGrow = 1;
            _labelsField.style.marginRight = 6;
            inputRow.Add(_labelsField);

            _queryButton = new Button(QueryLabels) { text = "查询" };
            _queryButton.style.width = 72;
            inputRow.Add(_queryButton);

            _statusBox = new HelpBox("输入 label 后点击查询。", HelpBoxMessageType.Info);
            _statusBox.style.marginBottom = 8;
            _root.Add(_statusBox);

            _summaryLabel = new Label("结果: 0");
            _summaryLabel.style.marginBottom = 6;
            _root.Add(_summaryLabel);

            _resultList = new ListView(_results, 48, MakeItem, BindItem)
            {
                selectionType = SelectionType.Single,
                showAlternatingRowBackgrounds = AlternatingRowBackground.All
            };
            _resultList.style.flexGrow = 1;
            _root.Add(_resultList);

            return _root;
        }

        private void QueryLabels()
        {
            var labels = ParseLabels(_labelsField.value);
            if (labels.Count == 0)
            {
                SetStatus("请至少输入一个 label。", HelpBoxMessageType.Warning);
                return;
            }

            _queryButton.SetEnabled(false);
            SetStatus("正在查询 Addressables 资源位置...", HelpBoxMessageType.Info);

            var handle = Addressables.LoadResourceLocationsAsync(labels, Addressables.MergeMode.Union, null);
            handle.Completed += OnLocationsLoaded;
        }

        private void OnLocationsLoaded(AsyncOperationHandle<IList<IResourceLocation>> handle)
        {
            try
            {
                _results.Clear();
                if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
                {
                    SetStatus($"查询失败: {handle.OperationException?.Message ?? "未知错误"}", HelpBoxMessageType.Error);
                    RefreshList();
                    return;
                }

                foreach (var location in handle.Result.Where(l => l != null).OrderBy(l => l.PrimaryKey))
                    _results.Add(new ResourceLocationEntry(location));

                SetStatus($"查询完成, 共 {_results.Count} 个资源位置。", HelpBoxMessageType.Info);
                RefreshList();
            }
            finally
            {
                _queryButton?.SetEnabled(true);
                if (handle.IsValid())
                    Addressables.Release(handle);
            }
        }

        private static List<object> ParseLabels(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return new List<object>();

            return input
                .Split(new[] { ',', ';', '\n', '\r', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .Cast<object>()
                .ToList();
        }

        private VisualElement MakeItem()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 4;
            row.style.paddingRight = 4;

            var text = new Label();
            text.name = "location-text";
            text.style.flexGrow = 1;
            text.style.whiteSpace = WhiteSpace.Normal;
            row.Add(text);

            var copy = new Button { text = "复制" };
            copy.name = "copy-button";
            copy.style.width = 54;
            copy.RegisterCallback<ClickEvent>(OnCopyClicked);
            row.Add(copy);

            return row;
        }

        private void BindItem(VisualElement element, int index)
        {
            var entry = _results[index];
            var label = element.Q<Label>("location-text");
            label.text = $"{entry.PrimaryKey}  |  {entry.ResourceType}  |  {entry.ProviderId}";
            label.tooltip = entry.InternalId;

            var copy = element.Q<Button>("copy-button");
            copy.userData = entry;
        }

        private static void OnCopyClicked(ClickEvent evt)
        {
            if (evt.currentTarget is not Button button)
                return;
            if (button.userData is ResourceLocationEntry entry)
                EditorGUIUtility.systemCopyBuffer = entry.PrimaryKey;
        }

        private void RefreshList()
        {
            _summaryLabel.text = $"结果: {_results.Count}";
            _resultList.Rebuild();
        }

        private void SetStatus(string text, HelpBoxMessageType type)
        {
            _statusBox.text = text;
            _statusBox.messageType = type;
        }

        private readonly struct ResourceLocationEntry
        {
            public readonly string PrimaryKey;
            public readonly string InternalId;
            public readonly string ProviderId;
            public readonly string ResourceType;

            public ResourceLocationEntry(IResourceLocation location)
            {
                PrimaryKey = location.PrimaryKey;
                InternalId = location.InternalId;
                ProviderId = location.ProviderId;
                ResourceType = location.ResourceType?.Name ?? "Unknown";
            }
        }
    }
}
