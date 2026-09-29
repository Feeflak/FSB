use std::{fs::File, io::read_to_string};

use anyhow::{Context, Result, bail, ensure};
use godot::{classes::Engine, prelude::*};

use crate::wing::AerodynamicData;

#[derive(GodotClass)]
#[class(tool, init, base=Resource)]
pub struct CoefficientLU {
    #[export]
    initialized: bool,
    #[export]
    debug: bool,

    #[export]
    csv_files: Array<GString>,

    // All AoA values from all Reynolds polars.
    // Re 50k  -> aoa_values[0..93]
    // Re 100k -> aoa_values[93..206]
    aoa_values: Vec<f32>,

    cl: Vec<f32>,
    cd: Vec<f32>,
    cm: Vec<f32>,

    reynold_numbers: Vec<u32>,

    // Start index of each Reynolds polar in aoa_values/cl/cd/cm.
    //
    // Example:
    //
    // offsets = [0, 93, 206, ...]
    //
    // Re 0 -> 0..93
    // Re 1 -> 93..206
    // ...
    reynold_offsets: Vec<usize>,
}

impl CoefficientLU {
    fn validate_csv_format(contents: &str) -> Result<()> {
        if !contents.contains("Alpha,Cl,Cd,Cdp,Cm,Top_Xtr,Bot_Xtr") {
            bail!(
                "the csv doesn't have the required data table format: \
                 Alpha,Cl,Cd,Cdp,Cm,Top_Xtr,Bot_Xtr"
            );
        }

        Ok(())
    }

    pub fn parse_file(&mut self, path: String) -> Result<()> {
        let contents = read_to_string(File::open(&path)?)
            .context("while reading a csv file with airfoil data")?;

        Self::validate_csv_format(&contents).context("file was invalid")?;

        let reynolds = {
            let idx = contents
                .find("Reynolds number,")
                .context("there was no reynolds number data")?;

            let str = contents
                .split_at(idx + "Reynolds number,".len())
                .1
                .split_once('\n')
                .context("malformed Reynolds number line")?
                .0
                .trim();

            str.parse::<u32>()
                .with_context(|| format!("the reynolds number was not a valid string: {str:?}"))?
        };

        ensure!(
            reynolds > 0,
            "Reynolds number must be greater than zero, found {reynolds}"
        );

        // Files need to be supplied in ascending Reynolds order.
        if let Some(&last_reynolds) = self.reynold_numbers.last() {
            ensure!(
                reynolds > last_reynolds,
                "Reynolds numbers must be strictly increasing: \
                 previous={last_reynolds}, current={reynolds}"
            );
        }

        // The current length is the start of this polar.
        self.reynold_offsets.push(self.aoa_values.len());
        self.reynold_numbers.push(reynolds);

        let mut table_lines = contents
            .split_once("Alpha,Cl,Cd,Cdp,Cm,Top_Xtr,Bot_Xtr")
            .unwrap()
            .1
            .lines();

        table_lines.next(); // skip header

        let mut local_aoa_values = Vec::new();
        let mut local_cl = Vec::new();
        let mut local_cd = Vec::new();
        let mut local_cm = Vec::new();

        for line in table_lines {
            if line.trim().is_empty() {
                continue;
            }

            let mut values = line.split(',');

            let aoa = values
                .next()
                .context("there was no aoa field in the table")?
                .trim()
                .parse::<f32>()
                .context("aoa")?;

            let cl = values
                .next()
                .context("there was no Cl field in the table")?
                .trim()
                .parse::<f32>()
                .context("Cl")?;

            let cd = values
                .next()
                .context("there was no Cd field in the table")?
                .trim()
                .parse::<f32>()
                .context("Cd")?;

            values.next(); // skip Cdp

            let cm = values
                .next()
                .context("there was no Cm field in the table")?
                .trim()
                .parse::<f32>()
                .context("Cm")?;
            local_aoa_values.push(aoa);
            local_cl.push(cl);
            local_cd.push(cd);
            local_cm.push(cm);
        }

        ensure!(
            local_aoa_values.len() >= 2,
            "Reynolds {reynolds} contains fewer than two AoA points"
        );

        // Each individual polar must have increasing AoA.
        for pair in local_aoa_values.windows(2) {
            ensure!(
                pair[1] > pair[0],
                "AoA values must be strictly increasing for \
                 Reynolds {reynolds}: {} -> {}",
                pair[0],
                pair[1]
            );
        }

        // Store the data.
        self.aoa_values.extend(local_aoa_values);
        self.cl.extend(local_cl);
        self.cd.extend(local_cd);
        self.cm.extend(local_cm);

        Ok(())
    }

    pub fn is_broken(&self) -> bool {
        self.reynold_numbers.is_empty()
    }

    pub fn init(&mut self) -> Result<()> {
        // Completely reset loaded data.
        self.aoa_values.clear();
        self.cl.clear();
        self.cd.clear();
        self.cm.clear();

        self.reynold_numbers.clear();
        self.reynold_offsets.clear();

        let files = self.csv_files.clone();

        ensure!(!files.is_empty(), "no CSV files were provided");

        for path in files.iter_shared() {
            self.parse_file(path.to_string())
                .with_context(|| format!("csv file path: {path:?}"))?;
        }

        ensure!(
            !self.reynold_numbers.is_empty(),
            "no Reynolds-number data was loaded"
        );

        Ok(())
    }

    #[inline]
    fn interpolate(low: f32, high: f32, t: f32) -> f32 {
        low + (high - low) * t
    }

    fn sample_for_reynold_index(&self, reynold_idx: usize, aoa: f32) -> AerodynamicData {
        let start = self.reynold_offsets[reynold_idx];

        let end = if reynold_idx + 1 < self.reynold_offsets.len() {
            self.reynold_offsets[reynold_idx + 1]
        } else {
            self.aoa_values.len()
        };

        let aoa_grid = &self.aoa_values[start..end];

        // Clamp AoA to the polar's range instead of extrapolating.
        // Extrapolating an airfoil polar can produce absurd coefficients
        // (e.g. CL > 400) when the simulation briefly leaves the flight envelope.
        let clamped_aoa = aoa.clamp(aoa_grid[0], aoa_grid[aoa_grid.len() - 1]);

        let high = aoa_grid
            .partition_point(|&value| value < clamped_aoa)
            .min(aoa_grid.len() - 1)
            .max(1);
        let low = high - 1;

        let aoa_low = aoa_grid[low];
        let aoa_high = aoa_grid[high];

        let t = (clamped_aoa - aoa_low) / (aoa_high - aoa_low);

        let low_idx = start + low;
        let high_idx = start + high;

        AerodynamicData {
            lift: Self::interpolate(self.cl[low_idx], self.cl[high_idx], t),

            drag: Self::interpolate(self.cd[low_idx], self.cd[high_idx], t),

            pitch: Self::interpolate(self.cm[low_idx], self.cm[high_idx], t),
        }
    }

    pub fn sample(&mut self, aoa: f32, reynold_number: u32) -> AerodynamicData {
        if !self.initialized || (!Engine::singleton().is_editor_hint() && self.is_broken()) {
            self.initialized = true;
            if let Err(err) = self.init() {
                godot_script_error!(
                    "while sampling aerodynamic data in \
                     a CoefficientLU: {err:?}"
                );

                return AerodynamicData {
                    lift: 0.0,
                    drag: 0.0,
                    pitch: 0.0,
                };
            }
        }
        if self.reynold_numbers.is_empty() {
            return AerodynamicData {
                lift: 0.,
                drag: 0.,
                pitch: 0.,
            };
        }

        let reynolds = &self.reynold_numbers;

        if reynold_number <= reynolds[0] {
            return self.sample_for_reynold_index(0, aoa);
        }
        let last_idx = reynolds.len() - 1;

        if reynold_number >= reynolds[last_idx] {
            return self.sample_for_reynold_index(last_idx, aoa);
        }
        let high_idx = reynolds.partition_point(|&re| re < reynold_number);

        let low_idx = high_idx - 1;

        let re_low = reynolds[low_idx] as f32;
        let re_high = reynolds[high_idx] as f32;
        let re = reynold_number as f32;

        // log interpolate
        let reynolds_t = (re / re_low).ln() / (re_high / re_low).ln();

        let low = self.sample_for_reynold_index(low_idx, aoa);

        let high = self.sample_for_reynold_index(high_idx, aoa);
        if self.debug {
            godot_print!(
                "Coefficients: Reynolds-number:{reynold_number} aoa:{aoa}, output:{:?}",
                AerodynamicData {
                    lift: Self::interpolate(low.lift, high.lift, reynolds_t),
                    drag: Self::interpolate(low.drag, high.drag, reynolds_t),
                    pitch: Self::interpolate(low.pitch, high.pitch, reynolds_t),
                }
            )
        }

        AerodynamicData {
            lift: Self::interpolate(low.lift, high.lift, reynolds_t),

            drag: Self::interpolate(low.drag, high.drag, reynolds_t),

            pitch: Self::interpolate(low.pitch, high.pitch, reynolds_t),
        }
    }
}
