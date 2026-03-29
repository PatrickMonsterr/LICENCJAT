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
    [SerializeField] private float ignisThreshold = 0.50f;
    [SerializeField] private float regenThreshold = 0.40f;
    [SerializeField] private float freezeThreshold = 0.55f;
    [SerializeField] private float slashThreshold = 0.75f;
    [SerializeField] private float shieldThreshold = 0.55f;
    [SerializeField] private float portalThreshold = 0.60f;

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
            new Vector2(-0.8f, -1.0f),
            new Vector2(0.8f, -1.0f),
            new Vector2(0.0f, 1.0f)
        });

        // REGEN = circle clockwise
        recognizer.AddTemplate("REGEN", new List<Vector2>
    {
        new Vector2(0.0f, 1.0f),
        new Vector2(0.7f, 0.7f),
        new Vector2(1.0f, 0.0f),
        new Vector2(0.7f, -0.7f),
        new Vector2(0.0f, -1.0f),
        new Vector2(-0.7f, -0.7f),
        new Vector2(-1.0f, 0.0f),
        new Vector2(-0.7f, 0.7f),
        new Vector2(0.0f, 1.0f)
    });

        // REGEN = circle counter-clockwise
        recognizer.AddTemplate("REGEN", new List<Vector2>
    {
        new Vector2(0.0f, 1.0f),
        new Vector2(-0.7f, 0.7f),
        new Vector2(-1.0f, 0.0f),
        new Vector2(-0.7f, -0.7f),
        new Vector2(0.0f, -1.0f),
        new Vector2(0.7f, -0.7f),
        new Vector2(1.0f, 0.0f),
        new Vector2(0.7f, 0.7f),
        new Vector2(0.0f, 1.0f)
    });

        // SLASH = single slash /
        recognizer.AddTemplate("SLASH", new List<Vector2>
    {
        new Vector2(-1.0f, -1.0f),
        new Vector2(-0.5f, -0.5f),
        new Vector2(0.0f, 0.0f),
        new Vector2(0.5f, 0.5f),
        new Vector2(1.0f, 1.0f)
    });

        // SHIELD = arc
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

        // FREEZE = zigzag
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

        // PORTAL = rectangular gate
    //    recognizer.AddTemplate("PORTAL", new List<Vector2>
    //{
    //    new Vector2(-0.6f, 1.0f),
    //    new Vector2(-0.6f, 0.0f),
    //    new Vector2(-0.6f, -1.0f),
    //    new Vector2(0.6f, -1.0f),
    //    new Vector2(0.6f, 0.0f),
    //    new Vector2(0.6f, 1.0f)
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
                    regenKeyword,
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
    private float GetThresholdForSpell(string spellName)
    {
        switch (spellName)
        {
            case "IGNIS": return ignisThreshold;
            case "REGEN": return regenThreshold;
            case "FREEZE": return freezeThreshold;
            case "SLASH": return slashThreshold;
            case "SHIELD": return shieldThreshold;
            case "PORTAL": return portalThreshold;
            default: return 0.50f;
        }
    }
    private float GetStrokePathLength(List<Vector2> points)
    {
        float length = 0f;

        for (int i = 1; i < points.Count; i++)
            length += Vector2.Distance(points[i - 1], points[i]);

        return length;
    }

    private float GetStartEndDistance(List<Vector2> points)
    {
        if (points == null || points.Count < 2)
            return 999f;

        return Vector2.Distance(points[0], points[points.Count - 1]);
    }

    private bool IsClosedShape(List<Vector2> points, float maxClosingDistance = 0.45f)
    {
        return GetStartEndDistance(points) <= maxClosingDistance;
    }

    private float GetPathStraightnessRatio(List<Vector2> points)
    {
        if (points == null || points.Count < 2)
            return 1f;

        float pathLength = GetStrokePathLength(points);
        float directDistance = Vector2.Distance(points[0], points[points.Count - 1]);

        if (directDistance < 0.0001f)
            return 999f;

        return pathLength / directDistance;
    }
    private bool PassesSpellShapeRules(string spellName, List<Vector2> stroke)
    {
        if (stroke == null || stroke.Count < 2)
            return false;

        if (spellName == "IGNIS")
        {
            bool closedEnough = IsClosedShape(stroke, 0.45f);
            float straightness = GetPathStraightnessRatio(stroke);

            Debug.Log($"[VoiceSpellCastingSystem] IGNIS shape check | Closed: {closedEnough} | Straightness: {straightness:0.000}");

            // Triangle should be closed and clearly not just a straight line
            return closedEnough && straightness > 2.0f;
        }

        if (spellName == "SLASH")
        {
            bool closedEnough = IsClosedShape(stroke, 0.45f);
            float straightness = GetPathStraightnessRatio(stroke);

            Debug.Log($"[VoiceSpellCastingSystem] SLASH shape check | Closed: {closedEnough} | Straightness: {straightness:0.000}");

            // Slash should stay open and fairly line-like
            return !closedEnough && straightness < 1.2f;
        }

        return true;
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

        DollarOneRecognizer.Result result = recognizer.RecognizeOnly(pendingSpell, stroke);

        float requiredThreshold = GetThresholdForSpell(pendingSpell);
        bool passesShapeRules = PassesSpellShapeRules(pendingSpell, stroke);

        Debug.Log($"[VoiceSpellCastingSystem] Expected: {pendingSpell} | Score: {result.Score:0.000} | Threshold: {requiredThreshold:0.000} | ShapeRules: {passesShapeRules}");

        bool accepted =
            result.Success &&
            result.Score >= requiredThreshold &&
            passesShapeRules;

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