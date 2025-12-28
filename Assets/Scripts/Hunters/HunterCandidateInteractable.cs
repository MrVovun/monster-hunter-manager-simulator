using UnityEngine;

public class HunterCandidateInteractable : Interactable
{
    private HunterRecruitmentManager recruitmentManager;
    private Hunter ownerHunter;
    private PlayerInteraction activePlayer;
    private bool awaitingRelease;

    private void Reset()
    {
        interactionPrompt = "[E] Speak";
        interactionType = InteractionType.Trigger;
        locksPlayer = true;
        useCustomCamera = false;
    }

    public void Initialize(HunterRecruitmentManager manager, HunterRecruitmentManager.RecruitmentCandidate candidate, Camera overrideCamera)
    {
        recruitmentManager = manager;
        ownerHunter = candidate != null ? candidate.spawnedHunter : null;
        customCamera = overrideCamera;
        locksPlayer = true;
        useCustomCamera = false;
    }

    public override void Interact(PlayerInteraction player)
    {
        if (recruitmentManager == null || ownerHunter == null)
        {
            return;
        }

        if (awaitingRelease) return;

        OnInteractionStart(player);
        activePlayer = player;
        awaitingRelease = true;
        if (!recruitmentManager.ShowCandidateProfile(ownerHunter, ReleaseInteraction))
        {
            ReleaseInteraction();
        }
    }

    private void ReleaseInteraction()
    {
        if (!awaitingRelease) return;
        awaitingRelease = false;
        if (activePlayer != null)
        {
            OnInteractionEnd(activePlayer);
            activePlayer = null;
        }
    }

    protected override void HandleCameraSwitch(PlayerInteraction player, bool entered)
    {
        if (recruitmentManager == null)
        {
            base.HandleCameraSwitch(player, entered);
            return;
        }

        if (player != null)
        {
            player.SetPlayerVisualsActive(!entered);
        }

        recruitmentManager.ToggleCandidateCamera(entered, player != null ? player.GetPlayerCamera() : null);
    }
}
