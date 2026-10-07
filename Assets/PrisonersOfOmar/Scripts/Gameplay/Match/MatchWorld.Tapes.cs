using System.Collections.Generic;
using PrisonersOfOmar.Audio;
using PrisonersOfOmar.Characters;
using PrisonersOfOmar.Map;
using PrisonersOfOmar.Net;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    /// <summary>(iteration 3) The VCR: no tape yet, playing, stopped (can be rewound), chewed (no more plays).</summary>
    public enum TapeMode : byte { Empty = 0, Playing, Stopped, Chewed }

    // (iteration 3) The home video tape and the journal. The tape goes into the VCR on the grandmother's TV (an item socket,
    // MatchWorld.Sockets.cs); the host then runs the VCR (MatchHost.Tapes.cs, Msg.TapeState): it plays for
    // Tuning.TapeSeconds - everyone in the room sees its pictures on the TV and hears it, and anyone at the VCR can watch
    // it closely (TapeScreen, in step with the TV) - then it can be rewound and played again a few times before the VCR
    // chews it. One shot gives away a code, sometimes a child's drawing gives away a shelter digit, and part way in there
    // is a scream on it that brings Omar. While it plays the grandmother stares at it and hardly notices anything.
    // Everything read or watched goes into the local journal (J).
    public sealed partial class MatchWorld
    {
        /// <summary>The code lock whose code the tape gives away (null = it only hints at the shelter).</summary>
        public CodeLockEntity TapeLock { get; private set; }
        public TapeShot[] TapeShots { get; private set; } = new TapeShot[0];
        /// <summary>The shelter code position the child's drawing on the tape gives away (-1 = none on this tape).</summary>
        public int TapeSpareDigit { get; private set; } = -1;
        public TapeMode Tape { get; private set; }
        /// <summary>Plays started so far tonight (the first one included).</summary>
        public int TapePlays { get; private set; }
        public bool TapePlaying => Tape == TapeMode.Playing;
        /// <summary>Seconds into the current play (the clock stops while the power is out).</summary>
        public float TapeElapsed => _tapeElapsed;
        /// <summary>She still stares at the screen: the tape plays, or stopped less than Tuning.TapeGrace seconds ago.</summary>
        public bool TapeEntrancing => TapePlaying || Time < _tapeGraceUntil;
        public float TapeRemaining => TapePlaying ? Mathf.Max(0f, Tuning.TapeSeconds - _tapeElapsed) : 0f;
        public Vector3 VcrPoint => _vcr != null ? _vcr.InteractPoint : Vector3.zero;

        const float TapeLoopVolume = 0.45f;
        SocketEntity _vcr;
        TvScreen _tv;
        float _tapeElapsed, _tapeGraceUntil = -1f;
        bool _tapeScreamPlayed;
        AudioSource _tapeLoop;
        bool[] _tapeSeen = new bool[0];
        GameObject _chewed;

        /// <summary>The shot on screen <paramref name="t"/> seconds into a play (every shot gets the same share of it).</summary>
        public int TapeShotAt(float t)
        {
            int n = TapeShots.Length;
            if (n == 0) return -1;
            return Mathf.Clamp(Mathf.FloorToInt(t / (Tuning.TapeSeconds / n)), 0, n - 1);
        }

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
            TapeShots = NoteTexts.TapeShots(TapeLock, TapeLock != null ? NoteTexts.PlaceName(TapeLock.Info.Area) : "", ShelterCode, rng, out int spare);
            TapeSpareDigit = spare;
            _tapeSeen = new bool[TapeShots.Length];
            _vcr.SolvedPrompt = VcrPrompt;
            _vcr.SolvedInteract = VcrInteract;
            _vcr.OmarPrompt = who => TapePlaying && PowerOn ? InteractPrompt.Press("STOP MAMA'S TAPE") : (InteractPrompt?)null;
            _vcr.OmarInteract = who => { if (TapePlaying) SendUse(UseTarget.Vcr, 1, -1); };
            if (Map.Root != null)
                foreach (var tv in Map.Root.GetComponentsInChildren<TvScreen>(true))
                    if (tv.name == "TvScreen_TvLivingRoom") _tv = tv;
        }

        InteractPrompt? VcrPrompt(Interactor who)
        {
            if (!PowerOn) return InteractPrompt.Info("THE VCR IS DEAD. NO POWER.");
            switch (Tape)
            {
                case TapeMode.Playing:
                    return InteractPrompt.Press("WATCH THE TAPE");
                case TapeMode.Stopped:
                    {
                        bool last = Tuning.TapePlays > 0 && Tuning.TapePlays - TapePlays == 1;
                        var p = InteractPrompt.Hold(last ? "REWIND AND PLAY IT AGAIN (THE TAPE IS WEARING THIN)" : "REWIND AND PLAY IT AGAIN", 2.5f, ItemType.None, 3f);
                        p.HoldSound = Snd.TapeRewindLoop;
                        return p;
                    }
                case TapeMode.Chewed:
                    return InteractPrompt.Info(HasJournal("tape") ? "THE VCR ATE THE TAPE. WHAT I SAW IS IN MY JOURNAL (J)" : "THE VCR ATE THE TAPE.");
                default:
                    return InteractPrompt.Info("THE TAPE IS IN THE VCR.");
            }
        }

        void VcrInteract(Interactor who)
        {
            if (!PowerOn) return;
            if (Tape == TapeMode.Playing) WatchTape();
            else if (Tape == TapeMode.Stopped) SendUse(UseTarget.Vcr, 0, -1);
        }

        void OnTapeState(int sender, NetReader r)
        {
            var mode = (TapeMode)r.ReadByte();
            int plays = r.ReadByte();
            float elapsed = r.ReadFloat();
            int player = r.ReadByte();
            var was = Tape;
            Tape = mode;
            TapePlays = plays;
            if (mode != TapeMode.Chewed && _chewed != null) { Destroy(_chewed); _chewed = null; }   // (admin reset)
            if (mode == TapeMode.Playing)
            {
                _tapeElapsed = elapsed;
                if (was != TapeMode.Playing) BeginTapePlayback(was != TapeMode.Empty);
            }
            else if (was == TapeMode.Playing) EndTapePlayback(mode, player);
        }

        void BeginTapePlayback(bool rewound)
        {
            _tapeScreamPlayed = false;
            _tapeGraceUntil = -1f;
            if (_tv != null) { _tv.Playback = true; _tv.PlaybackFrame = null; }
            Grandma?.MuteTv(true);
            if (_vcr != null && rewound) AudioManager.Play3D(Snd.TapePlay, _vcr.InteractPoint, 0.8f, 1f, 1.5f, 14f);
            // she knows that tape: a mutter from her chair
            if (Grandma != null && Grandma.Mode == GrandmaMode.WatchingTv)
                AudioManager.Play3D(Snd.GrandmaMutter, Grandma.Eye, 0.8f, 0.95f, 2f, 14f);
        }

        void EndTapePlayback(TapeMode mode, int player)
        {
            if (_tv != null) { _tv.Playback = false; _tv.PlaybackFrame = null; }
            Grandma?.MuteTv(false);
            AudioManager.Stop(_tapeLoop, 1f);
            _tapeLoop = null;
            _tapeGraceUntil = Time + Tuning.TapeGrace;
            if (_vcr == null) return;
            if (mode == TapeMode.Chewed)
            {
                // the VCR chews the worn tape: a grinding clunk and brown tape spilling out of the slot
                AudioManager.Play3D(Snd.TapeEject, _vcr.InteractPoint, 0.9f, 0.72f, 1.5f, 14f);
                PlayLater(Snd.TapeStop, _vcr.InteractPoint, 0.35f, 0.8f, 10f);
                ShowChewedTape();
                if (LocalAvatar != null && !LocalIsOmar && Vector3.Distance(LocalAvatar.Position, _vcr.InteractPoint) < 6f)
                    AddMessage("THE VCR CHEWS UP THE TAPE.", 3f);
            }
            else AudioManager.Play3D(Snd.TapeStop, _vcr.InteractPoint, 0.7f, 1f, 1.5f, 10f);
            if (player == LocalId && LocalIsOmar) AddMessage("YOU STOP MAMA'S TAPE.", 2.5f);
        }

        void ShowChewedTape()
        {
            if (_chewed != null || _vcr == null || _vcr.Info.SlotPoses.Length == 0) return;
            try
            {
                var pose = _vcr.Info.SlotPoses[0];
                var mb = new MeshBuilder();
                mb.SetMaterial(Rendering.PsxMaterials.GetColor(new Color(0.2f, 0.13f, 0.08f)));
                // two loose loops of tape hanging from the slot (the tape's spine faces -Z in its pose)
                mb.Push(pose.position, pose.rotation);
                mb.AddBox(new Vector3(-0.03f, -0.03f, -0.054f), new Vector3(0.012f, 0.06f, 0.001f), BoxUV.PerFace);
                mb.AddBox(new Vector3(0.025f, -0.045f, -0.056f), new Vector3(0.012f, 0.09f, 0.001f), BoxUV.PerFace);
                mb.Pop();
                _chewed = mb.Build("ChewedTape", _dynamicRoot, Layers.World);
            }
            catch (System.Exception e) { Debug.LogException(e); }
        }

        void TickTapes(float dt)
        {
            if (Tape != TapeMode.Playing) return;
            if (!PowerOn)
            {
                // no picture, no sound: the tape waits for the power to come back
                if (_tapeLoop != null) AudioManager.SetVolume(_tapeLoop, 0f);
                return;
            }
            _tapeElapsed = Mathf.Min(_tapeElapsed + dt, Tuning.TapeSeconds);
            if (_vcr != null)
            {
                if (_tapeLoop == null && _tapeElapsed >= 0.9f)   // after the insert clunk and the play whirr
                    _tapeLoop = AudioManager.Loop3D(Snd.TapeVideoLoop, _vcr.InteractPoint, TapeLoopVolume, 12f, AudioCategory.Sfx, null, 1.5f);
                else if (_tapeLoop != null) AudioManager.SetVolume(_tapeLoop, TapeLoopVolume);
                // the scream on the tape (the host makes the noise Omar hears)
                if (!_tapeScreamPlayed && Tuning.TapeScreamAt >= 0f && _tapeElapsed >= Tuning.TapeScreamAt)
                {
                    _tapeScreamPlayed = true;
                    AudioManager.Play3D(Snd.TapeScream, _vcr.InteractPoint, 1f, 1f, 3f, 28f);
                }
            }
            if (_tv != null)
            {
                int s = TapeShotAt(_tapeElapsed);
                _tv.PlaybackFrame = s >= 0 && TapeShots[s].Frame != null ? Tex.Props + "tape_" + TapeShots[s].Frame : null;
            }
        }

        /// <summary>Local: watch the tape closely (a CRT window in step with the TV) - what you see goes into the journal.</summary>
        public void WatchTape()
        {
            if (TapeShots.Length == 0 || !PowerOn || !TapePlaying) return;
            UI.UIManager.Instance?.Push(new UI.TapeScreen(this, false));
        }

        /// <summary>The local player saw this shot on the tape: the journal keeps every shot seen so far (the code first).</summary>
        public void SeeTapeShot(int shot)
        {
            if (shot < 0 || shot >= _tapeSeen.Length || _tapeSeen[shot]) return;
            _tapeSeen[shot] = true;
            var sb = new System.Text.StringBuilder();
            int code = NoteTexts.TapeCodeShot;
            bool clue = false;
            if (code < _tapeSeen.Length && _tapeSeen[code]) { sb.Append(TapeShots[code].Caption).Append("\n\n- - -\n"); clue = true; }
            bool gap = false;
            for (int i = 0; i < TapeShots.Length; i++)
            {
                if (i == code) continue;
                if (_tapeSeen[i]) { sb.Append('\n').Append(TapeShots[i].Caption); gap = false; }
                else if (!gap) { sb.Append("\n[...]"); gap = true; }
            }
            if (TapeSpareDigit >= 0 && NoteTexts.TapeSpareShot < _tapeSeen.Length && _tapeSeen[NoteTexts.TapeSpareShot]) clue = true;
            SetJournal("tape", "TAPE - 'MAMA 10/31'", sb.ToString(), clue);
        }

        // ------------------------------------------------------------------ journal (local)

        public readonly List<JournalEntry> Journal = new List<JournalEntry>();
        bool _journalHintPending;

        /// <summary>Remembers something read / watched (once per key). <paramref name="clue"/> = it holds a code / a digit.</summary>
        public void AddJournal(string key, string title, string text, bool clue = false)
        {
            if (HasJournal(key)) return;
            Journal.Add(new JournalEntry { Key = key, Title = title, Text = text, At = Time, Clue = clue });
            // the hint shows once the note / tape screen is closed (it would hide behind it), for the first few entries
            if (Journal.Count <= 3) _journalHintPending = true;
        }

        /// <summary>Adds the entry, or rewrites it (the tape: more of it seen).</summary>
        void SetJournal(string key, string title, string text, bool clue)
        {
            foreach (var e in Journal)
                if (e.Key == key) { e.Text = text; e.Clue |= clue; return; }
            AddJournal(key, title, text, clue);
        }

        public bool HasJournal(string key)
        {
            foreach (var e in Journal) if (e.Key == key) return true;
            return false;
        }

        /// <summary>Called when a note / the tape screen closes.</summary>
        public void ShowJournalHint()
        {
            if (!_journalHintPending) return;
            _journalHintPending = false;
            AddMessage("SAVED TO THE JOURNAL - PRESS J", 5f);
        }

        /// <summary>The shelter digits found so far, as "_ 4 _ 9" (the notes in the journal, a drawing seen on the tape).</summary>
        public string ShelterDigitsFound(out bool complete)
        {
            var d = new[] { "_", "_", "_", "_" };
            foreach (var e in Journal)
            {
                if (!e.Key.StartsWith("note:") || !int.TryParse(e.Key.Substring(5), out int ni) || ni < 0 || ni >= Notes.Length) continue;
                var n = Notes[ni];
                if (n != null && n.CodeDigit >= 0 && n.CodeDigit < 4) d[n.CodeDigit] = ShelterCode[n.CodeDigit].ToString();
            }
            int spareShot = NoteTexts.TapeSpareShot;
            if (TapeSpareDigit >= 0 && TapeSpareDigit < 4 && spareShot < _tapeSeen.Length && _tapeSeen[spareShot]) d[TapeSpareDigit] = ShelterCode[TapeSpareDigit].ToString();
            // the tape without a padlock to give away whispers the first digit
            if (TapeLock == null && NoteTexts.TapeCodeShot < _tapeSeen.Length && _tapeSeen[NoteTexts.TapeCodeShot]) d[0] = ShelterCode[0].ToString();
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
