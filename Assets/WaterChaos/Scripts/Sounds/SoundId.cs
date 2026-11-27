namespace WaterChaos.Sounds
{
    /// <summary>Every sound of the game; each has its clips, volume and range in <see cref="SoundSettings"/>.</summary>
    public enum SoundId
    {
        FireBurning,
        FireOut,
        ExtinguishHiss,
        WaterSpray,
        WaterSplash,
        PumpMotor,
        PumpStart,
        PumpStop,
        AxeSwing,
        AxeHit,
        Footstep,
        TruckSiren,
        TruckBrakes,
        TruckDoor,
        Flamethrower,
        Ambience,
        ObjectiveComplete,
        StoryOpen,
        PickUp,
        HoseConnect,
        DoorBreak,
        Knockdown,

        // Menus (new values go at the end: the settings store them by position).
        UiHover,
        UiClick,
        MenuOpen,
        MenuClose,
        GameStart,
        MenuMusic,

        // Rescue mission.
        CatMeow,
    }
}
