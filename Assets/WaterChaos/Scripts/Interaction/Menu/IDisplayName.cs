namespace WaterChaos.Interaction
{
    /// <summary>
    /// Readable name of an interactive object for the interaction menu,
    /// used instead of the scene object's name.
    /// </summary>
    public interface IDisplayName
    {
        string DisplayName { get; }
    }
}
