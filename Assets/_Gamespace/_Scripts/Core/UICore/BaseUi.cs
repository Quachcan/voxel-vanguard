using DG.Tweening;
using UnityEngine;

namespace _GameSpace._Scripts.Core.UICore
{
    public class BaseUi : MonoBehaviour
    {
        [Header("Setting UI")] 
        [SerializeField] protected GameObject graphicHolder;
        [SerializeField] protected GameObject backgroundObject;
        [SerializeField] protected bool setActiveOnStart;

        [Header("Tween Setting")]
        [SerializeField] protected float fadeDuration = 0.3f;
        [SerializeField] protected float scaleDuration = 0.3f;
        [SerializeField] protected float popScaleFrom = 0.6f;
        [SerializeField] protected Ease easeIn = Ease.OutBack;
        [SerializeField] protected Ease easeOut = Ease.InCubic;

        protected enum EnterAnimationType { None, Slide, FadeScalePop, SlideOvershoot, Bounce }
        protected enum ExitAnimationType { None, Slide, FadeScalePop, SlideOvershoot, Bounce }
        protected enum SlideDirection { Left, Right, Top, Bottom }

        [Header("Animation Mode")]
        [SerializeField] protected EnterAnimationType enterAnimationType;
        [SerializeField] protected ExitAnimationType exitAnimationType;

        [Header("Slide Setting")]
        [SerializeField] protected SlideDirection slideInFrom;
        [SerializeField] protected SlideDirection slideOutTo;
        [SerializeField] protected float slideDistance = 800f;

        [Header("Slide Overshoot")]
        [SerializeField] protected float overshootPx = 60f;
        [SerializeField] protected Ease slideEaseIn = Ease.OutBack;
        [SerializeField] protected Ease slideEaseOut = Ease.InCubic;

        [Header("Bounce / Shake")]
        [SerializeField] protected Vector3 punchScale = new Vector3(0.12f, 0.12f, 0);
        [SerializeField] protected float punchDuration = 0.3f;
        [SerializeField] protected int punchVibrato = 10;
        [SerializeField] protected float punchElasticity = 1f;

        [Header("Time Scale")]
        [SerializeField] protected bool useUnscaledTime = true;

        protected CanvasGroup CanvasGroup;
        protected Tweener FadeTween;
        protected Tweener ScaleTween;
        protected RectTransform RectTransform;
        protected Vector3 InitialLocalScale;
        protected Vector2 InitialAnchoredPos;

        protected virtual void Awake()
        {
            InitializeComponents();
        }

        /// <summary>
        /// Initialize all required components
        /// Call this in Awake or before using SetActive
        /// </summary>
        protected void InitializeComponents()
        {
            if (!graphicHolder)
                graphicHolder = this.gameObject;
            
            CanvasGroup = graphicHolder.GetComponent<CanvasGroup>();
            if (!CanvasGroup)
                CanvasGroup = graphicHolder.AddComponent<CanvasGroup>();
            
            RectTransform = graphicHolder.GetComponent<RectTransform>();
            if (!RectTransform)
                RectTransform = graphicHolder.AddComponent<RectTransform>();
            
            InitialLocalScale = graphicHolder.transform.localScale;

            if (RectTransform)
                InitialAnchoredPos = RectTransform.anchoredPosition;
        }

        protected virtual void Start()
        {
            // SetActive(setActiveOnStart);
        }
        
        public virtual void SetActive(bool active, bool instant = true, bool toggleBackground = true)
        {
            if (!CanvasGroup || !graphicHolder)
            {
                InitializeComponents();
            }
            
            if (!CanvasGroup)
            {
                Debug.LogError($"[BaseUi] CanvasGroup is still null on '{gameObject.name}' after initialization attempt!", this);
                return;
            }

            if (FadeTween != null && FadeTween.IsActive()) FadeTween.Kill();
            if (ScaleTween != null && ScaleTween.IsActive()) ScaleTween.Kill();

            if (active)
            {
                // if(toggleBackground && backgroundObject is not null)
                //     backgroundObject.SetActive(true);
                
                if (graphicHolder != null)
                    graphicHolder.SetActive(true);
                
                CanvasGroup.interactable = true;
                CanvasGroup.blocksRaycasts = true;

                if (instant)
                {
                    CanvasGroup.alpha = 1;
                    if (RectTransform) RectTransform.anchoredPosition = InitialAnchoredPos;
                    return;
                }

                switch (enterAnimationType)
                {
                    case EnterAnimationType.None:
                        CanvasGroup.alpha = 1;
                        if (RectTransform) RectTransform.anchoredPosition = InitialAnchoredPos;
                        if (graphicHolder != null) graphicHolder.transform.localScale = Vector3.one;
                        break;

                    case EnterAnimationType.FadeScalePop:
                        CanvasGroup.alpha = 0;
                        if (graphicHolder != null)
                        {
                            graphicHolder.transform.localScale = Vector3.one * popScaleFrom;
                            ScaleTween = graphicHolder.transform.DOScale(1f, scaleDuration).SetEase(easeIn).SetUpdate(useUnscaledTime);
                        }
                        FadeTween = CanvasGroup.DOFade(1f, fadeDuration).SetUpdate(useUnscaledTime);
                        break;

                    case EnterAnimationType.Slide:
                        CanvasGroup.alpha = 1f;
                        if (RectTransform)
                        {
                            var from = InitialAnchoredPos + VsSlideOffSet(slideInFrom, slideDistance);
                            RectTransform.anchoredPosition = from;
                            FadeTween = RectTransform.DOAnchorPos(InitialAnchoredPos, fadeDuration).SetEase(easeIn).SetUpdate(useUnscaledTime);
                        }
                        break;

                    case EnterAnimationType.SlideOvershoot:
                        CanvasGroup.alpha = 1f;
                        if (RectTransform)
                        {
                            var from = InitialAnchoredPos + VsSlideOffSet(slideInFrom, slideDistance);
                            var past = InitialAnchoredPos - VsSlideOffSet(slideInFrom, overshootPx);
                            RectTransform.anchoredPosition = from;

                            var seq = DOTween.Sequence().SetUpdate(useUnscaledTime);
                            seq.Append(RectTransform.DOAnchorPos(past, fadeDuration).SetEase(slideEaseIn));
                            seq.Append(RectTransform.DOAnchorPos(InitialAnchoredPos, 0.15f).SetEase(Ease.OutSine));
                        }
                        break;

                    case EnterAnimationType.Bounce:
                        CanvasGroup.alpha = 0f;
                        if (graphicHolder != null)
                        {
                            graphicHolder.transform.localScale = Vector3.one * popScaleFrom;
                            FadeTween = CanvasGroup.DOFade(1f, fadeDuration).SetUpdate(useUnscaledTime);
                            ScaleTween = graphicHolder.transform.DOScale(1f, scaleDuration)
                                .SetEase(easeIn)
                                .SetUpdate(useUnscaledTime)
                                .OnComplete(() =>
                                {
                                    if (graphicHolder != null)
                                    {
                                        graphicHolder.transform.DOPunchScale(punchScale, punchDuration, punchVibrato, punchElasticity)
                                            .SetUpdate(useUnscaledTime);
                                    }
                                });
                        }
                        break;
                }
            }
            else
            {
                CanvasGroup.interactable = false;
                CanvasGroup.blocksRaycasts = false;

                if (instant)
                {
                    CanvasGroup.alpha = 0f;
                    if (RectTransform != null) RectTransform.anchoredPosition = InitialAnchoredPos;
                    if (graphicHolder != null)
                    {
                        graphicHolder.transform.localScale = InitialLocalScale;
                        graphicHolder.SetActive(false);
                    }
                    return;
                }

                switch (exitAnimationType)
                {
                    case ExitAnimationType.None:
                        CanvasGroup.alpha = 0f;
                        if (graphicHolder != null)
                            graphicHolder.SetActive(false);
                        break;

                    case ExitAnimationType.FadeScalePop:
                        FadeTween = CanvasGroup.DOFade(0f, fadeDuration).SetUpdate(useUnscaledTime);
                        if (graphicHolder != null)
                        {
                            ScaleTween = graphicHolder.transform.DOScale(popScaleFrom, scaleDuration)
                                .SetEase(easeOut)
                                .SetUpdate(useUnscaledTime)
                                .OnComplete(() => {
                                    if (graphicHolder != null)
                                        graphicHolder.SetActive(false);
                                });
                        }
                        break;

                    case ExitAnimationType.Slide:
                        if (RectTransform != null)
                        {
                            var to = InitialAnchoredPos + VsSlideOffSet(slideOutTo, slideDistance);
                            FadeTween = RectTransform.DOAnchorPos(to, fadeDuration)
                                .SetEase(easeOut)
                                .SetUpdate(useUnscaledTime)
                                .OnComplete(() =>
                                {
                                    if (RectTransform != null)
                                        RectTransform.anchoredPosition = InitialAnchoredPos;
                                    if (graphicHolder != null)
                                        graphicHolder.SetActive(false);
                                });
                        }
                        break;

                    case ExitAnimationType.SlideOvershoot:
                        if (RectTransform != null)
                        {
                            var toPast = InitialAnchoredPos + VsSlideOffSet(slideOutTo, overshootPx);
                            var toOff = InitialAnchoredPos + VsSlideOffSet(slideOutTo, slideDistance);

                            var seq = DOTween.Sequence().SetUpdate(useUnscaledTime);
                            seq.Append(RectTransform.DOAnchorPos(toPast, 0.12f).SetEase(Ease.InSine));
                            seq.Append(RectTransform.DOAnchorPos(toOff, fadeDuration).SetEase(slideEaseOut));
                            seq.OnComplete(() =>
                            {
                                if (RectTransform != null)
                                    RectTransform.anchoredPosition = InitialAnchoredPos;
                                if (graphicHolder != null)
                                    graphicHolder.SetActive(false);
                            });
                        }
                        break;

                    case ExitAnimationType.Bounce:
                        if (graphicHolder != null)
                        {
                            var seqBounce = DOTween.Sequence().SetUpdate(useUnscaledTime);
                            seqBounce.Append(graphicHolder.transform
                                .DOPunchScale(-punchScale * 0.6f, 0.15f, punchVibrato / 2, punchElasticity));
                            seqBounce.Join(CanvasGroup.DOFade(0f, fadeDuration));
                            seqBounce.OnComplete(() =>
                            {
                                if (graphicHolder != null)
                                {
                                    graphicHolder.SetActive(false);
                                    graphicHolder.transform.localScale = Vector3.one;
                                }
                            });
                        }
                        break;
                }
            }
        }

        protected Vector2 VsSlideOffSet(SlideDirection direction, float dist)
        {
            switch (direction)
            {
                case SlideDirection.Left: return new Vector2(-Mathf.Abs(dist), 0);
                case SlideDirection.Right: return new Vector2(Mathf.Abs(dist), 0);
                case SlideDirection.Top: return new Vector2(0, Mathf.Abs(dist));
                case SlideDirection.Bottom: return new Vector2(0, -Mathf.Abs(dist));
                default: return Vector2.zero;
            }
        }

        protected virtual void OnEnable() { }
        protected virtual void OnDisable() { }
    }
}