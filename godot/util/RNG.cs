
using Godot;
public static class RNG
{
        public static ulong seed_base;
        public static void SetGDRandomSeed(Vector2 pos)
        {
                GD.Seed(seed_base ^
                (ulong)Mathf.FloorToInt(pos.X) * 73856093UL ^
                (ulong)Mathf.FloorToInt(pos.Y) * 19349663UL);
        }
        public static void SetGDRandomSeed(Vector3 pos)
        {
                GD.Seed(seed_base ^
                (ulong)Mathf.FloorToInt(pos.Z) * 53876596UL ^
                (ulong)Mathf.FloorToInt(pos.X) * 73856093UL ^
                (ulong)Mathf.FloorToInt(pos.Y) * 19349663UL);
        }
}
