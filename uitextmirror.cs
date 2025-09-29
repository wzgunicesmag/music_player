using UnityEngine;
using TMPro; // O usa UnityEngine.UI para Text normal

public class UITextMirror : MonoBehaviour
{
    public TMP_Text originalText; // arrastra el texto original desde el Inspector

    private TMP_Text mirrorText;

    void Awake()
    {
        mirrorText = GetComponent<TMP_Text>();
        if (mirrorText == null)
        {
            Debug.LogError("UITextMirror: Este objeto no tiene TMP_Text.");
            enabled = false;
        }
    }

    void Update()
    {
        if (originalText != null && mirrorText != null)
        {
            mirrorText.text = originalText.text;
        }
    }
}
