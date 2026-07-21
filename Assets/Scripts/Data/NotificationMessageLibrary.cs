using UnityEngine;

[CreateAssetMenu(fileName = "NotificationMessageLibrary", menuName = "Guild Manager/Notification Message Library")]
public class NotificationMessageLibrary : ScriptableObject
{
    [Header("Day Cycle")]
    public NotificationMessageTemplate dayPlanningMessage = new NotificationMessageTemplate(NotificationSeverity.Info, "Day {day}", "Planning phase started. Ring the bell when you are ready.");
    public NotificationMessageTemplate workdayStartedMessage = new NotificationMessageTemplate(NotificationSeverity.Info, "Workday Started", "Clients are available. Time advances when you perform actions.");
    public NotificationMessageTemplate eveningMessage = new NotificationMessageTemplate(NotificationSeverity.Warning, "Evening", "No new missions can be sent. Finish what is left and go to bed.");

    [Header("Missions")]
    public NotificationMessageTemplate missionSuccessMessage = new NotificationMessageTemplate(NotificationSeverity.Success, "Mission Success", "{order}. Gold: {gold}. XP: {xp}. {casualties}");
    public NotificationMessageTemplate missionFailedMessage = new NotificationMessageTemplate(NotificationSeverity.Warning, "Mission Failed", "{order}. Gold: {gold}. XP: {xp}. {casualties}");
    public NotificationMessageTemplate orderAcceptedMessage = new NotificationMessageTemplate(NotificationSeverity.Info, "Order Accepted", "{order} has been added to the war table.");
    public NotificationMessageTemplate orderReferredMessage = new NotificationMessageTemplate(NotificationSeverity.Success, "Order Referred", "{order} was referred for a fee.");
    public NotificationMessageTemplate partySentMessage = new NotificationMessageTemplate(NotificationSeverity.Info, "Party Sent", "{hunter_count} hunter{hunter_plural} left for {order}.");

    [Header("Economy")]
    public NotificationMessageTemplate notEnoughGoldMessage = new NotificationMessageTemplate(NotificationSeverity.Warning, "Not Enough Gold", "Needed {requested_gold} gold, but only {current_gold} is available.");
    public NotificationMessageTemplate unpaidUpkeepMessage = new NotificationMessageTemplate(NotificationSeverity.Warning, "Unpaid Upkeep", "Unpaid upkeep became {unpaid_amount} debt. Reputation ranks lost: {reputation_rank_loss}. Current rank: {reputation_rank}. Mission success penalty: -{success_penalty}%.");
    public NotificationMessageTemplate upkeepCrisisMessage = new NotificationMessageTemplate(NotificationSeverity.Warning, "Upkeep Crisis", "Unpaid upkeep became {unpaid_amount} debt. Reputation ranks lost: {reputation_rank_loss}. Current rank: {reputation_rank}. Mission success penalty: -{success_penalty}%.");
    public NotificationMessageTemplate gameOverMessage = new NotificationMessageTemplate(NotificationSeverity.Warning, "Game Over", "{reason}");

    [Header("Hunters")]
    public NotificationMessageTemplate hunterLeveledUpMessage = new NotificationMessageTemplate(NotificationSeverity.Success, "Hunter Leveled Up", "{hunter} reached level {level}.");
    public NotificationMessageTemplate hunterWoundedMessage = new NotificationMessageTemplate(NotificationSeverity.Warning, "Hunter Wounded", "{hunter} returned wounded from {order}.");
    public NotificationMessageTemplate hunterDiedMessage = new NotificationMessageTemplate(NotificationSeverity.Warning, "Hunter Died", "{hunter} died on {order}.");
    public NotificationMessageTemplate hunterTreatedMessage = new NotificationMessageTemplate(NotificationSeverity.Success, "Hunter Treated", "{hunter}'s wounds have been treated.");
    public NotificationMessageTemplate hunterLeftMessage = new NotificationMessageTemplate(NotificationSeverity.Warning, "Hunter Left", "{hunter} left because the guild could not pay upkeep.");

    [Header("Clients & Hiring")]
    public NotificationMessageTemplate newClientArrivedMessage = new NotificationMessageTemplate(NotificationSeverity.Info, "New Client Arrived", "{client_label} is waiting in the guild.");
    public NotificationMessageTemplate candidateArrivedMessage = new NotificationMessageTemplate(NotificationSeverity.Info, "Candidate Arrived", "{candidate} is waiting in the guild.");
    public NotificationMessageTemplate hiringCampaignEndedMessage = new NotificationMessageTemplate(NotificationSeverity.Warning, "Hiring Campaign Ended", "The hiring campaign has ended{reason_suffix}.");
    public NotificationMessageTemplate hiringUnavailableMessage = new NotificationMessageTemplate(NotificationSeverity.Warning, "Hiring Unavailable", "Hiring campaigns can only be started during the workday.");
    public NotificationMessageTemplate hiringBlockedMessage = new NotificationMessageTemplate(NotificationSeverity.Warning, "Hiring Blocked", "The guild cannot start a hiring campaign while upkeep debt is critical.");

    [Header("Construction")]
    public NotificationMessageTemplate constructionCompletedMessage = new NotificationMessageTemplate(NotificationSeverity.Success, "Construction Completed", "{construction} is now built.");
}
