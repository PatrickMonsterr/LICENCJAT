using System.Collections;
using UnityEngine;

public class AreaSpellEffect : MonoBehaviour
{
    [Header("Spell Effect")]
    [SerializeField] private float damagePerTick = 10f;
    [SerializeField] private float tickInterval = 0.5f;
    [SerializeField] private float radius = 2f;
    [SerializeField] private float duration = 3f;
    [SerializeField] private LayerMask targetLayers;
    [SerializeField] private Color spellHitColor = Color.red;

    private void Start()
    {
        StartCoroutine(ApplyEffectOverTime());
    }

    private IEnumerator ApplyEffectOverTime()
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            ApplyTick();
            yield return new WaitForSeconds(tickInterval);
            elapsed += tickInterval;
        }
    }

    private void ApplyTick()
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            radius,
            targetLayers,
            QueryTriggerInteraction.Ignore
        );

        foreach (Collider hit in hits)
        {
            TrainingDummy dummy = hit.GetComponentInParent<TrainingDummy>();
            if (dummy != null)
            {
                dummy.ApplySpellTick(damagePerTick, spellHitColor, tickInterval);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}