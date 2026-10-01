pub mod wing;
pub mod coefficient_lu;
pub mod air;
pub mod plane;
pub mod cam;

use godot::prelude::*;

struct MyExtension;

#[gdextension]
unsafe impl ExtensionLibrary for MyExtension {}

use godot::classes::{IRigidBody3D, Input, RigidBody3D, Sprite2D};

#[derive(GodotClass)]
#[class(init,base=RigidBody3D)]
struct Player {
    speed: f64,

    #[init(node = "../DebugDraw3D")] // <- Path to the node in the scene tree.
    debug_draw_3D: OnReady<Gd<Node3D>>,
    #[export]
    arrow_color: Color,
    base: Base<RigidBody3D>,
}
impl Player {
    fn draw_local(&mut self, offset: Vector3, col: Color, size: f32) {
        let pos = self.base().get_global_position();
        self.draw_arrow(pos, pos, col, size);
    }
    fn draw_arrow(&mut self, a: Vector3, b: Vector3, col: Color, size: f32) {
        self.debug_draw_3D.call(
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
#[godot_api]
impl IRigidBody3D for Player {
    fn process(&mut self, delta: f64) {
        //debug_draw.draw_line(
        //    Vector3.ZERO,
        //    Vector3(1.0, 0.5, 0.0),
        //    Color(1.0, 0.85, 0.1),
        //    3.0,
        //  )
// -->         self.base_mut().apply_force_ex(force).position(transform.local_position).done();
// var torque_world = airfoil.global_basis * Vector3(0, pitch_torque, 0)
// plane.apply_torque(torque_world)

        self.draw_arrow(Vector3::ZERO, Vector3::ONE * 10., self.arrow_color, 5.);
        // In GDScript, this would be:
        // rotation += angular_speed * delta
        let input = Input::singleton();

        if input.is_action_pressed("jump") {
            godot_print!("JUMP!"); // Prints to the Godot console
            let speed = self.speed as f32;
            self.base_mut().apply_central_force(Vector3::UP * speed);
        }
    }
}
