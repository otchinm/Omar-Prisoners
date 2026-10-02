using UnityEngine;

namespace PrisonersOfOmar.Map
{
    /// <summary>
    /// Builds "the Base of the Second Class" (the only map) at runtime. Deterministic for a given seed.
    /// Also sets the environment (fog / ambient / sky) for the match.
    /// </summary>
    public static class MapBuilder
    {
        public static MapData Build(int seed, Transform parent)
        {
            var root = new GameObject("Map_BaseOfTheSecondClass").transform;
            root.SetParent(parent, false);
            var data = new MapData { Root = root, Seed = seed, Nav = new NavGraph() };
            data.OmarSpawn = new Pose(new Vector3(0, 0, 5), Quaternion.identity);
            for (int i = 0; i < 4; i++) data.PrisonerSpawns.Add(new Pose(new Vector3(i * 2f, 0, 0), Quaternion.identity));
            data.PlayableBounds = new Bounds(Vector3.zero, new Vector3(300, 60, 300));
            return data;
        }
    }

    /// <summary>
    /// Small dark room shown behind the main menu (the player sits in it holding a lit lighter).
    /// </summary>
    public static class MenuSceneBuilder
    {
        /// <summary>Builds the menu room under <paramref name="parent"/> and returns the camera pose to use.</summary>
        public static Transform Build(Transform parent, out Pose cameraPose)
        {
            var root = new GameObject("MenuRoom").transform;
            root.SetParent(parent, false);
            cameraPose = new Pose(new Vector3(0, 1.5f, 0), Quaternion.identity);
            return root;
        }
    }
}
