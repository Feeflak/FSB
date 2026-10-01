using System;
using System.Collections.Generic;
using Godot;
[Tool]
public partial class CrankshaftVisuals : Node {


    public Crankshaft main;
    public EngineController engine;

    [Export] private MeshInstance3D chankshaftMesh;
    [Export] private Node3D crankPinSpawnPoint;
    [Export] private PackedScene crankPinPrefab;

    public override void _Process(double delta) {
        if (Engine.IsEditorHint())
            SpawnCrankPins();


        UpdateCrankShaftAndPinsMeshes();
        base._Process(delta);
    }

    public void SpawnCrankPins() {
        crankPinSpawnPoint.RotationDegrees = new(0, 0, 0);
        foreach (Node3D node in crankPinSpawnPoint.GetChildren()) {
            node.QueueFree();
        }

        foreach (Cylinder cylinder in engine.cylinders) {

            var node = crankPinPrefab.Instantiate(PackedScene.GenEditState.Instance);
            crankPinSpawnPoint.AddChild(node);
            var node3d = (Node3D)node;

            node3d.RotationDegrees = new(0, 0, cylinder.angleOffset);
            node3d.Scale = new(node3d.Scale.X, main.crankPinLength * engine.visualsScale, node3d.Scale.Z);
            node3d.Position = Vector3.Back * main.GetRelativeCylinderPlacement(cylinder.cylinderIndex).Z;
        }

    }


    private void UpdateCrankShaftAndPinsMeshes() {
        chankshaftMesh.Scale = new(1, main.crankshaftLength, 1);
        chankshaftMesh.RotationDegrees = new(90, main.shaftAngleDeg, 0);
        crankPinSpawnPoint.RotationDegrees = new(0, 0, main.shaftAngleDeg);
        chankshaftMesh.Position = new Vector3(0, 0, main.crankshaftLength / 2f);
    }
}
