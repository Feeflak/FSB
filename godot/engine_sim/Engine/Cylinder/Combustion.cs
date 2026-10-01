using System;
using Godot;

public class Combustion {

    public Cylinder main;

    //let's say that the gasoline is C8H18 - Octane  https://en.wikipedia.org/wiki/Octane
    // you shouldn't do half's of mol but it doesn't really matter
    // so reaction of burning it as gas goes like this:  12.5(O2) + C8H18 -> 8(CO2) + 9(H2O)
    // so per 1 mole of gas we need 12.5 moles of oxygen
    // molar mass of Octane - 114.23 g/mol
    // molar mass of oxygen - 32 g/mol
    // so ideally for 1g of oxygen we need 0,285575g of Octane

    private const float AirOxygenRatio = .21f;

    private const float AirNitrogenRatio = .79f;

    private const float OxygenFuelRatio = .285575f;
    private const float HeatFormOctaneCombustion = 44.427f; // kj/g

    //mas change:
    // Substrates mas: 12.5 * 32(O2) + 114.23(C8H18) = 514.23g
    // Products mas: 8*44(CO2)+ 9*(34) = 658

    // I think that mass shouldn't change inside chemic reaction SOOOOOOOOOOO...... https://en.wikipedia.org/wiki/Conservation_of_mass
    private const float burnMasChange = 1.2795830659f;

    private static float IdealOctaneBurnGramsFromAirGrams(float airG, out float oxygenG) {
        oxygenG = airG * AirOxygenRatio;
        return oxygenG * OxygenFuelRatio;
    }


    //WARN: I'm rounding those values so calculations might be less accurate but I don't care for now

    // So lets say that we have 100g of Air
    // Only 21g of it is oxygen and 79g is nitrogen 
    // we add 5.997 Of fuel
    // then we burn about 27g of oxygen and fuel and we get: about  1.27 times that
    // so 100 -21 + (21 + 6) * 1.28 = 113,545g is total mass of exhaust fumes in cylinder   
    //
    // and we have (21 + 6) * 1.28 = 34.545g of mixture of CO2 and H2O
    // ratio of CO2 = ( 8*44 ) / 658  = 0.535; and that's 18,482g 
    // so we have 16,063g of H2O


    public const float ExhaustFumesN2Ratio = 0.696f; //79 /113.545g
    public const float ExhaustFumesCO2Ratio = 0.163f; //18.482 /113.545g
    public const float ExhaustFumesH2ORatio = 0.141f;//16.063 /113.545g
    /// <summary> 
    /// WARN: this is not the most accurate because I assume that the burning process happens in 1 frame but that's not true at all
    ///
    /// calculates how much fuel you can burn using IdealOctaneBurnGramsFromAirGrams()
    ///  then calculates how temperature && mas will change and applies it
    /// </summary>
    public void BurnCurrentAir() {
        if (main.gasMasInsideCylinder == 0)
            return;

        float fuelMassG = IdealOctaneBurnGramsFromAirGrams(Mathf.Min(1, main.gasMasInsideCylinder - main.combustionFumesMass), out float oxygenG);
        main.gasMasInsideCylinder += fuelMassG;

        main.gasMasInsideCylinder += (1 - burnMasChange) * (fuelMassG + oxygenG);
        main.combustionFumesMass += burnMasChange * (fuelMassG + oxygenG);

        float specificHeat = SpecificHeat.GetCurrentSpecificHeat(main);

        float heatKJ = HeatFormOctaneCombustion * fuelMassG;
        float temperatureChange = heatKJ / (main.gasMasInsideCylinder / 1000f /* to kg */ * specificHeat); // it doesn't matter if this is in Kelvins or Celsius i think.....
        // GD.Print($"temperatureChange {temperatureChange} {heatKJ} {specificHeat} {main.gasMasInsideCylinder} ");
        main.gasTemperatureInsideCylinder += temperatureChange;
    }


    public const float GasConstant = 8.314f;

}
