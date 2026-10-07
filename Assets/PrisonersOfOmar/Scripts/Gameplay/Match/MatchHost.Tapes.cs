using PrisonersOfOmar.Net;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    // (iteration 3) The VCR, host side. The tape plays for Tuning.TapeSeconds (the clock stops while the power is out);
    // afterwards it can be rewound and played again until Tuning.TapePlays plays are used up, then the VCR chews it.
    // A live Omar is told when it starts and may stop it at the VCR; an AI Omar who comes to look watches for a few
    // seconds and stops it. Part way in there is a scream on the tape that carries through the house (a noise for Omar).
    // Every peer follows Msg.TapeState (MatchWorld.Tapes.cs).
    public sealed partial class MatchHost
    {
        TapeMode _tape;
        int _tapePlays;
        float _tapeElapsed, _aiWatch;
        bool _tapeScreamed;

        /// <summary>Every play allowed tonight is used up (the VCR chews the tape when this one stops).</summary>
        bool TapeUsedUp => Tuning.TapePlays > 0 && _tapePlays >= Tuning.TapePlays;

        /// <summary>The tape just went into the VCR (socket solved): the first play.</summary>
        void StartTapeHost(int player)
        {
            if (_tape != TapeMode.Empty) return;
            PlayTapeHost(player);
            var omar = S.FindOmar();
            if (omar != null && !omar.IsBot && omar.Connected) Message("SOMEONE PUT ON MAMA'S TAPE.", 4f, omar.Id);
        }

        void PlayTapeHost(int player)
        {
            _tapePlays++;
            _tapeElapsed = 0f;
            _tapeScreamed = false;
            _aiWatch = 0f;
            BroadcastTape(TapeMode.Playing, player);
        }

        void StopTapeHost(int player)
        {
            if (_tape != TapeMode.Playing) return;
            BroadcastTape(TapeUsedUp ? TapeMode.Chewed : TapeMode.Stopped, player);
        }

        void BroadcastTape(TapeMode mode, int player)
        {
            _tape = mode;
            var w = S.Begin(Msg.TapeState);
            w.WriteByte((byte)mode);
            w.WriteByte((byte)Mathf.Clamp(_tapePlays, 0, 255));
            w.WriteFloat(_tapeElapsed);
            w.WriteByte((byte)(player < 0 ? 255 : Mathf.Clamp(player, 0, 254)));
            S.SendToAll(NetChannel.Reliable);
        }

        /// <summary>UseTarget.Vcr: a prisoner rewinds and plays it again (action 0), Omar stops it (action 1).</summary>
        void UseVcr(int p, int action)
        {
            int vi = W.SocketIndex("Vcr");
            if (vi < 0 || !W.PowerOn) return;
            var vcr = W.Sockets[vi];
            if (!vcr.Solved || !Near(p, vcr.InteractPoint, 3.2f)) return;
            if (IsOmar(p))
            {
                if (action == 1 && _tape == TapeMode.Playing) StopTapeHost(p);
                return;
            }
            if (action != 0 || _tape != TapeMode.Stopped || TapeUsedUp) return;
            PlayTapeHost(p);
            DeliverNoise(vcr.InteractPoint, 4f);   // the rewind whine and the clunk
        }

        void TickTapeHost(float dt)
        {
            if (_tape != TapeMode.Playing || !W.PowerOn) return;
            _tapeElapsed += dt;
            int vi = W.SocketIndex("Vcr");
            Vector3 at = vi >= 0 ? W.Sockets[vi].InteractPoint : Vector3.zero;
            if (!_tapeScreamed && Tuning.TapeScreamAt >= 0f && _tapeElapsed >= Tuning.TapeScreamAt)
            {
                _tapeScreamed = true;
                if (vi >= 0 && Tuning.TapeScreamRadius > 0f) DeliverNoise(at, Tuning.TapeScreamRadius);
            }
            // an AI Omar who comes to see what is on watches for a few seconds, then switches it off
            if (vi >= 0)
            {
                bool near = false;
                foreach (var ai in _ais) if (ai != null && Vector3.Distance(PosOf(ai.OmarId), at) < 2.6f) near = true;
                _aiWatch = near ? _aiWatch + dt : Mathf.Max(0f, _aiWatch - dt);
                if (_aiWatch > 4f)
                {
                    foreach (var ai in _ais) if (ai != null) { StopTapeHost(ai.OmarId); break; }
                    return;
                }
            }
            if (_tapeElapsed >= Tuning.TapeSeconds) StopTapeHost(-1);
        }
    }
}
