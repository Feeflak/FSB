using Godot;
[Tool, GlobalClass]
public partial class StructureType : Resource
{
        /// all should implement: IStructureShape
        [Export] public Resource[] shapes;
        [Export] public float maximal_height_delta_inside_the_shapes;
        [Export(PropertyHint.Range, "0,1,0.001")] public float spawn_chance;
        [Export] public PackedScene model;
        [Export] public float base_scale;
        [Export] public float scale_change_amplitude;
        [Export] public int min_distance_from_grid_border_in_mesh_chunks;
        [Export] public int generation_attempts_per_structure_chunk;
}
