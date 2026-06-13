using DG.Tweening;
using UnityEngine;

namespace TGame.Tween
{
    public sealed class TTweenAnimatorStateNameAttribute : PropertyAttribute
    {
    }

    /// <summary>
    /// Animator State 构造器。
    /// 用 DOTween 驱动 Animator 的 normalizedTime,把 Animator Controller 中的一段 State 接入 TTweenPlay 时间轴。
    /// </summary>
    [AddComponentMenu("TGame/Tween/Nodes/TTweenAnimatorState")]
    public class TTweenAnimatorState : TTweenNode
    {
        [Header("Target")]
        [SerializeField]
        [Tooltip("要采样的 Animator,为空时使用自身或父级 Animator")]
        private Animator _target;

        [Header("State")]
        [SerializeField]
        [TTweenAnimatorStateName]
        [Tooltip("Animator Controller 中的 State 名称")]
        private string _stateName;

        [SerializeField]
        [Tooltip("Animator Layer 索引")]
        private int _layer = 0;

        [Header("Range")]
        [SerializeField]
        [Tooltip("起始 normalizedTime。0 = 动画开头,1 = 第一轮结尾")]
        private float _fromNormalizedTime = 0f;

        [SerializeField]
        [Tooltip("结束 normalizedTime。可大于 1 播放多轮循环 State")]
        private float _toNormalizedTime = 1f;

        [Header("Duration")]
        [SerializeField]
        [Tooltip("按 Animator State 原生时长播放时,是否把 Animator.speed 计入速度计算")]
        private bool _respectAnimatorSpeed = true;

        [Header("Animator Control")]
        [SerializeField]
        [Tooltip("播放 Tween 期间冻结 Animator 自身时间推进,只由 Tween 采样")]
        private bool _freezeAnimatorSpeed = true;

        [SerializeField]
        [Tooltip("Tween 完成、回退或 Kill 时恢复 Animator.speed")]
        private bool _restoreSpeed = true;

        [Header("Easing")]
        [SerializeField]
        private Ease _ease = Ease.Linear;

        [SerializeField]
        private AnimationCurve _customCurve;

        private float _savedSpeed = 1f;
        private bool _speedCaptured;
        private int _stateHash;

        public Animator Target
        {
            get => _target;
            set => _target = value;
        }

        public string StateName
        {
            get => _stateName;
            set => _stateName = value;
        }

        public int Layer
        {
            get => _layer;
            set => _layer = value;
        }

        public override float GetPlaybackDuration()
        {
            return TryGetPlaybackDuration(out var duration) ? duration : 0.0001f;
        }

        public bool TryGetPlaybackDuration(out float duration)
        {
            return TryResolveDuration(FindAnimatorTarget(false), out duration);
        }

        public override DG.Tweening.Tween BuildTween()
        {
            var target = ResolveTarget();
            if (target == null)
            {
                Debug.LogWarning($"[TTweenAnimatorState] {name} missing Animator target", this);
                return null;
            }

            if (string.IsNullOrEmpty(_stateName))
            {
                Debug.LogWarning($"[TTweenAnimatorState] {name} missing state name", this);
                return null;
            }

            if (!TryResolveStateHash(target, out _stateHash))
            {
                Debug.LogWarning($"[TTweenAnimatorState] Animator does not have state '{_stateName}' on layer {_layer}", this);
                return null;
            }

            float normalizedTime = _fromNormalizedTime;
            float duration = ResolveDuration(target);

            var tween = DOTween.To(
                () => normalizedTime,
                value =>
                {
                    normalizedTime = value;
                    Sample(target, normalizedTime);
                },
                _toNormalizedTime,
                duration);

            ApplyEase(tween);

            var seq = DOTween.Sequence();
            seq.Append(tween);
            seq.OnPlay(() =>
            {
                CaptureAnimatorSpeed(target);
                Sample(target, normalizedTime);
            });
            seq.OnComplete(() => RestoreAnimatorSpeed(target));
            seq.OnRewind(() => RestoreAnimatorSpeed(target));
            seq.OnKill(() => RestoreAnimatorSpeed(target));

            return seq;
        }

        private Animator ResolveTarget()
        {
            return FindAnimatorTarget(true);
        }

        private Animator FindAnimatorTarget(bool cacheTarget)
        {
            var target = _target;
            if (target != null)
                return target;

            target = GetComponent<Animator>();
            if (target == null)
                target = GetComponentInParent<Animator>();

            if (cacheTarget)
                _target = target;

            return target;
        }

        private bool TryResolveStateHash(Animator target, out int stateHash)
        {
            stateHash = 0;
            if (target == null)
                return false;

            if (_layer < 0 || _layer >= target.layerCount)
                return false;

            int directHash = Animator.StringToHash(_stateName);
            if (target.HasState(_layer, directHash))
            {
                stateHash = directHash;
                return true;
            }

            string layerName = target.GetLayerName(_layer);
            int fullPathHash = Animator.StringToHash($"{layerName}.{_stateName}");
            if (target.HasState(_layer, fullPathHash))
            {
                stateHash = fullPathHash;
                return true;
            }

            return false;
        }

        private float ResolveDuration(Animator target)
        {
            if (TryResolveDuration(target, out var duration))
                return duration;

            Debug.LogWarning($"[TTweenAnimatorState] Cannot resolve Animator state duration for '{_stateName}'", this);
            return 0.0001f;
        }

        private bool TryResolveDuration(Animator target, out float duration)
        {
            duration = 0f;
            float range = Mathf.Abs(_toNormalizedTime - _fromNormalizedTime);
            if (range <= 0f)
            {
                duration = 0.0001f;
                return true;
            }

            if (TryGetStateLength(target, out var stateLength))
            {
                duration = Mathf.Max(0.0001f, stateLength * range / GetAnimatorSpeedFactor(target));
                return true;
            }

            return false;
        }

        private bool TryGetStateLength(Animator target, out float stateLength)
        {
            stateLength = 0f;
            if (target == null || _layer < 0 || _layer >= target.layerCount)
                return false;

            if (!TryResolveStateHash(target, out var stateHash))
                return false;

            var currentState = target.GetCurrentAnimatorStateInfo(_layer);
            int currentHash = currentState.fullPathHash;
            float currentNormalizedTime = currentState.normalizedTime;
            float currentSpeed = target.speed;

            target.Play(stateHash, _layer, _fromNormalizedTime);
            target.Update(0f);

            var sampledState = target.GetCurrentAnimatorStateInfo(_layer);
            stateLength = sampledState.length;

            if (currentHash != 0)
            {
                target.Play(currentHash, _layer, currentNormalizedTime);
                target.Update(0f);
            }
            target.speed = currentSpeed;

            return stateLength > 0f;
        }

        private float GetAnimatorSpeedFactor(Animator target)
        {
            if (!_respectAnimatorSpeed || target == null)
                return 1f;

            float speed = Mathf.Abs(target.speed);
            return speed > 0.0001f ? speed : 1f;
        }

        private void CaptureAnimatorSpeed(Animator target)
        {
            if (!_freezeAnimatorSpeed || target == null)
                return;

            if (!_speedCaptured)
            {
                _savedSpeed = target.speed;
                _speedCaptured = true;
            }
            target.speed = 0f;
        }

        private void RestoreAnimatorSpeed(Animator target)
        {
            if (!_restoreSpeed || !_speedCaptured || target == null)
                return;

            target.speed = _savedSpeed;
            _speedCaptured = false;
        }

        private void Sample(Animator target, float normalizedTime)
        {
            if (target == null)
                return;

            target.Play(_stateHash, _layer, normalizedTime);
            target.Update(0f);
        }

        private void ApplyEase(DG.Tweening.Tween tween)
        {
            if (_customCurve != null && _customCurve.length > 0)
                tween.SetEase(_customCurve);
            else
                tween.SetEase(_ease);
        }

        private void Reset()
        {
            _target = GetComponent<Animator>();
            if (_target == null)
                _target = GetComponentInParent<Animator>();
        }
    }

#if UNITY_EDITOR
    [UnityEditor.CustomPropertyDrawer(typeof(TTweenAnimatorStateNameAttribute))]
    internal sealed class TTweenAnimatorStateNameDrawer : UnityEditor.PropertyDrawer
    {
        public override void OnGUI(Rect position, UnityEditor.SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != UnityEditor.SerializedPropertyType.String)
            {
                UnityEditor.EditorGUI.PropertyField(position, property, label);
                return;
            }

            var animator = ResolveAnimator(property);
            var layer = ResolveLayer(property);
            var controller = ResolveController(animator);
            var stateNames = CollectStateNames(controller, layer);

            if (stateNames.Count == 0)
            {
                UnityEditor.EditorGUI.PropertyField(position, property, label);
                return;
            }

            var values = new System.Collections.Generic.List<string>(stateNames.Count + 2) { string.Empty };
            var displayNames = new System.Collections.Generic.List<string>(stateNames.Count + 2) { "<None>" };
            values.AddRange(stateNames);
            displayNames.AddRange(stateNames);

            string currentValue = property.stringValue;
            string relativeCurrentValue = ToRelativeStateName(currentValue, controller, layer);
            int selectedIndex = values.IndexOf(currentValue);
            if (selectedIndex < 0)
                selectedIndex = values.IndexOf(relativeCurrentValue);

            if (selectedIndex < 0 && !string.IsNullOrEmpty(currentValue))
            {
                values.Add(currentValue);
                displayNames.Add($"{currentValue} (Missing)");
                selectedIndex = values.Count - 1;
            }

            if (selectedIndex < 0)
                selectedIndex = 0;

            UnityEditor.EditorGUI.BeginProperty(position, label, property);
            UnityEditor.EditorGUI.BeginChangeCheck();
            int newIndex = UnityEditor.EditorGUI.Popup(position, label.text, selectedIndex, displayNames.ToArray());
            if (UnityEditor.EditorGUI.EndChangeCheck())
                property.stringValue = values[newIndex];
            UnityEditor.EditorGUI.EndProperty();
        }

        private static Animator ResolveAnimator(UnityEditor.SerializedProperty property)
        {
            var targetProp = property.serializedObject.FindProperty("_target");
            var animator = targetProp != null ? targetProp.objectReferenceValue as Animator : null;
            if (animator != null)
                return animator;

            if (property.serializedObject.targetObject is Component component)
            {
                animator = component.GetComponent<Animator>();
                return animator != null ? animator : component.GetComponentInParent<Animator>();
            }

            return null;
        }

        private static int ResolveLayer(UnityEditor.SerializedProperty property)
        {
            var layerProp = property.serializedObject.FindProperty("_layer");
            return layerProp != null ? layerProp.intValue : 0;
        }

        private static UnityEditor.Animations.AnimatorController ResolveController(Animator animator)
        {
            RuntimeAnimatorController controller = animator != null ? animator.runtimeAnimatorController : null;
            while (controller is AnimatorOverrideController overrideController)
                controller = overrideController.runtimeAnimatorController;
            return controller as UnityEditor.Animations.AnimatorController;
        }

        private static System.Collections.Generic.List<string> CollectStateNames(
            UnityEditor.Animations.AnimatorController controller,
            int layer)
        {
            var stateNames = new System.Collections.Generic.List<string>();
            if (controller == null || layer < 0 || layer >= controller.layers.Length)
                return stateNames;

            CollectStateNames(controller.layers[layer].stateMachine, string.Empty, stateNames);
            return stateNames;
        }

        private static void CollectStateNames(
            UnityEditor.Animations.AnimatorStateMachine stateMachine,
            string prefix,
            System.Collections.Generic.List<string> stateNames)
        {
            if (stateMachine == null)
                return;

            var states = stateMachine.states;
            for (int i = 0; i < states.Length; i++)
            {
                var state = states[i].state;
                if (state == null)
                    continue;

                string stateName = string.IsNullOrEmpty(prefix) ? state.name : $"{prefix}.{state.name}";
                if (!stateNames.Contains(stateName))
                    stateNames.Add(stateName);
            }

            var childStateMachines = stateMachine.stateMachines;
            for (int i = 0; i < childStateMachines.Length; i++)
            {
                var childStateMachine = childStateMachines[i].stateMachine;
                if (childStateMachine == null)
                    continue;

                string childPrefix = string.IsNullOrEmpty(prefix)
                    ? childStateMachine.name
                    : $"{prefix}.{childStateMachine.name}";
                CollectStateNames(childStateMachine, childPrefix, stateNames);
            }
        }

        private static string ToRelativeStateName(
            string stateName,
            UnityEditor.Animations.AnimatorController controller,
            int layer)
        {
            if (string.IsNullOrEmpty(stateName) || controller == null || layer < 0 || layer >= controller.layers.Length)
                return stateName;

            string layerPrefix = controller.layers[layer].name + ".";
            return stateName.StartsWith(layerPrefix, System.StringComparison.Ordinal)
                ? stateName.Substring(layerPrefix.Length)
                : stateName;
        }
    }

    [UnityEditor.CustomEditor(typeof(TTweenAnimatorState))]
    internal sealed class TTweenAnimatorStateEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", "_duration");

            if (target is TTweenAnimatorState animatorState)
            {
                UnityEditor.EditorGUILayout.Space(4);
                using (new UnityEditor.EditorGUI.DisabledScope(true))
                {
                    float duration = animatorState.TryGetPlaybackDuration(out var resolvedDuration)
                        ? resolvedDuration
                        : 0f;
                    UnityEditor.EditorGUILayout.FloatField("Playback Duration", duration);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
}
