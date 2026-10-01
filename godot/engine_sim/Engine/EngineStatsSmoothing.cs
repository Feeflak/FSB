using Godot;
using System.Collections.Generic;
[Tool]
public partial class EngineStatsSmoothing : Node {
    public EngineController engine;

    public override void _Process(double delta) {
        CalculateAndSetSmoothedData();
        base._Process(delta);
    }

    private float CalculateAverageGasTemperature() {

        float temperatureSum = 0;

        foreach (Cylinder cylinder in engine.cylinders) {
            temperatureSum += cylinder.gasTemperatureInsideCylinder;
        }
        return temperatureSum / (float)engine.cylinders.Length;
    }

    public void AddDataToSmoothing(float currentTorque) {
        AddValuesToStatisticsSmoothing(currentTorque, CalculateAverageGasTemperature());
    }
    private void AddValuesToStatisticsSmoothing(float torque, float averageGasTemperature) {

        torques.Add(torque);
        temperatures.Add(averageGasTemperature);
    }



    public int currentSmoothingFrame = 0;
    private void CalculateAndSetSmoothedData() {
        currentSmoothingFrame++;
        if (currentSmoothingFrame < averageSmoothingFrames)
            return;
        currentSmoothingFrame = 0;

        engine.averageTemperature = CalculateAverageTemperature();
        engine.averageTorque = CalculateAverageTorque();
    }

    [Export] public int averageSmoothingFrames;
    public List<float> torques = new();
    public List<float> temperatures = new();

    private float CalculateAverageTemperature() {
        float sum = 0;
        foreach (float temperature in temperatures) {
            sum += temperature;
        }
        int count = temperatures.Count;
        temperatures.Clear();
        return sum / (float)count;
    }
    private float CalculateAverageTorque() {
        float sum = 0;
        foreach (float torque in torques) {
            sum += torque;
        }
        int count = torques.Count;
        torques.Clear();
        return sum / (float)count;
    }
}

