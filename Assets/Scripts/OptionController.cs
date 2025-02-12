using UnityEngine;

public class OptionController : MonoBehaviour
{
    Animator anim;
    bool currentState = false;
    private void Awake() {
        anim = GetComponent<Animator>();
        EnablePanel();
    }
    public void EnablePanel() {
        currentState = true;
        SetState();
        anim.SetBool("Open", true);
    }
    public void Disablepanel() {
        anim.SetBool("Open", false);
        currentState = false;
        Invoke("SetState", 0.5f);
    }

    private void SetState() {
        gameObject.SetActive(currentState);
    }
}
