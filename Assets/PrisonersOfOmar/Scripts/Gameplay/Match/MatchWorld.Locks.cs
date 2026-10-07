using System.Collections.Generic;
using System.Text;
using PrisonersOfOmar.Audio;
using PrisonersOfOmar.Map;
using PrisonersOfOmar.Net;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    // (iteration 3) Code locks, client side. Every peer derives the same codes from the seed; the host checks the tries
    // (MatchHost.Locks.cs). Network ids = index in CodeLocks: the shelter keypad first, then the map's code locks, then the
    // ones the gameplay puts on furniture (locked drawers).
    public sealed partial class MatchWorld
    {
        public readonly List<CodeLockEntity> CodeLocks = new List<CodeLockEntity>();

        /// <summary>A code lock opened (fresh = just now, not a late state).</summary>
        public event System.Action<CodeLockEntity> CodeLockOpened;

        void BuildCodeLocks()
        {
            var rng = DeterministicRandom.For(Seed, "codes");
            if (Map.Shelter != null && Map.Shelter.Keypad != null)
            {
                var info = new CodeLockInfo
                {
                    Name = "Shelter", Area = Map.AreaAt(Map.Shelter.Keypad.bounds.center), Kind = CodeKind.Digits, Length = 4, Label = "SHELTER",
                    UseText = "USE THE KEYPAD", OpenText = "THE SHELTER DOOR IS OPEN", Interact = Map.Shelter.Keypad, WrongNoise = 5f, OpenNoise = 18f,
                };
                AddCodeLock(info, string.Concat(ShelterCode[0], ShelterCode[1], ShelterCode[2], ShelterCode[3]));
            }
            foreach (var info in Map.CodeLocks) AddCodeLock(info, CodeLockEntity.RandomCode(info.Kind, info.Length, info.Symbols, rng));
        }

        /// <summary>Registers a code lock (same order on every peer). <paramref name="embedded"/> = another entity (a locked
        /// drawer) owns the collider, prompt and input. Returns it.</summary>
        public CodeLockEntity AddCodeLock(CodeLockInfo info, string code, bool embedded = false)
        {
            var lk = new CodeLockEntity(CodeLocks.Count, info, code, embedded);
            CodeLocks.Add(lk);
            return lk;
        }

        public CodeLockEntity FindCodeLock(string name)
        {
            foreach (var lk in CodeLocks) if (lk.Info.Name == name) return lk;
            return null;
        }

        public void OpenCodeLock(CodeLockEntity lk) => UI.UIManager.Instance?.Push(new UI.CodeLockScreen(this, lk));

        public void SendCode(CodeLockEntity lk, string code)
        {
            var w = Session.Begin(Msg.CodeReq);
            w.WriteShort((short)lk.Index);
            w.WriteString(code);
            Session.SendToHost(NetChannel.Reliable);
        }

        void OnCodeResult(int sender, NetReader r)
        {
            int id = r.ReadShort();
            byte status = r.ReadByte();
            var screen = UI.UIManager.Instance != null ? UI.UIManager.Instance.Find<UI.CodeLockScreen>() : null;
            if (screen != null && screen.Lock.Index == id) screen.Result(status);
        }

        void OnCodeLockState(int sender, NetReader r)
        {
            int id = r.ReadShort();
            bool open = r.ReadBool();
            int player = r.ReadByte();
            if (id < 0 || id >= CodeLocks.Count) return;
            var lk = CodeLocks[id];
            bool fresh = open && !lk.Open;
            lk.Open = open;
            if (!fresh) return;
            // the one who typed it heard it on their screen; a padlock on furniture is heard through its drawer (key click)
            if (player != LocalId && !lk.Embedded) AudioManager.Play3D(Snd.KeypadOk, lk.InteractPoint, 0.8f, 1f, 2f, 16f);
            CodeLockOpened?.Invoke(lk);
        }
    }

    /// <summary>(iteration 3) A lock opened by entering the right code (<see cref="CodeLockInfo"/>).</summary>
    public sealed class CodeLockEntity : IInteractable
    {
        public readonly int Index;
        public readonly CodeLockInfo Info;
        /// <summary>The right code: digits ("0417"), time ("0745" = 7:45) or button indices ("2031").</summary>
        public readonly string Code;
        public bool Open;
        /// <summary>The prompt and the input come from another entity (a locked drawer), not the lock's own collider.</summary>
        public readonly bool Embedded;
        /// <summary>Clock locks: where the hands were left (local, so the next try starts there). 0 = untouched.</summary>
        public int DraftHour, DraftMinute;

        public CodeLockEntity(int index, CodeLockInfo info, string code, bool embedded = false)
        {
            Index = index; Info = info; Code = code; Embedded = embedded;
            if (info.Interact != null && !embedded) InteractableRef.Attach(info.Interact, this);
        }

        public Vector3 InteractPoint => Info.Interact != null ? Info.Interact.bounds.center : Vector3.zero;

        public bool GetPrompt(Interactor who, out InteractPrompt p)
        {
            p = default;
            if (who.IsOmar) return false;
            if (Open)
            {
                if (Info.OpenText == null) return false;
                p = InteractPrompt.Info(Info.OpenText);
                return true;
            }
            p = InteractPrompt.Press(Info.UseText ?? "TRY A CODE");
            return true;
        }

        public void Interact(Interactor who) { if (!Open) MatchWorld.Instance?.OpenCodeLock(this); }

        /// <summary>A code for this kind of lock (same on every peer: drawn from the seeded generator).</summary>
        public static string RandomCode(CodeKind kind, int length, int symbols, DeterministicRandom rng)
        {
            var sb = new StringBuilder();
            switch (kind)
            {
                case CodeKind.Time:
                    {
                        // never 12:00 (where the hands start)
                        int h, m;
                        do { h = rng.Range(1, 13); m = rng.Range(0, 12) * 5; } while (h == 12 && m == 0);
                        sb.Append(h.ToString("00")).Append(m.ToString("00"));
                        break;
                    }
                case CodeKind.Sequence:
                    for (int i = 0; i < length; i++) sb.Append(rng.Range(0, Mathf.Clamp(symbols, 2, 9)));
                    break;
                default:
                    for (int i = 0; i < length; i++) sb.Append(rng.Range(0, 10));
                    break;
            }
            return sb.ToString();
        }

        /// <summary>The code as people would write it: "0417", "7:45", or the button names.</summary>
        public string Pretty()
        {
            switch (Info.Kind)
            {
                case CodeKind.Time: return Code.Length == 4 ? int.Parse(Code.Substring(0, 2)) + ":" + Code.Substring(2) : Code;
                case CodeKind.Sequence:
                    var sb = new StringBuilder();
                    foreach (char c in Code) { if (sb.Length > 0) sb.Append(' '); sb.Append(SymbolName(c - '0')); }
                    return sb.ToString();
                default: return Code;
            }
        }

        /// <summary>Names of the sequence buttons (the panels are painted with these colours).</summary>
        public static readonly string[] SymbolNames = { "RED", "GREEN", "BLUE", "YELLOW", "WHITE", "BLACK", "ORANGE", "PURPLE", "GREY" };
        public static string SymbolName(int i) => i >= 0 && i < SymbolNames.Length ? SymbolNames[i] : "?";
    }
}
