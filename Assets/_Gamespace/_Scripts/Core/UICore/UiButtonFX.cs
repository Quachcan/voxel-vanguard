using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace _Workspace._Scripts.Core.UICore
{
    /// <summary>
    /// Advanced UI Button Effects Manager
    /// Handles scale, color, rotation, and custom animations for buttons with event feedback.
    /// Supports hover, press, click, and loop animations with flexible presets.
    /// </summary>
    public class UiButtonFX : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private enum FxPreset
        {
            None,
            PressScale,
            PressAndPunch,
            HoverLift,
            PulseOnClick,
            SoftBlend,
            BouncePress,
            RotateClick,
            GlowEffect
        }

        private enum LoopStyle
        {
            None,
            PulseScale,
            Sway,
            FloatY,
            ShakePos,
            ShakeRot,
            Breathing,
            Rotate,
            SlideHorizontal,
            BounceYoyo
        }

        [Header("Targets")]
        [SerializeField] private Transform target;
        [SerializeField] private Graphic targetGraphic;

        [Header("Preset")]
        [SerializeField] private FxPreset preset = FxPreset.PressAndPunch;

        [Header("Hover (PC)")]
        [SerializeField] private bool enableHover;
        [SerializeField] private float hoverScale = 1.03f;
        [SerializeField] private float hoverDuration = 0.15f;
        [SerializeField] private Ease hoverEase = Ease.OutQuad;

        [Header("Press")]
        [SerializeField] private bool enablePressScale = true;
        [SerializeField] private float pressedScale = 0.94f;
        [SerializeField] private float pressInDuration = 0.08f;
        [SerializeField] private float pressOutDuration = 0.15f;
        [SerializeField] private Ease pressOutEase = Ease.OutBack;

        [Header("Click")]
        [SerializeField] private bool enableClickPunch = true;
        [SerializeField] private float punchScale = 0.08f;
        [SerializeField] private float punchDuration = 0.18f;
        [SerializeField] private int punchVibrato = 8;
        [SerializeField] private float punchElasticity = 0.9f;

        [Header("Rotation (Click)")]
        [SerializeField] private bool enableClickRotation;
        [SerializeField] private float clickRotationAngle = 20f;
        [SerializeField] private float clickRotationDuration = 0.25f;
        [SerializeField] private Ease clickRotationEase = Ease.OutQuad;

        [Header("Bounce (Press)")]
        [SerializeField] private bool enableBouncePress;
        [SerializeField] private float bouncePressScale = 1.15f;
        [SerializeField] private float bouncePressOutScale = 0.92f;
        [SerializeField] private float bounceDuration = 0.3f;
        //[SerializeField] private int bounceVibrato = 5;
        //[SerializeField] private float bounceElasticity = 1.2f;

        [Header("Color (optional)")]
        [SerializeField] private bool enableColor;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color hoverColor = Color.white;
        [SerializeField] private Color pressedColor = Color.white;
        [SerializeField] private Color disabledColor = new(1f, 1f, 1f, 0.5f);
        [SerializeField] private float colorDuration = 0.1f;

        [Header("Loop Animation")]
        [SerializeField] private LoopStyle loopStyle = LoopStyle.None;
        [SerializeField] private bool playLoopOnEnable;
        
        [SerializeField] private Vector3 loopScale = new(1.05f, 1.05f, 1f);
        [SerializeField] private float loopSwayAngle = 8f;
        [SerializeField] private float loopFloatY = 8f;
        [SerializeField] private float loopShakeStrength = 10f;
        [SerializeField] private float loopDuration = 1.0f;
        [SerializeField] private Ease loopEase = Ease.InOutSine;

        [Header("Timing")]
        [Tooltip("If true, all tweens and timers use unscaled time (ignore Time.timeScale)")]
        [SerializeField] private bool useUnscaledTime;

        [Header("Audio (optional)")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip hoverClip;
        [SerializeField] private AudioClip clickClip;
        [SerializeField] private AudioClip pressClip;
        [SerializeField] private AudioClip releaseClip;

        [Header("Long Press (optional)")]
        [SerializeField] private bool enableLongPress;
        [SerializeField] private float longPressDuration = 0.6f;
        [Tooltip("If true, a long-press will prevent the click event/punch on release")]
        [SerializeField] private bool suppressClickOnLongPress = true;

        [Header("Events")]
        public UnityEvent onHoverEnter;
        public UnityEvent onHoverExit;
        public UnityEvent onPressedDown;
        public UnityEvent onReleased;
        public UnityEvent onClicked;
        public UnityEvent onLongPress;
        public UnityEvent onInteractableChanged;

        [Header("Behavior")]
        [Tooltip("If true and the Button becomes non-interactable at runtime, stop loop animations automatically")]
        [SerializeField] private bool stopLoopWhenDisabled = true;
        
        private Button _button;
        private Vector3 _baseScale;
        private Vector3 _basePos;
        private Quaternion _baseRot;
        private RectTransform _rt;
        private Vector2 _baseAnchoredPos;

        private Tweener _scaleTw;
        private Tweener _colorTw;
        private Tweener _loopTwPos;
        private Tweener _loopTwRot;
        private Tweener _loopTwScale;
        
        private bool _hovered;
        private bool _pressed;
        private bool _isUI;
        
        // Runtime state
        private bool _lastInteractable;
        private float _pressStartTime;
        private bool _longPressFired;
        private bool _suppressNextClick;

        private void Reset()
        {
            target = transform;
            targetGraphic = GetComponent<Graphic>();
        }
        
        private void Awake()
        {
            _button = GetComponent<Button>();
            if (!target) target = transform;
            _rt = target as RectTransform;
            _isUI = _rt != null;

            _baseScale = target.localScale;
            _baseRot   = target.localRotation;
            if (_isUI)
            {
                if (_rt != null) _baseAnchoredPos = _rt.anchoredPosition;
            }
            else
                _basePos = target.localPosition;
            
            if (enableColor && targetGraphic)
            {
                targetGraphic.color = _button && !_button.interactable ? disabledColor : normalColor;
            }

            ApplyPreset(preset);
            _lastInteractable = !_button || _button.interactable;
        }

        private void OnEnable()
        {
            // Re-evaluate in case target was reassigned in Inspector while disabled
            if (!_rt || (_isUI && _rt.transform != target))
            {
                _rt = target as RectTransform;
                _isUI = _rt != null;
            }

            KillAllLoopTweens();
            
            Canvas.ForceUpdateCanvases();
            if (_isUI)
            {
                if (_rt != null) _baseAnchoredPos = _rt.anchoredPosition;
            }
            else      _basePos         = target.localPosition;

            ResetTransform();

            if (enableColor && targetGraphic)
                targetGraphic.color = _button && !_button.interactable ? disabledColor : normalColor;

            if (playLoopOnEnable) StartLoop();
        }

        private void OnDisable()
        {
            KillAllTweens();
            ResetTransform();
            _hovered = _pressed = false;
        }

        private void Update()
        {
            // Long press
            if (enableLongPress && _pressed && !_longPressFired && _button && _button.interactable)
            {
                if (TimeNow() - _pressStartTime >= longPressDuration)
                {
                    _longPressFired = true;
                    if (suppressClickOnLongPress) _suppressNextClick = true;
                    onLongPress?.Invoke();
                }
            }

            // Monitor interactable changes
            if (_button)
            {
                bool current = _button.interactable;
                if (current != _lastInteractable)
                {
                    _lastInteractable = current;
                    onInteractableChanged?.Invoke();

                    if (enableColor && targetGraphic)
                    {
                        Color targetCol = current ? (_hovered ? hoverColor : normalColor) : disabledColor;
                        ColorTo(targetCol, colorDuration);
                    }

                    if (!current)
                    {
                        if (stopLoopWhenDisabled) StopLoop();
                        // Reset visual to base
                        ResetTransform();
                    }
                    else
                    {
                        if (playLoopOnEnable) StartLoop();
                    }
                }
            }
        }

        public void SetPreset(int p)
        {
            ApplyPreset((FxPreset)p);
            RefreshVisualState();
        }

        private void RefreshVisualState()
        {
            if (enableColor && targetGraphic)
            {
                bool interactable = !_button || _button.interactable;
                var c = interactable ? (_pressed ? pressedColor : (_hovered ? hoverColor : normalColor)) : disabledColor;
                targetGraphic.color = c;
            }

            // Scale
            if (enablePressScale && _pressed)
                target.localScale = _baseScale * pressedScale;
            else if (enableHover && _hovered)
                target.localScale = _baseScale * hoverScale;
            else
                target.localScale = _baseScale;
        }

        private void ApplyPreset(FxPreset p)
        {
            preset = p;
            switch (preset)
            {
                case FxPreset.None:
                    enablePressScale = false;
                    enableHover = false;
                    enableClickPunch = false;
                    enableClickRotation = false;
                    enableBouncePress = false;
                    break;

                case FxPreset.PressScale:
                    enablePressScale = true;
                    enableHover = false;
                    enableClickPunch = false;
                    enableClickRotation = false;
                    enableBouncePress = false;
                    break;

                case FxPreset.PressAndPunch:
                    enablePressScale = true;
                    enableHover = false;
                    enableClickPunch = true;
                    enableClickRotation = false;
                    enableBouncePress = false;
                    break;

                case FxPreset.HoverLift:
                    enablePressScale = false;
                    enableHover = true;
                    enableClickPunch = false;
                    enableClickRotation = false;
                    enableBouncePress = false;
                    break;

                case FxPreset.PulseOnClick:
                    enablePressScale = false;
                    enableHover = false;
                    enableClickPunch = true;
                    enableClickRotation = false;
                    enableBouncePress = false;
                    break;

                case FxPreset.SoftBlend:
                    enablePressScale = true;
                    enableHover = true;
                    enableClickPunch = false;
                    enableClickRotation = false;
                    enableBouncePress = false;
                    hoverScale = 1.02f;
                    pressedScale = 0.97f;
                    break;

                case FxPreset.BouncePress:
                    enablePressScale = false;
                    enableHover = false;
                    enableClickPunch = false;
                    enableClickRotation = false;
                    enableBouncePress = true;
                    break;

                case FxPreset.RotateClick:
                    enablePressScale = true;
                    enableHover = false;
                    enableClickPunch = false;
                    enableClickRotation = true;
                    enableBouncePress = false;
                    break;

                case FxPreset.GlowEffect:
                    enableColor = true;
                    enableHover = true;
                    enableClickPunch = false;
                    enableClickRotation = false;
                    enableBouncePress = false;
                    hoverScale = 1.05f;
                    break;
            }
        }

        // ========== Pointer Events ==========
        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            if (!_button || !_button.interactable) return;

            if (enableHover && !_pressed)
                ScaleTo(_baseScale * hoverScale, hoverDuration, hoverEase);

            if (enableColor && targetGraphic && (_button && _button.interactable))
                ColorTo(hoverColor, colorDuration);

            PlayClip(hoverClip);
            onHoverEnter?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            if (!_button || !_button.interactable) return;

            if (!_pressed)
                ScaleTo(_baseScale, hoverDuration, hoverEase);

            if (enableColor && targetGraphic && (_button && _button.interactable))
                ColorTo(normalColor, colorDuration);

            onHoverExit?.Invoke();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pressed = true;
            if (!_button || !_button.interactable) return;

            _pressStartTime = TimeNow();
            _longPressFired = false;
            _suppressNextClick = false;

            if (enablePressScale)
                ScaleTo(_baseScale * pressedScale, pressInDuration, Ease.Linear);

            if (enableBouncePress)
                PlayBounceAnimation();

            if (enableColor && targetGraphic)
                ColorTo(pressedColor, colorDuration);

            PlayClip(pressClip);
            onPressedDown?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pressed = false;
            if (!_button || !_button.interactable) return;

            var targetScale = (_hovered && enableHover) ? _baseScale * hoverScale : _baseScale;
            if (enablePressScale)
                ScaleTo(targetScale, pressOutDuration, pressOutEase);

            if (enableColor && targetGraphic)
                ColorTo(_hovered ? hoverColor : normalColor, colorDuration);

            PlayClip(releaseClip);
            onReleased?.Invoke();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_button || !_button.interactable) return;
            if (_suppressNextClick)
            {
                _suppressNextClick = false;
                return;
            }

            if (enableClickPunch)
            {
                _scaleTw?.Kill();
                target.localScale = (_hovered && enableHover) ? _baseScale * hoverScale : _baseScale;
                ApplyUpdate(target.DOPunchScale(Vector3.one * punchScale, punchDuration, punchVibrato, punchElasticity));
            }

            if (enableClickRotation)
                PlayRotationAnimation();

            PlayClip(clickClip);
            onClicked?.Invoke();
        }

        // ========== Loop Animation ==========
        [ContextMenu("Start Loop")]
        public void StartLoop()
        {
            KillAllLoopTweens();
            if (stopLoopWhenDisabled && _button && !_button.interactable)
                return;
            switch (loopStyle)
            {
                case LoopStyle.None:
                    return;

                case LoopStyle.PulseScale:
                    _loopTwScale = ApplyUpdate(
                        target.DOScale(Vector3.Scale(_baseScale, loopScale), loopDuration)
                              .SetEase(loopEase)
                              .SetLoops(-1, LoopType.Yoyo)
                    );
                    break;

                case LoopStyle.Sway:
                    target.localRotation = _baseRot;
                    _loopTwRot = ApplyUpdate(
                        target.DOLocalRotate(new Vector3(0, 0, loopSwayAngle), loopDuration * 0.5f)
                              .SetRelative(false)
                              .SetEase(loopEase)
                              .SetLoops(-1, LoopType.Yoyo)
                    );
                    break;

                case LoopStyle.FloatY:
                    if (_isUI)
                        _loopTwPos = ApplyUpdate(
                            _rt.DOAnchorPosY(_baseAnchoredPos.y + loopFloatY, loopDuration * 0.5f)
                              .SetEase(loopEase).SetLoops(-1, LoopType.Yoyo)
                        );
                    else
                        _loopTwPos = ApplyUpdate(
                            target.DOLocalMoveY(_basePos.y + loopFloatY, loopDuration * 0.5f)
                                  .SetEase(loopEase).SetLoops(-1, LoopType.Yoyo)
                        );
                    break;

                case LoopStyle.ShakePos:
                    if (_isUI)
                        _loopTwPos = ApplyUpdate(
                            _rt.DOShakeAnchorPos(loopDuration, loopShakeStrength, 10, 90, false, true)
                              .SetLoops(-1, LoopType.Restart)
                        );
                    else
                        _loopTwPos = ApplyUpdate(
                            target.DOShakePosition(loopDuration, loopShakeStrength, 10, 90, false, true)
                                  .SetLoops(-1, LoopType.Restart)
                        );
                    break;

                case LoopStyle.ShakeRot:
                    _loopTwRot = ApplyUpdate(
                        target.DOShakeRotation(loopDuration, loopShakeStrength * 5f, vibrato: 10, randomness: 90, fadeOut: true)
                              .SetLoops(-1, LoopType.Restart)
                    );
                    break;

                case LoopStyle.Breathing:
                    _loopTwScale = ApplyUpdate(
                        target.DOScale(Vector3.Scale(_baseScale, new Vector3(loopScale.x, loopScale.y, 1f)), loopDuration * 1.2f)
                              .SetEase(Ease.InOutSine)
                              .SetLoops(-1, LoopType.Yoyo)
                    );
                    break;

                case LoopStyle.Rotate:
                    target.localRotation = _baseRot;
                    _loopTwRot = ApplyUpdate(
                        target.DOLocalRotate(new Vector3(0, 0, 360f), loopDuration, RotateMode.FastBeyond360)
                              .SetEase(Ease.Linear)
                              .SetLoops(-1, LoopType.Restart)
                    );
                    break;

                case LoopStyle.SlideHorizontal:
                    if (_isUI)
                        _loopTwPos = ApplyUpdate(
                            _rt.DOAnchorPosX(_baseAnchoredPos.x + loopFloatY, loopDuration * 0.5f)
                              .SetEase(loopEase).SetLoops(-1, LoopType.Yoyo)
                        );
                    else
                        _loopTwPos = ApplyUpdate(
                            target.DOLocalMoveX(_basePos.x + loopFloatY, loopDuration * 0.5f)
                                  .SetEase(loopEase).SetLoops(-1, LoopType.Yoyo)
                        );
                    break;

                case LoopStyle.BounceYoyo:
                    _loopTwScale = ApplyUpdate(
                        target.DOScale(Vector3.Scale(_baseScale, loopScale), loopDuration * 0.6f)
                              .SetEase(Ease.OutBounce)
                              .SetLoops(-1, LoopType.Yoyo)
                    );
                    break;
            }
        }

        [ContextMenu("Stop Loop")]
        public void StopLoop()
        {
            KillAllLoopTweens();
            ResetTransform();
        }

        // ========== Helpers ==========
        private T ApplyUpdate<T>(T t) where T : Tween
        {
            if (t == null) return null;
            t.SetUpdate(useUnscaledTime);
            return t;
        }

        private float TimeNow() => useUnscaledTime ? Time.unscaledTime : Time.time;

        private void PlayClip(AudioClip clip)
        {
            if (!audioSource || !clip) return;
            audioSource.PlayOneShot(clip);
        }

        private void ScaleTo(Vector3 s, float dur, Ease ease)
        {
            _scaleTw?.Kill();
            _scaleTw = ApplyUpdate(target.DOScale(s, dur).SetEase(ease));
        }

        private void ColorTo(Color c, float dur)
        {
            if (!targetGraphic) return;
            _colorTw?.Kill();
            _colorTw = ApplyUpdate(targetGraphic.DOColor(c, dur));
        }

        private void KillAllTweens()
        {
            _scaleTw?.Kill();
            _colorTw?.Kill();
            KillAllLoopTweens();
        }

        private void KillAllLoopTweens()
        {
            _loopTwPos?.Kill();
            _loopTwRot?.Kill();
            _loopTwScale?.Kill();
            _loopTwPos = _loopTwRot = _loopTwScale = null;
        }

        private void ResetTransform()
        {
            if (!target) return;
            target.localScale   = _baseScale;
            target.localRotation= _baseRot;

            if (_isUI && _rt) _rt.anchoredPosition = _baseAnchoredPos;
            else if (!_isUI) target.localPosition= _basePos;
        }

        // ========== New Animation Methods ==========
        /// <summary>
        /// Plays a bounce animation on press (spring-like effect)
        /// </summary>
        private void PlayBounceAnimation()
        {
            _scaleTw?.Kill();
            var sequence = DOTween.Sequence();
            sequence.Append(target.DOScale(_baseScale * bouncePressScale, bounceDuration * 0.4f)
                                  .SetEase(Ease.OutCubic));
            sequence.Append(target.DOScale(_baseScale * bouncePressOutScale, bounceDuration * 0.6f)
                                  .SetEase(Ease.OutElastic));
            ApplyUpdate(sequence);
        }

        /// <summary>
        /// Plays a rotation animation on click
        /// </summary>
        private void PlayRotationAnimation()
        {
            var rotTween = target.DOLocalRotate(
                new Vector3(0, 0, clickRotationAngle),
                clickRotationDuration,
                RotateMode.FastBeyond360);
            rotTween.SetEase(clickRotationEase)
                   .OnComplete(() =>
                   {
                       target.DOLocalRotate(Vector3.zero, clickRotationDuration * 0.5f)
                             .SetEase(Ease.OutQuad);
                   });
            ApplyUpdate(rotTween);
        }
    }
}