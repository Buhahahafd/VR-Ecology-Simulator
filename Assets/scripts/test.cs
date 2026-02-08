using UnityEngine;
using UnityEngine.InputSystem;

public class test : MonoBehaviour
{

    public InputActionProperty testActionValue;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        float value = testActionValue.action.ReadValue<float>();
        Debug.Log("Value:" + value);
    }
}
