using System.Collections.Generic;
using Godot;
[Tool]
public partial class ObjectsGenerator : Node
{
        [Export] GroundMeshGen ground_mesh_gen;
        [Export] float minimal_tree_spacing;
        [Export(PropertyHint.Range, "0,1,0.001")] float base_tree_spawn_chance;
        [Export] int rock_spawn_attempts_per_mesh_chunk;
        [Export] int grass_spawn_attempts_per_mesh_chunk;

        public class ObjectTypeSpawnData
        {
                public int instance_count;
                public Mesh mesh;
                public float[] instance_transforms;
                public PackedScene collision_shape;
                public Vector4[] colliders_pos_scale;
                public ObjectTypeSpawnData(Mesh mesh, PackedScene collision_shape, List<ObjectInstantiationData> object_instances)
                {
                        this.mesh = mesh;
                        this.collision_shape = collision_shape;

                        const int FloatsPerPackedTransform = 12;
                        instance_count = object_instances.Count;
                        instance_transforms = new float[object_instances.Count * FloatsPerPackedTransform];
                        colliders_pos_scale = new Vector4[object_instances.Count];
                        for (int i = 0; i < object_instances.Count; i++)
                        {
                                var object_data = object_instances[i];
                                colliders_pos_scale[i] = new Vector4(object_data.pos.X, object_data.pos.Y, object_data.pos.Z, object_data.scale.X);
                                Basis basis = new(Quaternion.FromEuler(object_data.rot * Mathf.DegToRad(1.0f)));
                                basis = basis.Scaled(object_data.scale);

                                var base_index = i * FloatsPerPackedTransform;

                                instance_transforms[base_index] = basis.X.X;
                                instance_transforms[base_index + 1] = basis.Y.X;
                                instance_transforms[base_index + 2] = basis.Z.X;
                                instance_transforms[base_index + 3] = object_data.pos.X;

                                instance_transforms[base_index + 4] = basis.X.Y;
                                instance_transforms[base_index + 5] = basis.Y.Y;
                                instance_transforms[base_index + 6] = basis.Z.Y;
                                instance_transforms[base_index + 7] = object_data.pos.Y;

                                instance_transforms[base_index + 8] = basis.X.Z;
                                instance_transforms[base_index + 9] = basis.Y.Z;
                                instance_transforms[base_index + 10] = basis.Z.Z;
                                instance_transforms[base_index + 11] = object_data.pos.Z;
                        }
                }

        }

        public ObjectTypeSpawnData[] GenerateObjectsData(int chunk_size,
                BiomeGenerator.TextureData biome_data, StructureGen.StructureGrid structure_grid, Vector2 base_world_position)
        {
                chunk_size -= 2;

                RNG.SetGDRandomSeed(base_world_position);
                Dictionary<TerrainObject, List<ObjectInstantiationData>> object_instances_dictionary = [];


                TreeObjectsGenerator.GenerateObjectsForMeshChunk(base_tree_spawn_chance, minimal_tree_spacing, chunk_size,
                                  ground_mesh_gen, biome_data, base_world_position, structure_grid, ref object_instances_dictionary);

                GenerateObjectsWithoutSpacing(BiomeObjectsGenData.GetterType.grass, grass_spawn_attempts_per_mesh_chunk, chunk_size,
                                        biome_data, base_world_position, structure_grid, ref object_instances_dictionary);

                GenerateObjectsWithoutSpacing(BiomeObjectsGenData.GetterType.rock, rock_spawn_attempts_per_mesh_chunk, chunk_size,
                                        biome_data, base_world_position, structure_grid, ref object_instances_dictionary);


                var output = new ObjectTypeSpawnData[object_instances_dictionary.Count];
                int i = 0;
                foreach (var instances_for_type in object_instances_dictionary)
                {
                        output[i] = new(instances_for_type.Key.mesh, instances_for_type.Key.collision_shape, instances_for_type.Value);
                        i++;
                }

                return output;
        }
        public void GenerateObjectsWithoutSpacing(BiomeObjectsGenData.GetterType object_type, int spawn_attempts, int chunk_size,
                BiomeGenerator.TextureData biome_data, Vector2 base_world_position, StructureGen.StructureGrid structure_grid,
                ref Dictionary<TerrainObject, List<ObjectInstantiationData>> object_instances_dictionary)
        {
                for (int i = 0; i < spawn_attempts; i++)
                {
                        Vector2 uv = new(GD.Randf(), GD.Randf());
                        var world_pos_2d = uv * chunk_size + base_world_position;
                        if (!structure_grid.IsObjectValid(world_pos_2d))
                                continue;

                        var height = ground_mesh_gen.CalculateHeight(world_pos_2d, out var terrain_aspects);
                        var biomes_influence = biome_data.GetBiomeInfluenceForUV(uv);
                        Vector3 world_pos_3d = new(world_pos_2d.X, height, world_pos_2d.Y);

                        foreach (var biome_influence in biomes_influence)
                        {
                                // it gives better results - objects from different biomes overlap less
                                var influence_cubed = biome_influence.influence * biome_influence.influence * biome_influence.influence;
                                if (GD.Randf() > influence_cubed)
                                        continue;
                                var object_inst_data = biome_influence.biome.objects_data.GetObjectOfType(object_type, world_pos_3d, terrain_aspects);

                                if (object_inst_data == null)
                                        continue;

                                if (object_instances_dictionary.TryGetValue(object_inst_data.Value.obj, out var list_of_instances))
                                        list_of_instances.Add(object_inst_data.Value);
                                else
                                        object_instances_dictionary.Add(object_inst_data.Value.obj, [object_inst_data.Value]);

                                break;
                        }
                }
        }
        private static void SpawnObjectType(ObjectTypeSpawnData spawn_data, Node3D parent_for_objects)
        {
                var mesh_instance = new MultiMeshInstance3D();
                parent_for_objects.AddChild(mesh_instance);

                mesh_instance.Multimesh = new()
                {
                        Mesh = spawn_data.mesh,
                        TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                        InstanceCount = spawn_data.instance_count,
                        Buffer = spawn_data.instance_transforms
                };

                if (spawn_data.collision_shape == null)
                        return;

                foreach (var collider_data in spawn_data.colliders_pos_scale)
                {
                        var pos = new Vector3(collider_data.X, collider_data.Y, collider_data.Z);
                        var scale = collider_data.W;

                        var collision_node = (Node3D)spawn_data.collision_shape.Instantiate();
                        collision_node.Scale = Vector3.One * scale;
                        collision_node.Position = pos;
                        parent_for_objects.AddChild(collision_node);
                }

        }

        public static void SpawnObjects(ObjectTypeSpawnData[] input, Node3D parent_for_objects)
        {
                foreach (var spawn_data in input)
                {
                        SpawnObjectType(spawn_data, parent_for_objects);
                }
        }
}
