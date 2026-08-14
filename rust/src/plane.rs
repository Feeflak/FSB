use std::ops::{Deref, DerefMut};

use godot::{
    classes::{Engine, IRigidBody3D, Input, RigidBody3D},
    prelude::*,
};

use crate::wing::Wing;

#[derive(GodotClass)]
#[class(tool,init,base=RigidBody3D)]
struct Plane {
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
impl Plane {
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
struct WingEffect {
    pub force: Vector3,
    pub torque: Vector3,
    pub pos: Vector3,
}
#[godot_api]
impl IRigidBody3D for Plane {
    fn process(&mut self, delta: f64) {
        let air_speed = self.base().get_linear_velocity();
        let mut effects = Vec::with_capacity(self.wings.len());
        let mut inertia = Vector3::ZERO;
        let mut center_of_mass = Vector3::ZERO;
        let mut mass = 0;
        for mut wing in self.wings.iter_shared() {
            if self.show_debug {
                wing.bind_mut().draw_debug_arrows(air_speed);
            }
            let aero_data = wing.bind_mut().calculate_aerodynamic_data(air_speed);
            let force = wing.get_basis().inverse()
                * Vector3 {
                    x: 0.,
                    y: aero_data.lift,
                    z: -aero_data.drag,
                };
            let torque = wing.get_basis().inverse() * Vector3::UP * aero_data.pitch;
            effects.push(WingEffect {
                force,
                torque,
                pos: wing.get_position(),
            });
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
                + self.base().get_basis().col_c().normalized() * self.thrust,
            Color::MAGENTA,
            1.,
        );
        let thrust_vector = self.base().get_basis().col_c().normalized() * thrust;

        let mut rb = self.base_mut();
        rb.apply_force(thrust_vector);

        for effect in effects {
            rb.apply_force_ex(effect.force).position(effect.pos).done();
            rb.apply_torque(effect.torque);
        }
    }
}
