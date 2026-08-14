use godot::{
    classes::{Engine, Input},
    prelude::*,
};

use crate::{air, coefficient_lu::CoefficientLU};

#[derive(GodotClass)]
#[class(tool,init,base=Resource)]
pub struct Flap {
    #[export]
    pub action_name_to_respond: GString,
    pub aoa_change: f32,
}
#[derive(GodotClass)]
#[class(tool,init,base=Node3D)]
pub struct Wing {
    #[export]
    pub flaps: Array<Gd<Flap>>,
    #[export]
    pub mass: f32,
    #[export]
    pub center_of_mass_offset: Vector3,

    #[export]
    pub aoa: f32,
    #[export]
    pub chord: f32,
    #[export]
    pub span: f32,

    #[export]
    pub debug_scale: f32,
    #[export]
    pub coefficient_lu: OnEditor<Gd<CoefficientLU>>,

    #[export]
    debug_draw_3d: OnEditor<Gd<Node3D>>,
    base: Base<Node3D>,
}
pub struct AerodynamicData {
    pub lift: f32,
    pub drag: f32,
    pub pitch: f32,
}

impl Wing {
    fn calc_center_of_mass(&self) {}
    fn draw_local(&mut self, offset: Vector3, col: Color, size: f32) {
        let pos = self.base().get_global_position();
        self.draw_arrow(pos, pos + offset, col, size);
    }

    fn draw_sphere(&mut self, pos: Vector3, radious: f32, color: Color) {
        self.debug_draw_3d.call(
            "draw_sphere",
            &[
                Variant::from(pos),
                Variant::from(Quaternion::IDENTITY),
                Variant::from(radious),
                Variant::from(color),
            ],
        );
    }
    fn draw_arrow(&mut self, a: Vector3, b: Vector3, col: Color, size: f32) {
        self.debug_draw_3d.call(
            "draw_line",
            &[
                Variant::from(a),
                Variant::from(b),
                Variant::from(col),
                Variant::from(size),
            ],
        );
    }
}

impl Wing {
    fn get_aerodynamic_value(&self, coefficient: f32, air_speed: Vector3) -> f32 {
        coefficient
            * air_speed.length_squared()
            * air::air_density(self.base().get_global_position().y)
            * self.chord
            * self.span
            / 2.
    }
    /// deg
    fn calc_aoa(&self, air_speed: Vector3) -> f32 {
        let mut base = self.base().get_transform().clone();
        base.origin = Vector3::ZERO;
        let local_airspeed = base.affine_inverse() * -air_speed;
        let base_aoa = -local_airspeed.y.atan2(local_airspeed.z).to_degrees();
        let mut flap_effect = 0.;
        let input = Input::singleton();
        for flap_o in self.flaps.iter_shared() {
            let flap = flap_o.bind();
            flap_effect += flap.aoa_change
                * if Engine::singleton().is_editor_hint() {
                    0.
                } else {
                    input.get_action_strength(&flap.action_name_to_respond.to_string_name())
                };
        }
        base_aoa + flap_effect
    }
    fn calc_reynold(&self, air_speed: Vector3) -> f32 {
        (air_speed.length() * self.chord)
            / air::kinematic_air_density(self.base().get_global_position().y)
    }
    pub fn calculate_aerodynamic_data(&mut self, air_speed: Vector3) -> AerodynamicData {
        let aoa = self.calc_aoa(air_speed);
        let reynolds = self.calc_reynold(air_speed).floor() as u32;
        let coefficient = self.coefficient_lu.bind_mut().sample(aoa, reynolds);
        AerodynamicData {
            lift: self.get_aerodynamic_value(coefficient.lift, air_speed),
            drag: self.get_aerodynamic_value(coefficient.drag, air_speed),
            pitch: self.get_aerodynamic_value(coefficient.pitch, air_speed),
        }
    }
    pub fn draw_debug_arrows(&mut self, air_speed: Vector3) {
        self.aoa = self.calc_aoa(air_speed);
        let data = self.calculate_aerodynamic_data(air_speed);
        let base_transform = self.base().get_transform();
        let air_dir = air_speed.try_normalized().unwrap_or(Vector3::FORWARD);
        let lift = air_dir
            .cross(Vector3::LEFT)
            .try_normalized()
            .unwrap_or(Vector3::ZERO)
            * data.lift;
        let torque = air_dir
            .cross(Vector3::DOWN)
            .try_normalized()
            .unwrap_or(Vector3::ZERO)
            * data.pitch;
        let drag = air_dir * -data.drag;

        let wing_dir = base_transform.basis.col_c();

        let global_pos = self.base().get_global_position();

        self.draw_arrow(
            global_pos,
            global_pos + wing_dir * 5.,
            Color::BLACK,
            self.debug_scale,
        );
        self.draw_arrow(
            global_pos,
            global_pos + wing_dir * 5.,
            Color::BLACK,
            self.debug_scale,
        );
        self.draw_arrow(global_pos, global_pos + drag, Color::RED, self.debug_scale);
        self.draw_arrow(
            global_pos,
            global_pos + torque,
            Color::ORANGE,
            self.debug_scale,
        );
        self.draw_arrow(global_pos, global_pos + lift, Color::AQUA, self.debug_scale);

        {
            let mut base = base_transform.clone();
            base.origin = Vector3::ZERO;
            self.draw_arrow(
                global_pos,
                global_pos + base.affine_inverse() * -air_speed,
                Color::MAGENTA,
                self.debug_scale,
            );
        }
        self.draw_arrow(
            global_pos,
            global_pos + air_speed,
            Color::DARK_BLUE,
            self.debug_scale,
        );
        self.draw_sphere(
            global_pos + self.center_of_mass_offset,
            self.debug_scale,
            Color::GREEN,
        );
    }
}
