using Godot;

public struct ObjectInstantiationData(Vector3 pos, Vector3 rot, Vector3 scale, TerrainObject obj)
{
        public Vector3 pos = pos;
        public Vector3 rot = rot;
        public Vector3 scale = scale;
        public TerrainObject obj = obj;
}

