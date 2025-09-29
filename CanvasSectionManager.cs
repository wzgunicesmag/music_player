using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

// Implementamos las interfaces de arrastre directamente
public class SwipeSectionManager : MonoBehaviour, IBeginDragHandler, IEndDragHandler
{
    [System.Serializable]
    public class Section
    {
        public string sectionName;
        public RectTransform sectionTransform;
        public MusicPlayerController musicPlayerController;
        public MonoBehaviour[] elementsToAnimate; // Componentes que se animarán al entrar a esta sección
    }
    
    [Header("Configuración General")]
    public List<Section> sections = new List<Section>();
    public TracklistController tracklistController;
    
    [Header("Navegación")]
    public ScrollRect scrollRect;
    public float swipeThreshold = 0.05f; // Umbral para detectar un swipe
    public float snapSpeed = 10f; // Velocidad de ajuste al centro
    public float snapPrecision = 0.001f; // Precisión para el ajuste al centro
    
    [Header("Efectos Visuales")]
    public bool scaleEffect = true;
    public float inactiveScale = 0.85f; // Escala para secciones no activas
    public float activeScale = 1.0f; // Escala para sección activa
    
    // Variables privadas
    private int currentSectionIndex = 0;
    private bool isDragging = false;
    private bool isSnapping = false;
    private Vector2 startDragPosition;
    private Vector2 endDragPosition;
    
    private void Start()
    {
        // Buscar TracklistController si no está asignado
        if (tracklistController == null)
        {
            tracklistController = FindAnyObjectByType<TracklistController>();
        }
        
        // Configurar el ScrollRect si no está asignado
        if (scrollRect == null)
        {
            scrollRect = GetComponent<ScrollRect>();
            if (scrollRect == null)
            {
                scrollRect = gameObject.AddComponent<ScrollRect>();
            }
        }
        
        // Configurar el ScrollRect
        scrollRect.horizontal = true;
        scrollRect.vertical = false;
        scrollRect.inertia = true;
        scrollRect.decelerationRate = 0.1f; // Desaceleración alta para mejor snap
        
        // NO necesitamos suscribirnos a eventos de arrastre
        // La clase ya implementa IBeginDragHandler y IEndDragHandler
        
        // Posicionar todas las secciones inicialmente
        OrganizeSections();
        
        // Ir a la primera sección sin animación
        GoToSection(0, false);
        
        // Asignar el TracklistController a cada MusicPlayerController
        foreach (var section in sections)
        {
            if (section.musicPlayerController != null && tracklistController != null)
            {
                section.musicPlayerController.tracklistController = tracklistController;
            }
        }
    }
    
    // Implementación de IBeginDragHandler
    public void OnBeginDrag(PointerEventData eventData)
    {
        isDragging = true;
        isSnapping = false;
        startDragPosition = eventData.position;
        StopAllCoroutines();
    }
    
    // Implementación de IEndDragHandler
    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;
        endDragPosition = eventData.position;
        
        // Detectar la dirección del swipe
        float dragDelta = endDragPosition.x - startDragPosition.x;
        
        if (Mathf.Abs(dragDelta) > Screen.width * swipeThreshold)
        {
            // Si el arrastre fue significativo, ir a la siguiente o anterior sección
            if (dragDelta > 0)
            {
                // Swipe hacia la derecha = sección anterior
                // No permitir ir más allá de la primera sección
                if (currentSectionIndex > 0)
                {
                    GoToSection(currentSectionIndex - 1);
                }
                else
                {
                    // Volver a la sección actual si estamos en la primera
                    GoToSection(0);
                }
            }
            else
            {
                // Swipe hacia la izquierda = siguiente sección
                // No permitir ir más allá de la última sección
                if (currentSectionIndex < sections.Count - 1)
                {
                    GoToSection(currentSectionIndex + 1);
                }
                else
                {
                    // Volver a la sección actual si estamos en la última
                    GoToSection(sections.Count - 1);
                }
            }
        }
        else
        {
            // Si el arrastre no fue significativo, volver a la sección actual
            SnapToNearestSection();
        }
    }
    
    private void Update()
    {
        // Si no estamos arrastrando ni haciendo snap, comprobar si necesitamos ajustar la posición
        if (!isDragging && !isSnapping)
        {
            // Comprobar si el contenido está casi detenido
            if (Mathf.Abs(scrollRect.velocity.x) < 50f)
            {
                SnapToNearestSection();
            }
        }
        
        // Actualizar efectos visuales basados en la posición
        UpdateVisualEffects();
    }
    
    /// <summary>
    /// Organiza las secciones horizontalmente una al lado de otra
    /// </summary>
    private void OrganizeSections()
    {
        if (scrollRect.content == null || sections.Count == 0)
            return;
            
        // Configurar el ancho del contenido para incluir todas las secciones
        float totalWidth = 0;
        float maxHeight = 0;
        
        // Primero determinamos el ancho total necesario
        for (int i = 0; i < sections.Count; i++)
        {
            if (sections[i].sectionTransform != null)
            {
                totalWidth += sections[i].sectionTransform.rect.width;
                maxHeight = Mathf.Max(maxHeight, sections[i].sectionTransform.rect.height);
            }
        }
        
        // Configurar el tamaño del contenido
        scrollRect.content.sizeDelta = new Vector2(totalWidth, maxHeight);
        
        // Posicionar cada sección una al lado de la otra
        float currentX = 0;
        for (int i = 0; i < sections.Count; i++)
        {
            if (sections[i].sectionTransform != null)
            {
                // Posicionar la sección
                sections[i].sectionTransform.anchorMin = new Vector2(0, 0.5f);
                sections[i].sectionTransform.anchorMax = new Vector2(0, 0.5f);
                sections[i].sectionTransform.pivot = new Vector2(0, 0.5f);
                sections[i].sectionTransform.anchoredPosition = new Vector2(currentX, 0);
                
                // Avanzar la posición X para la siguiente sección
                currentX += sections[i].sectionTransform.rect.width;
                
                // Inicialmente todas las secciones están a escala inactiva
                if (scaleEffect)
                {
                    sections[i].sectionTransform.localScale = new Vector3(inactiveScale, inactiveScale, 1f);
                }
            }
        }
        
        // Asegurar que el contenido comienza en la posición correcta
        scrollRect.horizontalNormalizedPosition = 0;
    }
    
    /// <summary>
    /// Navega a una sección específica
    /// </summary>
    public void GoToSection(int sectionIndex, bool animate = true)
    {
        if (sectionIndex < 0 || sectionIndex >= sections.Count || scrollRect.content == null)
            return;
            
        currentSectionIndex = sectionIndex;
        
        // Detener cualquier animación de snap en progreso
        isSnapping = false;
        StopAllCoroutines();
        
        if (animate)
        {
            isSnapping = true;
            StartCoroutine(AnimateToSection(sectionIndex));
        }
        else
        {
            // Calcular la posición normalizada para centrarse en la sección
            float normalizedPos = CalculateNormalizedPositionForSection(sectionIndex);
            scrollRect.horizontalNormalizedPosition = normalizedPos;
            
            // Actualizar escalas y activar animaciones inmediatamente
            UpdateVisualEffects();
            ActivateSectionAnimations(sectionIndex);
        }
    }
    
    /// <summary>
    /// Navega a la siguiente sección
    /// </summary>
    public void GoToNextSection()
    {
        // No ir más allá de la última sección
        if (currentSectionIndex < sections.Count - 1)
        {
            GoToSection(currentSectionIndex + 1);
        }
    }
    
    /// <summary>
    /// Navega a la sección anterior
    /// </summary>
    public void GoToPreviousSection()
    {
        // No ir más allá de la primera sección
        if (currentSectionIndex > 0)
        {
            GoToSection(currentSectionIndex - 1);
        }
    }
    
    /// <summary>
    /// Calcula la posición normalizada para centrar una sección específica
    /// </summary>
    private float CalculateNormalizedPositionForSection(int sectionIndex)
    {
        if (sectionIndex < 0 || sectionIndex >= sections.Count || scrollRect.content == null)
            return 0;
            
        // Calcular la posición X de la sección
        float sectionPosX = sections[sectionIndex].sectionTransform.anchoredPosition.x;
        
        // Calcular el ancho visible del viewport
        float viewportWidth = ((RectTransform)scrollRect.viewport.transform).rect.width;
        
        // Ajustar para centrar la sección, teniendo en cuenta el ancho de la sección
        sectionPosX += (sections[sectionIndex].sectionTransform.rect.width / 2) - (viewportWidth / 2);
        
        // Calcular la posición normalizada (0-1)
        float contentWidth = scrollRect.content.rect.width - viewportWidth;
        if (contentWidth <= 0)
            return 0;
            
        float normalizedPos = Mathf.Clamp01(sectionPosX / contentWidth);
        return normalizedPos;
    }
    
    /// <summary>
    /// Detecta cuál es la sección más cercana a la vista actual
    /// </summary>
    private int GetNearestSectionIndex()
    {
        float normalizedPos = scrollRect.horizontalNormalizedPosition;
        float minDistance = float.MaxValue;
        int nearestIndex = 0;
        
        for (int i = 0; i < sections.Count; i++)
        {
            float sectionPos = CalculateNormalizedPositionForSection(i);
            float distance = Mathf.Abs(normalizedPos - sectionPos);
            
            if (distance < minDistance)
            {
                minDistance = distance;
                nearestIndex = i;
            }
        }
        
        return nearestIndex;
    }
    
    /// <summary>
    /// Ajusta automáticamente el scroll a la sección más cercana
    /// </summary>
    private void SnapToNearestSection()
    {
        int nearestIndex = GetNearestSectionIndex();
        
        if (nearestIndex != currentSectionIndex || !isSnapping)
        {
            currentSectionIndex = nearestIndex;
            isSnapping = true;
            StopAllCoroutines();
            StartCoroutine(AnimateToSection(nearestIndex));
        }
    }
    
    /// <summary>
    /// Anima el desplazamiento hacia una sección específica
    /// </summary>
    private IEnumerator AnimateToSection(int sectionIndex)
    {
        float startPos = scrollRect.horizontalNormalizedPosition;
        float targetPos = CalculateNormalizedPositionForSection(sectionIndex);
        float time = 0;
        
        // Desactivar inertia durante el snap para evitar interferencias
        bool originalInertia = scrollRect.inertia;
        scrollRect.inertia = false;
        scrollRect.velocity = Vector2.zero;
        
        while (Mathf.Abs(scrollRect.horizontalNormalizedPosition - targetPos) > snapPrecision)
        {
            time += Time.deltaTime * snapSpeed;
            scrollRect.horizontalNormalizedPosition = Mathf.Lerp(startPos, targetPos, Mathf.SmoothStep(0, 1, time));
            yield return null;
        }
        
        // Asegurar que llegamos exactamente a la posición objetivo
        scrollRect.horizontalNormalizedPosition = targetPos;
        
        // Restaurar inertia
        scrollRect.inertia = originalInertia;
        isSnapping = false;
        
        // Pequeño retraso antes de activar las animaciones
        yield return new WaitForSeconds(0.2f);
        
        // Activar las animaciones de los elementos de esta sección
        ActivateSectionAnimations(sectionIndex);
    }
    
    /// <summary>
    /// Activa las animaciones de los elementos de una sección
    /// </summary>
    private void ActivateSectionAnimations(int sectionIndex)
    {
        if (sectionIndex < 0 || sectionIndex >= sections.Count)
            return;
            
        Section section = sections[sectionIndex];
        
        // Animar elementos si hay configurados
        if (section.elementsToAnimate != null && section.elementsToAnimate.Length > 0)
        {
            foreach (var element in section.elementsToAnimate)
            {
                if (element == null) continue;
                
                // Si es Auto_Animator, llamar a su método PlayAnimation
                if (element is Auto_Animator autoAnimator)
                {
                    autoAnimator.PlayAnimation();
                    continue;
                }
                
                // Para otros componentes, no usar SetActive (causa desaparición)
                var elementGO = element.gameObject;
                
                // Intentar llamar a un método genérico de reinicio de animación
                element.SendMessage("RestartAnimation", SendMessageOptions.DontRequireReceiver);
                
                // Si tiene un Animator, reiniciarlo de forma más segura
                Animator animator = elementGO.GetComponent<Animator>();
                if (animator != null)
                {
                    animator.Rebind();
                    animator.Update(0f);
                }
            }
        }
        
        // Forzar actualización del UI
        if (section.musicPlayerController != null && tracklistController != null)
        {
            section.musicPlayerController.ForceUpdateUI();
        }
        
        // Actualizar colores de botones
        if (tracklistController != null)
        {
            tracklistController.UpdateTrackButtonsColors();
            tracklistController.ForceUIUpdate();
        }
    }
    
    /// <summary>
    /// Actualiza los efectos visuales basados en la posición actual
    /// </summary>
    private void UpdateVisualEffects()
    {
        if (!scaleEffect) return;
        
        float scrollPos = scrollRect.horizontalNormalizedPosition;
        
        for (int i = 0; i < sections.Count; i++)
        {
            if (sections[i].sectionTransform == null) continue;
            
            float sectionPos = CalculateNormalizedPositionForSection(i);
            float distance = Mathf.Abs(scrollPos - sectionPos);
            
            // Normalizar la distancia (0 = centrado, 1 = muy lejos)
            distance = Mathf.Clamp01(distance * 3); // Multiplicar por 3 para acelerar el cambio de escala
            
            // Calcular la escala basada en la distancia
            float scale = Mathf.Lerp(activeScale, inactiveScale, distance);
            
            // Aplicar la escala
            sections[i].sectionTransform.localScale = new Vector3(scale, scale, 1);
        }
    }
}