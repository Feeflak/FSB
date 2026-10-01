use std::f32::consts::PI;

use godot::{
    classes::{Engine, IRigidBody3D, Input},
    prelude::*,
};

use crate::coefficient_lu::CoefficientLU;

#[derive(GodotClass)]
#[class(tool,init,base=Resource)]
pub struct Trim {
    #[export]
    pub positive_action_name_to_respond: GString,

    #[export]
    pub negative_action_name_to_respond: GString,

    #[export]
    pub step: f32,
    #[export]
    pub max_aoa_change: f32,
    #[export]
    pub min_aoa_change: f32,
    pub current_aoa_change: f32,
}

#[derive(GodotClass)]
#[class(tool,init,base=Resource)]
pub struct Flap {
    #[export]
    pub action_name_to_respond: GString,

    #[export]
    pub rotation: f32,
}

#[derive(GodotClass)]
#[class(tool,init,base=Node3D)]
pub struct Wing {
    // used as a base rotation for applying rotation from flap.
    base_rotation: Vector3,
    #[export]
    aerodynamic_center_chord_part: f32,
    #[export]
    rotation_direction_from_flaps: Vector3,
    #[export]
    flaps: Array<Gd<Flap>>,
    #[export]
    trims: Array<Gd<Trim>>,
    #[export]
    dry_mass: f32,
    #[export]
    pub max_fuel_mass: f32,
    pub current_fuel_mass: f32,
    #[export]
    debug_airfoil_height: f32,
    #[export]
    center_of_mass_offset: Vector3,

    pub aoa: f32,

    #[export]
    pub(crate) root_point: Vector3,
    #[export]
    root_chord: f32,
    #[export]
    pub(crate) tip_point: Vector3,
    #[export]
    tip_chord: f32,

    #[export]
    debug_scale: f32,
    #[export]
    coefficient_lu: OnEditor<Gd<CoefficientLU>>,

    #[export]
    debug_draw_3d: OnEditor<Gd<Node3D>>,
    base: Base<Node3D>,
}
#[derive(Debug)]
pub struct AerodynamicData {
    pub lift: f32,
    pub drag: f32,
    pub pitch: f32,
}

pub struct AerodynamicVectors {
    pub force: Vector3,
    pub torque: Vector3,
    pub lift: Vector3,
    pub drag: Vector3,
}

impl Wing {
    /// Geometric center of mass of the trapezoidal wing + com offset, in the wing's local space.
    pub fn center_of_mass_local(&self) -> Vector3 {
        let span = self.tip_point - self.root_point;
        let span_len = span.length();
        if span_len == 0.0 {
            return self.root_point;
        }
        let lambda = if self.root_chord == 0.0 {
            0.0
        } else {
            self.tip_chord / self.root_chord
        };
        // Distance from root along span as a fraction of the span length.
        let t = (1.0 + 2.0 * lambda) / (3.0 * (1.0 + lambda));
        let geometric_center_of_mass = self.root_point + span * t;
        geometric_center_of_mass + self.center_of_mass_offset
    }
    /// Moment of inertia about the wing's center of mass, expressed
    /// in the wing's principal axes (x = span, y = normal, z = chord).
    pub fn inertia_about_com(&self) -> Vector3 {
        let mass = self.total_mass();
        let span_len = self.tip_point.distance_to(self.root_point);
        let root_chord = self.root_chord;
        let tip_chord = self.tip_chord;
        if mass == 0.0 || span_len == 0.0 || root_chord + tip_chord == 0.0 {
            return Vector3::ZERO;
        }
        // I about the span axis (rotation around the span).
        let i_span = mass * (root_chord * root_chord + tip_chord * tip_chord) / 24.0;
        // I about the chord axis (rotation around the mean chord).
        let i_chord = mass
            * span_len
            * span_len
            * (root_chord * root_chord + 4.0 * root_chord * tip_chord + tip_chord * tip_chord)
            / (18.0 * (root_chord + tip_chord).powi(2));
        // I about the normal axis (perpendicular to the wing plane).
        let i_normal = i_span + i_chord;
        Vector3::new(i_span, i_normal, i_chord)
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

    pub fn total_mass(&self) -> f32 {
        self.dry_mass + self.current_fuel_mass
    }
    fn draw_your_shape(&mut self) {
        //  a
        //c
        //d  b
        //
        let a = self.base().get_global_position()
            + self.base().get_global_basis()
                * (self.root_point + self.root_chord / 2. * Vector3::FORWARD);
        let b = self.base().get_global_position()
            + self.base().get_global_basis()
                * (self.root_point + self.root_chord / 2. * Vector3::BACK);
        let c = self.base().get_global_position()
            + self.base().get_global_basis()
                * (self.tip_point + self.tip_chord / 2. * Vector3::FORWARD);
        let d = self.base().get_global_position()
            + self.base().get_global_basis()
                * (self.tip_point + self.tip_chord / 2. * Vector3::BACK);

        let up = (self.debug_airfoil_height / 2.) * self.base().get_global_basis().col_b();
        let color = if self.coefficient_lu.bind().is_broken() {
            Color::RED
        } else {
            Color::GREEN_YELLOW
        };

        self.draw_arrow(a - up, a + up, color, self.debug_scale);
        self.draw_arrow(b - up, b + up, color, self.debug_scale);
        self.draw_arrow(c - up, c + up, color, self.debug_scale);
        self.draw_arrow(d - up, d + up, color, self.debug_scale);

        self.draw_arrow(c + up, a + up, color, self.debug_scale);
        self.draw_arrow(b + up, a + up, color, self.debug_scale);
        self.draw_arrow(b + up, d + up, color, self.debug_scale);
        self.draw_arrow(d + up, c + up, color, self.debug_scale);

        self.draw_arrow(c - up, a - up, color, self.debug_scale);
        self.draw_arrow(b - up, a - up, color, self.debug_scale);
        self.draw_arrow(b - up, d - up, color, self.debug_scale);
        self.draw_arrow(d - up, c - up, color, self.debug_scale);
    }
    fn calc_reynold(&self, air_speed: f32, kinematic_viscosity: f32) -> f32 {
        (air_speed * self.mean_aerodynamic_chord()) / kinematic_viscosity
    }

    fn coefficients(
        &mut self,
        air_speed: f32,
        aoa: f32,
        kinematic_viscosity: f32,
    ) -> AerodynamicData {
        let reynolds = self.calc_reynold(air_speed, kinematic_viscosity).floor() as u32;
        let coefficient = self.coefficient_lu.bind_mut().sample(aoa, reynolds);

        AerodynamicData {
            lift: coefficient.lift,
            drag: coefficient.drag,
            pitch: coefficient.pitch,
        }
    }

    fn mean_aerodynamic_chord(&self) -> f32 {
        if self.root_chord == 0. {
            0.
        } else {
            let lambda = self.tip_chord / self.root_chord;
            (2. / 3.) * self.root_chord * (1. + lambda + lambda * lambda) / (1. + lambda)
        }
    }
    fn flaps_and_trims_effect(&self) -> f32 {
        if Engine::singleton().is_editor_hint() {
            0.
        } else {
            let input = Input::singleton();
            let mut effect = 0.;
            for gd in self.flaps.iter_shared() {
                let flap = gd.bind();
                effect += flap.rotation
                    * input.get_action_strength(&flap.action_name_to_respond.to_string());
            }
            for mut gd in self.trims.iter_shared() {
                let mut trim = gd.bind_mut();

                let input = input
                    .is_action_just_pressed(&trim.negative_action_name_to_respond.to_string())
                    as i32
                    - input
                        .is_action_just_pressed(&trim.positive_action_name_to_respond.to_string())
                        as i32;
                trim.current_aoa_change = (trim.current_aoa_change + trim.step * input as f32)
                    .clamp(trim.min_aoa_change, trim.max_aoa_change);

                effect += trim.current_aoa_change;
            }
            effect
        }
    }

    pub fn calculate_aerodynamic_vectors(
        &mut self,
        global_air_velocity: Vector3,
        aircraft_angular_velocity: Vector3,
        aircraft_center_of_mass_global_pos: Vector3,
        air_density: f32,
        kinematic_viscosity: f32,
    ) -> AerodynamicVectors {
        let basis = self.base().get_global_basis();
        let global_pos = self.base().get_global_position();
        let root_global = global_pos + basis * self.root_point;
        let tip_global = global_pos + basis * self.tip_point;

        let span_dir = (tip_global - root_global).normalized_or_zero();
        let forward_global = basis * Vector3::FORWARD;
        let chord_dir =
            (forward_global - span_dir * forward_global.dot(span_dir)).normalized();
        let up_global = basis * Vector3::UP;
        let mut normal_dir = span_dir.cross(chord_dir).normalized();
        if normal_dir.dot(up_global) < 0.0 {
            normal_dir = -normal_dir;
        }

        let span_len = self.tip_point.distance_to(self.root_point);

        let awg_chord = (self.root_chord + self.tip_chord) / 2.;
        let wing_area = awg_chord * span_len;

        let relative_air_velocity = {
            let aerodynamic_center_local = (self.root_point + self.tip_point) / 2.
                + chord_dir * awg_chord * self.aerodynamic_center_chord_part;
            let global_aerodynamic_center = global_pos + aerodynamic_center_local;
            let linear_velocity_from_rotation = aircraft_angular_velocity
                .cross(global_aerodynamic_center - aircraft_center_of_mass_global_pos);
            -(linear_velocity_from_rotation + global_air_velocity)
        };

        let speed = relative_air_velocity.length();
        let airflow_in_wing_plane = (relative_air_velocity
            - span_dir * relative_air_velocity.dot(span_dir))
        .normalized_or_zero();

        let mut lift_dir = span_dir.cross(airflow_in_wing_plane).normalized_or_zero();
        if lift_dir.dot(normal_dir) < 0.0 {
            lift_dir = -lift_dir;
        }

        let coefficients = {
            let velocity_normal = relative_air_velocity.dot(normal_dir);
            let velocity_chord = relative_air_velocity.dot(chord_dir);
            let aoa = velocity_normal.atan2(velocity_chord).to_degrees();
            self.aoa = aoa;

            self.coefficients(speed, aoa, kinematic_viscosity)
        };

        let q = 0.5 * air_density * speed * speed;
        let lift = lift_dir * wing_area * q * coefficients.lift;
        let drag = relative_air_velocity.normalized_or_zero() * wing_area * q * coefficients.drag;
        let torque = basis.col_a()
            * self.mean_aerodynamic_chord()
            * wing_area
            * q
            * coefficients.pitch;

        AerodynamicVectors {
            force: lift + drag,
            torque,
            lift,
            drag,
        }
    }

    pub fn draw_debug_arrows(
        &mut self,
        global_air_velocity: Vector3,
        aircraft_angular_velocity: Vector3,
        aircraft_center_of_gravity_global_pos: Vector3,
        air_density: f32,
        kinematic_viscosity: f32,
    ) {
        let vectors = self.calculate_aerodynamic_vectors(
            global_air_velocity,
            aircraft_angular_velocity,
            aircraft_center_of_gravity_global_pos,
            air_density,
            kinematic_viscosity,
        );
        self.draw_debug_visuals(&vectors, global_air_velocity);
    }

    pub fn draw_debug_visuals(&mut self, vectors: &AerodynamicVectors, global_air_velocity: Vector3) {
        self.draw_your_shape();
        let base_transform = self.base().get_global_transform();

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
            global_pos + vectors.drag,
            Color::RED,
            self.debug_scale,
        );
        self.draw_arrow(
            global_pos,
            global_pos + vectors.torque,
            Color::ORANGE,
            self.debug_scale,
        );
        self.draw_arrow(
            global_pos,
            global_pos + vectors.lift,
            Color::DARK_BLUE,
            self.debug_scale,
        );
        self.draw_arrow(
            global_pos,
            global_pos + global_air_velocity,
            Color::AQUA,
            self.debug_scale,
        );
        self.draw_sphere(
            global_pos + base_transform.basis * self.center_of_mass_local(),
            self.debug_scale / 50.,
            Color::GREEN,
        );
    }
    pub fn update_flaps_effect(&mut self) {
        if !Engine::singleton().is_editor_hint() {
            let rotation_amount = self.flaps_and_trims_effect();
            let rotation = self.base_rotation
                + self.rotation_direction_from_flaps * rotation_amount * PI / 180.0;
            self.base_mut().set_rotation(rotation);
        }
    }
}

#[godot_api]
impl INode3D for Wing {
    fn ready(&mut self) {
        self.base_rotation = self.base().get_rotation();
    }
}
