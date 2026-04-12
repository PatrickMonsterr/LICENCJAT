using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class TrainingDummy : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth = 100f;

    [Header("Regeneration")]
    [SerializeField] private float regenDelay = 2f;
    [SerializeField] private float regenPerSecond = 15f;

    [Header("Visual Feedback")]
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Color defaultColor = Color.white;
    [SerializeField] private float colorHoldExtraTime = 0.1f;

    [Header("Health Bar")]
    [SerializeField] private Image healthFillImage;

    private Material runtimeMaterial;
    private Coroutine regenCoroutine;
    private Coroutine colorResetCoroutine;

    private float lastSpellAffectTime = -999f;
    private Color currentSpellColor = Color.white;

    private void Awake()
    {
        currentHealth = maxHealth;

        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();

        if (targetRenderer != null)
        {
            runtimeMaterial = targetRenderer.material;

            if (runtimeMaterial.HasProperty("_BaseColor"))
                defaultColor = runtimeMaterial.GetColor("_BaseColor");
            else if (runtimeMaterial.HasProperty("_Color"))
                defaultColor = runtimeMaterial.color;
        }

        UpdateHealthBar();
    }

    public void ApplySpellTick(float damage, Color spellColor, float tickInterval)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        UpdateHealthBar();

        currentSpellColor = spellColor;
        lastSpellAffectTime = Time.time;

        SetColor(spellColor);

        if (colorResetCoroutine != null)
            StopCoroutine(colorResetCoroutine);

        colorResetCoroutine = StartCoroutine(HoldColorUntilEffectEnds(tickInterval + colorHoldExtraTime));

        if (regenCoroutine != null)
            StopCoroutine(regenCoroutine);

        regenCoroutine = StartCoroutine(RegenerateHealthAfterDelay());
    }
    public void ApplyHealTick(float healAmount, Color spellColor, float tickInterval)
    {
        currentHealth += healAmount;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        UpdateHealthBar();

        currentSpellColor = spellColor;
        lastSpellAffectTime = Time.time;

        SetColor(spellColor);

        if (colorResetCoroutine != null)
            StopCoroutine(colorResetCoroutine);

        colorResetCoroutine = StartCoroutine(HoldColorUntilEffectEnds(tickInterval + colorHoldExtraTime));

        if (regenCoroutine != null)
            StopCoroutine(regenCoroutine);

        regenCoroutine = StartCoroutine(RegenerateHealthAfterDelay());
    }

    private IEnumerator HoldColorUntilEffectEnds(float holdTime)
    {
        yield return new WaitForSeconds(holdTime);

        if (Time.time >= lastSpellAffectTime + holdTime - 0.01f)
        {
            SetColor(defaultColor);
        }
    }

    private IEnumerator RegenerateHealthAfterDelay()
    {
        yield return new WaitForSeconds(regenDelay);

        while (currentHealth < maxHealth)
        {
            // Do not regenerate if spell ticks are still happening recently
            if (Time.time - lastSpellAffectTime < regenDelay)
            {
                yield return null;
                continue;
            }

            currentHealth += regenPerSecond * Time.deltaTime;
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
            UpdateHealthBar();
            yield return null;
        }
    }

    private void SetColor(Color color)
    {
        if (runtimeMaterial == null)
            return;

        if (runtimeMaterial.HasProperty("_BaseColor"))
            runtimeMaterial.SetColor("_BaseColor", color);
        else if (runtimeMaterial.HasProperty("_Color"))
            runtimeMaterial.color = color;
    }

    private void UpdateHealthBar()
    {
        if (healthFillImage != null)
            healthFillImage.fillAmount = currentHealth / maxHealth;
    }
}