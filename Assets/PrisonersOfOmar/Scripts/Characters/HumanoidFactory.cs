using UnityEngine;

namespace PrisonersOfOmar.Characters
{
    /// <summary>Builds the procedural low-poly characters (skinned mesh + bones + animator).</summary>
    public static class HumanoidFactory
    {
        /// <summary>
        /// Builds an animated character as a child of <paramref name="parent"/> (local origin, facing +Z).
        /// The returned rig has a <see cref="HumanoidAnimator"/>. Layer of all parts = <paramref name="layer"/>.
        /// </summary>
        public static HumanoidRig Build(CharacterSkin skin, Transform parent, int layer = Layers.Player)
        {
            var go = new GameObject("Humanoid_" + skin);
            go.layer = layer;
            go.transform.SetParent(parent, false);
            var rig = go.AddComponent<HumanoidRig>();
            rig.Skin = skin;
            go.AddComponent<HumanoidAnimator>();
            return rig;
        }

        /// <summary>
        /// Builds a static (non animated, combined MeshRenderer) human figure: mannequins and corpses used as level decoration.
        /// Origin between the feet (or at the pelvis on the ground for lying poses), facing +Z.
        /// </summary>
        public static GameObject BuildFigure(FigureKind kind, FigurePose pose, Transform parent, int seed = 0)
        {
            var go = new GameObject("Figure_" + kind);
            go.layer = Layers.Corpse;
            go.transform.SetParent(parent, false);
            return go;
        }

        public static string DisplayName(CharacterSkin skin)
        {
            switch (skin)
            {
                case CharacterSkin.Prisoner1: return "THE ATHLETE";
                case CharacterSkin.Prisoner2: return "THE GIRL IN RED";
                case CharacterSkin.Prisoner3: return "THE REDHEAD";
                case CharacterSkin.Prisoner4: return "THE NERD";
                default: return "OMAR";
            }
        }
    }
}
