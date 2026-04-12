using System.Collections.Generic;
using UnityEngine;

public class PortalManager : MonoBehaviour
{
    public static PortalManager Instance { get; private set; }

    [SerializeField] private int maxPortals = 2;

    private readonly List<PortalTeleporter> activePortals = new List<PortalTeleporter>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void RegisterPortal(PortalTeleporter portal)
    {
        if (portal == null)
            return;

        if (activePortals.Count >= maxPortals)
        {
            PortalTeleporter oldestPortal = activePortals[0];

            if (oldestPortal != null)
                Destroy(oldestPortal.gameObject);

            activePortals.RemoveAt(0);
        }

        activePortals.Add(portal);
        RefreshLinks();
    }

    public void UnregisterPortal(PortalTeleporter portal)
    {
        if (portal == null)
            return;

        activePortals.Remove(portal);
        RefreshLinks();
    }

    private void RefreshLinks()
    {
        if (activePortals.Count == 0)
            return;

        if (activePortals.Count == 1)
        {
            activePortals[0].SetLinkedPortal(null);
            return;
        }

        activePortals[0].SetLinkedPortal(activePortals[1]);
        activePortals[1].SetLinkedPortal(activePortals[0]);
    }
}