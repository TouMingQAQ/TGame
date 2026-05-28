using System.Collections.Generic;
using System.IO;
using TGame.ToolBox;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TGame.ToolBox
{
    [System.Serializable]
    public class CurveEntry
    {
        public string Name;
        public AnimationCurve Curve;

        public CurveEntry(string name, AnimationCurve curve)
        {
            Name = name;
            Curve = curve;
        }
    }

    [ToolBox("曲线工具箱", Order = 2)]
    public class AnimationCurveBox : IToolBoxContentVisualElement
    {
        private const string LibPath = "Assets/Resources/CurveLibrary.asset";

        private CurveLibrary _library;
        private List<CurveEntry> _classicCurves;
        private VisualElement _root;
        private VisualElement _diyContainer;
        private VisualElement _classicContainer;
        private TextField _newNameField;

        public VisualElement CreateContent()
        {
            _root = new VisualElement();
            _root.style.flexGrow = 1;
            _root.style.paddingLeft = 6;
            _root.style.paddingRight = 6;
            _root.style.paddingTop = 4;

            var title = new Label("Animation Curve 工具箱");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 14;
            title.style.marginBottom = 4;
            _root.Add(title);

            if (_classicCurves == null)
                InitClassicCurves();
            if (_library == null)
                LoadLibrary();

            BuildDIYSection();
            BuildClassicSection();

            return _root;
        }

        private void BuildDIYSection()
        {
            var foldout = new Foldout();
            foldout.text = "DIY Curve";
            foldout.value = false;
            _root.Add(foldout);

            var addRow = new VisualElement();
            addRow.style.flexDirection = FlexDirection.Row;
            addRow.style.marginBottom = 4;
            foldout.Add(addRow);

            _newNameField = new TextField();
            _newNameField.style.flexGrow = 1;
            addRow.Add(_newNameField);

            var addBtn = new Button(AddCurve);
            addBtn.text = "➕ 添加";
            addBtn.style.width = 70;
            addRow.Add(addBtn);

            _diyContainer = new VisualElement();
            foldout.Add(_diyContainer);

            RefreshDIYList();
        }

        private void BuildClassicSection()
        {
            var foldout = new Foldout();
            foldout.text = "Classic Curve";
            foldout.value = false;
            _root.Add(foldout);

            _classicContainer = new VisualElement();
            foldout.Add(_classicContainer);

            RefreshClassicList();
        }

        private void RefreshDIYList()
        {
            _diyContainer.Clear();

            if (_diyCurves == null || _diyCurves.Count == 0)
            {
                _diyContainer.Add(new Label("暂无 DIY 曲线，在上方输入名称添加")
                {
                    style = { unityTextAlign = TextAnchor.MiddleCenter, color = Color.gray }
                });
                return;
            }

            for (int i = 0; i < _diyCurves.Count; i++)
            {
                var index = i;
                _diyContainer.Add(BuildCurveItem(_diyCurves[i], true,
                    () => { _diyCurves.RemoveAt(index); SaveDIYCurves(); RefreshDIYList(); }));
            }
        }

        private void RefreshClassicList()
        {
            _classicContainer.Clear();
            foreach (var entry in _classicCurves)
                _classicContainer.Add(BuildCurveItem(entry, false, null));
        }

        private VisualElement BuildCurveItem(CurveEntry entry, bool editable, System.Action onDelete)
        {
            var container = new VisualElement();
            container.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f);
            container.style.borderTopWidth = 1;
            container.style.borderTopColor = new Color(0.33f, 0.33f, 0.33f);
            container.style.marginBottom = 4;
            container.style.paddingTop = 4;
            container.style.paddingBottom = 4;
            container.style.paddingLeft = 6;
            container.style.paddingRight = 6;

            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.marginBottom = 4;
            container.Add(headerRow);

            if (editable)
            {
                var nameField = new TextField();
                nameField.value = entry.Name;
                nameField.style.flexGrow = 1;
                nameField.RegisterValueChangedCallback(evt =>
                {
                    entry.Name = evt.newValue;
                    SaveDIYCurves();
                });
                headerRow.Add(nameField);

                var delBtn = new Button(onDelete);
                delBtn.text = "✕";
                delBtn.style.width = 24;
                headerRow.Add(delBtn);
            }
            else
            {
                var label = new Label(entry.Name);
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
                label.style.unityTextAlign = TextAnchor.MiddleLeft;
                headerRow.Add(label);
            }

            var curveField = new IMGUIContainer(() =>
            {
                EditorGUI.BeginChangeCheck();
                entry.Curve = EditorGUILayout.CurveField(entry.Curve, GUILayout.Height(60));
                if (EditorGUI.EndChangeCheck() && editable)
                    SaveDIYCurves();
            });
            curveField.style.height = 62;
            curveField.style.flexGrow = 1;
            container.Add(curveField);

            return container;
        }

        // --- data ---

        private List<CurveEntry> _diyCurves => _library?.Entries;

        private void AddCurve()
        {
            var name = _newNameField.value;
            if (string.IsNullOrWhiteSpace(name)) return;
            _diyCurves.Add(new CurveEntry(name, new AnimationCurve(
                new Keyframe(0, 0), new Keyframe(1, 1))));
            _newNameField.value = "";
            SaveDIYCurves();
            RefreshDIYList();
        }

        private void LoadLibrary()
        {
            _library = AssetDatabase.LoadAssetAtPath<CurveLibrary>(LibPath);
            if (_library == null)
            {
                var dir = Path.GetDirectoryName(LibPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                _library = ScriptableObject.CreateInstance<CurveLibrary>();
                _library.Entries.Add(new CurveEntry("My Curve",
                    new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 1))));
                AssetDatabase.CreateAsset(_library, LibPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"<color=#66ccff>[Curve]</color> 已创建 CurveLibrary 资产: {LibPath}");
            }
        }

        private void SaveDIYCurves()
        {
            if (_library != null)
            {
                EditorUtility.SetDirty(_library);
                AssetDatabase.SaveAssetIfDirty(_library);
            }
        }

        // --- classic curve generators ---

        private void InitClassicCurves()
        {
            _classicCurves = new List<CurveEntry>
            {
                new("线性 (Linear)", AnimationCurve.Linear(0, 0, 1, 1)),
                new("缓入 (Ease In)", CreateEaseIn()),
                new("缓出 (Ease Out)", CreateEaseOut()),
                new("缓入缓出 (Ease In Out)", AnimationCurve.EaseInOut(0, 0, 1, 1)),
                new("平滑步进 (Smooth Step)", CreateSmoothStep()),
                new("脉冲 (Punch)", CreatePunch()),
                new("反弹 (Bounce)", CreateBounce()),
                new("回退 (Back)", CreateBack()),
                new("弹性 (Elastic)", CreateElastic()),
                new("过冲 (Overshoot)", CreateOvershoot()),
                new("先快后慢", CreateFastInSlowOut()),
                new("先慢后快", CreateSlowInFastOut()),
                new("往复 (Ping Pong)", CreatePingPong()),
            };
        }

        private static AnimationCurve CreateEaseIn()
        {
            var curve = new AnimationCurve();
            var k1 = new Keyframe(0, 0, 0, 0, 0, 0.33f) { weightedMode = WeightedMode.Both };
            var k2 = new Keyframe(1, 1, 2.5f, 0, 0.33f, 0) { weightedMode = WeightedMode.Both };
            curve.AddKey(k1);
            curve.AddKey(k2);
            return curve;
        }

        private static AnimationCurve CreateEaseOut()
        {
            var curve = new AnimationCurve();
            var k1 = new Keyframe(0, 0, 0, 2.5f, 0, 0.33f) { weightedMode = WeightedMode.Both };
            var k2 = new Keyframe(1, 1, 0, 0, 0.33f, 0) { weightedMode = WeightedMode.Both };
            curve.AddKey(k1);
            curve.AddKey(k2);
            return curve;
        }

        private static AnimationCurve CreateSmoothStep()
        {
            var curve = new AnimationCurve();
            for (int i = 0; i <= 10; i++)
            {
                float t = i / 10f;
                float v = t * t * (3 - 2 * t);
                curve.AddKey(t, v);
            }
            for (int i = 0; i < curve.length; i++)
                curve.SmoothTangents(i, 0);
            return curve;
        }

        private static AnimationCurve CreatePunch()
        {
            var curve = new AnimationCurve();
            curve.AddKey(0.00f, 1f);
            curve.AddKey(0.11f, 0.72f);
            curve.AddKey(0.22f, 0.42f);
            curve.AddKey(0.34f, 0.20f);
            curve.AddKey(0.46f, 0.08f);
            curve.AddKey(0.58f, 0.02f);
            curve.AddKey(0.70f, 0.00f);
            curve.AddKey(0.82f, 0.00f);
            curve.AddKey(1.00f, 0f);
            for (int i = 0; i < curve.length; i++)
                curve.SmoothTangents(i, 0);
            return curve;
        }

        private static AnimationCurve CreateBounce()
        {
            var curve = new AnimationCurve();
            curve.AddKey(new Keyframe(0.00f, 0));
            curve.AddKey(new Keyframe(0.15f, 1f));
            curve.AddKey(new Keyframe(0.30f, 0.55f));
            curve.AddKey(new Keyframe(0.45f, 0.90f));
            curve.AddKey(new Keyframe(0.60f, 0.75f));
            curve.AddKey(new Keyframe(0.75f, 0.95f));
            curve.AddKey(new Keyframe(0.90f, 0.88f));
            curve.AddKey(new Keyframe(1.00f, 1f));
            for (int i = 0; i < curve.length; i++)
                curve.SmoothTangents(i, 0);
            return curve;
        }

        private static AnimationCurve CreateBack()
        {
            var curve = new AnimationCurve();
            var k1 = new Keyframe(0, -0.3f, 0, 2.5f, 0, 0.4f) { weightedMode = WeightedMode.Both };
            var k2 = new Keyframe(1, 1, 0, 0, 0.4f, 0) { weightedMode = WeightedMode.Both };
            curve.AddKey(k1);
            curve.AddKey(k2);
            return curve;
        }

        private static AnimationCurve CreateElastic()
        {
            var curve = new AnimationCurve();
            for (int i = 0; i <= 30; i++)
            {
                float t = i / 30f;
                float v = Mathf.Pow(2, 10 * (t - 1)) * Mathf.Cos((20 * Mathf.PI * t) / 3);
                curve.AddKey(t, v);
            }
            curve.AddKey(1, 1);
            for (int i = 0; i < curve.length; i++)
                curve.SmoothTangents(i, 0);
            return curve;
        }

        private static AnimationCurve CreateOvershoot()
        {
            var curve = new AnimationCurve();
            var k1 = new Keyframe(0, 0, 0, 3f, 0, 0.45f) { weightedMode = WeightedMode.Both };
            var k2 = new Keyframe(1, 1.2f, 0, 0, 0.45f, 0) { weightedMode = WeightedMode.Both };
            curve.AddKey(k1);
            curve.AddKey(k2);
            return curve;
        }

        private static AnimationCurve CreateFastInSlowOut()
        {
            var curve = new AnimationCurve();
            var k1 = new Keyframe(0, 0, 3f, 0, 0, 0.2f) { weightedMode = WeightedMode.Both };
            var k2 = new Keyframe(1, 1, 0, 0, 0.8f, 0) { weightedMode = WeightedMode.Both };
            curve.AddKey(k1);
            curve.AddKey(k2);
            return curve;
        }

        private static AnimationCurve CreateSlowInFastOut()
        {
            var curve = new AnimationCurve();
            var k1 = new Keyframe(0, 0, 0, 0, 0, 0.8f) { weightedMode = WeightedMode.Both };
            var k2 = new Keyframe(1, 1, 0, 3f, 0.2f, 0) { weightedMode = WeightedMode.Both };
            curve.AddKey(k1);
            curve.AddKey(k2);
            return curve;
        }

        private static AnimationCurve CreatePingPong()
        {
            var curve = new AnimationCurve();
            curve.AddKey(new Keyframe(0, 0));
            curve.AddKey(new Keyframe(0.5f, 1));
            curve.AddKey(new Keyframe(1, 0));
            for (int i = 0; i < curve.length; i++)
                curve.SmoothTangents(i, 0);
            return curve;
        }
    }
}
