// TODO: Finite airfoil
// Sounds
// Engine
// Map
use godot::{
    classes::{Camera3D, Engine, IRigidBody3D, Input, Label, RigidBody3D},
    prelude::*,
};

use crate::{air, wing::Wing};

#[derive(GodotClass)]
#[class(tool,init,base=RigidBody3D)]
pub(crate) struct Plane {
    #[export]
    drive_train: OnEditor<Gd<Node>>,
    #[export]
    //Hz
    speed_sample_rate: f32,
    speed_sample_timer: f32,
    #[export]
    last_speed: Vector3,

    #[export]
    aoa_label_wing: OnEditor<Gd<Wing>>,

    #[export]
    thrust: f32,
    #[export]
    wings: Array<Gd<Wing>>,
    #[export]
    show_debug: bool,
    base: Base<RigidBody3D>,

    #[export]
    debug_draw_3d: OnEditor<Gd<Node3D>>,
}
pub struct UIInfo {
    pub velocity_kmh: u32,
    pub altitude_m: u32,
    pub aoa: f32,
    pub mach: f32,
    pub g: f32,
}
struct MassStats {
    pub inertia: Vector3,
    pub total_mass: f32,
    pub global_com: Vector3,
}
impl Plane {
    fn total_inertia(&self, global_com: Vector3) -> Vector3 {
        let body_basis = self.base().get_global_basis();
        let body_basis_inv = body_basis.inverse();
        let mut total_inertia = Vector3::ZERO;
        for wing_gd in self.wings.iter_shared() {
            let wing = wing_gd.bind();
            let mass = wing.total_mass();
            if mass == 0.0 {
                continue;
            }
            let wing_com_local = wing.center_of_mass_local();
            let global_wing_com =
                wing_gd.get_global_position() + wing_gd.get_global_basis() * wing_com_local;
            let body_wing_com = body_basis_inv * (global_wing_com - global_com);
            let wing_local_inertia = wing.inertia_about_com();
            let span_local = (wing.tip_point - wing.root_point).normalized_or_zero();
            let chord_ref_local = Vector3::FORWARD;
            let chord_local = (chord_ref_local - span_local * chord_ref_local.dot(span_local))
                .normalized_or_zero();
            let chord_local = if chord_local.length_squared() > 0.0 {
                chord_local
            } else {
                Vector3::BACK
            };
            let normal_local = span_local.cross(chord_local).normalized_or_zero();
            let relative_basis = body_basis_inv * wing_gd.get_global_basis();
            let u_body = relative_basis * span_local;
            let v_body = relative_basis * normal_local;
            let w_body = relative_basis * chord_local;
            let i_x = wing_local_inertia.x * u_body.x * u_body.x
                + wing_local_inertia.y * v_body.x * v_body.x
                + wing_local_inertia.z * w_body.x * w_body.x;
            let i_y = wing_local_inertia.x * u_body.y * u_body.y
                + wing_local_inertia.y * v_body.y * v_body.y
                + wing_local_inertia.z * w_body.y * w_body.y;
            let i_z = wing_local_inertia.x * u_body.z * u_body.z
                + wing_local_inertia.y * v_body.z * v_body.z
                + wing_local_inertia.z * w_body.z * w_body.z;
            let dx = body_wing_com.x;
            let dy = body_wing_com.y;
            let dz = body_wing_com.z;
            let parallel_axis = Vector3::new(
                mass * (dy * dy + dz * dz),
                mass * (dx * dx + dz * dz),
                mass * (dx * dx + dy * dy),
            );
            total_inertia += Vector3::new(i_x, i_y, i_z) + parallel_axis;
        }
        total_inertia
    }
    fn mass_stats(&self) -> MassStats {
        let mut vector_sum = Vector3::ZERO;
        let mut total_mass = 0.;
        for wing_gd in self.wings.iter_shared() {
            let wing = wing_gd.bind();
            let mass = wing.total_mass();
            total_mass += mass;
            let global_wing_com = wing_gd.get_global_position()
                + wing_gd.get_global_basis() * wing.center_of_mass_local();
            vector_sum += (global_wing_com) * mass;
        }
        let global_com = if total_mass > 0.0 {
            vector_sum / total_mass
        } else {
            self.base().get_global_position()
        };
        MassStats {
            inertia: self.total_inertia(global_com),
            global_com,
            total_mass,
        }
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

    fn set_mass_stats_on_rigid_body(&mut self) {
        let mass_stats = self.mass_stats();
        let global_pos = self.base().get_global_position();

        self.base_mut().set_mass(mass_stats.total_mass);
        self.base_mut().set_inertia(mass_stats.inertia);
        {
            let local_com =
                self.base().get_global_basis().inverse() * (mass_stats.global_com - global_pos);
            self.base_mut().set_center_of_mass(local_com);
        }
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
    fn get_velocity_kmh(&self) -> u32 {
        let rb = self.base();

        (rb.get_global_basis().inverse() * rb.get_linear_velocity() * 3.6)
            .z
            .floor() as u32
    }

    pub fn get_ui_info(&self) -> UIInfo {
        let rb = self.base();
        let velocity_kmh = self.get_velocity_kmh();
        let altitude_m = self.base().get_global_position().y;
        let aoa = if velocity_kmh > 5 {
            (self.aoa_label_wing.bind().aoa * 10.).floor() / 10.
        } else {
            0.
        };
        let speed_delta = self.last_speed - self.base().get_linear_velocity();
        let up_speed_delta = (rb.get_global_basis().inverse() * speed_delta).y;
        let g = up_speed_delta / 9.8;
        let mach = air::get_match_number(velocity_kmh as f32 / 3.6, altitude_m);

        UIInfo {
            velocity_kmh,
            g,
            aoa,
            altitude_m: altitude_m as u32,
            mach,
        }
    }
    fn set_drivetrain_properties(&mut self) {
        let altitude = self.base().get_global_position().y;
        self.drive_train
            .set("airDensity", &air::air_density(altitude).to_variant());
        self.drive_train
            .set("airTemperature", &air::air_temp(altitude).to_variant());

        let velocity = {
            let rb = self.base();
            (rb.get_global_basis().inverse() * rb.get_linear_velocity()).z
        };
        self.drive_train
            .set("frontalAirVelocity", &velocity.to_variant());
    }
}
struct WingEffect {
    pub force: Vector3,
    pub torque: Vector3,
    pub pos: Vector3,
}

#[godot_api]
impl IRigidBody3D for Plane {
    fn process(&mut self, _delta: f64) {
        if Engine::singleton().is_editor_hint() {
            self.set_mass_stats_on_rigid_body();
        }

        self.draw_sphere(
            self.base().get_global_position()
                + self.base().get_global_basis() * self.base().get_center_of_mass(),
            0.1,
            Color::GOLD,
        );
        self.draw_sphere(
            self.base().get_global_position()
                + self.base().get_global_basis() * self.base().get_center_of_mass(),
            0.1,
            Color::GOLD,
        );

        self.draw_sphere(
            self.base().get_global_position()
                + self.base().get_global_basis() * self.base().get_center_of_mass(),
            0.5,
            Color::GOLD,
        );
        let air_speed = self.base().get_linear_velocity();
        let angular_velocity = self.base().get_angular_velocity();
        let com = self.base().get_global_position() + self.base().get_center_of_mass();
        if self.show_debug {
            for mut wing in self.wings.iter_shared() {
                wing.bind_mut()
                    .draw_debug_arrows(air_speed, angular_velocity, com);
            }
        }
    }

    fn physics_process(&mut self, _delta: f64) {
        self.set_drivetrain_properties();
        let air_speed = self.base().get_linear_velocity();
        self.speed_sample_timer += _delta as f32;

        if self.speed_sample_timer > 1. / self.speed_sample_rate {
            self.speed_sample_timer = 0.;
            self.last_speed = air_speed;
        }

        self.set_mass_stats_on_rigid_body();

        let mut effects = Vec::with_capacity(self.wings.len());
        let mut total_aero_force = Vector3::ZERO;
        let mut total_aero_torque = Vector3::ZERO;
        let base_pos = self.base().get_global_position();
        let angular_velocity = self.base().get_angular_velocity();

        let mass_stats = self.mass_stats();

        for mut wing in self.wings.iter_shared() {
            wing.bind_mut().update_flaps_effect();
            let aero_vectors = wing.bind_mut().calculate_aerodynamic_vectors(
                air_speed,
                angular_velocity,
                mass_stats.global_com,
            );
            effects.push(WingEffect {
                force: aero_vectors.force,
                torque: aero_vectors.torque,
                pos: wing.get_global_position() - base_pos,
            });
            total_aero_force += aero_vectors.force;
            total_aero_torque += aero_vectors.torque;
        }

        let input = Input::singleton();
        let thrust = self.thrust
            * if Engine::singleton().is_editor_hint() {
                0.
            } else {
                input.get_action_strength("throttle")
            };

        self.draw_arrow(
            self.base().get_global_position(),
            self.base().get_global_position()
                + self.base().get_basis().col_c().normalized_or_zero() * self.thrust,
            Color::MAGENTA,
            1.,
        );
        let thrust_vector = self.base().get_global_basis().col_c().normalized_or_zero() * thrust;

        let mut rb = self.base_mut();
        rb.apply_force(thrust_vector);

        for effect in effects {
            rb.apply_force_ex(effect.force).position(effect.pos).done();
            rb.apply_torque(effect.torque);
        }
    }
}
