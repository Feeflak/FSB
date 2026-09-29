// TODO: BETTER FLAPS, MASS && COM && INERTIA CALCULATIONS
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

    #[export]
    pub aoa_change: f32,
}
#[derive(GodotClass)]
#[class(tool,init,base=Node3D)]
pub struct Wing {
    #[export]
    pub aerodynamic_center_chord_part: f32,
    #[export]
    pub flaps: Array<Gd<Flap>>,
    #[export]
    pub dry_mass: f32,
    #[export]
    pub max_fuel_mass: f32,
    pub current_fuel_mass: f32,
    #[export]
    pub debug_airfoil_height: f32,
    #[export]
    pub center_of_mass_offset: Vector3,

    pub aoa: f32,

    #[export]
    pub root_point: Vector3,
    #[export]
    pub root_chord: f32,
    #[export]
    pub tip_point: Vector3,
    #[export]
    pub tip_chord: f32,

    #[export]
    pub debug_scale: f32,
    #[export]
    pub coefficient_lu: OnEditor<Gd<CoefficientLU>>,

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
    fn calc_reynold(&self, air_speed: f32) -> f32 {
        (air_speed * self.mean_aerodynamic_chord())
            / air::kinematic_air_density(self.base().get_global_position().y)
    }

    fn coefficients(&mut self, air_speed: f32, aoa: f32) -> AerodynamicData {
        let reynolds = self.calc_reynold(air_speed).floor() as u32;
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
    fn flaps_effect(&self) -> f32 {
        if Engine::singleton().is_editor_hint() {
            0.
        } else {
            let input = Input::singleton();
            let mut effect = 0.;
            for gd in self.flaps.iter_shared() {
                let flap = gd.bind();
                effect += flap.aoa_change
                    * input.get_action_strength(&flap.action_name_to_respond.to_string());
            }
            effect
        }
    }

    pub fn calculate_aerodynamic_vectors(
        &mut self,
        global_air_velocity: Vector3,
        aircraft_angular_velocity: Vector3,
        aircraft_center_of_mass_global_pos: Vector3,
    ) -> AerodynamicVectors {
        let basis = self.base().get_global_basis();
        let origin = self.base().get_global_position();
        let root_global = origin + basis * self.root_point;
        let tip_global = origin + basis * self.tip_point;

        let global_pos = self.base().get_global_position();

        let span_dir = (tip_global - root_global).normalized_or_zero();
        let chord_dir = (basis * Vector3::FORWARD
            - span_dir * (basis * Vector3::FORWARD).dot(span_dir))
        .normalized();
        let mut normal_dir = span_dir.cross(chord_dir).normalized();
        if normal_dir.dot(basis * Vector3::UP) < 0.0 {
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
            // deg
            let mut aoa = {
                let velocity_normal = relative_air_velocity.dot(normal_dir);
                let velocity_chord = relative_air_velocity.dot(chord_dir);
                velocity_normal.atan2(velocity_chord).to_degrees()
            };
            aoa += self.flaps_effect();

            self.aoa = aoa;

            self.coefficients(speed, aoa)
        };

        let q = 0.5 * air::air_density(global_pos.y) * speed * speed;
        let lift = lift_dir * wing_area * q * coefficients.lift;
        let drag = relative_air_velocity.normalized_or_zero() * wing_area * q * coefficients.drag;
        let torque = self.base().get_global_basis().col_a()
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
    ) {
        let vectors = self.calculate_aerodynamic_vectors(
            global_air_velocity,
            aircraft_angular_velocity,
            aircraft_center_of_gravity_global_pos,
        );
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
            global_pos + base_transform.basis * self.center_of_mass_offset,
            self.debug_scale / 50.,
            Color::GREEN,
        );
    }
}
