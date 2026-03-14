using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Windows.Speech;

public class VoiceSpellCastingSystem : MonoBehaviour
{
    [Serializable]
    public class SpellDefinition
    {
        [Header("Identity")]
        public string spellId;
        public string voiceKeyword;

        [Header("Casting")]
        public GameObject spellPrefab;
        public float maxCastDistance = 25f;

        [Header("Placement")]
        public float spawnYOffset = 0.02f;
        public bool alignToSurfaceNormal = true;
    }

    [Header("Voice Recognition")]
    [SerializeField] private ConfidenceLevel minimumConfidence = ConfidenceLevel.Medium;

    [Header("Casting Source")]
    [SerializeField] private Transform wandTip;
    [SerializeField] private LayerMask castLayers = ~0;

    [Header("Available Spells")]
    [SerializeField] private List<SpellDefinition> spells = new List<SpellDefinition>();

    private KeywordRecognizer keywordRecognizer;
    private readonly Dictionary<string, SpellDefinition> spellLookup = new Dictionary<string, SpellDefinition>(StringComparer.OrdinalIgnoreCase);

    private void Start()
    {
        if (wandTip == null)
        {
            Debug.LogError("[VoiceSpellCastingSystem] Wand Tip is not assigned.");
            enabled = false;
            return;
        }

        BuildSpellLookup();

        if (spellLookup.Count == 0)
        {
            Debug.LogError("[VoiceSpellCastingSystem] No valid spells configured.");
            enabled = false;
            return;
        }

        try
        {
            keywordRecognizer = new KeywordRecognizer(new List<string>(spellLookup.Keys).ToArray(), minimumConfidence);
            keywordRecognizer.OnPhraseRecognized += OnPhraseRecognized;
            keywordRecognizer.Start();

            Debug.Log($"[VoiceSpellCastingSystem] Voice recognizer started. Registered spells: {string.Join(", ", spellLookup.Keys)}");
        }
        catch (Exception e)
        {
            Debug.LogError("[VoiceSpellCastingSystem] Failed to start voice recognizer: " + e.Message);
            enabled = false;
        }
    }

    private void BuildSpellLookup()
    {
        spellLookup.Clear();

        foreach (SpellDefinition spell in spells)
        {
            if (spell == null)
                continue;

            if (string.IsNullOrWhiteSpace(spell.voiceKeyword))
            {
                Debug.LogWarning("[VoiceSpellCastingSystem] Skipping spell with empty voice keyword.");
                continue;
            }

            if (spell.spellPrefab == null)
            {
                Debug.LogWarning($"[VoiceSpellCastingSystem] Skipping spell '{spell.voiceKeyword}' because spellPrefab is missing.");
                continue;
            }

            if (spellLookup.ContainsKey(spell.voiceKeyword))
            {
                Debug.LogWarning($"[VoiceSpellCastingSystem] Duplicate voice keyword detected: '{spell.voiceKeyword}'. Keeping the first one.");
                continue;
            }

            spellLookup.Add(spell.voiceKeyword, spell);
        }
    }

    private void OnPhraseRecognized(PhraseRecognizedEventArgs args)
    {
        Debug.Log($"[VoiceSpellCastingSystem] Recognized: '{args.text}' | Confidence: {args.confidence}");

        if (spellLookup.TryGetValue(args.text, out SpellDefinition spell))
        {
            CastSpell(spell);
        }
        else
        {
            Debug.LogWarning($"[VoiceSpellCastingSystem] No spell mapped for recognized phrase '{args.text}'.");
        }
    }

    private void CastSpell(SpellDefinition spell)
    {
        Vector3 origin = wandTip.position;
        Vector3 direction = wandTip.forward;

        Debug.DrawRay(origin, direction * spell.maxCastDistance, Color.red, 2f);

        if (Physics.Raycast(origin, direction, out RaycastHit hit, spell.maxCastDistance, castLayers, QueryTriggerInteraction.Ignore))
        {
            Vector3 spawnPosition = hit.point + hit.normal * spell.spawnYOffset;

            Quaternion spawnRotation = spell.alignToSurfaceNormal
                ? Quaternion.FromToRotation(Vector3.up, hit.normal)
                : Quaternion.identity;

            Instantiate(spell.spellPrefab, spawnPosition, spawnRotation);

            Debug.Log($"[VoiceSpellCastingSystem] Cast spell '{spell.voiceKeyword}' at position {hit.point} on '{hit.collider.name}'.");
        }
        else
        {
            Debug.LogWarning($"[VoiceSpellCastingSystem] Spell '{spell.voiceKeyword}' failed: no valid hit target.");
        }
    }

    private void OnDestroy()
    {
        if (keywordRecognizer != null)
        {
            keywordRecognizer.OnPhraseRecognized -= OnPhraseRecognized;

            if (keywordRecognizer.IsRunning)
                keywordRecognizer.Stop();

            keywordRecognizer.Dispose();
            Debug.Log("[VoiceSpellCastingSystem] Voice recognizer stopped.");
        }
    }
}