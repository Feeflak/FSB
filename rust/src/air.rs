// K
const SEA_LEVEL_TEMPERATURE: f32 = 288.15;
// K/m
const TEMP_LAPSE_RATE: f32 = 0.0065;
// K
const REFERENCE_TEMPERATURE: f32 = 273.15;
// Pa
const SEA_LEVEL_PRESSURE: f32 = 101325.;
// m/s^2
const GRAVITY: f32 = 9.80665;
// J/(Kg*K)
const GAS_CONSTANT_FOR_AIR: f32 = 287.05;
// Pa * s
const REFERENCE_DYNAMIC_VISCOSITY_OF_AIR: f32 = 1.716e-5;
// K
const SUTHERLAND_CONSTANT_FOR_GAS: f32 = 111.0;
const IDEAL_GAS_CONSTANT: f32 = 8.31446; /*R          in  J/(mol·K) */
const MOLAR_MASS_OF_DRY_AIR: f32 = 0.0289652; /*M       in kg/mol */
const ADIABATIC_INDEX_FOR_AIR: f32 = 1.4;

pub fn air_temp(altitude: f32) -> f32 {
    SEA_LEVEL_TEMPERATURE + altitude * TEMP_LAPSE_RATE
}
pub fn air_density(altitude: f32) -> f32 {
    let temp = air_temp(altitude);
    // https://en.wikipedia.org/wiki/International_Standard_Atmosphere
    let perssure = SEA_LEVEL_PRESSURE
        * (temp / SEA_LEVEL_TEMPERATURE).powf(GRAVITY / (GAS_CONSTANT_FOR_AIR * TEMP_LAPSE_RATE));
    // ideal gas law
    perssure / (GAS_CONSTANT_FOR_AIR * temp)
}
/// altitude: meter
pub fn kinematic_air_density(altitude: f32) -> f32 {
    let temp = air_temp(altitude);
    // https://en.wikipedia.org/wiki/International_Standard_Atmosphere
    let perssure = SEA_LEVEL_PRESSURE
        * (temp / SEA_LEVEL_TEMPERATURE).powf(GRAVITY / (GAS_CONSTANT_FOR_AIR * TEMP_LAPSE_RATE));
    // ideal gas law
    let density = perssure / (GAS_CONSTANT_FOR_AIR * temp);
    //  Sutherland equation
    let dynamic_viscosity = REFERENCE_DYNAMIC_VISCOSITY_OF_AIR
        * (temp / REFERENCE_TEMPERATURE).powf(1.5)
        * (REFERENCE_TEMPERATURE + SUTHERLAND_CONSTANT_FOR_GAS)
        / (temp + SUTHERLAND_CONSTANT_FOR_GAS);
    dynamic_viscosity / density
}
pub fn get_match_number(velocity: f32, altitude: f32) -> f32 {
    let temp = air_temp(altitude);

    let speed_of_sound =
        ((ADIABATIC_INDEX_FOR_AIR * IDEAL_GAS_CONSTANT * temp) / MOLAR_MASS_OF_DRY_AIR).sqrt();

    return velocity / speed_of_sound;
}
