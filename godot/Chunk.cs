using Godot;

[Tool]
public partial class Chunk : StaticBody3D
{

        [Export] public MeshInstance3D mesh_instance;
        [Export] public CollisionShape3D collider;
        public int biome_map_index;
}
