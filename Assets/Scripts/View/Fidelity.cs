using UnityEngine;
using UnityEngine.Rendering;

namespace LastLight.View
{
    /// <summary>
    /// Settings ▸ Graphics fidelity: one choice from Low to Ultra for everything about the picture
    /// that costs. High is the game as it was first graded. The camera, post-processing and moon
    /// follow it in <see cref="Core.Stage.ApplyFidelity"/>; the shaders read its keywords and step
    /// count, and the particle effects its density.
    /// </summary>
    public static class Fidelity
    {
        public const int Low = 0, Medium = 1, High = 2, Ultra = 3;
        public static readonly string[] Names = { "Low", "Medium", "High", "Ultra" };

        public static int Level { get; private set; } = High;

        /// <summary>The atmosphere's raymarch steps.</summary>
        public static int Steps => StepsFor(Level);
        public static int StepsFor(int level) => level switch { Low => 10, Medium => 16, Ultra => 40, _ => 24 };

        /// <summary>Particle counts and emission rates (rain, spray, smoke, wakes, sparks).</summary>
        public static float Particles => Level switch { Low => 0.5f, Medium => 0.75f, Ultra => 1.6f, _ => 1f };

        /// <summary>A count of particles at this fidelity, never below one.</summary>
        public static int Count(int high) => Mathf.Max(1, Mathf.RoundToInt(high * Particles));

        /// <summary>What a step changes, for the line under Settings.</summary>
        public static string About(int level) => level switch
        {
            Low => "Low: the cheapest fog and haze, FXAA, a quarter-size bloom, plainer glints on the sea, half the particles and no grain.",
            Medium => "Medium: lighter fog and haze, SMAA, half-size bloom and three quarters of the particles.",
            Ultra => "Ultra: moon shadows, the finest fog, temporal anti-aliasing, a finer sea, denser rain and spray, and depth of field.",
            _ => "High: the game as graded. Detailed fog and haze, SMAA and full bloom.",
        };

        static readonly GlobalKeyword LowKeyword = GlobalKeyword.Create("LL_FIDELITY_LOW");
        static readonly GlobalKeyword UltraKeyword = GlobalKeyword.Create("LL_FIDELITY_ULTRA");

        /// <summary>Sets the level and the shaders' keywords. The step count goes through
        /// <see cref="ShaderGlobals.Steps"/> unless <paramref name="steps"/> is false (-llSteps).</summary>
        public static void Set(int level, bool steps = true)
        {
            Level = Mathf.Clamp(level, Low, Ultra);
            if (steps) ShaderGlobals.Steps = Steps;
            Shader.SetKeyword(LowKeyword, Level == Low);
            Shader.SetKeyword(UltraKeyword, Level == Ultra);
        }
    }
}
