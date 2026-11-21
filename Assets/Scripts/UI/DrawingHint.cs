using TMPro;
using UnityEngine;

public class DrawingHint : MonoBehaviour
{
    [SerializeField] private DrawingCanvas drawing;
    private TMP_Text label;
    private void Awake() => label = GetComponent<TMP_Text>();
    private void Update()
    {
        if(label != null) label.enabled = drawing != null && !drawing.HasInk;
    }
}
