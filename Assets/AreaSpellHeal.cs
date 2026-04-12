using System.Collections;
using UnityEngine;

public class AreaSpellHeal : MonoBehaviour
{
    [Header("Heal Effect")]
    [SerializeField] private float healPerTick = 10f;
    [SerializeField] private float tickInterval = 0.5f;
    [SerializeField] private float radius = 2f;
    [SerializeField] private float duration = 3f;
    [SerializeField] private LayerMask targetLayers;
    [SerializeField] private Color healColor = Color.green;

    private void Start()
    {
        StartCoroutine(ApplyHealOverTime());
    }

    private IEnumerator ApplyHealOverTime()
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            ApplyHealTick();
            yield return new WaitForSeconds(tickInterval);
            elapsed += tickInterval;
        }
    }

    private void ApplyHealTick()
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
                dummy.ApplyHealTick(healPerTick, healColor, tickInterval);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}