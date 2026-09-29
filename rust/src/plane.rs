// TODO: Finite airfoil
// Cam
// Sounds
// Engine
// Map
use godot::{
    classes::{Camera3D, Engine, IRigidBody3D, Input, Label, RigidBody3D},
    prelude::*,
};

use crate::wing::Wing;

#[derive(GodotClass)]
#[class(tool,init,base=RigidBody3D)]
struct Plane {
    cam_idx: usize,
    #[export]
    cameras: Array<Gd<Camera3D>>,
    #[export]
    aoa_label: OnEditor<Gd<Label>>,

    #[export]
    aoa_label_wing: OnEditor<Gd<Wing>>,
    #[export]
    speed_label: OnEditor<Gd<Label>>,

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
struct MassStats {
    pub inertia: Vector3,
    pub total_mass: f32,
    pub global_com: Vector3,
}
impl Plane {
    fn mass_stats(&self) -> MassStats {
        let mut vector_sum = Vector3::ZERO;
        let mut total_mass = 0.;
        for wing_gd in self.wings.iter_shared() {
            let wing = wing_gd.bind();
            let mass = wing.total_mass();
            total_mass += mass;
            let global_wing_com = wing_gd.get_global_position()
                + wing_gd.get_global_basis() * wing.center_of_mass_offset;
            vector_sum += (global_wing_com) * mass;
        }
        MassStats {
            inertia: -Vector3::ONE,
            global_com: vector_sum / total_mass,
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
}
struct WingEffect {
    pub force: Vector3,
    pub torque: Vector3,
    pub pos: Vector3,
}

#[godot_api]
impl IRigidBody3D for Plane {
    fn ready(&mut self) {
        self.cameras.get(self.cam_idx).unwrap().make_current();
        // GDExtension classes do not get _process called unless processing is enabled.
        self.base_mut().set_process(true);
    }

    fn process(&mut self, _delta: f64) {
        if Engine::singleton().is_editor_hint() {
            self.set_mass_stats_on_rigid_body();
        }
        if !Engine::singleton().is_editor_hint() && Input::singleton().is_action_just_pressed("cam")
        {
            self.cam_idx = (self.cam_idx + 1) % self.cameras.len();
            self.cameras.get(self.cam_idx).unwrap().make_current();
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
        for mut wing in self.wings.iter_shared() {
            wing.bind_mut()
                .draw_debug_arrows(air_speed, angular_velocity, com);
        }
    }

    fn physics_process(&mut self, _delta: f64) {
        self.set_mass_stats_on_rigid_body();

        let air_speed = self.base().get_linear_velocity();

        self.speed_label
            .set_text(format!("{}km/h", (air_speed.length() * 3.6).floor() as u32).as_str());
        self.aoa_label.set_text(
            format!(
                "{}deg",
                if air_speed.length() * 3.6 > 5. {
                    (self.aoa_label_wing.bind().aoa).floor() as u32
                } else {
                    0
                }
            )
            .as_str(),
        );

        let mut effects = Vec::with_capacity(self.wings.len());
        let mut total_aero_force = Vector3::ZERO;
        let mut total_aero_torque = Vector3::ZERO;
        let mut _inertia = Vector3::ZERO;
        let mut _center_of_mass = Vector3::ZERO;
        let mut _mass = 0;
        let base_pos = self.base().get_global_position();
        let angular_velocity = self.base().get_angular_velocity();

        let mass_stats = self.mass_stats();

        for mut wing in self.wings.iter_shared() {
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
