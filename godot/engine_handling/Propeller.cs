using Godot;
using System;

public partial class Propeller : Node
{
        [Export] private float hubRadius;
        [Export] private uint bladeElements;
        [Export] private uint blades;
        [Export] private float bladeLength = 2;/* m */

        [Export] Curve chordBasedOnDistanceFromBeginningOfBlade;
        [Export] Curve bladeAngleBasedOnDistanceFromBeginningOfBladeRad;
        [Export] Curve dragBasedOnAoA;
        [Export] Curve liftBasedOnAoA;
        public void HandlePhysics(float delta, float planeForwardVelocity, float airDensity, float angularVelocityOfPropellerRad, out float outputThrust, out float dragTorque, out string angleOfAttackDebug)
        {
                outputThrust = 0;
                dragTorque = 0;
                float bladeElementDistanceRatio = bladeLength / bladeElements;
                angleOfAttackDebug = "";
                for (int element = 0; element < bladeElements; element++)
                {


                        float distanceFromBeginningOfBlade = (element + .5f) * bladeElementDistanceRatio;
                        float distanceFromCenter = hubRadius + distanceFromBeginningOfBlade;

                        float positionPercentage = (float)element / bladeElements;
                        float chord = chordBasedOnDistanceFromBeginningOfBlade.SampleBaked(positionPercentage);
                        float bladeAngle = Mathf.DegToRad(bladeAngleBasedOnDistanceFromBeginningOfBladeRad.SampleBaked(positionPercentage));

                        float elementVelocity = distanceFromCenter * angularVelocityOfPropellerRad;

                        float totalVelocity = Mathf.Sqrt(planeForwardVelocity * planeForwardVelocity + elementVelocity * elementVelocity);

                        float inflowAngle = Mathf.Atan2(planeForwardVelocity, elementVelocity);
                        float angleOfAttack = Mathf.RadToDeg(bladeAngle * inflowAngle);
                        angleOfAttackDebug += $"{angleOfAttack}      {bladeAngle} \n";
                        float liftCoefficient = liftBasedOnAoA.SampleBaked(angleOfAttack);
                        float dragCoefficient = dragBasedOnAoA.SampleBaked(angleOfAttack);

                        float forcesValue = 0.5f * airDensity * totalVelocity * totalVelocity * chord * elementVelocity;
                        float lift = forcesValue * liftCoefficient;
                        float drag = forcesValue * dragCoefficient;


                        float thrust = lift * Mathf.Cos(inflowAngle) - drag * Mathf.Sin(inflowAngle);
                        float torque = (lift * Mathf.Sin(inflowAngle) + drag * Mathf.Cos(inflowAngle)) * distanceFromCenter;

                        dragTorque += torque;
                        outputThrust += thrust;
                }

                dragTorque *= blades * delta;
                outputThrust *= blades * delta;
        }

}
