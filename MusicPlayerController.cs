using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

public class MusicPlayerController : MonoBehaviour
{
    [Header("Referencia al controlador de pistas")]
    public TracklistController tracklistController;

    [Header("UI Elements")]
    [Tooltip("Botón único que alterna entre play/pause. Dejar vacío si usas botones separados")]
    public Button playPauseButton;
    [Tooltip("Botón play separado. Dejar vacío si usas el botón combinado")]
    public Button playButton;
    [Tooltip("Botón pause separado. Dejar vacío si usas el botón combinado")]
    public Button pauseButton;
    public Button nextButton;
    public Button previousButton;
    public Slider progressSlider;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI artistText;
    public TextMeshProUGUI albumText;
    public TextMeshProUGUI currentTimeText;
    public TextMeshProUGUI totalTimeText;

    [Header("Animator")]
    public Animator animator;
    public string playTrigger = "ToPlay";
    public string pauseTrigger = "ToPause";
    
    private bool isDraggingSlider = false;
    
    void Start()
    {
        if (tracklistController == null)
        {
            // Reemplazar FindObjectOfType (obsoleto) por FindAnyObjectByType
            tracklistController = FindAnyObjectByType<TracklistController>();
            if (tracklistController == null)
            {
                Debug.LogError("No se encontró TracklistController. El reproductor no funcionará correctamente.");
                return;
            }
        }
        
        // Configurar los listeners para los eventos del TracklistController
        tracklistController.OnTrackChanged += OnTrackChanged;
        tracklistController.OnPlayStateChanged += OnPlayStateChanged;
        tracklistController.OnTrackTimeChanged += OnTrackTimeChanged;
        
        // Agregar listener para la secuencia de cambio de pista
        tracklistController.OnTrackChangeSequence += OnTrackChangeSequence;
        
        // Configurar botones de reproducción según el modo que queremos usar
        if (playPauseButton != null) 
        {
            // Modo de botón único que alterna entre play/pause
            playPauseButton.onClick.AddListener(tracklistController.TogglePlayPause);
        } 
        else if (playButton != null && pauseButton != null) 
        {
            // Modo de botones separados
            playButton.onClick.AddListener(tracklistController.Play);
            pauseButton.onClick.AddListener(tracklistController.Pause);
            
            // Establecer visibilidad inicial de los botones
            UpdatePlayPauseButtonsVisibility(tracklistController.IsPlaying);
        }

        // Configurar otros botones
        if (nextButton != null)
            nextButton.onClick.AddListener(tracklistController.PlayNext);
            
        if (previousButton != null)
            previousButton.onClick.AddListener(tracklistController.PlayPrevious);
            
        if (progressSlider != null)
        {
            progressSlider.onValueChanged.AddListener(OnSliderValueChanged);
            
            // Agregar eventos para detectar cuando el usuario comienza a arrastrar el slider
            var sliderEvents = progressSlider.gameObject.GetComponent<SliderDragDetector>();
            if (sliderEvents == null)
                sliderEvents = progressSlider.gameObject.AddComponent<SliderDragDetector>();
                
            sliderEvents.onStartDrag.AddListener(() => isDraggingSlider = true);
            sliderEvents.onEndDrag.AddListener(() => {
                isDraggingSlider = false;
                OnSliderValueChanged(progressSlider.value);
            });
        }
        
        // Actualizar la UI con la información de la pista actual
        if (tracklistController.CurrentTrack != null)
            OnTrackChanged(tracklistController.CurrentTrack);
            
        // Actualizar estado de reproducción
        OnPlayStateChanged(tracklistController.IsPlaying);

        // Asegurar que los botones de pista se actualizan después de la inicialización
        if (tracklistController != null)
        {
            tracklistController.UpdateTrackButtonsColors();
        }
    }
    
    void OnDestroy()
    {
        // Eliminar los listeners para evitar referencias perdidas
        if (tracklistController != null)
        {
            tracklistController.OnTrackChanged -= OnTrackChanged;
            tracklistController.OnPlayStateChanged -= OnPlayStateChanged;
            tracklistController.OnTrackTimeChanged -= OnTrackTimeChanged;
            tracklistController.OnTrackChangeSequence -= OnTrackChangeSequence;
        }
    }
    
    // Callbacks para los eventos del TracklistController
    public void OnTrackChanged(Track track)
    {
        // Actualizar el título y reiniciar sus animaciones
        if (titleText != null)
        {
            titleText.text = track.title;
            ResetAnimationComponent(titleText.gameObject);
        }
        
        // Actualizar artista y reiniciar sus animaciones
        if (artistText != null)
        {
            artistText.text = track.artist;
            ResetAnimationComponent(artistText.gameObject);
        }
        
        // Actualizar álbum y reiniciar sus animaciones
        if (albumText != null)
        {
            albumText.text = track.album;
            ResetAnimationComponent(albumText.gameObject);
        }
        
        // Actualizar tiempo actual y reiniciar sus animaciones
        if (currentTimeText != null)
        {
            currentTimeText.text = TracklistController.FormatTime(0f);
            ResetAnimationComponent(currentTimeText.gameObject);
        }
        
        // Actualizar tiempo total y reiniciar sus animaciones
        if (totalTimeText != null)
        {
            totalTimeText.text = TracklistController.FormatTime(track.clip.length);
            ResetAnimationComponent(totalTimeText.gameObject);
        }
        
        // Forzar actualización de los colores de los botones
        if (tracklistController != null)
        {
            tracklistController.UpdateTrackButtonsColors();
        }
        
        // Forzar actualización de los componentes Canvas
        Canvas.ForceUpdateCanvases();
    }
    
    public void OnPlayStateChanged(bool isPlaying)
    {
        // Actualizar la UI según el estado de reproducción
        if (animator != null)
        {
            animator.ResetTrigger(isPlaying ? pauseTrigger : playTrigger);
            animator.SetTrigger(isPlaying ? playTrigger : pauseTrigger);
        }
        
        // Actualizar la visibilidad de los botones de play/pause
        UpdatePlayPauseButtonsVisibility(isPlaying);
    }
    
    void OnTrackTimeChanged(float currentTime, float totalTime)
    {
        // Solo actualizar el slider si el usuario no está arrastrándolo
        if (progressSlider != null && !isDraggingSlider)
        {
            progressSlider.value = totalTime > 0 ? currentTime / totalTime : 0f;
        }
        
        if (currentTimeText != null)
        {
            currentTimeText.text = TracklistController.FormatTime(currentTime);
        }
        
        if (totalTimeText != null && totalTimeText.text == "00:00")
        {
            totalTimeText.text = TracklistController.FormatTime(totalTime);
        }
    }
    
    // Manejo del slider
    void OnSliderValueChanged(float value)
    {
        if (isDraggingSlider)
            return;
            
        tracklistController.Seek(value);
    }
    
    // Método para actualizar la visibilidad de los botones de play/pause
    void UpdatePlayPauseButtonsVisibility(bool isPlaying)
    {
        // Solo actualizar si estamos usando botones separados
        if (playButton == null || pauseButton == null) 
            return;
            
        // Mostrar el botón correspondiente según el estado actual
        playButton.gameObject.SetActive(!isPlaying);
        pauseButton.gameObject.SetActive(isPlaying);
    }
    
    // Método para reiniciar componentes de animación en un GameObject
    void ResetAnimationComponent(GameObject textObject)
    {
        // Buscar el componente Auto_Animator específicamente
        Auto_Animator animator = textObject.GetComponent<Auto_Animator>();
        if (animator != null)
        {
            // Acceder al campo privado animationComplete mediante reflexión y establecerlo a false
            System.Reflection.FieldInfo fieldInfo = typeof(Auto_Animator).GetField("animationComplete", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (fieldInfo != null)
            {
                fieldInfo.SetValue(animator, false); // Restablece la bandera animationComplete
            }
            
            // Llamar al método PlayAnimation del Auto_Animator
            animator.PlayAnimation();
            return; // Si encontramos el Auto_Animator, usamos ese y salimos
        }
        
        // Como respaldo, si no hay Auto_Animator, intentamos los métodos anteriores
        // 1. Desactivar y reactivar el GameObject completo para reiniciar cualquier script
        textObject.SetActive(false);
        textObject.SetActive(true);
        
        // 2. Notificar a los scripts personalizados que deben reiniciar su animación
        textObject.SendMessage("RestartAnimation", SendMessageOptions.DontRequireReceiver);
        
        // 3. Si hay algún Animator en el GameObject, reiniciarlo también
        Animator anim = textObject.GetComponent<Animator>();
        if (anim != null)
        {
            anim.Rebind();
            anim.Update(0f);
        }
    }
    
    // Método para forzar actualización de UI en cualquier momento
    public void ForceUpdateUI()
    {
        if (tracklistController != null && tracklistController.CurrentTrack != null)
        {
            OnTrackChanged(tracklistController.CurrentTrack);
            OnPlayStateChanged(tracklistController.IsPlaying);
            
            // Forzar actualización de colores de botones
            tracklistController.UpdateTrackButtonsColors();
        }
    }
    
    // Nuevo método para manejar la secuencia de cambio de pista
    void OnTrackChangeSequence(int previousIndex, int newIndex)
    {
        if (animator == null) return;
        
        // Solo ejecutar si estaba reproduciendo
        if (tracklistController.IsPlaying)
        {
            // 1. Reproducir la animación de pausa
            animator.ResetTrigger(playTrigger);
            animator.SetTrigger(pauseTrigger);
            
            // 2. Después de un breve retraso, reproducir la animación de play
            StartCoroutine(PlayAnimationAfterDelay(playTrigger, 0.3f));
        }
    }

    IEnumerator PlayAnimationAfterDelay(string triggerName, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (animator != null)
        {
            animator.ResetTrigger(pauseTrigger);
            animator.SetTrigger(triggerName);
        }
    }
}

// Clase auxiliar para detectar cuando el usuario arrastra el slider
public class SliderDragDetector : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public UnityEvent onStartDrag = new UnityEvent();
    public UnityEvent onEndDrag = new UnityEvent();
    
    public void OnPointerDown(PointerEventData eventData)
    {
        onStartDrag.Invoke();
    }
    
    public void OnPointerUp(PointerEventData eventData)
    {
        onEndDrag.Invoke();
    }
}