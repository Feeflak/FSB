use std::ops::Deref;

use godot::{
    global::{atan2, rad_to_deg},
    prelude::*,
};

use crate::{air, coefficient_lu::CoefficientLU};

#[derive(GodotClass)]
#[class(tool,init,base=Node3D)]
struct Wing {
    #[export]
    pub aoa: f32,
    #[export]
    pub chord: f32,
    #[export]
    pub span: f32,
    #[export]
    pub air_speed: Vector3,

    #[export]
    pub debug_line_thickness: f32,
    #[export]
    pub coefficient_lu: OnEditor<Gd<CoefficientLU>>,

    #[export]
    // <- Path to the node in the scene tree.
    debug_draw_3d: OnEditor<Gd<Node3D>>,
    base: Base<Node3D>,
}
pub struct AerodynamicData {
    pub lift: f32,
    pub drag: f32,
    pub pitch: f32,
}

impl Wing {
    fn draw_local(&mut self, offset: Vector3, col: Color, size: f32) {
        let pos = self.base().get_global_position();
        self.draw_arrow(pos, pos + offset, col, size);
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
    fn get_aerodynamic_value(&self, coefficient: f32) -> f32 {
        coefficient * self.air_speed.length_squared() * self.chord * self.span / 2.
    }
    /// deg
    fn calc_aoa(&self) -> f32 {
        let mut base = self.base().get_transform().clone();
        base.origin = Vector3::ZERO;
        let local_airspeed = base.affine_inverse() * -self.air_speed;
        let aoa = local_airspeed.y.atan2(local_airspeed.z);
        -aoa.to_degrees()
    }
    fn calc_reynold(&self) -> f32 {
        (self.air_speed.length() * self.chord)
            / air::kinematic_air_density(self.base().get_transform().origin.y)
    }
    pub fn calculate_aerodynamic_data(&mut self) -> AerodynamicData {
        let aoa = self.calc_aoa();
        let reynolds = self.calc_reynold().floor() as u32;
        let coefficient = self.coefficient_lu.bind_mut().sample(aoa, reynolds);
        AerodynamicData {
            lift: self.get_aerodynamic_value(coefficient.lift),
            drag: self.get_aerodynamic_value(coefficient.drag),
            pitch: self.get_aerodynamic_value(coefficient.pitch),
        }
    }
    fn draw_debug_arrows(&mut self) {
        self.aoa = self.calc_aoa();
        let data = self.calculate_aerodynamic_data();
        let base_transform = self.base().get_transform();
        let air_dir = self.air_speed.normalized();

        let lift = air_dir.cross(Vector3::LEFT).normalized() * data.lift;
        let torque = air_dir.cross(Vector3::DOWN).normalized() * data.pitch;
        let drag = air_dir * -data.drag;

        let wing_dir = base_transform.basis.col_c();

        self.draw_arrow(
            base_transform.origin,
            base_transform.origin + wing_dir * 5.,
            Color::BLACK,
            self.debug_line_thickness,
        );
        self.draw_arrow(
            base_transform.origin,
            base_transform.origin + drag,
            Color::RED,
            self.debug_line_thickness,
        );
        self.draw_arrow(
            base_transform.origin,
            base_transform.origin + torque,
            Color::ORANGE,
            self.debug_line_thickness,
        );
        self.draw_arrow(
            base_transform.origin,
            base_transform.origin + lift,
            Color::AQUA,
            self.debug_line_thickness,
        );

        {
            let mut base = base_transform.clone();
            base.origin = Vector3::ZERO;
            self.draw_arrow(
                base_transform.origin,
                base_transform.origin + base.affine_inverse() * -self.air_speed,
                Color::MAGENTA,
                self.debug_line_thickness,
            );
        }
        self.draw_arrow(
            base_transform.origin,
            base_transform.origin + self.air_speed,
            Color::DARK_BLUE,
            self.debug_line_thickness,
        );
    }
}
#[godot_api]
impl INode3D for Wing {
    fn process(&mut self, delta: f64) {
        self.draw_debug_arrows();
    }
}
