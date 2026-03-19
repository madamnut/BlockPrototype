# Vanilla Continents Chain

Fetched on `2026-03-20` from the current `misode/mcmeta` data branch and stored here for DEEPSLATE analysis.

Relevant files:

- `worldgen/noise_settings/overworld.json`
- `worldgen/density_function/overworld/continents.json`
- `worldgen/density_function/shift_x.json`
- `worldgen/density_function/shift_z.json`
- `worldgen/noise/continentalness.json`
- `worldgen/noise/offset.json`

Current chain:

1. `noise_router.continents` -> `minecraft:overworld/continents`
2. `overworld/continents`:
   - `flat_cache(shifted_noise(...))`
   - `noise = minecraft:continentalness`
   - `shift_x = minecraft:shift_x`
   - `shift_z = minecraft:shift_z`
   - `xz_scale = 0.25`
   - `y_scale = 0.0`
3. `shift_x`:
   - `flat_cache(cache_2d(shift_a(minecraft:offset)))`
4. `shift_z`:
   - `flat_cache(cache_2d(shift_b(minecraft:offset)))`
5. `noise/continentalness`:
   - `firstOctave = -9`
   - `amplitudes = [1,1,2,2,2,1,1,1,1]`
6. `noise/offset`:
   - `firstOctave = -3`
   - `amplitudes = [1,1,1,0]`

Interpretation:

- Vanilla `continents` is not a plain 2D Perlin sample.
- It is a **warped shifted noise**:
  - sample the `continentalness` normal noise
  - but first warp X/Z using `shift_x` and `shift_z`
  - and those shift functions are themselves derived from the `offset` noise
- `flat_cache` and `cache_2d` indicate that this chain is evaluated as a 2D field and reused aggressively.
