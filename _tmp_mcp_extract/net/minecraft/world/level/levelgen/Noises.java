package net.minecraft.world.level.levelgen;

import net.minecraft.core.Holder;
import net.minecraft.core.HolderGetter;
import net.minecraft.core.registries.Registries;
import net.minecraft.resources.Identifier;
import net.minecraft.resources.ResourceKey;
import net.minecraft.world.level.levelgen.synth.NormalNoise;

public class Noises {
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189269_ = m_189309_("temperature");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189278_ = m_189309_("vegetation");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189279_ = m_189309_("continentalness");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189280_ = m_189309_("erosion");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189281_ = m_189309_("temperature_large");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189282_ = m_189309_("vegetation_large");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189283_ = m_189309_("continentalness_large");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189284_ = m_189309_("erosion_large");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189285_ = m_189309_("ridge");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189286_ = m_189309_("offset");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189287_ = m_189309_("aquifer_barrier");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189288_ = m_189309_("aquifer_fluid_level_floodedness");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189289_ = m_189309_("aquifer_lava");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189290_ = m_189309_("aquifer_fluid_level_spread");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189291_ = m_189309_("pillar");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189292_ = m_189309_("pillar_rareness");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189293_ = m_189309_("pillar_thickness");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189294_ = m_189309_("spaghetti_2d");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189295_ = m_189309_("spaghetti_2d_elevation");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189296_ = m_189309_("spaghetti_2d_modulator");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189297_ = m_189309_("spaghetti_2d_thickness");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189298_ = m_189309_("spaghetti_3d_1");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189299_ = m_189309_("spaghetti_3d_2");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189300_ = m_189309_("spaghetti_3d_rarity");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189301_ = m_189309_("spaghetti_3d_thickness");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189302_ = m_189309_("spaghetti_roughness");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189243_ = m_189309_("spaghetti_roughness_modulator");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189244_ = m_189309_("cave_entrance");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189245_ = m_189309_("cave_layer");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189246_ = m_189309_("cave_cheese");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189247_ = m_189309_("ore_veininess");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189248_ = m_189309_("ore_vein_a");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189249_ = m_189309_("ore_vein_b");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189250_ = m_189309_("ore_gap");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189251_ = m_189309_("noodle");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189252_ = m_189309_("noodle_thickness");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189253_ = m_189309_("noodle_ridge_a");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189254_ = m_189309_("noodle_ridge_b");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189255_ = m_189309_("jagged");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189256_ = m_189309_("surface");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189257_ = m_189309_("surface_secondary");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189258_ = m_189309_("clay_bands_offset");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189259_ = m_189309_("badlands_pillar");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189260_ = m_189309_("badlands_pillar_roof");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189261_ = m_189309_("badlands_surface");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189262_ = m_189309_("iceberg_pillar");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189263_ = m_189309_("iceberg_pillar_roof");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189264_ = m_189309_("iceberg_surface");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189265_ = m_189309_("surface_swamp");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189266_ = m_189309_("calcite");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189267_ = m_189309_("gravel");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189268_ = m_189309_("powder_snow");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189270_ = m_189309_("packed_ice");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189271_ = m_189309_("ice");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189272_ = m_189309_("soul_sand_layer");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189273_ = m_189309_("gravel_layer");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189274_ = m_189309_("patch");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189275_ = m_189309_("netherrack");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189276_ = m_189309_("nether_wart");
    public static final ResourceKey<NormalNoise.NoiseParameters> f_189277_ = m_189309_("nether_state_selector");

    private static ResourceKey<NormalNoise.NoiseParameters> m_189309_(String p_189310_) {
        return ResourceKey.m_135790_(Registries.f_256865_, Identifier.m_438827_(p_189310_));
    }

    public static NormalNoise m_255421_(
        HolderGetter<NormalNoise.NoiseParameters> p_256362_, PositionalRandomFactory p_256306_, ResourceKey<NormalNoise.NoiseParameters> p_256639_
    ) {
        Holder<NormalNoise.NoiseParameters> holder = p_256362_.m_255043_(p_256639_);
        return NormalNoise.m_230511_(p_256306_.m_224540_(holder.m_203543_().orElseThrow().m_447358_()), holder.m_203334_());
    }
}
