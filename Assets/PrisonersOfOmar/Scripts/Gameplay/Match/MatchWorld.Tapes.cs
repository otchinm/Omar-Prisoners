using System.Collections.Generic;
using PrisonersOfOmar.Audio;
using PrisonersOfOmar.Map;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    // (iteration 3) The home video tape and the journal. The tape goes into the VCR on the grandmother's TV (an item socket,
    // MatchWorld.Sockets.cs); from then on the TV plays it for a while (everyone sees the blue picture and hears it) and
    // anyone at the VCR can watch it: a few shots of a 1987 birthday, one of which gives away a code. Everything read or
    // watched goes into the local journal (J).
    public sealed partial class MatchWorld
    {
        /// <summary>The code lock whose code the tape gives away (null = it only hints at the shelter).</summary>
        public CodeLockEntity TapeLock { get; private set; }
        public string[] TapeShots { get; private set; } = new string[0];
        const float TapePlaySeconds = 75f;
        SocketEntity _vcr;
        TvScreen _tv;
        float _tapeUntil = -1f;
        AudioSource _tapeLoop;

        void BuildTapes()
        {
            int vi = SocketIndex("Vcr");
            if (vi < 0) return;
            _vcr = Sockets[vi];
            var rng = DeterministicRandom.For(Seed, "tapes");
            // the clock (when the map has one), else the last combination padlock when there are several (each lock has
            // exactly one hint: this one is not written in any note)
            TapeLock = FindCodeLock("GrandfatherClock");
            if (TapeLock == null)
            {
                var drawers = CodeLocks.FindAll(l => l.Info.Name.StartsWith("Drawer"));
                if (drawers.Count >= 2) TapeLock = drawers[drawers.Count - 1];
            }
            TapeShots = NoteTexts.TapeShots(TapeLock, TapeLock != null ? NoteTexts.PlaceName(TapeLock.Info.Area) : "", ShelterCode[0], rng);
            _vcr.SolvedPrompt = who => InteractPrompt.Press("WATCH THE TAPE");
            _vcr.SolvedInteract = who => WatchTape();
            if (Map.Root != null)
                foreach (var tv in Map.Root.GetComponentsInChildren<TvScreen>(true))
                    if (tv.name == "TvScreen_TvLivingRoom") _tv = tv;
            SocketChanged += OnTapeSocket;
        }

        void OnTapeSocket(SocketEntity s, bool fresh)
        {
            if (s != _vcr || !fresh) return;
            _tapeUntil = UnityEngine.Time.time + TapePlaySeconds;
            if (_tv != null) _tv.Playback = true;
            _tapeLoop = AudioManager.Loop3D(Snd.StaticLoop, _vcr.InteractPoint, 0.3f, 10f, AudioCategory.Sfx, null, 0.5f);
        }

        void TickTapes(float dt)
        {
            if (_tapeUntil < 0f || UnityEngine.Time.time < _tapeUntil) return;
            _tapeUntil = -1f;
            if (_tv != null) _tv.Playback = false;
            AudioManager.Stop(_tapeLoop, 1f);
            _tapeLoop = null;
            if (_vcr != null) AudioManager.Play3D(Snd.TapeStop, _vcr.InteractPoint, 0.7f, 1f, 1.5f, 10f);
        }

        /// <summary>Local: watch the tape (full screen), and keep its transcript in the journal.</summary>
        public void WatchTape()
        {
            if (TapeShots.Length == 0) return;
            AddJournal("tape", "TAPE - 'MAMA 10/31'", string.Join("\n\n", TapeShots));
            UI.UIManager.Instance?.Push(new UI.TapeScreen(TapeShots));
        }

        // ------------------------------------------------------------------ journal (local)

        public readonly List<JournalEntry> Journal = new List<JournalEntry>();

        /// <summary>Remembers something read / watched (once per key).</summary>
        public void AddJournal(string key, string title, string text)
        {
            foreach (var e in Journal) if (e.Key == key) return;
            Journal.Add(new JournalEntry { Key = key, Title = title, Text = text, At = Time });
            if (Journal.Count == 1) AddMessage("ADDED TO THE JOURNAL (J)", 3f);
        }
    }

    public sealed class JournalEntry
    {
        public string Key, Title, Text;
        /// <summary>Match clock when it was found.</summary>
        public float At;
    }
}
