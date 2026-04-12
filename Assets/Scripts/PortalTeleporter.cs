using System.Collections;
using UnityEngine;

public class PortalTeleporter : MonoBehaviour
{
    [Header("Teleport Setup")]
    [SerializeField] private Transform exitPoint;
    [SerializeField] private float reentryBlockTime = 0.5f;
    [SerializeField] private string playerTag = "Player";

    private PortalTeleporter linkedPortal;
    private bool canTeleport = true;

    private void Start()
    {
        if (exitPoint == null)
            exitPoint = transform;

        if (PortalManager.Instance != null)
            PortalManager.Instance.RegisterPortal(this);
    }

    private void OnDestroy()
    {
        if (PortalManager.Instance != null)
            PortalManager.Instance.UnregisterPortal(this);
    }

    public void SetLinkedPortal(PortalTeleporter other)
    {
        linkedPortal = other;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!canTeleport)
            return;

        if (!other.CompareTag(playerTag))
            return;

        if (linkedPortal == null)
            return;

        Transform target = linkedPortal.exitPoint != null ? linkedPortal.exitPoint : linkedPortal.transform;

        CharacterController cc = other.GetComponent<CharacterController>();
        if (cc == null)
            cc = other.GetComponentInParent<CharacterController>();

        Transform playerRoot = cc != null ? cc.transform : other.transform;

        if (cc != null)
            cc.enabled = false;

        playerRoot.position = target.position;

        if (cc != null)
            cc.enabled = true;

        StartCoroutine(BlockTeleport());
        linkedPortal.StartCoroutine(linkedPortal.BlockTeleport());
    }

    private IEnumerator BlockTeleport()
    {
        canTeleport = false;
        yield return new WaitForSeconds(reentryBlockTime);
        canTeleport = true;
    }
}