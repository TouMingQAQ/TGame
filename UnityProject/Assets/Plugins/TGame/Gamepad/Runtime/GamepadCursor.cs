using UnityEngine;

namespace TGame.Gamepad
{
    [DisallowMultipleComponent]
    [AddComponentMenu("TGame/Gamepad/Gamepad Cursor")]
    public sealed class GamepadCursor : MonoBehaviour, IGamepadCursor
    {
        [SerializeField] private RectTransform _visual;
        [SerializeField, Min(0f)] private float _followSpeed = 20f;
        [SerializeField] private bool _snapOnTargetChange = true;

        private GamepadItem _target;
        private CanvasGroup _canvasGroup;
        private bool _snapPending;
        private bool _hasPosition;
        private bool _warnedMissingRect;

        private void Awake()
        {
            EnsureVisual();
            SetVisible(false);
        }

        private void OnEnable()
        {
            _hasPosition = false;
            UpdateVisual(0f);
        }

        private void OnDisable()
        {
            SetVisible(false);
        }

        private void LateUpdate()
        {
            UpdateVisual(Time.unscaledDeltaTime);
        }

        public void SetTarget(GamepadItem item)
        {
            if (_target != item)
            {
                _target = item;
                _snapPending = _snapOnTargetChange;
            }

            if (isActiveAndEnabled)
                UpdateVisual(0f);
        }

        public void Move(GameObject target)
        {
            var item = target ? target.GetComponent<GamepadItem>() : null;
            SetTarget(item);
        }

        public void Show()
        {
            if (_target != null) SetVisible(true);
        }

        public void Hide()
        {
            _target = null;
            _snapPending = false;
            SetVisible(false);
        }

        private bool EnsureVisual()
        {
            if (_visual == null)
                _visual = transform as RectTransform;

            if (_visual == null)
            {
                if (!_warnedMissingRect)
                {
                    Debug.LogWarning("GamepadCursor needs a RectTransform visual.", this);
                    _warnedMissingRect = true;
                }

                return false;
            }

            if (_canvasGroup == null || _canvasGroup.transform != _visual)
            {
                _canvasGroup = _visual.GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                    _canvasGroup = _visual.gameObject.AddComponent<CanvasGroup>();
            }

            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
            return true;
        }

        private void UpdateVisual(float deltaTime)
        {
            if (!EnsureVisual())
                return;

            if (_target == null || !_target.IsSelected || !_target.IsNavigable)
            {
                SetVisible(false);
                return;
            }

            RectTransform anchor = _target.CursorAnchor;
            RectTransform parent = _visual.parent as RectTransform;
            if (anchor == null || parent == null || !anchor.gameObject.activeInHierarchy)
            {
                SetVisible(false);
                return;
            }

            Canvas sourceCanvas = anchor.GetComponentInParent<Canvas>();
            Canvas destinationCanvas = _visual.GetComponentInParent<Canvas>();
            if (sourceCanvas == null || destinationCanvas == null)
            {
                SetVisible(false);
                return;
            }

            Camera sourceCamera = GetCanvasCamera(sourceCanvas);
            Camera destinationCamera = GetCanvasCamera(destinationCanvas);
            if ((sourceCanvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay && sourceCamera == null) ||
                (destinationCanvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay && destinationCamera == null))
            {
                SetVisible(false);
                return;
            }

            Vector2 offset = _target.CursorOffset;
            Vector3 worldPoint = anchor.TransformPoint(new Vector3(offset.x, offset.y, 0f));
            if (sourceCamera != null && sourceCamera.WorldToScreenPoint(worldPoint).z <= 0f)
            {
                SetVisible(false);
                return;
            }

            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(sourceCamera, worldPoint);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent, screenPoint, destinationCamera, out Vector2 localPoint))
            {
                SetVisible(false);
                return;
            }

            Vector3 position = new Vector3(localPoint.x, localPoint.y, 0f);
            if (!_hasPosition || _snapPending || _followSpeed <= 0f)
                _visual.localPosition = position;
            else
                _visual.localPosition = Vector3.Lerp(_visual.localPosition, position,
                    1f - Mathf.Exp(-_followSpeed * deltaTime));

            _snapPending = false;
            _hasPosition = true;
            SetVisible(true);
        }

        private static Camera GetCanvasCamera(Canvas canvas)
        {
            Canvas root = canvas.rootCanvas;
            if (root.renderMode == RenderMode.ScreenSpaceOverlay)
                return null;

            return root.worldCamera != null ? root.worldCamera : Camera.main;
        }

        private void SetVisible(bool visible)
        {
            if (!visible)
                _hasPosition = false;

            if (_canvasGroup == null)
                return;

            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }
    }
}
