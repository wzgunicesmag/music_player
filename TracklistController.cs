using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // Añadir esta importación
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class Track
{
    public AudioClip clip;
    public string title;
    public string artist;
    public string album;
    [Tooltip("Botón opcional para seleccionar esta canción directamente")]
    public Button trackButton;
    [Tooltip("Imagen de portada/carátula para esta canción")]
    public Sprite coverImage;
}

public class TracklistController : MonoBehaviour
{
    [Header("Audio Sources (cross-fade)")]
    public AudioSource primarySource;
    public AudioSource secondarySource;
    public float crossfadeDuration = 0f;

    [Header("Fade al Play/Pause")]
    [Tooltip("0 = sin fundido, >0 = duración en segundos")]
    public float playPauseFadeDuration = 0f;

    [Header("Pistas")]
    public List<Track> tracks;
    private int currentIndex = 0;

    [Header("UI de Carátula")]
    [Tooltip("Componente Image donde se mostrará la carátula de la canción")]
    public Image coverImageDisplay;
    [Tooltip("Duración de la transición fade in/out de la carátula (en segundos)")]
    public float coverTransitionDuration = 0.5f;
    [Tooltip("Color de tinte para la imagen cuando no hay carátula")]
    public Color defaultCoverTint = Color.gray;

    [Header("Animación de Carátula")]
    [Tooltip("¿Animar la carátula cuando se está reproduciendo música?")]
    public bool animateCoverWhilePlaying = true;
    [Tooltip("Velocidad de la animación (valores más bajos = más lento)")]
    [Range(0.1f, 10f)] // Ya ampliado hasta 10
    public float coverAnimationSpeed = 0.5f;
    [Tooltip("Cantidad de movimiento aleatorio (valores más altos = más movimiento)")]
    [Range(0f, 20f)]
    public float coverAnimationMovementAmount = 5f;
    [Tooltip("Cantidad de escalado (valores más altos = más zoom)")]
    [Range(0f, 1f)] // Ampliado hasta 1 en lugar de 0.1
    public float coverAnimationScaleAmount = 0.03f;

    // Variables privadas para manejar la transición entre imágenes
    private Image secondaryCoverDisplay;
    private CanvasGroup primaryCoverCanvasGroup;
    private CanvasGroup secondaryCoverCanvasGroup;

    // Variables privadas para la animación
    private Vector3 coverOriginalScale;
    private Vector2 coverOriginalPosition;
    private Coroutine coverAnimationCoroutine;

    // Estado de reproducción
    private bool isPlaying = false;
    private bool isChangingTrack = false; // Agregar esta variable para evitar doble clic accidental
    
    // Eventos para notificar cambios a los MusicPlayerControllers
    public delegate void TrackChangedHandler(Track newTrack);
    public event TrackChangedHandler OnTrackChanged;
    
    public delegate void PlayStateChangedHandler(bool isPlaying);
    public event PlayStateChangedHandler OnPlayStateChanged;
    
    public delegate void TrackTimeChangedHandler(float currentTime, float totalTime);
    public event TrackTimeChangedHandler OnTrackTimeChanged;
    
    // Agregamos un evento específico para cambios de canción con animación
    public delegate void TrackChangeSequenceHandler(int previousIndex, int newIndex);
    public event TrackChangeSequenceHandler OnTrackChangeSequence;
    
    // Propiedades públicas
    public int CurrentTrackIndex => currentIndex;
    public bool IsPlaying => isPlaying;
    public Track CurrentTrack => tracks.Count > 0 ? tracks[currentIndex] : null;
    
    void Start()
    {
        primarySource.volume = 1f;
        secondarySource.volume = 0f;

        // Configuración para las carátulas
        SetupCoverImageDisplay();

        if (tracks.Count == 0) return;
        LoadTrack(currentIndex, primarySource);
        
        // Configurar botones para cada pista
        for (int i = 0; i < tracks.Count; i++)
        {
            if (tracks[i].trackButton != null)
            {
                int trackIndex = i; // Capturar el índice en una variable local
                tracks[i].trackButton.onClick.AddListener(() => PlaySpecificTrack(trackIndex));
            }
        }
        
        // Actualizar los colores de los botones inicialmente
        UpdateTrackButtonsColors();
    }
    
    void Update()
    {
        var src = primarySource;
        if (src.clip == null) return;

        float t = src.time;
        float L = src.clip.length;
        
        // Notificar a los controladores de UI sobre el tiempo actual
        OnTrackTimeChanged?.Invoke(t, L);

        if (!src.isPlaying && isPlaying && t >= L)
            PlayNext();
    }
    
    // API Pública
    public void Play()
    {
        if (isPlaying) return;
        
        if (playPauseFadeDuration > 0f)
        {
            primarySource.volume = 0f;
            primarySource.Play();
            StartCoroutine(FadeAudio(primarySource, 0f, 1f, playPauseFadeDuration));
        }
        else
        {
            primarySource.Play();
            primarySource.volume = 1f;
        }
        
        isPlaying = true;
        OnPlayStateChanged?.Invoke(isPlaying);
        
        // Iniciar animación de carátula
        UpdateCoverAnimation(true);
    }
    
    public void Pause()
    {
        if (!isPlaying) return;
        
        if (playPauseFadeDuration > 0f)
        {
            StartCoroutine(FadeOutAndPause(primarySource, playPauseFadeDuration));
        }
        else
        {
            primarySource.Pause();
        }
        
        isPlaying = false;
        OnPlayStateChanged?.Invoke(isPlaying);
        
        // Detener animación de carátula
        UpdateCoverAnimation(false);
    }
    
    public void TogglePlayPause()
    {
        if (!isPlaying)
            Play();
        else
            Pause();
    }
    
    public void PlayNext()
    {
        // Evitar múltiples cambios simultáneos
        if (isChangingTrack) return;
        
        int nextIndex = (currentIndex + 1) % tracks.Count;
        StartTrackChange(nextIndex);
    }

    public void PlayPrevious()
    {
        // Evitar múltiples cambios simultáneos
        if (isChangingTrack) return;
        
        int prevIndex = (currentIndex - 1 + tracks.Count) % tracks.Count;
        StartTrackChange(prevIndex);
    }
    
    public void PlaySpecificTrack(int trackIndex)
    {
        // Evitar múltiples cambios simultáneos
        if (isChangingTrack) return;
        
        // Verificar que el índice sea válido
        if (trackIndex < 0 || trackIndex >= tracks.Count)
        {
            Debug.LogError($"Índice de canción no válido: {trackIndex}. Debe estar entre 0 y {tracks.Count-1}");
            return;
        }
        
        // Si es la canción actual y ya está reproduciendo, no hacer nada
        if (trackIndex == currentIndex && isPlaying)
            return;
        
        // Cambiar de pista
        StartTrackChange(trackIndex);
    }
    
    public void Seek(float value)
    {
        if (primarySource.clip != null)
            primarySource.time = value * primarySource.clip.length;
    }
    
    public float GetNormalizedPlaybackPosition()
    {
        if (primarySource.clip != null)
            return primarySource.time / primarySource.clip.length;
        return 0f;
    }
    
    // Métodos internos
    void StartTrackChange(int newIndex)
    {
        // Si ya estamos cambiando o es el mismo índice, salir
        if (isChangingTrack || newIndex == currentIndex) return;
        
        // Marcar que estamos en proceso de cambio
        isChangingTrack = true;
        
        // Guardar índice anterior para la notificación de secuencia
        int previousIndex = currentIndex;
        
        // Actualizamos el índice actual inmediatamente
        currentIndex = newIndex;
        
        // Actualizar los colores de los botones de inmediato
        UpdateTrackButtonsColors();
        
        // Notificar secuencia de cambio (pause->play)
        OnTrackChangeSequence?.Invoke(previousIndex, newIndex);
        
        if (crossfadeDuration > 0f)
            StartCoroutine(CrossfadeToTrack(newIndex));
        else
            StartCoroutine(DirectTrackChangeWithAnimation(newIndex));
    }
    
    IEnumerator DirectTrackChangeWithAnimation(int newIndex)
    {
        try {
            // Detener reproducción actual
            primarySource.Stop();
            primarySource.time = 0f;
            
            // Iniciar transición de imagen de carátula
            if (coverImageDisplay != null)
            {
                StartCoroutine(FadeCoverImage(tracks[currentIndex].coverImage));
            }
            
            // Cargar nueva pista
            LoadTrack(currentIndex, primarySource);
            
            // Pequeña pausa para asegurar que todo está listo
            yield return new WaitForSeconds(0.05f);
            
            // Reproducir
            primarySource.Play();
            isPlaying = true;
            // Usamos true para preservar valores actuales durante el cambio
            UpdateCoverAnimation(true, true);
            
            // Notificar cambios
            OnPlayStateChanged?.Invoke(isPlaying);
            OnTrackChanged?.Invoke(tracks[currentIndex]);
            UpdateCoverAnimation(isPlaying);
        }
        finally {
            // IMPORTANTE: Marcar que ya no estamos cambiando de pista
            isChangingTrack = false;
        }
    }
    
    IEnumerator CrossfadeToTrack(int newIndex)
    {
        try {
            // Asegurarse de que newIndex es válido
            if (newIndex < 0 || newIndex >= tracks.Count) {
                Debug.LogError("Índice de pista no válido en CrossfadeToTrack: " + newIndex);
                yield break;
            }
            
            // Iniciar transición de imagen de carátula
            if (coverImageDisplay != null)
            {
                StartCoroutine(FadeCoverImage(tracks[newIndex].coverImage));
            }
            
            // Cargar nueva pista en la fuente secundaria
            LoadTrack(newIndex, secondarySource);
            secondarySource.time = 0f;
            secondarySource.volume = 0f;
            
            // Reproducir nueva pista
            secondarySource.Play();

            float elapsed = 0f;
            float startVol = primarySource.volume;

            // Realizar crossfade
            while (elapsed < crossfadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / crossfadeDuration);
                primarySource.volume = Mathf.Lerp(startVol, 0f, t);
                secondarySource.volume = Mathf.Lerp(0f, 1f, t);
                yield return null;
            }

            // Finalizar crossfade
            primarySource.Stop();
            primarySource.volume = 1f;
            secondarySource.volume = 1f;

            // Intercambiar fuentes
            AudioSource tmp = primarySource;
            primarySource = secondarySource;
            secondarySource = tmp;

            isPlaying = true;
            // Usamos true para preservar valores actuales durante el cambio
            UpdateCoverAnimation(true, true);
            
            // Notificar cambios
            OnPlayStateChanged?.Invoke(isPlaying);
            OnTrackChanged?.Invoke(tracks[currentIndex]);
            UpdateCoverAnimation(isPlaying);
        }
        finally {
            // IMPORTANTE: Marcar que ya no estamos cambiando de pista
            isChangingTrack = false;
        }
    }
    
    IEnumerator FadeAudio(AudioSource src, float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            src.volume = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        src.volume = to;
    }

    IEnumerator FadeOutAndPause(AudioSource src, float duration)
    {
        float startVol = src.volume;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            src.volume = Mathf.Lerp(startVol, 0f, elapsed / duration);
            yield return null;
        }
        src.volume = 0f;
        src.Pause();
    }
    
    void LoadTrack(int index, AudioSource src)
    {
        var tr = tracks[index];
        src.clip = tr.clip;
        
        // Notificar cambio de pista
        if (src == primarySource)
        {
            OnTrackChanged?.Invoke(tr);
            
            // Si no estamos en una transición con crossfade, actualizamos la carátula directamente
            if (coverImageDisplay != null && !isChangingTrack)
            {
                UpdateCoverImage(tr.coverImage);
            }
        }
    }
    
    // Método para actualizar todos los colores de los botones de pista
    public void UpdateTrackButtonsColors()
    {
        // Recorrer todas las pistas y actualizar el estado visual de sus botones
        for (int i = 0; i < tracks.Count; i++)
        {
            ConfigureButtonColor(i, i == currentIndex);
        }
        
        // Forzar actualización de layouts
        Canvas.ForceUpdateCanvases();
    }

    private IEnumerator DelayedGraphicUpdate()
    {
        // Esperar al siguiente frame para asegurar que los cambios se aplican
        yield return null;
        
        // Forzar actualización de todos los canvas
        // Reemplazar método obsoleto por la versión actual
        Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (var canvas in allCanvases) 
        {
            if (canvas != null) 
            {
                canvas.enabled = false;
                canvas.enabled = true;
            }
        }
        
        // Forzar actualización de layouts
        Canvas.ForceUpdateCanvases();
    }

    // Método simplificado para marcar botones seleccionados
    void ConfigureButtonColor(int trackIndex, bool isSelected)
    {
        if (trackIndex < 0 || trackIndex >= tracks.Count)
            return;
            
        Button trackButton = tracks[trackIndex].trackButton;
        if (trackButton == null)
            return;
        
        // Obtener el componente Image del botón
        Image buttonImage = trackButton.GetComponent<Image>();
        if (buttonImage == null)
            return;
        
        // Obtener los colores del botón desde su ColorBlock
        Color normalColor = trackButton.colors.normalColor;
        Color selectedColor = trackButton.colors.selectedColor;
        
        // Aplicar solo el color según estado, sin afectar escala ni selección UI
        if (isSelected)
        {
            // Asignar el color de selección sin cambiar nada más
            buttonImage.color = selectedColor;
        }
        else
        {
            // Asignar el color normal sin cambiar nada más
            buttonImage.color = normalColor;
        }
        
        Debug.Log($"Botón Track {trackIndex}: Estado={isSelected}, Color aplicado={buttonImage.color}");
    }
    
    // Un nuevo método público para forzar actualización completa que pueden llamar otros componentes
    public void ForceUIUpdate()
    {
        // Actualizar colores de botones
        UpdateTrackButtonsColors();
        
        // Forzar actualización de todos los canvas en la escena
        // Reemplazar método obsoleto por la versión actual
        Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (var canvas in allCanvases) 
        {
            // Truco para forzar redibujado
            canvas.enabled = false;
            canvas.enabled = true;
        }
        
        // Forzar actualización de layouts
        Canvas.ForceUpdateCanvases();
        
        // Notificar cambio de pista para actualizaciones adicionales
        if (tracks.Count > 0 && currentIndex >= 0 && currentIndex < tracks.Count)
        {
            OnTrackChanged?.Invoke(tracks[currentIndex]);
        }
    }
    
    // Método para configurar la imagen de carátula y su componente secundario
    private void SetupCoverImageDisplay()
    {
        if (coverImageDisplay != null)
        {
            // Asegurarnos que tiene CanvasGroup
            primaryCoverCanvasGroup = coverImageDisplay.gameObject.GetComponent<CanvasGroup>();
            if (primaryCoverCanvasGroup == null)
                primaryCoverCanvasGroup = coverImageDisplay.gameObject.AddComponent<CanvasGroup>();
            
            // Crear la imagen secundaria para transiciones
            GameObject secondaryCoverObj = new GameObject("SecondaryCoverImage");
            secondaryCoverObj.transform.SetParent(coverImageDisplay.transform.parent);
            
            // Copiar todas las propiedades relevantes del RectTransform
            RectTransform primaryRT = coverImageDisplay.rectTransform;
            RectTransform secondaryRT = secondaryCoverObj.AddComponent<RectTransform>();
            secondaryRT.anchorMin = primaryRT.anchorMin;
            secondaryRT.anchorMax = primaryRT.anchorMax;
            secondaryRT.pivot = primaryRT.pivot;
            secondaryRT.anchoredPosition = primaryRT.anchoredPosition;
            secondaryRT.sizeDelta = primaryRT.sizeDelta;
            secondaryRT.rotation = primaryRT.rotation;
            secondaryRT.localScale = primaryRT.localScale;
            
            // Posicionar detrás de la principal
            secondaryCoverObj.transform.SetSiblingIndex(coverImageDisplay.transform.GetSiblingIndex());
            
            // Agregar componentes
            secondaryCoverDisplay = secondaryCoverObj.AddComponent<Image>();
            secondaryCoverCanvasGroup = secondaryCoverObj.AddComponent<CanvasGroup>();
            secondaryCoverCanvasGroup.alpha = 0f;
            
            // Copiar propiedades de la imagen
            secondaryCoverDisplay.sprite = coverImageDisplay.sprite;
            secondaryCoverDisplay.type = coverImageDisplay.type;
            secondaryCoverDisplay.preserveAspect = coverImageDisplay.preserveAspect;
            
            // Si hay una pista actual, mostrar su carátula
            if (tracks.Count > 0 && currentIndex >= 0)
                UpdateCoverImage(tracks[currentIndex].coverImage);
            
            // Guardar valores originales para la animación
            coverOriginalScale = coverImageDisplay.transform.localScale;
            coverOriginalPosition = coverImageDisplay.rectTransform.anchoredPosition;
        }
    }
    
    // Actualiza la imagen de carátula sin transición
    private void UpdateCoverImage(Sprite newCover)
    {
        if (coverImageDisplay == null) return;
        
        if (newCover != null)
        {
            coverImageDisplay.sprite = newCover;
            coverImageDisplay.color = Color.white;
        }
        else
        {
            // Usar una imagen por defecto o simplemente colorear la actual
            coverImageDisplay.color = defaultCoverTint;
        }
    }

    // Realizar transición entre dos imágenes de carátula
    private IEnumerator FadeCoverImage(Sprite newCover)
    {
        if (coverImageDisplay == null || primaryCoverCanvasGroup == null || 
            secondaryCoverDisplay == null || secondaryCoverCanvasGroup == null) 
            yield break;
        
        // Capturar valores actuales de animación para continuidad
        Vector3 currentScale = coverImageDisplay.transform.localScale;
        Vector2 currentPosition = coverImageDisplay.rectTransform.anchoredPosition;
        
        // Preparar imagen secundaria
        secondaryCoverDisplay.sprite = newCover;
        secondaryCoverDisplay.color = newCover != null ? Color.white : defaultCoverTint;
        secondaryCoverCanvasGroup.alpha = 0f;
        
        // Aplicar misma escala y posición para mantener continuidad visual
        secondaryCoverDisplay.transform.localScale = currentScale;
        secondaryCoverDisplay.rectTransform.anchoredPosition = currentPosition;
        
        // Realizar fade out de la imagen principal y fade in de la secundaria
        float elapsed = 0f;
        while (elapsed < coverTransitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / coverTransitionDuration);
            
            primaryCoverCanvasGroup.alpha = 1f - t;
            secondaryCoverCanvasGroup.alpha = t;
            
            yield return null;
        }
        
        // Finalizar la transición
        primaryCoverCanvasGroup.alpha = 0f;
        secondaryCoverCanvasGroup.alpha = 1f;
        
        // Intercambiar referencias para la próxima transición
        coverImageDisplay.sprite = secondaryCoverDisplay.sprite;
        coverImageDisplay.color = secondaryCoverDisplay.color;
        
        // Mantener la escala y posición actuales en lugar de resetearlas
        // Esto asegura que la animación continúe sin interrupciones
        
        primaryCoverCanvasGroup.alpha = 1f;
        secondaryCoverCanvasGroup.alpha = 0f;
    }
    
    // Método para controlar el inicio/parada de la animación (modificado)
    private void UpdateCoverAnimation(bool isPlaying, bool preserveCurrentValues = false)
    {
        if (!animateCoverWhilePlaying || coverImageDisplay == null)
            return;
        
        // Capturar valores actuales si queremos preservarlos
        Vector3 currentScale = preserveCurrentValues ? 
            coverImageDisplay.transform.localScale : coverOriginalScale;
        Vector2 currentPosition = preserveCurrentValues ? 
            coverImageDisplay.rectTransform.anchoredPosition : coverOriginalPosition;
        
        // Detener cualquier animación en curso
        if (coverAnimationCoroutine != null)
        {
            StopCoroutine(coverAnimationCoroutine);
            coverAnimationCoroutine = null;
        }
        
        // Si se detiene, restaurar a valores originales
        if (!isPlaying)
        {
            // Animación suave de retorno a la posición original
            coverAnimationCoroutine = StartCoroutine(ReturnToOriginalTransform(0.3f));
            return;
        }
        
        // Si está reproduciendo, iniciar con transición suave
        if (preserveCurrentValues && (currentScale != coverOriginalScale || currentPosition != coverOriginalPosition))
        {
            // Iniciar con una transición suave desde los valores actuales
            coverAnimationCoroutine = StartCoroutine(SmoothCoverAnimationTransition(currentScale, currentPosition));
        }
        else
        {
            // Iniciar animación normal
            coverAnimationCoroutine = StartCoroutine(AnimateCoverImage(coverOriginalScale, coverOriginalPosition));
        }
    }

    // Método para volver suavemente a la posición original
    private IEnumerator ReturnToOriginalTransform(float duration)
    {
        Vector3 startScale = coverImageDisplay.transform.localScale;
        Vector2 startPosition = coverImageDisplay.rectTransform.anchoredPosition;
        
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            
            coverImageDisplay.transform.localScale = Vector3.Lerp(startScale, coverOriginalScale, t);
            coverImageDisplay.rectTransform.anchoredPosition = Vector2.Lerp(startPosition, coverOriginalPosition, t);
            
            yield return null;
        }
        
        coverImageDisplay.transform.localScale = coverOriginalScale;
        coverImageDisplay.rectTransform.anchoredPosition = coverOriginalPosition;
        coverAnimationCoroutine = null;
    }

    // Coroutina modificada para animar la carátula con valores iniciales específicos
    private IEnumerator AnimateCoverImage(Vector3 startScale, Vector2 startPosition)
    {
        // Variables para la animación suave
        float randomOffsetX = 0f;
        float randomOffsetY = 0f;
        float randomScaleOffset = 0f;
        
        // Valores objetivo para Perlin Noise (con semilla aleatoria para variedad)
        float noiseStartX = Random.Range(0f, 100f);
        float noiseStartY = Random.Range(0f, 100f);
        float noiseStartScale = Random.Range(0f, 100f);
        
        // Calcular el offset inicial para que el primer frame coincida con la posición de inicio
        Vector2 initialOffset = startPosition - coverOriginalPosition;
        float initialScaleFactor = startScale.x / coverOriginalScale.x - 1f;
        
        while (true)
        {
            // Generar valores suaves usando Perlin Noise
            float time = Time.time * coverAnimationSpeed;
            
            // Movimiento suave
            randomOffsetX = (Mathf.PerlinNoise(noiseStartX + time * 0.3f, 0) - 0.5f) * coverAnimationMovementAmount;
            randomOffsetY = (Mathf.PerlinNoise(0, noiseStartY + time * 0.3f) - 0.5f) * coverAnimationMovementAmount;
            
            // Escala suave (siempre ligeramente positiva para zoom in/out)
            randomScaleOffset = Mathf.PerlinNoise(noiseStartScale + time * 0.2f, noiseStartScale + time * 0.2f) * coverAnimationScaleAmount;
            
            // Aplicar a la imagen
            if (coverImageDisplay != null)
            {
                // Ajustar posición
                coverImageDisplay.rectTransform.anchoredPosition = coverOriginalPosition + new Vector2(randomOffsetX, randomOffsetY);
                
                // Ajustar escala (siempre aumentando para hacer zoom in)
                float scaleValue = 1f + randomScaleOffset;
                coverImageDisplay.transform.localScale = coverOriginalScale * scaleValue;
            }
            
            yield return null;
        }
    }
    
    // Transición suave entre animaciones de carátula
    private IEnumerator SmoothCoverAnimationTransition(Vector3 startScale, Vector2 startPosition, float duration = 0.5f)
    {
        // Capturar los valores originales de la animación
        Vector3 targetScale = coverOriginalScale * (1f + Random.Range(0f, coverAnimationScaleAmount));
        Vector2 targetPosition = coverOriginalPosition + new Vector2(
            Random.Range(-coverAnimationMovementAmount, coverAnimationMovementAmount) * 0.5f,
            Random.Range(-coverAnimationMovementAmount, coverAnimationMovementAmount) * 0.5f
        );
        
        float elapsed = 0f;
        float transitionDuration = duration;
        
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / transitionDuration); // Curva suavizada
            
            // Interpolar entre los valores actuales y los nuevos objetivos
            coverImageDisplay.transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            coverImageDisplay.rectTransform.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, t);
            
            yield return null;
        }
        
        // Una vez completada la transición, iniciar la animación regular
        coverAnimationCoroutine = StartCoroutine(AnimateCoverImage(targetScale, targetPosition));
    }
    
    // Utilidades
    public static string FormatTime(float time)
    {
        int m = Mathf.FloorToInt(time / 60f);
        int s = Mathf.FloorToInt(time % 60f);
        return $"{m:D2}:{s:D2}";
    }

    // Añadir método OnDestroy si no existe
    void OnDestroy()
    {
        // Detener todas las corrutinas activas
        if (coverAnimationCoroutine != null)
        {
            StopCoroutine(coverAnimationCoroutine);
        }
    }
}