using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class SpellBookToggle : MonoBehaviour
{
    [SerializeField] private GameObject spellBookObject;

    private InputDevice leftHandDevice;
    private bool previousXPressed;

    private void Start()
    {
        if (spellBookObject != null)
        {
            spellBookObject.SetActive(false);
        }
    }

    private void Update()
    {
        EnsureLeftHandDevice();

        bool xPressed = false;

        if (leftHandDevice.isValid)
        {
            leftHandDevice.TryGetFeatureValue(CommonUsages.primaryButton, out xPressed);
        }

        if (xPressed && !previousXPressed)
        {
            ToggleSpellBook();
        }

        previousXPressed = xPressed;
    }

    private void ToggleSpellBook()
    {
        if (spellBookObject == null)
            return;

        spellBookObject.SetActive(!spellBookObject.activeSelf);
    }

    private void EnsureLeftHandDevice()
    {
        if (leftHandDevice.isValid)
            return;

        List<InputDevice> devices = new List<InputDevice>();
        InputDevices.GetDevicesAtXRNode(XRNode.LeftHand, devices);

        if (devices.Count > 0)
        {
            leftHandDevice = devices[0];
        }
    }
}