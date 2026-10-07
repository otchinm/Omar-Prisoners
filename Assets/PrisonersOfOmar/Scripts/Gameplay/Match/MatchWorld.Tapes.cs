using System.Collections.Generic;
using PrisonersOfOmar.Audio;
using PrisonersOfOmar.Characters;
using PrisonersOfOmar.Map;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    // (iteration 3) The home video tape and the journal. The tape goes into the VCR on the grandmother's TV (an item socket,
    // MatchWorld.Sockets.cs); from then on the TV plays it for a while (everyone sees the blue picture and hears it) and
    // anyone at the VCR can watch it: a few shots of a 1987 birthday, one of which gives away a code. While it plays the
    // grandmother in her chair stares at it and hardly notices anything around her. Everything read or watched goes into
    // the local journal (J).
    public sealed partial class MatchWorld
    {
        /// <summary>The code lock whose code the tape gives away (null = it only hints at the shelter).</summary>
        public CodeLockEntity TapeLock { get; private set; }
        public string[] TapeShots { get; private set; } = new string[0];
        /// <summary>The TV is playing the tape right now (every peer runs the same timer from the socket state).</summary>
        public bool TapePlaying => _tapeUntil > 0f;
        const float TapePlaySeconds = 75f, TapeLoopVolume = 0.3f;
        SocketEntity _vcr;
        TvScreen _tv;
        float _tapeUntil = -1f, _tapeLoopAt = -1f;
        AudioSource _tapeLoop;

        void BuildTapes()
        {
            int vi = SocketIndex("Vcr");
            if (vi < 0) return;
            _vcr = Sockets[vi];
            var rng = DeterministicRandom.For(Seed, "tapes");
            // the clock (when the map has one), else the last combination padlock when there are several (each lock has
            // exactly one hint: this one is not written in any note; its drawer holds a small key, never the car keys / fuse)
            TapeLock = FindCodeLock("GrandfatherClock");
            if (TapeLock == null)
            {
                var drawers = CodeLocks.FindAll(l => l.Info.Name.StartsWith("Drawer"));
                if (drawers.Count >= 2) TapeLock = drawers[drawers.Count - 1];
            }
            TapeShots = NoteTexts.TapeShots(TapeLock, TapeLock != null ? NoteTexts.PlaceName(TapeLock.Info.Area) : "", ShelterCode[0], rng);
            _vcr.SolvedPrompt = who => PowerOn ? InteractPrompt.Press("WATCH THE TAPE") : InteractPrompt.Info("THE VCR IS DEAD. NO POWER.");
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
            _tapeLoopAt = UnityEngine.Time.time + 0.9f;   // after the insert clunk and the play whirr
            if (_tv != null) _tv.Playback = true;
            Grandma?.MuteTv(true);
            // she knows that tape: a mutter from her chair
            if (Grandma != null && Grandma.Mode == GrandmaMode.WatchingTv)
                AudioManager.Play3D(Snd.GrandmaMutter, Grandma.Eye, 0.8f, 0.95f, 2f, 14f);
        }

        void TickTapes(float dt)
        {
            if (_tapeUntil < 0f) return;
            if (!PowerOn)
            {
                // no picture, no sound: the tape waits for the power to come back
                _tapeUntil += dt;
                if (_tapeLoopAt > 0f) _tapeLoopAt += dt;
                if (_tapeLoop != null) AudioManager.SetVolume(_tapeLoop, 0f);
                return;
            }
            if (_tapeLoopAt > 0f && UnityEngine.Time.time >= _tapeLoopAt && _vcr != null)
            {
                _tapeLoopAt = -1f;
                _tapeLoop = AudioManager.Loop3D(Snd.TvStaticLoop, _vcr.InteractPoint, TapeLoopVolume, 10f, AudioCategory.Sfx, null, 1.5f);
            }
            else if (_tapeLoop != null) AudioManager.SetVolume(_tapeLoop, TapeLoopVolume);
            if (UnityEngine.Time.time < _tapeUntil) return;
            _tapeUntil = -1f;
            if (_tv != null) _tv.Playback = false;
            Grandma?.MuteTv(false);
            AudioManager.Stop(_tapeLoop, 1f);
            _tapeLoop = null;
            if (_vcr != null) AudioManager.Play3D(Snd.TapeStop, _vcr.InteractPoint, 0.7f, 1f, 1.5f, 10f);
        }

        /// <summary>Local: watch the tape (full screen), and keep its transcript in the journal (the code shot first).</summary>
        public void WatchTape()
        {
            if (TapeShots.Length == 0 || !PowerOn) return;
            var sb = new System.Text.StringBuilder();
            int code = NoteTexts.TapeCodeShot;
            if (code >= 0 && code < TapeShots.Length) sb.Append(TapeShots[code]).Append("\n\n- - -\n");
            for (int i = 0; i < TapeShots.Length; i++) if (i != code) sb.Append('\n').Append(TapeShots[i]);
            AddJournal("tape", "TAPE - 'MAMA 10/31'", sb.ToString(), true);
            UI.UIManager.Instance?.Push(new UI.TapeScreen(TapeShots, NoteTexts.TapeCodeShot));
        }

        // ------------------------------------------------------------------ journal (local)

        public readonly List<JournalEntry> Journal = new List<JournalEntry>();
        bool _journalHintPending;

        /// <summary>Remembers something read / watched (once per key). <paramref name="clue"/> = it holds a code / a digit.</summary>
        public void AddJournal(string key, string title, string text, bool clue = false)
        {
            foreach (var e in Journal) if (e.Key == key) return;
            Journal.Add(new JournalEntry { Key = key, Title = title, Text = text, At = Time, Clue = clue });
            // the hint shows once the note / tape screen is closed (it would hide behind it), for the first few entries
            if (Journal.Count <= 3) _journalHintPending = true;
        }

        /// <summary>Called when a note / the tape screen closes.</summary>
        public void ShowJournalHint()
        {
            if (!_journalHintPending) return;
            _journalHintPending = false;
            AddMessage("SAVED TO THE JOURNAL - PRESS J", 5f);
        }

        /// <summary>The shelter digits found so far, as "_ 4 _ 9" (from the notes in the journal).</summary>
        public string ShelterDigitsFound(out bool complete)
        {
            var d = new[] { "_", "_", "_", "_" };
            foreach (var e in Journal)
            {
                if (!e.Key.StartsWith("note:") || !int.TryParse(e.Key.Substring(5), out int ni) || ni < 0 || ni >= Notes.Length) continue;
                var n = Notes[ni];
                if (n != null && n.CodeDigit >= 0 && n.CodeDigit < 4) d[n.CodeDigit] = ShelterCode[n.CodeDigit].ToString();
            }
            complete = System.Array.IndexOf(d, "_") < 0;
            return string.Join(" ", d);
        }
    }

    public sealed class JournalEntry
    {
        public string Key, Title, Text;
        /// <summary>Match clock when it was found.</summary>
        public float At;
        /// <summary>Holds a code or a code digit (marked in the list).</summary>
        public bool Clue;
    }
}
