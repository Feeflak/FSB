using Godot;
using System;
using System.Collections.Generic;
public class TickSystem {
    public float updatesPerSecond = 60;
    private float timer;
    public List<Action<float>> toCall = new();
    private float CalculateDeltaTimeForCalls => 1f / updatesPerSecond;
    public void Update(float delta /* ms */, bool debug = false) {
        timer += delta;
        float deltaForCalls = CalculateDeltaTimeForCalls;
        uint callsAmount = (uint)MathF.Floor(timer / deltaForCalls);
        timer -= callsAmount * deltaForCalls;
        if (debug)
            GD.Print($"Tick system update: callsAmount {callsAmount} delta: {delta}");
        for (uint i = 0; i < callsAmount; i++) {
            foreach (Action<float> action in toCall) {
                action(deltaForCalls);
            }
        }

    }

}
