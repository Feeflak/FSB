using Godot;
[Tool, GlobalClass]
public partial class CylinderVisuals : Node3D {

    public Cylinder main;
    public EngineController engine;
    [Export] private MeshInstance3D gasInsideCylinder;
    [Export] private MeshInstance3D piston;

    public void UpdateMeshes() {
        float bore = engine.bore * engine.visualsScale;
        float pistonHeight = engine.pistonHeight * engine.visualsScale;
        float strokeLength = engine.strokeLength * engine.visualsScale;
        float additionalUpwardHeight = engine.additionalUpwardHeight * engine.visualsScale;
        // if (Engine.IsEditorHint())
        // {
        piston.Scale = new Vector3(bore, pistonHeight, bore);
        // }

        piston.Position = new(0, strokeLength * main.PistonPosition - pistonHeight / 2f, 0);

        var height = strokeLength + additionalUpwardHeight - strokeLength * main.PistonPosition;
        gasInsideCylinder.Position = new(0, strokeLength + additionalUpwardHeight - height / 2f, 0);
        gasInsideCylinder.Scale = new Vector3(bore, height, bore);

        var material = (ShaderMaterial)gasInsideCylinder.GetSurfaceOverrideMaterial(0);
        material.SetShaderParameter("pressure", main.currentPressure);
    }
}
