using System.Collections.Generic;
using PrisonersOfOmar.Gameplay;
using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.UI
{
    /// <summary>A UI screen drawn with <see cref="VhsUI"/>.</summary>
    public abstract class UIScreen
    {
        /// <summary>Modal screens block gameplay input and unlock the cursor.</summary>
        public virtual bool Modal => true;
        /// <summary>Draw the pixel mouse cursor while this screen is on top.</summary>
        public virtual bool ShowCursor => Modal;
        /// <summary>Screens below a non-transparent screen are not drawn.</summary>
        public virtual bool Opaque => false;
        /// <summary>Frame the screen was pushed (input is ignored during that frame so the opening key does not close it).</summary>
        internal int OpenedFrame;
        public virtual void OnOpen() { }
        public virtual void OnClose() { }
        /// <summary>Draw and (if <paramref name="hasInput"/>) handle input. Called once per frame in Update.</summary>
        public abstract void Draw(VhsUI ui, bool hasInput);
    }

    /// <summary>Cached UI textures (null when missing, no log spam).</summary>
    public static class UITex
    {
        static readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();

        public static Texture2D Get(string path)
        {
            if (_cache.TryGetValue(path, out var t)) return t;
            t = Resources.Load<Texture2D>(path);
            if (t != null) { t.filterMode = FilterMode.Point; t.wrapMode = TextureWrapMode.Clamp; }
            _cache[path] = t;
            return t;
        }
    }

    /// <summary>
    /// Screen stack + HUD layer. Records UI in Update (after gameplay) and renders through the camera rig overlay.
    /// </summary>
    [DefaultExecutionOrder(500)]
    public sealed class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        public readonly VhsUI UI = new VhsUI();
        readonly List<UIScreen> _stack = new List<UIScreen>();
        PsxCameraRig _hooked;

        /// <summary>Always-drawn bottom layer (in-match HUD). Not part of the stack.</summary>
        public UIScreen Hud;

        public UIScreen Top => _stack.Count > 0 ? _stack[_stack.Count - 1] : null;
        public bool AnyModal
        {
            get { for (int i = 0; i < _stack.Count; i++) if (_stack[i].Modal) return true; return false; }
        }

        public static UIManager Create(Transform parent)
        {
            var go = new GameObject("UIManager");
            go.transform.SetParent(parent, false);
            return go.AddComponent<UIManager>();
        }

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; Unhook(); }

        public void Push(UIScreen s)
        {
            if (s == null) return;
            s.OpenedFrame = Time.frameCount;
            _stack.Add(s);
            s.OnOpen();
        }

        public void Pop()
        {
            if (_stack.Count == 0) return;
            var s = _stack[_stack.Count - 1];
            _stack.RemoveAt(_stack.Count - 1);
            s.OnClose();
        }

        public void Remove(UIScreen s)
        {
            if (_stack.Remove(s)) s.OnClose();
        }

        public void Replace(UIScreen s)
        {
            Clear();
            Push(s);
        }

        public void Clear()
        {
            while (_stack.Count > 0) Pop();
        }

        public bool Contains<T>() where T : UIScreen
        {
            for (int i = 0; i < _stack.Count; i++) if (_stack[i] is T) return true;
            return false;
        }

        public T Find<T>() where T : UIScreen
        {
            for (int i = 0; i < _stack.Count; i++) if (_stack[i] is T t) return t;
            return null;
        }

        void Hook()
        {
            var rig = PsxCameraRig.Instance;
            if (rig == _hooked) return;
            Unhook();
            if (rig == null) return;
            rig.DrawOverlay += OnOverlay;
            _hooked = rig;
        }

        void Unhook()
        {
            if (_hooked != null) _hooked.DrawOverlay -= OnOverlay;
            _hooked = null;
        }

        void OnOverlay(RenderTexture rt) => UI.Render();

        void Update()
        {
            Hook();
            var rig = PsxCameraRig.Instance;
            Vector2Int res = rig != null ? rig.Resolution : new Vector2Int(426, 240);
            Vector2 mouse = rig != null ? rig.ScreenToLowRes(Input.mousePosition) : Vector2.zero;
            bool modal = AnyModal;
            bool click = Input.GetMouseButtonDown(0) && !GameInput.CursorLocked;
            UI.BeginFrame(res.x, res.y, mouse, click);

            // which screens to draw: from the last opaque one upwards
            int first = 0;
            for (int i = _stack.Count - 1; i >= 0; i--) if (_stack[i].Opaque) { first = i; break; }

            try
            {
                if (Hud != null && first == 0 && !(_stack.Count > 0 && _stack[0].Opaque)) Hud.Draw(UI, !modal);
            }
            catch (System.Exception e) { Debug.LogException(e); }

            var top = Top;
            for (int i = first; i < _stack.Count; i++)
            {
                var s = _stack[i];
                try { s.Draw(UI, s == top && Time.frameCount > s.OpenedFrame); }
                catch (System.Exception e) { Debug.LogException(e); }
                if (i >= _stack.Count) break; // screen closed itself
            }

            UI.CursorVisible = Top != null && Top.ShowCursor;
            GameInput.GameplayBlocked = AnyModal;
            UI.EndFrame();
        }
    }
}
