using System.Collections.Generic;
using PrisonersOfOmar.Net;

namespace PrisonersOfOmar.Gameplay
{
    /// <summary>Per participant result shown on the ending screen.</summary>
    public sealed class EndingEntry
    {
        public int Id;
        public string Name;
        public PlayerRole Role;
        public LifeState Life;
        public EscapeRoute Route;
        public int Captures;
    }

    /// <summary>The outcome of a match (decided by the host, identical on every peer).</summary>
    public sealed class EndingResult
    {
        public EndingId Id;
        /// <summary>Index into the twist pool (-1 = none). Chosen randomly by the host.</summary>
        public int Twist = -1;
        public int Escaped;
        public int Lost;
        public float Duration;
        public readonly List<EndingEntry> Entries = new List<EndingEntry>();

        public bool OmarWon => Escaped == 0;
        public bool Everyone => Lost == 0 && Escaped > 0;

        public void Write(NetWriter w)
        {
            w.WriteByte((byte)Id);
            w.WriteSByte((sbyte)Twist);
            w.WriteByte((byte)Escaped);
            w.WriteByte((byte)Lost);
            w.WriteFloat(Duration);
            w.WriteByte((byte)Entries.Count);
            foreach (var e in Entries)
            {
                w.WriteByte((byte)e.Id);
                w.WriteString(e.Name);
                w.WriteByte((byte)e.Role);
                w.WriteByte((byte)e.Life);
                w.WriteByte((byte)e.Route);
                w.WriteByte((byte)e.Captures);
            }
        }

        public static EndingResult Read(NetReader r)
        {
            var res = new EndingResult
            {
                Id = (EndingId)r.ReadByte(),
                Twist = r.ReadSByte(),
                Escaped = r.ReadByte(),
                Lost = r.ReadByte(),
                Duration = r.ReadFloat(),
            };
            int n = r.ReadByte();
            for (int i = 0; i < n; i++)
            {
                res.Entries.Add(new EndingEntry
                {
                    Id = r.ReadByte(),
                    Name = r.ReadString(),
                    Role = (PlayerRole)r.ReadByte(),
                    Life = (LifeState)r.ReadByte(),
                    Route = (EscapeRoute)r.ReadByte(),
                    Captures = r.ReadByte(),
                });
            }
            return res;
        }

        public string Title => Endings.Title(Id);
        public string Image => Endings.Image(Id);
        public string Text => Endings.Text(this);
    }

    /// <summary>Ending names, stills and epilogues (multiple alternative endings + random twists).</summary>
    public static class Endings
    {
        public static EndingId ForRoute(EscapeRoute r)
        {
            switch (r)
            {
                case EscapeRoute.Road: return EndingId.LongRoad;
                case EscapeRoute.Car: return EndingId.Headlights;
                case EscapeRoute.Shelter: return EndingId.Underground;
                case EscapeRoute.Radio: return EndingId.Signal;
                case EscapeRoute.Fire: return EndingId.Ashes;
                default: return EndingId.SecondClass;
            }
        }

        public static string Title(EndingId id)
        {
            switch (id)
            {
                case EndingId.LongRoad: return "THE LONG ROAD";
                case EndingId.Headlights: return "HEADLIGHTS";
                case EndingId.Underground: return "UNDERGROUND";
                case EndingId.Signal: return "SIGNAL";
                case EndingId.Ashes: return "ASHES";
                case EndingId.Dawn: return "DAWN";
                default: return "THE SECOND CLASS";
            }
        }

        public static string Image(EndingId id)
        {
            switch (id)
            {
                case EndingId.LongRoad: return "Textures/UI/ending_road";
                case EndingId.Headlights: return "Textures/UI/ending_car";
                case EndingId.Underground: return "Textures/UI/ending_tunnel";
                case EndingId.Signal: return "Textures/UI/ending_signal";
                case EndingId.Ashes: return "Textures/UI/ending_ashes";
                case EndingId.Dawn: return "Textures/UI/ending_dawn";
                default: return "Textures/UI/ending_caught";
            }
        }

        public static string RouteName(EscapeRoute r)
        {
            switch (r)
            {
                case EscapeRoute.Road: return "WALKED OUT THE MAIN GATE";
                case EscapeRoute.Car: return "DROVE THROUGH THE GATE";
                case EscapeRoute.Shelter: return "CRAWLED OUT THE TUNNEL";
                case EscapeRoute.Radio: return "RESCUED BY HELICOPTER";
                case EscapeRoute.Fire: return "RAN THROUGH THE FIRE";
                default: return "ESCAPED";
            }
        }

        static readonly string[] Base =
        {
            "",
            "YOU WALKED UNTIL THE SKY TURNED GRAY. THE ROAD NEVER SEEMED TO END, BUT THE BASE OF THE SECOND CLASS FELL BEHIND YOU, SWALLOWED BY THE TREES.",
            "THE ENGINE COUGHED, THEN ROARED. YOU SMASHED THROUGH THE GATE AND DID NOT STOP UNTIL THE GAS LIGHT BLINKED RED ON AN EMPTY HIGHWAY.",
            "THE TUNNEL STANK OF RUST AND WET EARTH. AT THE END: A LADDER, A HATCH, AND THE COLD AIR OF THE FOREST. NOBODY FOLLOWED. YOU THINK.",
            "THE VOICE ON THE RADIO PROMISED HELP. A SEARCHLIGHT CUT THROUGH THE DEAD CORN AND THE ROTORS FLATTENED THE FIELD AS THEY PULLED YOU INSIDE.",
            "THE DRUMS WENT UP LIKE THE END OF THE WORLD. YOU RAN THROUGH THE BURNING GAP IN THE FENCE WHILE THE BASE OF THE SECOND CLASS BURNED UNTIL MORNING.",
            "NOBODY LEFT THE BASE OF THE SECOND CLASS. THE CAGES ARE FULL AGAIN. OMAR SHARPENS HIS CLEAVER AND WAITS FOR THE NEXT TAPE.",
            "THE SUN CAME UP GRAY OVER THE FIELD. THOSE STILL INSIDE HEARD HIS BOOTS ON THE STAIRS. THE RECKONING HAD COME.",
        };

        static readonly string[] Twists =
        {
            "",
            "A TRUCK STOPPED FOR YOU AT DAWN. THE DRIVER DID NOT SAY A WORD. HE WORE A SACK OVER HIS HEAD.",
            "IN THE REAR-VIEW MIRROR, TWO HEADLIGHTS FOLLOWED YOU ALL THE WAY TO THE CITY. THEY ARE STILL PARKED OUTSIDE.",
            "THE HATCH OPENED INTO ANOTHER BASEMENT. ANOTHER CAGE. SOMEWHERE ABOVE, A TV WAS PLAYING STATIC.",
            "THE PILOT NEVER SAID A WORD. THE HELICOPTER FLEW EAST, AWAY FROM THE CITY... TOWARDS ANOTHER BASE.",
            "WHEN THE FIRE DIED THEY SEARCHED THE ASHES FOR DAYS. THEY NEVER FOUND HIS BODY.",
            "SOMEONE ELSE FOUND THE TAPE. THEY PRESSED PLAY.",
            "THE POLICE FOUND THE HOUSE EMPTY. ONLY A VHS TAPE ON THE TABLE, LABELED WITH YOUR NAMES.",
        };

        public static bool HasTwist(EndingId id) => (int)id > 0 && (int)id < Twists.Length;

        public static string Text(EndingResult r)
        {
            int i = (int)r.Id;
            string t = i >= 0 && i < Base.Length ? Base[i] : Base[(int)EndingId.SecondClass];
            if (r.Escaped > 0)
            {
                if (r.Lost == 0) t += "\n\nEVERYONE MADE IT OUT. NOBODY WILL EVER BELIEVE YOU.";
                else t += "\n\nNOT EVERYONE MADE IT. SOME OF YOU ARE STILL IN THE PENS.";
            }
            if (r.Twist > 0 && r.Twist < Twists.Length) t += "\n\n" + Twists[r.Twist];
            return t;
        }

        public static string Outcome(EndingEntry e)
        {
            if (e.Role == PlayerRole.Omar) return "";
            switch (e.Life)
            {
                case LifeState.Escaped: return RouteName(e.Route);
                case LifeState.Dead: return "BUTCHERED";
                case LifeState.Caged: return "LEFT IN THE PENS";
                case LifeState.Gone: return "LOST SIGNAL";
                default: return "STILL INSIDE";
            }
        }
    }

    /// <summary>Texts of the notes scattered around the level.</summary>
    public static class NoteTexts
    {
        static readonly string[] Ordinals = { "FIRST", "SECOND", "THIRD", "FOURTH" };

        public static string CodeNote(int position, int digit, DeterministicRandom rng, bool onWall)
        {
            string[] mask = { "_", "_", "_", "_" };
            mask[position] = digit.ToString();
            string pattern = string.Join(" ", mask);
            switch (rng.Range(0, 4))
            {
                case 0: return pattern + "\n\nTHE SHELTER DOOR. DON'T FORGET.";
                case 1: return "THE " + Ordinals[position] + " NUMBER IS " + digit + "\n\nTHE BUNKER UNDER THE HOUSE. THE KEYPAD.";
                case 2: return onWall ? pattern + "\n\nHE CHANGES IT EVERY NIGHT" : "...NUMBER " + (position + 1) + " OF THE CODE IS " + digit + ". I HEARD HIM SAY IT TO THE RADIO.";
                default: return pattern + "\n\nIF YOU READ THIS, GET OUT THROUGH THE SHELTER.";
            }
        }

        // ---------------------------------------------------------------- (iteration 3) hints for code locks, the tape

        /// <summary>Note spots a prisoner can't reach before escaping (behind the shelter door, past the chained main gate):
        /// never a code digit or a hint there.</summary>
        public static bool LateArea(string area) => area == "Tunnel" || area == "OutsideGate";

        static readonly Dictionary<string, string> Places = new Dictionary<string, string>
        {
            { "Living", "LIVING ROOM" }, { "Dining", "DINING ROOM" }, { "Hall", "DOWNSTAIRS HALL" }, { "UpperHall", "UPSTAIRS HALL" },
            { "ClockBedroom", "BEDROOM WITH THE BIG CLOCK" }, { "CageRoom", "ROOM WITH THE CAGES" }, { "Storage", "STORAGE ROOM" },
            { "Closet", "SUPPLY CLOSET" }, { "RadioRoom", "RADIO ROOM" }, { "Study", "STUDY" }, { "Kitchen", "KITCHEN" },
            { "Bathroom", "BATHROOM" }, { "Stairs", "STAIRS" }, { "Generator", "GENERATOR ROOM" }, { "Furnace", "FURNACE ROOM" },
            { "Antechamber", "BASEMENT" }, { "Corridor", "BASEMENT CORRIDOR" }, { "Exhibit", "BASEMENT" }, { "ParkingLot", "PARKING LOT" },
            { "FuelDepot", "FUEL DEPOT" }, { "WaterTower", "WATER TOWER" }, { "OutsideGate", "ROAD" },
        };

        /// <summary>"House.ClockBedroom" -> "BEDROOM WITH THE BIG CLOCK" (what a person would call the place).</summary>
        public static string PlaceName(string area)
        {
            if (string.IsNullOrEmpty(area)) return "HOUSE";
            int dot = area.LastIndexOf('.');
            string s = dot >= 0 ? area.Substring(dot + 1) : area;
            if (Places.TryGetValue(s, out var nice)) return nice;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                if (i > 0 && char.IsUpper(s[i]) && !char.IsUpper(s[i - 1])) sb.Append(' ');
                sb.Append(char.ToUpperInvariant(s[i]));
            }
            return sb.ToString();
        }

        static string Spaced(string code) => string.Join("-", System.Linq.Enumerable.Select(code, c => c.ToString()));

        /// <summary>A note that gives away a code lock's code (always readable, never cryptic beyond a nudge).</summary>
        public static string LockHint(CodeLockEntity lk, string place, DeterministicRandom rng, bool onWall)
        {
            string code = lk.Pretty();
            switch (lk.Info.Kind)
            {
                case Map.CodeKind.Time:
                    switch (rng.Range(0, 3))
                    {
                        case 0: return "THE CLOCK STOPPED AT " + code + " THE NIGHT SHE CAME HOME.\n\nHE NEVER LET ANYONE WIND IT AGAIN.";
                        case 1: return code + "\n" + code + "\n" + code + "\n\nSET THE HANDS. THE WALL OPENS.";
                        default: return "MAMA'S CLOCK. " + code + ". ALWAYS " + code + ".";
                    }
                case Map.CodeKind.Sequence:
                    return onWall ? code : "THE BUTTONS IN THE " + place + ":\n\n" + code + "\n\nIN THAT ORDER. DON'T PRESS ANYTHING ELSE.";
                default:
                    string spaced = Spaced(lk.Code);
                    switch (rng.Range(0, 4))
                    {
                        case 0: return "THE LOCKED DRAWER IN THE " + place + "\n\n" + spaced;
                        case 1: return onWall ? spaced + "\n\n(" + place + ")" : "SHE KEEPS HER THINGS LOCKED IN THE " + place + ". THE LITTLE DIAL LOCK: " + spaced + ".";
                        case 2: return "COMBINATION - " + place + " - " + spaced + "\n\nHE DOESN'T KNOW I SAW.";
                        default: return spaced + "\n\nTHE PADLOCK IN THE " + place + ". BURN THIS.";
                    }
            }
        }

        /// <summary>Index of the shot in <see cref="TapeShots"/> that gives the code away.</summary>
        public const int TapeCodeShot = 3;
        /// <summary>Index of the shot during which the scream on the tape comes (Tuning.TapeScreamAt falls in it).</summary>
        public const int TapeScreamShot = 4;
        /// <summary>Index of the shot that may hold a spare digit of the shelter code (a child's drawing).</summary>
        public const int TapeSpareShot = 1;

        /// <summary>The home video on the tape, one captioned shot (with its picture, Textures/Props/tape_*) per slot, picked
        /// by the seed: an opening, the family, the man, the code (<paramref name="lk"/>, may be null: then a shelter digit),
        /// the jump with the scream, the aftermath, the end (and, rarely, an empty wheelchair). <paramref name="spareDigit"/>
        /// = the shelter code position the child's drawing gives away (-1 = none on this tape).</summary>
        public static TapeShot[] TapeShots(CodeLockEntity lk, string place, int[] shelter, DeterministicRandom rng, out int spareDigit)
        {
            string date = "OCT 31 19" + rng.Range(84, 92);
            var shots = new TapeShot[7];
            switch (rng.Range(0, 3))
            {
                case 0: shots[0] = Shot("kitchen", date + "  11:48 PM\n\nA KITCHEN. SOMEONE HUMS. THE CAMERA SHAKES."); break;
                case 1: shots[0] = Shot("porch", date + "  9:02 PM\n\nTHE FRONT PORCH. TWO PUMPKINS GRIN IN THE DARK. SOMEONE GIGGLES BEHIND THE CAMERA."); break;
                default: shots[0] = Shot("livingtv", date + "  10:15 PM\n\nTHIS VERY ROOM. THE TV HISSES. THE CHAIR IN FRONT OF IT IS EMPTY."); break;
            }
            // the tape gives a shelter digit anyway when there is no lock for it: the drawing then shows another one
            spareDigit = -1;
            int first = shelter != null && shelter.Length > 0 ? shelter[0] : 0;
            if (shelter != null && shelter.Length == 4 && rng.Chance(0.5f)) spareDigit = lk == null ? rng.Range(1, 4) : rng.Range(0, 4);
            if (spareDigit >= 0)
                shots[1] = Shot("drawing", "A CHILD'S DRAWING ON THE FRIDGE: A HOUSE, A BIG DARK MAN, A RED " + shelter[spareDigit] + ".\n\n'MY BOY DREW THE "
                                + Ordinals[spareDigit] + " NUMBER OF THE BUNKER. CLEVER BOY.'");
            else
                switch (rng.Range(0, 3))
                {
                    case 0: shots[1] = Shot("cake", "AN OLD WOMAN IN A WHEELCHAIR. A CAKE. SHE IS TRYING TO BLOW OUT THE CANDLES."); break;
                    case 1: shots[1] = Shot("cake", "SHE SINGS TO THE CAMERA, OFF KEY: 'HAPPY BIRTHDAY TO YOU...' NOBODY SINGS WITH HER."); break;
                    default: shots[1] = Shot("drawing", "A CHILD'S DRAWING ON THE FRIDGE: A HOUSE, A WOMAN IN A CHAIR, A BIG DARK MAN WITH NO FACE."); break;
                }
            switch (rng.Range(0, 3))
            {
                case 0: shots[2] = Shot("doorway", "A HUGE MAN FILLS THE DOORWAY BEHIND HER. HE DOES NOT MOVE. HE HOLDS A CLEAVER."); break;
                case 1: shots[2] = Shot("reflection", "THE TV IS OFF. IN THE BLACK GLASS: THE ONE HOLDING THE CAMERA. HE IS ENORMOUS."); break;
                default: shots[2] = Shot("hand", "HEAVY BREATHING BEHIND THE CAMERA. A HUGE HAND COVERS THE LENS. SHE LAUGHS: 'NOT YET, MY BOY.'"); break;
            }
            if (lk != null && lk.Info.Kind == Map.CodeKind.Time)
                shots[TapeCodeShot] = Shot("clock", "SHE POINTS AT THE BIG CLOCK. IT SAYS " + lk.Pretty() + ".\n\n'IT STOPPED WHEN YOUR FATHER LEFT. LEAVE IT LIKE THAT, MY BOY.'");
            else if (lk != null)
                shots[TapeCodeShot] = Shot("padlock", "SHE LAUGHS AND TAPS A LITTLE PADLOCK.\n\n'MY THINGS IN THE " + place + ". " + Spaced(lk.Code) + ". DON'T YOU FORGET IT, MY BOY.'");
            else
                shots[TapeCodeShot] = Shot("mama", "SHE WHISPERS TO THE CAMERA: 'THE BUNKER. IT STARTS WITH " + first + "...'");
            shots[TapeScreamShot] = rng.Chance(0.5f)
                ? Shot(null, "THE PICTURE JUMPS. SOMEONE SCREAMS. STATIC.")
                : Shot(null, "THE PICTURE ROLLS. A SCREAM. FOR A MOMENT THE DATE IN THE CORNER SAYS NOV 02 1993.");
            switch (rng.Range(0, 3))
            {
                case 0: shots[5] = Shot("cage", "A CAGE IN A DARK ROOM. SOMEONE INSIDE RATTLES THE DOOR. THE CAMERA MOVES CLOSER. AND CLOSER."); break;
                case 1: shots[5] = Shot("corn", "THE YARD AT NIGHT. A FLASHLIGHT RUNS BETWEEN THE CORN. THE MAN WALKS AFTER IT. HE IS NOT IN A HURRY."); break;
                default: shots[5] = Shot("stairs", "THE BASEMENT STAIRS. A FLASHLIGHT GOES DOWN. A DOOR SLAMS. THE LIGHT DOES NOT COME BACK UP."); break;
            }
            if (rng.Chance(0.05f))
                shots[6] = Shot("wheelchair", "THE CAMERA TURNS. A LIVING ROOM. A TV PLAYING A BLUE SCREEN. AN EMPTY WHEELCHAIR.\n\n■ STOP");
            else
                switch (rng.Range(0, 3))
                {
                    case 0: shots[6] = Shot("smile", "THE MAN LOOKS INTO THE LENS. HE SMILES.\n\n■ STOP"); break;
                    case 1: shots[6] = Shot("fallen", "THE CAMERA FALLS. SIDEWAYS: CAGES, A BARE FOOT. IT DOES NOT MOVE.\n\n■ STOP"); break;
                    default: shots[6] = Shot("mama", "SHE LEANS INTO THE LENS: 'BE GOOD FOR MAMA.'\n\n■ STOP"); break;
                }
            return shots;
        }

        static TapeShot Shot(string frame, string caption) => new TapeShot { Frame = frame, Caption = caption };

        /// <summary>(iteration 3) The note that points at tonight's tape (<see cref="ItemSpawner.TapePlace"/> kinds).</summary>
        public static string TapePointer(string kind, string place)
        {
            switch (kind)
            {
                case "underbed": return "MAMA'S TAPE IS UNDER THE BED WITH THE DEAD MAN. HE STOLE IT FOR HER. HE DIDN'T GET FAR.";
                case "desk": return "HE KEEPS HER BIRTHDAY TAPE IN HIS DESK UPSTAIRS. HE WATCHES IT ALONE.";
                case "locker": return "THE TAPE IS ON TOP OF THE LOCKER IN THE RADIO ROOM. SO SHE CAN'T REACH IT.";
                case "sofa": return "SHE SITS ON IT. THE TAPE IS ON THE SOFA RIGHT BEHIND HER. DON'T LET HER HEAR YOU.";
                case "shelf": return "HE PUT MAMA'S TAPE ON THE SHELF WITH OUR THINGS. IN THE STORAGE ROOM.";
                case "cagetable": return "THE TAPE IS ON THE TABLE BY THE CAGES. HE WANTS US TO WATCH IT.";
                case "crate": return "HE DROPPED MAMA'S TAPE RUNNING DOWN TO THE BASEMENT. IT'S IN THE CRATE BY THE STAIRS.";
                default: return "SHE LOCKED HER TAPE IN A DRAWER IN THE " + place + ". A LITTLE PADLOCK.";
            }
        }

        public static readonly string[] Lore =
        {
            "SHE ONLY GOES QUIET WHEN THE BIRTHDAY TAPE PLAYS. THEN SHE SEES NOTHING BUT THE SCREEN.",
            "THE OLD WOMAN NEVER LOOKS AWAY FROM THE TV. COME FROM BEHIND. SLOW. LOW.",
            "THE TAPE HAS A SCREAM ON IT. HE ALWAYS COMES DOWN TO SEE WHO PUT IT ON.",
            "THOSE LITTLE KEYS FIT ALL HER CHEAP PADLOCKS.",
            "DAY 41. HE CALLS THIS PLACE THE BASE OF THE SECOND CLASS. THE FIRST CLASS NEVER CAME BACK.",
            "IF THE PICTURE STARTS TO HISS AND JUMP, HE IS CLOSE. HIDE. DON'T BREATHE.",
            "THE CAR IN THE LOT STILL RUNS. THE KEYS ARE SOMEWHERE IN THE HOUSE. THE TANK IS EMPTY.",
            "THE RADIO UPSTAIRS WORKS IF THE POWER IS ON. SOMEONE TOOK THE FUSE FROM THE BOX IN THE BASEMENT.",
            "WATCH YOUR FEET. HE STRINGS WIRES ACROSS THE DOORWAYS. WHEN THE SIREN GOES OFF, HE COMES RUNNING.",
            "THE DRUMS BEHIND THE BARN ARE FULL OF FUEL. ONE SPARK AND THE FENCE IS GONE.",
            "THE MANNEQUINS DOWNSTAIRS MOVE WHEN YOU DON'T LOOK AT THEM. I'M SURE OF IT NOW.",
            "HE CAN'T SEE YOU IN THE DARK. HE SEES THE FLAME. HE HEARS YOU RUN.",
            "THE GATE CHAIN IS OLD. BOLT CUTTERS WOULD DO IT. HE KEEPS TOOLS IN THE SHED AND THE BARN.",
            "HE TAKES EVERYTHING FROM YOU WHEN HE PUTS YOU BACK IN THE CAGE. EVERYTHING BUT THE LIGHTER.",
            "THIRD TIME HE CATCHES YOU, HE DOESN'T BRING YOU BACK UPSTAIRS.",
            "THE WARDROBES ARE SAFE. UNLESS HE ALREADY SAW YOU GO IN.",
            "SOMETIMES THE WHOLE HOUSE BENDS. THE WALLS SWIM. THE TV SAYS IT'S A TRACKING ERROR.",
            "I COUNTED 4 CAGES. THERE WERE 5 OF US.",
            "PLAY. REWIND. PLAY. REWIND. HE WATCHES THE TAPES OF THE OTHERS EVERY NIGHT.",
            "THE PHONE IN THE KITCHEN RINGS AT 3 AM. DON'T ANSWER IT.",
        };
    }

    /// <summary>(iteration 3) One shot of the home video: its caption and its picture (Textures/Props/tape_&lt;Frame&gt;, null = static).</summary>
    public struct TapeShot
    {
        public string Caption, Frame;
    }
}
