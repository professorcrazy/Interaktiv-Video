using UnityEngine;
using TMPro;
using UnityEngine.UI;
public class OptionBtnController : MonoBehaviour
{

    [SerializeField] TMP_Text btnText;
    private Button btn;    
    public void InitializeOptoonButton(string text) { 
        btnText.text = text;
    }
    public void InitializeOptoonButton(string text, Color txtColor) {
        btnText.text = text;
        btnText.color = txtColor;
    }
}
