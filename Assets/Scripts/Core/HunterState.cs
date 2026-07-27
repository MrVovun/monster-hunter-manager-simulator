public enum HunterState
{
    Idle,       // Present in guild, available for assignment
    OnMission,  // Currently on a mission, unavailable
    Dead,       // No longer usable, recorded in statistics
    Candidate,  // Visiting the guild as a recruit candidate
    Healing,    // Walking to or resting in the infirmary
    Sleeping,   // Resting in the dormitory between days
    Armory      // Temporarily posed in the armory equipment view
}
