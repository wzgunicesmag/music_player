using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(CanvasGroup))]
public class Auto_Animator : MonoBehaviour
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

    [Header("Configuración de Animación")]
    public AnimationType animationType = AnimationType.Fade;
    public float duration = 0.5f;
    public float delay = 0f;
    public bool animateOnStart = true;
    
    [Header("Configuración de Deslizamiento")]
    public Vector3 slideOffset = new Vector3(0, -500f, 0);
    public float overshootAmount = 0.15f; // Qué tanto se pasa del punto final (15% por defecto)
    public float overshootPoint = 0.7f; // En qué punto de la animación ocurre el overshoot (0-1)
    
    [Header("Configuración de Rotación")]
    public Vector3 rotationAxis = new Vector3(0, 1, 0);
    public float rotationDegrees = 90f;

    private CanvasGroup canvasGroup;
    private Vector3 originalScale;
    private Vector3 originalPos;
    private Quaternion originalRotation;
    private bool animationComplete = false;

    void Awake()
    {
        // Obtener o agregar CanvasGroup
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        // Guardar valores originales
        originalScale = transform.localScale;
        originalPos = transform.localPosition;
        originalRotation = transform.localRotation;
    }

    void Start()
    {
        if (animateOnStart)
        {
            // Configurar el estado inicial oculto
            SetInitialState();
            
            // Iniciar la animación después del delay
            StartCoroutine(DelayedAnimate());
        }
    }

    private IEnumerator DelayedAnimate()
    {
        // Esperar el tiempo de delay
        if (delay > 0)
            yield return new WaitForSeconds(delay);
            
        // Iniciar la animación
        StartCoroutine(Animate());
    }
    
    public void PlayAnimation()
    {
        if (!animationComplete)
        {
            StopAllCoroutines();
            SetInitialState();
            
            // Modificar para usar el DelayedAnimate y respetar el delay configurado
            if (delay > 0)
                StartCoroutine(DelayedAnimate());
            else
                StartCoroutine(Animate());
        }
    }
    
    // Hacer público el método para reiniciar la animación desde fuera
    public void RestartAnimation()
    {
        // Resetear la bandera que controla si la animación se ha completado
        animationComplete = false;
        
        // Llamar a PlayAnimation para iniciar de nuevo la animación
        PlayAnimation();
    }

    private void SetInitialState()
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
                transform.localScale = Vector3.zero;
                break;
            case AnimationType.Slide:
                transform.localPosition = originalPos + slideOffset;
                break;
            case AnimationType.Rotate:
            case AnimationType.Flip:
                transform.localRotation = originalRotation * Quaternion.Euler(rotationAxis * rotationDegrees);
                break;
            case AnimationType.Swing:
                transform.localRotation = originalRotation * Quaternion.Euler(rotationAxis * (rotationDegrees / 2));
                break;
            case AnimationType.Zoom:
                transform.localScale = Vector3.zero;
                canvasGroup.alpha = 0f;
                break;
        }
    }

    private IEnumerator Animate()
    {
        float timer = 0f;
        
        while (timer < duration)
        {
            float normalizedTime = timer / duration;
            float easedTime = GetEasedTime(normalizedTime);
            
            ApplyAnimation(easedTime);
            
            timer += Time.deltaTime;
            yield return null;
        }

        // Asegurar estado final
        ResetToOriginalState();
        animationComplete = true;
    }

    private float GetEasedTime(float t)
    {
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

        return easedTime;
    }

    private void ResetToOriginalState()
    {
        canvasGroup.alpha = 1f;
        transform.localScale = originalScale;
        transform.localPosition = originalPos;
        transform.localRotation = originalRotation;
    }

    private void ApplyAnimation(float progress)
    {
        switch (animationType)
        {
            case AnimationType.Fade:
                canvasGroup.alpha = progress;
                break;
                
            case AnimationType.Bounce:
                transform.localScale = originalScale * progress;
                break;
                
            case AnimationType.Elastic:
                transform.localScale = originalScale * progress;
                break;
                
            case AnimationType.Slide:
                // Aplicar efecto de sobrepaso al movimiento
                float overshootProgress = SlideEaseWithOvershoot(progress);
                
                // Calcular posición con sobrepaso
                Vector3 targetPos = Vector3.Lerp(
                    originalPos + slideOffset,
                    originalPos,
                    overshootProgress
                );
                
                transform.localPosition = targetPos;
                
                // Agregar efecto de fade durante el deslizamiento
                canvasGroup.alpha = progress;
                break;
                
            case AnimationType.Scale:
                transform.localScale = originalScale * progress;
                break;
                
            case AnimationType.Rotate:
                float angle = rotationDegrees * (1 - progress);
                transform.localRotation = originalRotation * Quaternion.Euler(rotationAxis * angle);
                break;
                
            case AnimationType.Flip:
                float flipAngle = rotationDegrees * (1 - progress);
                transform.localRotation = originalRotation * Quaternion.Euler(Vector3.up * flipAngle);
                // Escalar para crear efecto de perspectiva
                float scaleProgress = Mathf.Abs(Mathf.Cos(flipAngle * Mathf.Deg2Rad));
                transform.localScale = new Vector3(originalScale.x * scaleProgress, originalScale.y, originalScale.z);
                break;
                
            case AnimationType.Swing:
                float swingAngle = (rotationDegrees/2) * Mathf.Sin(progress * Mathf.PI);
                transform.localRotation = originalRotation * Quaternion.Euler(rotationAxis * swingAngle);
                break;
                
            case AnimationType.Zoom:
                transform.localScale = originalScale * progress;
                canvasGroup.alpha = progress;
                break;
                
            case AnimationType.Pop:
                // Pop tiene un pequeño sobrepaso incorporado
                float scaleMultiplier = progress < 0.8f ? 
                    progress / 0.8f : 
                    1.0f + 0.1f * (1.0f - (progress - 0.8f) / 0.2f);
                transform.localScale = originalScale * scaleMultiplier;
                break;
        }
    }

    // Funciones de suavizado
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
        // Inicio rápido, luego finalización más lenta con un pequeño rebote
        return t < 0.5f ? 4 * t * t * t : 1 - Mathf.Pow(-2 * t + 2, 3) / 2;
    }

    // Función de sobrepaso más configurable
    float SlideEaseWithOvershoot(float t)
    {
        // Aceleración rápida hasta que alcanzamos el punto de sobrepaso
        if (t < overshootPoint) {
            // Acelerar más rápido que lineal para ganar impulso
            return Mathf.Pow(t / overshootPoint, 0.7f); // Pequeño ease-in para un inicio más suave
        } else {
            // Calcular tiempo normalizado en la fase de sobrepaso
            float phase = (t - overshootPoint) / (1f - overshootPoint);
            
            // Efecto elástico de doble rebote con decaimiento natural
            // Mayor frecuencia (18f) para múltiples rebotes visibles
            // Menor coeficiente de amortiguación (3f) permite que los rebotes sean más visibles antes de decaer
            return 1.0f + overshootAmount * Mathf.Exp(-phase * 3f) * Mathf.Cos(phase * 18f);
        }
    }
}