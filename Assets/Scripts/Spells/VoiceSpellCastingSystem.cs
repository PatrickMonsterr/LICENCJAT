using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Windows.Speech;
using UnityEngine.XR;

public class VoiceSpellCastingSystem : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private Camera xrCamera;
    [SerializeField] private Transform wandTip;
    [SerializeField] private SpellDrawingSurface drawingSurface;

    [Header("Spell Prefabs")]
    [SerializeField] private GameObject ignisAoEPrefab;
    [SerializeField] private GameObject regenAoEPrefab;
    [SerializeField] private GameObject freezeAoEPrefab;
    [SerializeField] private GameObject slashAoEPrefab;
    [SerializeField] private GameObject shieldPrefab;
    [SerializeField] private GameObject portalPrefab;

    [SerializeField] private LayerMask castLayers = ~0;
    [SerializeField] private float maxCastDistance = 25f;
    [SerializeField] private float castSurfaceOffset = 0.02f;

    [Header("Gesture Recognition")]
    [SerializeField] private float acceptThreshold = 0.75f;
    [SerializeField] private float gestureTimeout = 5f;
    [SerializeField] private int minimumPointsRequired = 12;

    [Header("Voice")]
    [SerializeField] private string ignisKeyword = "IGNIS";
    [SerializeField] private string regenKeyword = "REGEN";
    [SerializeField] private string freezeKeyword = "FREEZE";
    [SerializeField] private string slashKeyword = "SLASH";
    [SerializeField] private string shieldKeyword = "SHIELD";
    [SerializeField] private string portalKeyword = "PORTAL";
    [SerializeField] private ConfidenceLevel minimumConfidence = ConfidenceLevel.Medium;

    private KeywordRecognizer keywordRecognizer;
    private readonly DollarOneRecognizer recognizer = new DollarOneRecognizer();

    private InputDevice rightHandDevice;
    private bool previousTriggerPressed;

    private bool awaitingGesture;
    private string pendingSpell;
    private float awaitingUntil;

    private string queuedVoiceKeyword;
    private bool hasQueuedVoiceKeyword;

    private bool hasLockedTarget;
    private Vector3 lockedTargetPosition;
    private Vector3 lockedTargetNormal;

    private void Awake()
    {
        RegisterTemplates();
    }

    private void Start()
    {
        if (xrCamera == null)
        {
            Debug.LogError("[VoiceSpellCastingSystem] XR Camera is not assigned.");
            enabled = false;
            return;
        }

        if (wandTip == null)
        {
            Debug.LogError("[VoiceSpellCastingSystem] Wand Tip is not assigned.");
            enabled = false;
            return;
        }

        if (drawingSurface == null)
        {
            Debug.LogError("[VoiceSpellCastingSystem] Drawing Surface is not assigned.");
            enabled = false;
            return;
        }

        if (ignisAoEPrefab == null)
        {
            Debug.LogError("[VoiceSpellCastingSystem] Ignis AoE Prefab is not assigned.");
            enabled = false;
            return;
        }

        StartVoiceRecognizer();
    }

    private void Update()
    {
        ProcessQueuedVoiceCommand();

        if (!awaitingGesture)
            return;

        if (Time.time > awaitingUntil)
        {
            Debug.Log("[VoiceSpellCastingSystem] Gesture timeout. Cancelling pending spell.");
            CancelPendingSpell();
            return;
        }

        EnsureRightHandDevice();

        bool triggerPressed = false;
        if (rightHandDevice.isValid)
            rightHandDevice.TryGetFeatureValue(CommonUsages.triggerButton, out triggerPressed);

        if (triggerPressed && !previousTriggerPressed)
        {
            drawingSurface.BeginStroke();
            Debug.Log("[VoiceSpellCastingSystem] Drawing started.");
        }

        if (triggerPressed)
        {
            drawingSurface.TryAddPointFromRay(wandTip.position, wandTip.forward);
        }

        if (!triggerPressed && previousTriggerPressed)
        {
            FinishGesture();
        }

        previousTriggerPressed = triggerPressed;
    }

    private void RegisterTemplates()
    {
        // IGNIS = triangle
        recognizer.AddTemplate("IGNIS", new List<Vector2>
    {
        new Vector2(0.0f, 1.0f),
        new Vector2(-0.2f, 0.5f),
        new Vector2(-0.4f, 0.0f),
        new Vector2(-0.6f, -0.5f),
        new Vector2(-0.85f, -1.0f),

        new Vector2(-0.3f, -1.0f),
        new Vector2(0.3f, -1.0f),
        new Vector2(0.85f, -1.0f),

        new Vector2(0.6f, -0.5f),
        new Vector2(0.4f, 0.0f),
        new Vector2(0.2f, 0.5f),
        new Vector2(0.0f, 1.0f)
    });

        //    // REGEN = spiral
        //    recognizer.AddTemplate("REGEN", new List<Vector2>
        //{
        //    new Vector2(0.0f, 1.0f),
        //    new Vector2(0.5f, 0.9f),
        //    new Vector2(0.9f, 0.5f),
        //    new Vector2(1.0f, 0.0f),
        //    new Vector2(0.8f, -0.5f),
        //    new Vector2(0.4f, -0.8f),
        //    new Vector2(0.0f, -0.9f),
        //    new Vector2(-0.4f, -0.7f),
        //    new Vector2(-0.6f, -0.3f),
        //    new Vector2(-0.6f, 0.0f),
        //    new Vector2(-0.4f, 0.3f),
        //    new Vector2(-0.1f, 0.4f),
        //    new Vector2(0.2f, 0.3f),
        //    new Vector2(0.35f, 0.1f),
        //    new Vector2(0.3f, -0.1f),
        //    new Vector2(0.1f, -0.2f),
        //    new Vector2(-0.05f, -0.15f)
        //});

        //    // FREEZE = zigzag
        //    recognizer.AddTemplate("FREEZE", new List<Vector2>
        //{
        //    new Vector2(-1.0f, 0.8f),
        //    new Vector2(-0.5f, 0.2f),
        //    new Vector2(0.0f, 0.8f),
        //    new Vector2(0.5f, 0.2f),
        //    new Vector2(1.0f, 0.8f),
        //    new Vector2(0.5f, -0.2f),
        //    new Vector2(0.0f, -0.8f),
        //    new Vector2(-0.5f, -0.2f),
        //    new Vector2(-1.0f, -0.8f)
        //});

        // SLASH = single slash 
        recognizer.AddTemplate("SLASH", new List<Vector2>
    {
        new Vector2(-1.0f, -1.0f),
        new Vector2(-0.5f, -0.5f),
        new Vector2(0.0f, 0.0f),
        new Vector2(0.5f, 0.5f),
        new Vector2(1.0f, 1.0f)
    });

        //    // SHIELD = arc
        //    recognizer.AddTemplate("SHIELD", new List<Vector2>
        //{
        //    new Vector2(-1.0f, -0.5f),
        //    new Vector2(-0.8f, 0.0f),
        //    new Vector2(-0.5f, 0.4f),
        //    new Vector2(0.0f, 0.7f),
        //    new Vector2(0.5f, 0.4f),
        //    new Vector2(0.8f, 0.0f),
        //    new Vector2(1.0f, -0.5f)
        //});

        //    // PORTAL = oval
        //    recognizer.AddTemplate("PORTAL", new List<Vector2>
        //{
        //    new Vector2(0.0f, 1.0f),
        //    new Vector2(0.4f, 0.8f),
        //    new Vector2(0.7f, 0.3f),
        //    new Vector2(0.8f, 0.0f),
        //    new Vector2(0.7f, -0.3f),
        //    new Vector2(0.4f, -0.8f),
        //    new Vector2(0.0f, -1.0f),
        //    new Vector2(-0.4f, -0.8f),
        //    new Vector2(-0.7f, -0.3f),
        //    new Vector2(-0.8f, 0.0f),
        //    new Vector2(-0.7f, 0.3f),
        //    new Vector2(-0.4f, 0.8f),
        //    new Vector2(0.0f, 1.0f)
        //});
    }

    private void StartVoiceRecognizer()
    {
        try
        {
            keywordRecognizer = new KeywordRecognizer(
                new[]
                {
                    ignisKeyword,
                    //regenKeyword,
                    //freezeKeyword,
                    slashKeyword,
                    //shieldKeyword,
                    //portalKeyword
                },
                minimumConfidence
            );

            keywordRecognizer.OnPhraseRecognized += OnPhraseRecognized;
            keywordRecognizer.Start();

            Debug.Log($"[VoiceSpellCastingSystem] Voice recognizer started. Say '{ignisKeyword}'.");
        }
        catch (Exception e)
        {
            Debug.LogError("[VoiceSpellCastingSystem] Failed to start voice recognizer: " + e.Message);
            enabled = false;
        }
    }

    private void OnPhraseRecognized(PhraseRecognizedEventArgs args)
    {
        queuedVoiceKeyword = args.text.ToUpperInvariant();
        hasQueuedVoiceKeyword = true;
    }

    private void ProcessQueuedVoiceCommand()
    {
        if (!hasQueuedVoiceKeyword)
            return;

        string keyword = queuedVoiceKeyword;
        hasQueuedVoiceKeyword = false;
        queuedVoiceKeyword = null;

        Debug.Log($"[VoiceSpellCastingSystem] Voice recognized: {keyword}");

        string spellToPrepare = null;

        if (keyword == ignisKeyword.ToUpperInvariant()) spellToPrepare = "IGNIS";
        else if (keyword == regenKeyword.ToUpperInvariant()) spellToPrepare = "REGEN";
        else if (keyword == freezeKeyword.ToUpperInvariant()) spellToPrepare = "FREEZE";
        else if (keyword == slashKeyword.ToUpperInvariant()) spellToPrepare = "SLASH";
        else if (keyword == shieldKeyword.ToUpperInvariant()) spellToPrepare = "SHIELD";
        else if (keyword == portalKeyword.ToUpperInvariant()) spellToPrepare = "PORTAL";

        if (string.IsNullOrEmpty(spellToPrepare))
            return;

        if (!TryLockCurrentTarget())
            return;

        pendingSpell = spellToPrepare;
        awaitingGesture = true;
        awaitingUntil = Time.time + gestureTimeout;
        previousTriggerPressed = false;

        drawingSurface.ShowForCamera(xrCamera);

        Debug.Log($"[VoiceSpellCastingSystem] Waiting for {pendingSpell} gesture.");
    }
    private bool TryLockCurrentTarget()
    {
        if (Physics.Raycast(
            wandTip.position,
            wandTip.forward,
            out RaycastHit hit,
            maxCastDistance,
            castLayers,
            QueryTriggerInteraction.Ignore))
        {
            hasLockedTarget = true;
            lockedTargetPosition = hit.point;
            lockedTargetNormal = hit.normal;

            Debug.Log($"[VoiceSpellCastingSystem] Target locked at {hit.point} on {hit.collider.name}");
            return true;
        }

        hasLockedTarget = false;
        Debug.LogWarning("[VoiceSpellCastingSystem] No valid target found. Aim at the floor before saying IGNIS.");
        return false;
    }
    private void FinishGesture()
    {
        List<Vector2> stroke = drawingSurface.GetStrokePoints2D();

        Debug.Log($"[VoiceSpellCastingSystem] Points collected: {stroke.Count}");

        if (stroke.Count < minimumPointsRequired)
        {
            Debug.LogWarning("[VoiceSpellCastingSystem] Not enough points. Gesture rejected.");
            CancelPendingSpell();
            return;
        }

        DollarOneRecognizer.Result result = recognizer.Recognize(stroke);

        Debug.Log($"[VoiceSpellCastingSystem] Recognizer result: {result.Name} | Score: {result.Score:0.000}");

        bool accepted =
            result.Success &&
            result.Name == pendingSpell &&
            result.Score >= acceptThreshold;

        if (accepted)
        {
            Debug.Log("[VoiceSpellCastingSystem] Gesture accepted.");
            CastPendingSpell();
        }
        else
        {
            Debug.LogWarning("[VoiceSpellCastingSystem] Gesture rejected.");
        }

        CancelPendingSpell();
    }

    private void CastPendingSpell()
    {
        if (!hasLockedTarget)
        {
            Debug.LogWarning("[VoiceSpellCastingSystem] Cast failed. No locked target.");
            return;
        }

        GameObject prefabToCast = GetPrefabForSpell(pendingSpell);

        if (prefabToCast == null)
        {
            Debug.LogWarning($"[VoiceSpellCastingSystem] Spell '{pendingSpell}' recognized correctly, but no prefab is assigned yet.");
            return;
        }

        Vector3 spawnPosition = lockedTargetPosition + lockedTargetNormal * castSurfaceOffset;
        Quaternion spawnRotation = Quaternion.FromToRotation(Vector3.up, lockedTargetNormal);

        Instantiate(prefabToCast, spawnPosition, spawnRotation);

        Debug.Log($"[VoiceSpellCastingSystem] {pendingSpell} cast at locked target {lockedTargetPosition}.");
    }

    private GameObject GetPrefabForSpell(string spellName)
    {
        switch (spellName)
        {
            case "IGNIS": return ignisAoEPrefab;
            case "REGEN": return regenAoEPrefab;
            case "FREEZE": return freezeAoEPrefab;
            case "SLASH": return slashAoEPrefab;
            case "SHIELD": return shieldPrefab;
            case "PORTAL": return portalPrefab;
            default: return null;
        }
    }

    private void CancelPendingSpell()
    {
        awaitingGesture = false;
        pendingSpell = null;
        previousTriggerPressed = false;

        hasLockedTarget = false;
        lockedTargetPosition = Vector3.zero;
        lockedTargetNormal = Vector3.up;

        drawingSurface.HideSurface();
    }

    private void EnsureRightHandDevice()
    {
        if (rightHandDevice.isValid)
            return;

        List<InputDevice> devices = new List<InputDevice>();
        InputDevices.GetDevicesAtXRNode(XRNode.RightHand, devices);

        if (devices.Count > 0)
            rightHandDevice = devices[0];
    }

    private void OnDestroy()
    {
        if (keywordRecognizer != null)
        {
            keywordRecognizer.OnPhraseRecognized -= OnPhraseRecognized;

            if (keywordRecognizer.IsRunning)
                keywordRecognizer.Stop();

            keywordRecognizer.Dispose();
        }
    }
}