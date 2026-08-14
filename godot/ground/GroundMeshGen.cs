using Godot;
[Tool]
public partial class GroundMeshGen : Node
{
        [Export] private TerrainAspectsSolver terrain_aspects_solver;
        [Export] int resolution;

        [Export] private NoiseComponent high_frequency_noise;
        [Export] private NoiseComponent medium_frequency_noise;
        [Export] private NoiseComponent low_frequency_noise;
        [Export] private TerrainAspectEffectOnMesh moisture_effect;
        [Export] private TerrainAspectEffectOnMesh elevation_effect;
        [Export] private TerrainAspectEffectOnMesh temperature_effect;
        [Export] private TerrainAspectEffectOnMesh roughness_effect;

        public class MeshData
        {
                public Vector3[] vertices;
                public Vector3[] vertices_padded;
                public int[] indices;
                public Vector3[] normals;
                public Vector2[] uvs;
                public float[] height_map;
                public float[] tangents;
                public Vector2I base_world_pos;
        }


        private int triangles_per_dimension;
        private float triangle_size;
        private int size;

        public void ApplyData(MeshData data, MeshInstance3D mesh_instance, CollisionShape3D collider)
        {
                var arrays = new Godot.Collections.Array();
                arrays.Resize((int)Mesh.ArrayType.Max);

                arrays[(int)Mesh.ArrayType.Vertex] = data.vertices;
                arrays[(int)Mesh.ArrayType.Index] = data.indices;
                arrays[(int)Mesh.ArrayType.Normal] = data.normals;
                arrays[(int)Mesh.ArrayType.TexUV] = data.uvs;
                arrays[(int)Mesh.ArrayType.Tangent] = data.tangents;

                HeightMapShape3D shape = new()
                {
                        MapWidth = triangles_per_dimension,
                        MapDepth = triangles_per_dimension,
                        MapData = data.height_map
                };
                collider.Shape = shape;
                collider.Scale = new Vector3(triangle_size, 1, triangle_size);
                // `- size/2f` is needed because otherwise this will be the center point for the collider, and for mesh this will be the bottom left corner. 
                collider.Position = new(data.base_world_pos.X + size / 2f, 0, data.base_world_pos.Y + size / 2f);


                var mesh = new ArrayMesh();
                mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

                mesh_instance.Mesh = mesh;
        }

        public void Initialize(int size)
        {
                this.size = size;
                triangles_per_dimension = resolution + 1;
                triangle_size = size / (float)resolution;
        }
        /// The 'Initialize' function needs to be called first
        public MeshData GenerateChunkData(Vector2I base_world_pos)
        {
                var mesh_data = new MeshData
                {
                        base_world_pos = base_world_pos
                };

                GenerateUVsAndVertexes(mesh_data);
                GenerateIndices(mesh_data);
                GenerateNormals(mesh_data);
                GenerateTangents(mesh_data);
                return mesh_data;
        }
        private MeshData GenerateUVsAndVertexes(MeshData mesh_data)
        {
                int paddedWidth = triangles_per_dimension + 2;

                mesh_data.vertices_padded = new Vector3[paddedWidth * paddedWidth];
                mesh_data.vertices = new Vector3[triangles_per_dimension * triangles_per_dimension];
                mesh_data.height_map = new float[triangles_per_dimension * triangles_per_dimension];

                mesh_data.uvs = new Vector2[triangles_per_dimension * triangles_per_dimension];

                for (int x = -1; x < triangles_per_dimension + 1; x++)
                {
                        for (int z = -1; z < triangles_per_dimension + 1; z++)
                        {
                                var relative_pos = new Vector2I(x, z);
                                Vector2 worldPos = (Vector2)relative_pos * triangle_size + mesh_data.base_world_pos;
                                float height = CalculateHeight(worldPos, out _);

                                Vector3 vertex_pos = new(worldPos.X, height, worldPos.Y);

                                mesh_data.vertices_padded[x + 1 + (z + 1) * paddedWidth] = vertex_pos;

                                if (x < 0 || z < 0 || x >= triangles_per_dimension || z >= triangles_per_dimension)
                                        continue;

                                int i = x + z * triangles_per_dimension;

                                mesh_data.height_map[i] = height;
                                mesh_data.vertices[i] = vertex_pos;
                                mesh_data.uvs[i] = new Vector2(
                                    x / (float)(triangles_per_dimension - 1),
                                    z / (float)(triangles_per_dimension - 1)
                                );
                        }
                }
                return mesh_data;

        }

        private static void GenerateTangents(MeshData mesh_data)
        {
                int vertexCount = mesh_data.vertices.Length;

                var raw_tangents = new Vector3[vertexCount];
                var raw_bitangents = new Vector3[vertexCount];

                // Accumulate tangents per triangle
                for (int i = 0; i < mesh_data.indices.Length; i += 3)
                {
                        int idx0 = mesh_data.indices[i];
                        int idx1 = mesh_data.indices[i + 1];
                        int idx2 = mesh_data.indices[i + 2];

                        Vector3 v0 = mesh_data.vertices[idx0];
                        Vector3 v1 = mesh_data.vertices[idx1];
                        Vector3 v2 = mesh_data.vertices[idx2];

                        Vector2 uv0 = mesh_data.uvs[idx0];
                        Vector2 uv1 = mesh_data.uvs[idx1];
                        Vector2 uv2 = mesh_data.uvs[idx2];

                        Vector3 edge_1 = v1 - v0;
                        Vector3 edge_2 = v2 - v0;

                        float uv_delta_x1 = uv1.X - uv0.X;
                        float uv_delta_x2 = uv2.X - uv0.X;
                        float uv_delta_y1 = uv1.Y - uv0.Y;
                        float uv_delta_y2 = uv2.Y - uv0.Y;

                        float signed_area_of_triangle = uv_delta_x1 * uv_delta_y2 - uv_delta_x2 * uv_delta_y1;
                        // we check if the triangle is valid
                        if (Mathf.Abs(signed_area_of_triangle) < 1e-8f)
                                continue;

                        float inv_area_of_triangle = 1.0f / signed_area_of_triangle;

                        Vector3 tangent_dir = (edge_1 * uv_delta_y2 - edge_2 * uv_delta_y1) * inv_area_of_triangle;
                        Vector3 bitangent_dir = (edge_2 * uv_delta_x1 - edge_1 * uv_delta_x2) * inv_area_of_triangle;

                        // sum up the tangents for tech vertex in the triangle. 
                        // This will be later normalized and will result in smoother output.
                        raw_tangents[idx0] += tangent_dir;
                        raw_tangents[idx1] += tangent_dir;
                        raw_tangents[idx2] += tangent_dir;


                        raw_bitangents[idx0] += bitangent_dir;
                        raw_bitangents[idx1] += bitangent_dir;
                        raw_bitangents[idx2] += bitangent_dir;
                }


                mesh_data.tangents = new float[vertexCount * 4];
                for (int i = 0; i < vertexCount; i++)
                {
                        Vector3 normal = mesh_data.normals[i];
                        Vector3 raw_tangent = raw_tangents[i];

                        // Gram-Schmidt orthogonalization -> https://en.wikipedia.org/wiki/Gram%E2%80%93Schmidt_process
                        Vector3 normalized_tangent = (raw_tangent - normal * normal.Dot(raw_tangent)).Normalized();

                        float handedness = (normal.Cross(raw_tangent).Dot(raw_bitangents[i]) < 0.0f) ? -1.0f : 1.0f;

                        int baseIndex = i * 4;
                        mesh_data.tangents[baseIndex + 0] = normalized_tangent.X;
                        mesh_data.tangents[baseIndex + 1] = normalized_tangent.Y;
                        mesh_data.tangents[baseIndex + 2] = normalized_tangent.Z;
                        mesh_data.tangents[baseIndex + 3] = handedness;
                }

        }


        public float CalculateHeight(Vector2 world_position, out TerrainAspectsSolver.TerrainAspects terrain_aspects)
        {
                terrain_aspects = terrain_aspects_solver.SolveForPos(world_position);

                TerrainAspectEffectOnMesh.OutputData noise_amplitude_data = new();
                moisture_effect.AddEffectToOutput(terrain_aspects.moisture, ref noise_amplitude_data);
                elevation_effect.AddEffectToOutput(terrain_aspects.elevation, ref noise_amplitude_data);
                temperature_effect.AddEffectToOutput(terrain_aspects.temperature, ref noise_amplitude_data);
                roughness_effect.AddEffectToOutput(terrain_aspects.roughness, ref noise_amplitude_data);

                float output_height
                     = high_frequency_noise.Sample(world_position) * noise_amplitude_data.high_freq_noise_amplitude
                     + medium_frequency_noise.Sample(world_position) * noise_amplitude_data.medium_freq_noise_amplitude
                     + low_frequency_noise.Sample(world_position) * noise_amplitude_data.low_freq_noise_amplitude
                     + noise_amplitude_data.base_height;


                return output_height;
        }

        //  padding is needed for generating normals to avoid any seems between chunks.
        private void GenerateNormals(MeshData mesh_data)
        {
                int paddedWidth = triangles_per_dimension + 2;
                mesh_data.normals = new Vector3[triangles_per_dimension * triangles_per_dimension];

                for (int x = 0; x < triangles_per_dimension; x++)
                {
                        for (int z = 0; z < triangles_per_dimension; z++)
                        {
                                // padded indices
                                int left = x + (z + 1) * paddedWidth;
                                int right = x + 2 + (z + 1) * paddedWidth;
                                int down = x + 1 + (z + 0) * paddedWidth;
                                int up = x + 1 + (z + 2) * paddedWidth;

                                // central difference for normal
                                Vector3 normal = new Vector3(
                                    mesh_data.vertices_padded[left].Y - mesh_data.vertices_padded[right].Y,
                                    2.0f,
                                    mesh_data.vertices_padded[down].Y - mesh_data.vertices_padded[up].Y
                                ).Normalized();

                                mesh_data.normals[x + z * triangles_per_dimension] = normal;
                        }
                }

        }

        private void GenerateIndices(MeshData mesh_data)
        {

                int vertex_count = triangles_per_dimension - 1;
                mesh_data.indices = new int[vertex_count * vertex_count * 6];
                int array_index = 0;

                for (int z = 0; z < vertex_count; z++)
                {
                        for (int x = 0; x < vertex_count; x++)
                        {
                                int vertex_idx = x + z * triangles_per_dimension;
                                // counter-clockwise order. 
                                mesh_data.indices[array_index++] = vertex_idx;
                                mesh_data.indices[array_index++] = vertex_idx + 1;
                                mesh_data.indices[array_index++] = vertex_idx + triangles_per_dimension;

                                mesh_data.indices[array_index++] = vertex_idx + triangles_per_dimension + 1;
                                mesh_data.indices[array_index++] = vertex_idx + triangles_per_dimension;
                                mesh_data.indices[array_index++] = vertex_idx + 1;
                        }
                }

        }


}
