// Stand-ins for game types whose real sources would drag half of the runtime into the offline map QA harness.
// Only the members that the compiled game files call are provided. NOT game code; Unity never compiles this folder.
using UnityEngine;

namespace PrisonersOfOmar.Rendering
{
    /// <summary>
    /// Stand-in for Rendering/PsxRenderDriver.cs (needs PsxCameraRig -> VHS presenter / camera stack). Headless: there is
    /// no world camera and no shader globals to refresh.
    /// </summary>
    public sealed class PsxRenderDriver : MonoBehaviour
    {
        public static void Ensure() { }
        public static Camera WorldCamera() => null;
        internal static void ReapplyCached() { }
    }
}
