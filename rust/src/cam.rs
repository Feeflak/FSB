use godot::{
    builtin::Side,
    classes::{Camera3D, Control, Engine, Input, Label, RigidBody3D},
    prelude::*,
};

use super::plane::Plane;

#[derive(GodotClass)]
#[class(tool,init,base=Node3D)]
struct CamManager {
    #[export]
    plane: OnEditor<Gd<Plane>>,
    cam_idx: usize,
    #[export]
    cameras: Array<Gd<Camera3D>>,

    #[export]
    cam_rotation_offset: Vector3,
    #[export]
    cam_sensitivity: Vector2,
    #[export]
    cam_max_yaw_pitch_deg: Vector2,

    #[export]
    artificial_horizon: OnEditor<Gd<Control>>,
    #[export]
    nose_direction: OnEditor<Gd<Control>>,
    #[export]
    flight_direction: OnEditor<Gd<Control>>,

    #[export]
    velocity: OnEditor<Gd<Label>>,
    #[export]
    altitude: OnEditor<Gd<Label>>,
    #[export]
    mach: OnEditor<Gd<Label>>,
    #[export]
    g: OnEditor<Gd<Label>>,
    #[export]
    aoa: OnEditor<Gd<Label>>,

    #[export]
    fps: OnEditor<Gd<Label>>,
}
impl CamManager {
    fn get_current_cam(&self) -> Gd<Camera3D> {
        self.cameras.get(self.cam_idx).unwrap()
    }

    fn position_control_based_on_unprojected_offset(
        &self,
        mut control: Gd<Control>,
        local_pos: Vector3,
        rotation: f32,
    ) {
        control.set_rotation(rotation);
        let cam = self.get_current_cam();
        let size = control.get_size();
        control.set_pivot_offset(size / 2.0);

        let cam_pos = cam.get_global_position();

        let world_pos = cam_pos + local_pos;
        let screen_pos = cam.unproject_position(world_pos);

        let viewport_center = self
            .artificial_horizon
            .get_viewport()
            .map(|v| v.get_visible_rect().size / 2.0)
            .unwrap_or(Vector2::ZERO);

        let half_size = size / 2.0;
        let left = screen_pos.x - viewport_center.x - half_size.x;
        let top = screen_pos.y - viewport_center.y - half_size.y;
        control.set_offset(Side::LEFT, left);
        control.set_offset(Side::TOP, top);
        control.set_offset(Side::RIGHT, left + size.x);
        control.set_offset(Side::BOTTOM, top + size.y);
    }

    fn draw_artificial_horizon(&mut self) {
        let cam = self.get_current_cam();
        let cam_basis = cam.get_global_transform().basis;

        let cam_forward = cam_basis * Vector3::FORWARD;
        let mut horizon_forward = cam_forward;
        horizon_forward.y = 0.0;
        horizon_forward = horizon_forward.normalized();
        self.position_control_based_on_unprojected_offset(
            self.artificial_horizon.clone(),
            horizon_forward,
            cam_basis.get_euler().z,
        );
    }

    fn draw_flight_direction(&mut self) {
        self.position_control_based_on_unprojected_offset(
            self.flight_direction.clone(),
            self.plane
                .get_linear_velocity()
                .try_normalized()
                .unwrap_or(Vector3::ZERO),
            0.,
        );
    }
    fn draw_nose_direction(&mut self) {
        self.position_control_based_on_unprojected_offset(
            self.nose_direction.clone(),
            self.plane.get_global_basis() * Vector3::FORWARD,
            0.,
        );
    }

    fn handle_cam_movement(&mut self, delta_time: f64) {
        let mut cam = self.get_current_cam();
        let cam_yaw = Input::singleton().get_action_strength("cam_yaw_right")
            - Input::singleton().get_action_strength("cam_yaw_left");
        let cam_pitch = Input::singleton().get_action_strength("cam_pitch_up")
            - Input::singleton().get_action_strength("cam_pitch_down");

        let mut rot = cam.get_rotation_degrees() - self.cam_rotation_offset
            + Vector3::new(
                cam_pitch * self.cam_sensitivity.x,
                cam_yaw * self.cam_sensitivity.y,
                0.,
            ) * (delta_time as f32);
        rot = rot.clamp(
            Vector3::new(
                -self.cam_max_yaw_pitch_deg.x,
                -self.cam_max_yaw_pitch_deg.y,
                0.,
            ),
            Vector3::new(
                self.cam_max_yaw_pitch_deg.x,
                self.cam_max_yaw_pitch_deg.y,
                0.,
            ),
        );
        cam.set_rotation_degrees(rot + self.cam_rotation_offset);
    }
}

#[godot_api]
impl INode3D for CamManager {
    fn ready(&mut self) {
        self.cameras.get(self.cam_idx).unwrap().make_current();
    }

    fn process(&mut self, delta: f64) {
        let fps = 1. / delta;
        self.fps.set_text(&(fps as u32).to_string());
        if !Engine::singleton().is_editor_hint() {
            if Input::singleton().is_action_just_pressed("cam") {
                self.cam_idx = (self.cam_idx + 1) % self.cameras.len();
                self.cameras.get(self.cam_idx).unwrap().make_current();
            }

            self.handle_cam_movement(delta);
        }

        self.draw_flight_direction();
        self.draw_artificial_horizon();
        self.draw_nose_direction();
        let ui_info = self.plane.bind().get_ui_info();
        self.altitude.set_text(&ui_info.altitude_m.to_string());
        self.velocity.set_text(&ui_info.velocity_kmh.to_string());
        self.mach
            .set_text(&((ui_info.mach * 10.).floor() / 10.).to_string());
        self.aoa
            .set_text(&((ui_info.aoa * 10.).floor() / 10.).to_string());
        self.g
            .set_text(&((ui_info.g * 10.).floor() / 10.).to_string());
    }
}
