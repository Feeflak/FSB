using Godot;
[Tool, GlobalClass]
public partial class BiomeObjectsGenData : Resource
{
        [Export] TerrainObject[] trees;
        [Export] TerrainObject[] grass;
        [Export] TerrainObject[] rocks;
        [Export] FastNoiseLite rotation_noise;
        [Export] FastNoiseLite scale_noise;
        [Export] FastNoiseLite spawn_chance_noise;

        public enum GetterType
        {
                rock,
                grass,
                tree
        }

        public ObjectInstantiationData? GetObjectOfType(GetterType type, Vector3 world_pos, TerrainAspectsSolver.TerrainAspects terrain_aspects)
        {
                TerrainObject[] array_of_obj = null;
                switch (type)
                {
                        case GetterType.rock:
                                array_of_obj = rocks;
                                break;
                        case GetterType.grass:
                                array_of_obj = grass;
                                break;
                        case GetterType.tree:
                                array_of_obj = trees;
                                break;
                }
                return GetObjectInstantiationDataForPosition(world_pos, array_of_obj, terrain_aspects);
        }

        private ObjectInstantiationData? GetObjectInstantiationDataForPosition(Vector3 world_pos, TerrainObject[] obj_array, TerrainAspectsSolver.TerrainAspects terrain_aspects)
        {
                RNG.SetGDRandomSeed(world_pos);
                for (int i = 0; i < obj_array.Length; i++)
                {
                        var obj = obj_array[i];

                        if (terrain_aspects.slope > obj.max_slope)
                        {
                                continue;
                        }
                        Vector2 random_offset = new(
                            i * 75487.76656f,
                            i * 56984.02931f
                        );
                        Vector2 noise_sampling_position = new Vector2(world_pos.X, world_pos.Z) + random_offset;
                        float chance_to_spawn = obj.base_chance_to_spawn + spawn_chance_noise.GetNoise2D(noise_sampling_position.X, noise_sampling_position.Y);

                        if (GD.Randf() > chance_to_spawn)
                        {
                                continue;
                        }
                        return GetInstantiationData(world_pos, obj);
                }

                return null;
        }
        private ObjectInstantiationData GetInstantiationData(Vector3 world_pos, TerrainObject obj)
        {
                world_pos -= Vector3.Up * obj.mesh_y_offset;
                var scale = obj.base_sale + scale_noise.GetNoise2D(world_pos.X, world_pos.Z) * obj.scale_change_amplitude;
                var rotation = GetRotation(new(world_pos.X, world_pos.Z), obj.rotation_amplitude);
                return new(world_pos, rotation, Vector3.One * scale, obj);
        }

        public Vector3 GetRotation(Vector2 pos, float rotation_amplitude)
        {
                var x_noise_sample = pos;
                var y_noise_sample = pos + Vector2.One * 9172970;
                var z_noise_sample = pos + Vector2.One * 1520209;

                // side to side rotation- trees always grow at some angle
                var x_rot = rotation_amplitude * rotation_noise.GetNoise2D(x_noise_sample.X, x_noise_sample.Y);
                var z_rot = rotation_amplitude * rotation_noise.GetNoise2D(z_noise_sample.X, z_noise_sample.Y);
                // direction that the objects is facing
                var y_rot = 180 * rotation_noise.GetNoise2D(y_noise_sample.X, y_noise_sample.Y);

                return new(x_rot, y_rot, z_rot);
        }

}
