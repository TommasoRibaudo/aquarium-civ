namespace AquariumCiv.Sim.Eco
{
    /// <summary>What covers one cell of the box. Saved as a byte, so the order is fixed: new kinds go at the end.</summary>
    public enum EcoCover : byte { Bare, Grass, Tall, Bush, Seedling, Young, Tree, Reeds, Water }
}
