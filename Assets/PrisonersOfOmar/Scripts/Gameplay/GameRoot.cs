using System.Collections;
using PrisonersOfOmar.Audio;
using PrisonersOfOmar.Characters;
using PrisonersOfOmar.Map;
using PrisonersOfOmar.Rendering;
using PrisonersOfOmar.UI;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    /// <summary>
    /// Creates the persistent game objects as soon as any scene loads (the project's scene is empty).
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (GameRoot.Instance != null) return;
            // silence anything the scene might contain (default camera / light / listener)
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) cam.gameObject.SetActive(false);
            foreach (var l in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) l.enabled = false;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) l.enabled = false;
            var go = new GameObject("[PrisonersOfOmar]");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<GameRoot>();
        }
    }

    /// <summary>Top level flow: boot -> menu -> lobby -> loading -> match -> ending -> lobby / menu.</summary>
    public sealed class GameRoot : MonoBehaviour
    {
        public static GameRoot Instance { get; private set; }

        public NetSession Session { get; private set; }
        public UIManager UIM { get; private set; }
        MatchWorld _match;
        MenuScene _menu;

        void Awake()
        {
            Instance = this;
            Application.runInBackground = true; // multiplayer must keep running when unfocused
            Settings.Load();
            ConfigurePhysics();
            QualitySettings.antiAliasing = 0;
            QualitySettings.shadows = ShadowQuality.Disable;
            RenderSettings.fog = false;
            RenderSettings.ambientLight = Color.black;

            var rig = PsxCameraRig.Create();
            rig.InternalHeight = Settings.InternalHeight;
            AudioManager.Create(transform);
            UIM = UIManager.Create(transform);
            Session = NetSession.Create(transform);
            Session.MatchStarting += OnMatchStarting;
            Session.MatchBegan += OnMatchBegan;
            Session.ReturnedToLobby += OnReturnedToLobby;
            Session.SessionEnded += OnSessionEnded;
            Settings.ApplyDisplay();

            EnsureMenuScene();
            UIM.Replace(new BootScreen());
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        static void ConfigurePhysics()
        {
            Physics.IgnoreLayerCollision(Layers.Item, Layers.Player, true);
            Physics.IgnoreLayerCollision(Layers.Item, Layers.Omar, true);
            Physics.IgnoreLayerCollision(Layers.Item, Layers.Item, true);
            Physics.IgnoreLayerCollision(Layers.Foliage, Layers.Player, true);
            Physics.IgnoreLayerCollision(Layers.Foliage, Layers.Omar, true);
            Physics.IgnoreLayerCollision(Layers.Corpse, Layers.Player, false);
            // Omar walks through unlocked door leaves and shoves them open (locked leaves move to World)
            Physics.IgnoreLayerCollision(Layers.Omar, Layers.Door, true);
            Physics.queriesHitTriggers = false;
        }

        // ------------------------------------------------------------------ menu background

        void EnsureMenuScene()
        {
            if (_menu != null) return;
            var go = new GameObject("MenuScene");
            go.transform.SetParent(transform, false);
            _menu = go.AddComponent<MenuScene>();
        }

        void DestroyMenuScene()
        {
            if (_menu != null) Destroy(_menu.gameObject);
            _menu = null;
        }

        void DestroyMatch()
        {
            if (_match != null) Destroy(_match.gameObject);
            _match = null;
            UIM.Hud = null;
        }

        // ------------------------------------------------------------------ actions from the UI

        public void HostGame(bool practice)
        {
            if (!Session.Host(Settings.PlayerName, Settings.Port, practice))
            {
                UIM.Push(new MessageScreen("CAN'T HOST", Session.LastError));
                return;
            }
            if (practice)
            {
                Session.Settings.AiOmar = true;
                Session.HostSettingsChanged();
            }
            UIM.Replace(new LobbyScreen());
        }

        public void JoinGame(string address, int port)
        {
            Session.Join(address, port, Settings.PlayerName);
            StartCoroutine(WaitForLobby());
        }

        IEnumerator WaitForLobby()
        {
            while (Session.State == SessionState.Connecting) yield return null;
            if (Session.State == SessionState.Lobby) UIM.Replace(new LobbyScreen());
        }

        public void LeaveSession()
        {
            Session.Leave();
            GoToMainMenu(null);
        }

        public void QuitGame()
        {
            Session.Leave();
            Settings.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ session events

        void GoToMainMenu(string error)
        {
            DestroyMatch();
            EnsureMenuScene();
            AudioManager.StopAllSfx();
            VhsEffect.ResetTransient();
            GameInput.SetCursorLocked(false);
            UIM.Replace(new MainMenuScreen());
            if (!string.IsNullOrEmpty(error)) UIM.Push(new MessageScreen("SIGNAL LOST", error));
        }

        void OnSessionEnded(string reason)
        {
            GoToMainMenu(reason);
        }

        void OnMatchStarting()
        {
            AudioManager.StopMusic(2f);
            AudioManager.Play2D(Snd.TapeRewindLoop, 0.5f, 1f, AudioCategory.Ui);
            UIM.Replace(new LoadingScreen());
            StartCoroutine(BuildMatch());
        }

        IEnumerator BuildMatch()
        {
            yield return null;
            yield return null; // let the loading screen render
            DestroyMenuScene();
            DestroyMatch();
            try
            {
                _match = MatchWorld.Create(Session, transform);
                _match.MatchEnded += OnMatchEnded;
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
            Session.NotifyLoaded();
        }

        void OnMatchBegan()
        {
            UIM.Clear();
            UIM.Hud = new HudScreen();
            AudioManager.StopMusic(0.5f);
        }

        void OnMatchEnded(EndingResult r)
        {
            UIM.Clear();
            UIM.Hud = null;
            UIM.Push(new EndingScreen(r));
        }

        void OnReturnedToLobby()
        {
            DestroyMatch();
            EnsureMenuScene();
            VhsEffect.ResetTransient();
            UIM.Replace(new LobbyScreen());
        }
    }

    /// <summary>
    /// The menu background: a dark room, your hand holding a lit lighter in the lower right
    /// (like the Puppet Combo menus), slow breathing sway of the camera.
    /// </summary>
    public sealed class MenuScene : MonoBehaviour
    {
        Pose _cam;
        FirstPersonArms _arms;
        PsxLight _light;
        GameObject _flame;
        AudioSource _amb;

        void Start()
        {
            AnomalySystem.Reset();
            AnomalySystem.SetBaseline(0.1f);
            PsxEnvironment.Set(new Color(0.03f, 0.03f, 0.04f), new Color(0.01f, 0.01f, 0.015f), 2f, 14f);
            try { MenuSceneBuilder.Build(transform, out _cam); }
            catch (System.Exception e) { Debug.LogException(e); _cam = new Pose(new Vector3(1000, 1.2f, 1000), Quaternion.identity); }
            var rig = PsxCameraRig.Instance;
            if (rig != null)
            {
                try
                {
                    _arms = FirstPersonArms.Create(CharacterSkin.Prisoner1, rig.transform);
                    _arms.SetHeld(ItemType.Lighter);
                    var anchor = _arms.HeldModel != null ? (Avatar.FindDeep(_arms.HeldModel.transform, "Anchor_Flame") ?? _arms.HeldModel.transform) : _arms.HandSocket;
                    _light = PsxLight.Create(anchor, Vector3.up * 0.03f, new Color(1f, 0.68f, 0.34f), 1.4f, 6f, PsxFlicker.Candle, "MenuLighter");
                    _light.Priority = 10;
                    _flame = PsxFx.CreateFlame(anchor, 1f);
                }
                catch (System.Exception e) { Debug.LogException(e); }
            }
            _amb = AudioManager.Loop2D(Snd.AmbMenu, 0.35f, AudioCategory.Ambience, 3f);
        }

        void OnDestroy()
        {
            if (_arms != null) Destroy(_arms.gameObject);
            if (_light != null) Destroy(_light.gameObject);
            if (_flame != null) Destroy(_flame);
            AudioManager.Stop(_amb, 0.5f);
        }

        void LateUpdate()
        {
            var rig = PsxCameraRig.Instance;
            if (rig == null) return;
            float t = Time.unscaledTime;
            Vector3 sway = new Vector3(Mathf.Sin(t * 0.37f) * 0.03f, Mathf.Sin(t * 0.61f) * 0.02f, 0);
            Quaternion look = _cam.rotation * Quaternion.Euler(Mathf.Sin(t * 0.23f) * 1.5f, Mathf.Sin(t * 0.17f) * 3f, Mathf.Sin(t * 0.29f) * 0.8f);
            rig.transform.SetPositionAndRotation(_cam.position + _cam.rotation * sway, look);
            rig.FieldOfView = 62f;
            if (_arms != null) { _arms.MoveSpeed = 0f; _arms.LookDelta = Vector2.zero; }
        }
    }
}
