using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(CanvasGroup))]
public class PopupPanelAnimator : MonoBehaviour
{
    public enum AnimationType
    {
        Fade,       // Fade in/out
        Bounce,     // Bounce effect
        Elastic,    // Springy motion
        Slide,      // Slide from position
        Scale,      // Simple scale
        Rotate,     // Rotation effect
        Flip,       // 3D flip
        Swing,      // Pendulum motion
        Zoom,       // Zoom from center with fade
        Pop         // Quick pop effect
    }

    [Header("Panel Configuration")]
    public GameObject panel;

    [Header("Enter Animation")]
    public AnimationType inAnimation = AnimationType.Fade;
    public float inDuration = 0.5f;
    
    [Header("Exit Animation")]
    public AnimationType outAnimation = AnimationType.Fade;
    public float outDuration = 0.5f;

    [Header("Slide Settings")]
    public Vector3 slideOffset = new Vector3(0, -500f, 0);
    public float overshootAmount = 0.15f; // Qué tanto se pasa del punto final (15% por defecto)
    public float overshootPoint = 0.7f; // En qué punto de la animación ocurre el overshoot (0-1)
    
    [Header("Rotation Settings")]
    public Vector3 rotationAxis = new Vector3(0, 1, 0);
    public float rotationDegrees = 90f;

    private CanvasGroup canvasGroup;
    private Vector3 originalScale;
    private Vector3 originalPos;
    private Quaternion originalRotation;

    void Awake()
    {
        if (panel == null)
        {
            Debug.LogError("PopupPanelAnimator: Panel not assigned.");
            enabled = false;
            return;
        }

        // Get or add CanvasGroup
        canvasGroup = panel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = panel.AddComponent<CanvasGroup>();
        }

        originalScale = panel.transform.localScale;
        originalPos = panel.transform.localPosition;
        originalRotation = panel.transform.localRotation;
        panel.SetActive(false);
    }

    public void ShowPanel()
    {
        if (!enabled) return;
        StopAllCoroutines();
        panel.SetActive(true);
        StartCoroutine(AnimateIn());
    }

    public void HidePanel()
    {
        if (!enabled) return;
        StopAllCoroutines();
        StartCoroutine(AnimateOut());
    }

    private IEnumerator AnimateIn()
    {
        // Reset to initial state
        SetInitialStateForAnimation(inAnimation, true);
        
        float timer = 0f;
        while (timer < inDuration)
        {
            float normalizedTime = timer / inDuration;
            float easedTime = GetEasedTime(normalizedTime, inAnimation, true);
            
            ApplyAnimation(inAnimation, easedTime, true);
            
            timer += Time.deltaTime;
            yield return null;
        }

        // Ensure final state
        ResetToOriginalState();
    }

    private IEnumerator AnimateOut()
    {
        float timer = 0f;
        
        while (timer < outDuration)
        {
            float normalizedTime = timer / outDuration;
            float easedTime = GetEasedTime(normalizedTime, outAnimation, false);
            
            ApplyAnimation(outAnimation, easedTime, false);
            
            timer += Time.deltaTime;
            yield return null;
        }

        // Final state for out animation
        SetFinalStateForAnimation(outAnimation);
        panel.SetActive(false);
        
        // Reset for next time
        ResetToOriginalState();
    }

    private void SetInitialStateForAnimation(AnimationType animationType, bool isEntering)
    {
        // Reset to original state first
        panel.transform.localPosition = originalPos;
        panel.transform.localScale = originalScale;
        panel.transform.localRotation = originalRotation;
        canvasGroup.alpha = 1f;

        if (isEntering)
        {
            switch (animationType)
            {
                case AnimationType.Fade:
                    canvasGroup.alpha = 0f;
                    break;
                case AnimationType.Bounce:
                case AnimationType.Elastic:
                case AnimationType.Scale:
                case AnimationType.Pop:
                    panel.transform.localScale = Vector3.zero;
                    break;
                case AnimationType.Slide:
                    panel.transform.localPosition = originalPos + slideOffset;
                    break;
                case AnimationType.Rotate:
                case AnimationType.Flip:
                    panel.transform.localRotation = originalRotation * Quaternion.Euler(rotationAxis * rotationDegrees);
                    break;
                case AnimationType.Swing:
                    panel.transform.localRotation = originalRotation * Quaternion.Euler(rotationAxis * (rotationDegrees / 2));
                    break;
                case AnimationType.Zoom:
                    panel.transform.localScale = Vector3.zero;
                    canvasGroup.alpha = 0f;
                    break;
            }
        }
    }

    private void SetFinalStateForAnimation(AnimationType animationType)
    {
        switch (animationType)
        {
            case AnimationType.Fade:
                canvasGroup.alpha = 0f;
                break;
            case AnimationType.Bounce:
            case AnimationType.Elastic:
            case AnimationType.Scale:
            case AnimationType.Pop:
                panel.transform.localScale = Vector3.zero;
                break;
            case AnimationType.Slide:
                panel.transform.localPosition = originalPos + slideOffset;
                break;
            case AnimationType.Rotate:
            case AnimationType.Flip:
                panel.transform.localRotation = originalRotation * Quaternion.Euler(rotationAxis * rotationDegrees);
                break;
            case AnimationType.Swing:
                panel.transform.localRotation = originalRotation * Quaternion.Euler(rotationAxis * (rotationDegrees / 2));
                break;
            case AnimationType.Zoom:
                panel.transform.localScale = Vector3.zero;
                canvasGroup.alpha = 0f;
                break;
        }
    }

    private void ResetToOriginalState()
    {
        canvasGroup.alpha = 1f;
        panel.transform.localScale = originalScale;
        panel.transform.localPosition = originalPos;
        panel.transform.localRotation = originalRotation;
    }

    private float GetEasedTime(float t, AnimationType animationType, bool isEntering)
    {
        if (!isEntering)
        {
            // For exit animations, we often reverse the easing
            t = 1 - t;
        }

        float easedTime;
        switch (animationType)
        {
            case AnimationType.Bounce:
                easedTime = BounceEaseOut(t);
                break;
            case AnimationType.Elastic:
                easedTime = ElasticEaseOut(t);
                break;
            case AnimationType.Swing:
                easedTime = SwingEaseOut(t);
                break;
            case AnimationType.Pop:
                easedTime = PopEaseOut(t);
                break;
            case AnimationType.Flip:
            case AnimationType.Rotate:
                easedTime = QuadEaseOut(t);
                break;
            default:
                easedTime = t;  // Linear
                break;
        }

        if (!isEntering)
        {
            // Convert back to exit progress
            easedTime = 1 - easedTime;
        }

        return easedTime;
    }

    private void ApplyAnimation(AnimationType animationType, float progress, bool isEntering)
    {
        switch (animationType)
        {
            case AnimationType.Fade:
                canvasGroup.alpha = isEntering ? progress : 1 - progress;
                break;
                
            case AnimationType.Bounce:
                panel.transform.localScale = originalScale * (isEntering ? progress : 1 - progress);
                break;
                
            case AnimationType.Elastic:
                panel.transform.localScale = originalScale * (isEntering ? progress : 1 - progress);
                break;
                
            case AnimationType.Slide:
                // Apply overshoot effect to the slide motion
                float overshootProgress = SlideEaseWithOvershoot(isEntering ? progress : 1 - progress);
                
                // Calculate position with overshoot
                Vector3 targetPos = Vector3.Lerp(
                    originalPos + slideOffset,
                    originalPos,
                    overshootProgress
                );
                
                panel.transform.localPosition = targetPos;
                
                // Add fade effect during slide
                canvasGroup.alpha = isEntering ? progress : 1 - progress;
                break;
                
            case AnimationType.Scale:
                panel.transform.localScale = originalScale * (isEntering ? progress : 1 - progress);
                break;
                
            case AnimationType.Rotate:
                float angle = rotationDegrees * (isEntering ? 1 - progress : progress);
                panel.transform.localRotation = originalRotation * Quaternion.Euler(rotationAxis * angle);
                break;
                
            case AnimationType.Flip:
                float flipAngle = rotationDegrees * (isEntering ? 1 - progress : progress);
                panel.transform.localRotation = originalRotation * Quaternion.Euler(Vector3.up * flipAngle);
                // Scale to create perspective effect
                float scaleProgress = Mathf.Abs(Mathf.Cos(flipAngle * Mathf.Deg2Rad));
                panel.transform.localScale = new Vector3(originalScale.x * scaleProgress, originalScale.y, originalScale.z);
                break;
                
            case AnimationType.Swing:
                float swingAngle = (rotationDegrees/2) * Mathf.Sin(progress * Mathf.PI * (isEntering ? 1 : 1));
                if (!isEntering) swingAngle *= -1;
                panel.transform.localRotation = originalRotation * Quaternion.Euler(rotationAxis * swingAngle);
                break;
                
            case AnimationType.Zoom:
                panel.transform.localScale = originalScale * (isEntering ? progress : 1 - progress);
                canvasGroup.alpha = isEntering ? progress : 1 - progress;
                break;
                
            case AnimationType.Pop:
                // Pop has a small overshoot built in
                float scaleMultiplier = isEntering 
                    ? (progress < 0.8f ? progress / 0.8f : 1.0f + 0.1f * (1.0f - (progress - 0.8f) / 0.2f))
                    : (progress > 0.2f ? (1.0f - progress) / 0.8f : 1.0f + 0.1f * (1.0f - progress / 0.2f));
                panel.transform.localScale = originalScale * scaleMultiplier;
                break;
        }
    }

    // Easing functions
    float BounceEaseOut(float t)
    {
        if (t < 1/2.75f) return 7.5625f * t * t;
        else if (t < 2/2.75f) { t -= 1.5f/2.75f; return 7.5625f * t * t + 0.75f; }
        else if (t < 2.5f/2.75f) { t -= 2.25f/2.75f; return 7.5625f * t * t + 0.9375f; }
        else { t -= 2.625f/2.75f; return 7.5625f * t * t + 0.984375f; }
    }

    float ElasticEaseOut(float t)
    {
        if (t == 0f || t == 1f) return t;
        float p = 0.3f;
        return Mathf.Pow(2, -10 * t) * Mathf.Sin((t - p/4f) * (2f*Mathf.PI)/p) + 1f;
    }

    float QuadEaseOut(float t)
    {
        return -1 * t * (t - 2);
    }

    float SwingEaseOut(float t)
    {
        return 0.5f - 0.5f * Mathf.Cos(t * Mathf.PI);
    }

    float PopEaseOut(float t)
    {
        // Quick start, then slower finish with slight bounce
        return t < 0.5f ? 4 * t * t * t : 1 - Mathf.Pow(-2 * t + 2, 3) / 2;
    }

    // More configurable overshoot function
    float SlideEaseWithOvershoot(float t)
    {
        // Fast acceleration until we reach the overshoot point
        if (t < overshootPoint) {
            // Accelerate faster than linear to build momentum
            return Mathf.Pow(t / overshootPoint, 0.7f); // Slight ease-in for smoother start
        } else {
            // Calculate normalized time in the overshoot phase
            float phase = (t - overshootPoint) / (1f - overshootPoint);
            
            // Elastic double-bounce effect with natural decay
            // Higher frequency (18f) for multiple visible bounces
            // Lower damping coefficient (3f) allows bounces to be more visible before decaying
            return 1.0f + overshootAmount * Mathf.Exp(-phase * 3f) * Mathf.Cos(phase * 18f);
        }
    }
}