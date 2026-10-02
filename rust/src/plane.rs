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
    #[init(val = 10.0)]
    //Hz
    speed_sample_rate: f32,
    speed_sample_timer: f32,
    #[export]
    last_speed: Vector3,

    #[export]
    aoa_label_wing: OnEditor<Gd<Wing>>,

    #[export]
    global_drag_modifier: f32,

    #[export]
    global_lift_modifier: f32,

    #[export]
    #[init(val = 1.0)]
    global_induced_drag_modifier: f32,

    #[export]
    wings: Array<Gd<Wing>>,
    #[export]
    show_debug: bool,
    base: Base<RigidBody3D>,

    #[export]
    debug_draw_3d: OnEditor<Gd<Node3D>>,

    // Cached to avoid recomputing every physics frame. Mass/inertia change
    // slowly (mostly fuel burn), so we refresh at a low fixed rate.
    #[export]
    #[init(val = 1.0)]
    mass_stats_update_rate: f32,
    mass_stats_timer: f32,
    cached_mass_stats: Option<MassStats>,

    // Reused between physics_process and process for debug drawing.
    last_wing_effects: Vec<WingEffect>,

    #[export]
    #[init(val = 500.0)]
    aero_update_rate: f32,
    aero_timer: f32,
    cached_aero_effects: Vec<WingEffect>,

    g_force: f32,
}
pub struct UIInfo {
    pub velocity_kmh: u32,
    pub altitude_m: u32,
    pub aoa: f32,
    pub mach: f32,
    pub g: f32,
}
#[derive(Clone)]
struct MassStats {
    pub inertia: Vector3,
    pub total_mass: f32,
    /// Center of mass in the plane's local space. Cached because it only
    /// changes with fuel/mass distribution, but transform it with the current
    /// body basis each frame to get a fresh global COM.
    pub local_com: Vector3,
}
impl Plane {
    fn total_inertia(&self, local_com: Vector3) -> Vector3 {
        let body_basis = self.base().get_global_basis();
        let body_basis_inv = body_basis.inverse();
        let body_pos = self.base().get_global_position();
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
            let local_wing_com = body_basis_inv * (global_wing_com - body_pos);
            let body_wing_com = local_wing_com - local_com;
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
        let body_basis = self.base().get_global_basis();
        let body_basis_inv = body_basis.inverse();
        let body_pos = self.base().get_global_position();

        let mut vector_sum = Vector3::ZERO;
        let mut total_mass = 0.;
        for wing_gd in self.wings.iter_shared() {
            let wing = wing_gd.bind();
            let mass = wing.total_mass();
            if mass == 0.0 {
                continue;
            }
            total_mass += mass;
            let global_wing_com = wing_gd.get_global_position()
                + wing_gd.get_global_basis() * wing.center_of_mass_local();
            let local_wing_com = body_basis_inv * (global_wing_com - body_pos);
            vector_sum += local_wing_com * mass;
        }
        let local_com = if total_mass > 0.0 {
            vector_sum / total_mass
        } else {
            Vector3::ZERO
        };

        MassStats {
            inertia: self.total_inertia(local_com),
            total_mass,
            local_com,
        }
    }

    fn update_cached_mass_stats(&mut self, delta: f32) -> MassStats {
        self.mass_stats_timer += delta;
        let rate = self.mass_stats_update_rate.max(0.001);
        let should_update = self.cached_mass_stats.is_none() || self.mass_stats_timer > 1.0 / rate;
        if should_update {
            self.mass_stats_timer = 0.0;
            self.cached_mass_stats = Some(self.mass_stats());
        }
        self.cached_mass_stats.clone().unwrap()
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

    fn set_mass_stats_on_rigid_body(&mut self, mass_stats: &MassStats) {
        self.base_mut().set_mass(mass_stats.total_mass);
        self.base_mut().set_inertia(mass_stats.inertia);
        self.base_mut().set_center_of_mass(mass_stats.local_com);
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
        let velocity_kmh = self.get_velocity_kmh();
        let altitude_m = self.base().get_global_position().y;
        let aoa = if velocity_kmh > 5 {
            (self.aoa_label_wing.bind().aoa * 10.).floor() / 10.
        } else {
            0.
        };

        let g = self.g_force;
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
#[derive(Clone)]
struct WingEffect {
    pub force: Vector3,
    pub torque: Vector3,
    pub lift: Vector3,
    pub drag: Vector3,
    pub pos: Vector3,
}

#[godot_api]
impl IRigidBody3D for Plane {
    fn ready(&mut self) {
        // Start the speed sample from the current velocity so the G-force
        // readout doesn't spike on the first frame.
        self.last_speed = self.base().get_linear_velocity();
    }

    fn process(&mut self, _delta: f64) {
        let is_editor = Engine::singleton().is_editor_hint();

        // In the editor refresh mass stats every frame so the inspector edits
        // are reflected immediately. At runtime they are updated in
        // physics_process at a much lower rate.
        if is_editor {
            let stats = self.mass_stats();
            self.set_mass_stats_on_rigid_body(&stats);
            self.cached_mass_stats = Some(stats);
        } else if self.cached_mass_stats.is_none() {
            self.cached_mass_stats = Some(self.mass_stats());
        }

        let base_pos = self.base().get_global_position();
        let base_basis = self.base().get_global_basis();
        let mass_stats = self.cached_mass_stats.clone().unwrap();
        let com_global = base_pos + base_basis * mass_stats.local_com;

        let air_speed = self.base().get_linear_velocity();
        if self.show_debug {
            self.draw_sphere(com_global, 0.1, Color::GOLD);
            self.draw_sphere(com_global, 0.1, Color::GOLD);
            self.draw_sphere(com_global, 0.5, Color::GOLD);
            for (mut wing, effect) in self.wings.iter_shared().zip(self.last_wing_effects.iter()) {
                let vectors = crate::wing::AerodynamicVectors {
                    torque: effect.torque,
                    lift: effect.lift,
                    drag: effect.drag,
                };
                wing.bind_mut().draw_debug_visuals(&vectors, air_speed);
            }
        }
    }

    fn physics_process(&mut self, _delta: f64) {
        let is_editor = Engine::singleton().is_editor_hint();
        let delta_f = _delta as f32;

        self.set_drivetrain_properties();
        let air_speed = self.base().get_linear_velocity();
        self.speed_sample_timer += delta_f;

        if self.speed_sample_timer > 1. / self.speed_sample_rate {
            self.speed_sample_timer = 0.;
            let sample_period = 1. / self.speed_sample_rate;
            let speed_delta = air_speed - self.last_speed;
            let world_accel = speed_delta / sample_period;
            let gravity = Vector3::new(0.0, -9.8, 0.0);
            let proper_accel_local =
                self.base().get_global_basis().inverse() * (world_accel - gravity);
            self.g_force = proper_accel_local.y / 9.8;
            self.last_speed = air_speed;
        }

        // Cache body transform and air properties once per frame. All wings
        // share roughly the same altitude, so one density/viscosity sample is
        // enough and avoids per-wing exponential/log calls.
        let base_pos = self.base().get_global_position();
        let base_basis = self.base().get_global_basis();
        let air_density = air::air_density(base_pos.y);
        let kinematic_viscosity = air::kinematic_air_density(base_pos.y);

        let mass_stats = self.update_cached_mass_stats(delta_f);
        if !is_editor {
            self.set_mass_stats_on_rigid_body(&mass_stats);
        }

        // Fresh global COM from cached local COM + current body transform.
        let current_com_global = base_pos + base_basis * mass_stats.local_com;

        self.aero_timer += delta_f;
        let aero_rate = self.aero_update_rate.max(0.001);
        let recompute_aero = self.aero_timer >= 1.0 / aero_rate;
        if recompute_aero {
            self.aero_timer = 0.0;
            self.cached_aero_effects.clear();
            self.cached_aero_effects.reserve(self.wings.len());

            for mut wing in self.wings.iter_shared() {
                wing.bind_mut().induced_drag_multiplier = self.global_induced_drag_modifier;
            }

            let angular_velocity = self.base().get_angular_velocity();
            for mut wing in self.wings.iter_shared() {
                let mut wing_bind = wing.bind_mut();
                wing_bind.update_flaps_effect();
                let aero_vectors = wing_bind.calculate_aerodynamic_vectors(
                    air_speed,
                    angular_velocity,
                    current_com_global,
                    air_density,
                    kinematic_viscosity,
                );
                let pos = wing_bind.base().get_global_position() - base_pos;
                self.cached_aero_effects.push(WingEffect {
                    force: aero_vectors.lift * self.global_lift_modifier
                        + aero_vectors.drag * self.global_drag_modifier,
                    torque: aero_vectors.torque,
                    lift: aero_vectors.lift * self.global_lift_modifier,
                    drag: aero_vectors.drag * self.global_drag_modifier,
                    pos,
                });
            }
        }

        let input = Input::singleton();

        let effects = self.cached_aero_effects.clone();
        let mut rb = self.base_mut();

        for effect in &effects {
            rb.apply_force_ex(effect.force).position(effect.pos).done();
            rb.apply_torque(effect.torque);
        }
        drop(rb);

        self.last_wing_effects = effects;
    }
}
