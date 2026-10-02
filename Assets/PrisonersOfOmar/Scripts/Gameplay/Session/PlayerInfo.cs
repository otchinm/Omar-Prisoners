using PrisonersOfOmar.Net;

namespace PrisonersOfOmar.Gameplay
{
    /// <summary>A participant (human or bot) as known by the lobby / match.</summary>
    public sealed class PlayerInfo
    {
        /// <summary>Host = 0, clients = connection id (1..254), AI Omar = an unused id picked when the match starts.</summary>
        public int Id;
        public string Name = "";
        public PlayerRole Role = PlayerRole.Prisoner;
        public CharacterSkin Skin = CharacterSkin.Prisoner1;
        public bool Ready;
        public bool IsBot;
        public bool Connected = true;


        public bool IsPrisoner => Role == PlayerRole.Prisoner;
        public bool IsOmar => Role == PlayerRole.Omar;

        public void Write(NetWriter w)
        {
            w.WriteByte((byte)Id);
            w.WriteString(Name);
            w.WriteByte((byte)Role);
            w.WriteByte((byte)Skin);
            byte flags = 0;
            if (Ready) flags |= 1;
            if (IsBot) flags |= 2;
            if (Connected) flags |= 4;
            w.WriteByte(flags);
        }

        public static PlayerInfo Read(NetReader r)
        {
            var p = new PlayerInfo
            {
                Id = r.ReadByte(),
                Name = Settings.CleanName(r.ReadString()),
                Role = (PlayerRole)r.ReadByte(),
                Skin = (CharacterSkin)r.ReadByte(),
            };
            byte f = r.ReadByte();
            p.Ready = (f & 1) != 0;
            p.IsBot = (f & 2) != 0;
            p.Connected = (f & 4) != 0;
            if (p.Role > PlayerRole.Spectator) p.Role = PlayerRole.Spectator;
            if (p.Skin > CharacterSkin.Omar) p.Skin = CharacterSkin.Prisoner1;
            return p;
        }

        public PlayerInfo Clone() => (PlayerInfo)MemberwiseClone();
    }

    /// <summary>Host-chosen options for a match.</summary>
    public sealed class MatchSettings
    {
        public int NightMinutes = 20;
        public bool AiOmar = true;
        public int Seed;

        public void Write(NetWriter w)
        {
            w.WriteByte((byte)NightMinutes);
            w.WriteBool(AiOmar);
            w.WriteInt(Seed);
        }

        public static MatchSettings Read(NetReader r)
        {
            var s = new MatchSettings { NightMinutes = r.ReadByte(), AiOmar = r.ReadBool(), Seed = r.ReadInt() };
            if (s.NightMinutes < 5 || s.NightMinutes > 60) s.NightMinutes = 20;
            return s;
        }
    }
}
