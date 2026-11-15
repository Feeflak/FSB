// TODO: Finite airfoil
// Sounds
// Engine
// Map
use godot::{
    classes::{Curve, Gradient},
    prelude::*,
};

use crate::{air, wing::Wing};

#[derive(GodotClass)]
#[class(tool,init,base=Node)]
pub(crate) struct HighGEffectController {
    #[export]
    wind_sound_controller: OnEditor<Gd<Node>>,
    #[export]
    gradient: OnEditor<Gd<Gradient>>,
    #[export]
    effect_change_based_on_g: OnEditor<Gd<Curve>>,
    #[export]
    effect_change_modifier: f32,
    current_effect: f32,
}
impl HighGEffectController {
    pub fn update_g_effect(&mut self, g: f32, delta_time: f32) {
        let change =
            self.effect_change_based_on_g.sample(g) * delta_time * self.effect_change_modifier;
        self.current_effect = (self.current_effect + change).clamp(0., 5.);
        self.gradient
            .set_offset(1, 1. - self.current_effect.min(1.));
        self.gradient.set_color(
            1,
            Color {
                r: 0.,
                g: 0.,
                b: 0.,
                a: 3. * self.current_effect,
            },
        );
    }
}
