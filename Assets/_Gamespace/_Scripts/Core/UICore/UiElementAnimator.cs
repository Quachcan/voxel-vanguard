using DG.Tweening;
using UnityEngine;

namespace _Workspace._Scripts.Core.UICore
{
    public class UiElementAnimator : MonoBehaviour
    {
        public enum AnimationType
        {
            None,
            FadeIn,
            ScalePop,
            SlideFromLeft,
            SlideFromRight,
            SlideFromTop,
            SlideFromBottom,
            Bounce,
            Rotate,
            FloatY
        }
        
        [Header("Animation Settings")]
        [SerializeField] private AnimationType animationType = AnimationType.ScalePop;
        [SerializeField] private float delay = 0f;
        [SerializeField] private float duration = 0.5f;
        [SerializeField] private Ease easeType = Ease.OutBack;
        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private bool loop = false;
        [SerializeField] private LoopType loopType = LoopType.Restart;
        
        [Header("Animation Properties")]
        [SerializeField] private float slideDistance = 200f;
        [SerializeField] private float floatDistance = 20f;
        [SerializeField] private float startScale = 0f;
        [SerializeField] private float targetScale = 1f;
        [SerializeField] private float rotationAmount = 360f;
        [SerializeField] private Vector3 punchScale = new Vector3(0.1f, 0.1f, 0.1f);
        
        [Header("References")]
        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private CanvasGroup canvasGroup;
        
        private Vector2 originalPosition;
        private Vector3 originalScale;
        private Tween currentTween;
        
        private void Awake()
        {
            if (rectTransform == null)
                rectTransform = GetComponent<RectTransform>();
                
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
                
            if (rectTransform != null)
            {
                originalPosition = rectTransform.anchoredPosition;
                originalScale = rectTransform.localScale;
            }
        }
        
        private void OnEnable()
        {
            if (playOnEnable)
            {
                PlayAnimation();
            }
        }
        
        private void OnDisable()
        {
            KillTween();
        }
        
        public void PlayAnimation()
        {
            KillTween();
            
            switch (animationType)
            {
                case AnimationType.None:
                    break;
                case AnimationType.FadeIn:
                    AnimateFadeIn();
                    break;
                case AnimationType.ScalePop:
                    AnimateScalePop();
                    break;
                case AnimationType.SlideFromLeft:
                    AnimateSlide(Vector2.left);
                    break;
                case AnimationType.SlideFromRight:
                    AnimateSlide(Vector2.right);
                    break;
                case AnimationType.SlideFromTop:
                    AnimateSlide(Vector2.up);
                    break;
                case AnimationType.SlideFromBottom:
                    AnimateSlide(Vector2.down);
                    break;
                case AnimationType.Bounce:
                    AnimateBounce();
                    break;
                case AnimationType.Rotate:
                    AnimateRotate();
                    break;
                case AnimationType.FloatY:
                    AnimateFloatY();
                    break;
            }
        }
        
        private void AnimateFadeIn()
        {
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            
            canvasGroup.alpha = 0f;
            currentTween = canvasGroup.DOFade(1f, duration)
                .SetDelay(delay)
                .SetEase(easeType);
                
            if (loop)
                currentTween.SetLoops(-1, loopType);
        }
        
        private void AnimateScalePop()
        {
            if (rectTransform == null) return;
            
            rectTransform.localScale = Vector3.one * startScale;
            currentTween = rectTransform.DOScale(targetScale, duration)
                .SetDelay(delay)
                .SetEase(easeType);
                
            if (loop)
                currentTween.SetLoops(-1, loopType);
        }
        
        private void AnimateSlide(Vector2 direction)
        {
            if (rectTransform == null) return;
            
            Vector2 startPos = originalPosition + direction * slideDistance;
            rectTransform.anchoredPosition = startPos;
            
            currentTween = rectTransform.DOAnchorPos(originalPosition, duration)
                .SetDelay(delay)
                .SetEase(easeType);
                
            if (loop)
                currentTween.SetLoops(-1, loopType);
        }
        
        private void AnimateBounce()
        {
            if (rectTransform == null) return;
            
            rectTransform.localScale = originalScale;
            
            Sequence sequence = DOTween.Sequence();
            sequence.SetDelay(delay);
            sequence.Append(rectTransform.DOPunchScale(punchScale, duration, 10, 1f));
            
            currentTween = sequence;
            
            if (loop)
                currentTween.SetLoops(-1, loopType);
        }
        
        private void AnimateRotate()
        {
            if (rectTransform == null) return;
            
            currentTween = rectTransform.DORotate(new Vector3(0, 0, rotationAmount), duration, RotateMode.FastBeyond360)
                .SetDelay(delay)
                .SetEase(easeType);
                
            if (loop)
                currentTween.SetLoops(-1, loopType);
        }
        
        private void AnimateFloatY()
        {
            if (rectTransform == null) return;
            
            Vector2 startPos = originalPosition;
            Vector2 targetPos = originalPosition + Vector2.up * floatDistance;
            
            rectTransform.anchoredPosition = startPos;
            
            Sequence sequence = DOTween.Sequence();
            sequence.SetDelay(delay);
            sequence.Append(rectTransform.DOAnchorPos(targetPos, duration).SetEase(easeType));
            sequence.Append(rectTransform.DOAnchorPos(startPos, duration).SetEase(easeType));
            
            currentTween = sequence;
            
            if (loop)
                currentTween.SetLoops(-1, loopType);
        }
        
        public void ResetToOriginal()
        {
            KillTween();
            
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = originalPosition;
                rectTransform.localScale = originalScale;
                rectTransform.rotation = Quaternion.identity;
            }
            
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }
        }
        
        private void KillTween()
        {
            if (currentTween != null && currentTween.IsActive())
            {
                currentTween.Kill();
            }
        }
        
        private void OnDestroy()
        {
            KillTween();
        }
    }
}