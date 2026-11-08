using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using BuildingModel.Model;

namespace WoodFraming.Connections
{
    /// <summary>
    /// The project's defaults for the details that join the model's members (owner 2026-11-07: not in the building model): the
    /// floors' and the roof's wood structural panels (SDPWS Tables 4.2A and 4.2C) and their nails and blocking, the framing's
    /// nailed face, an upper story's sill nail and a top plate splice's nail. Its own section of the project file
    /// ("ConnectionDefaults"); a file without it, or without one value, takes the starting values (<see cref="Start"/>, the one
    /// place they are written). The designs read it; none writes it (a page to edit it is on the ROADMAP).
    /// </summary>
    [DataContract]
    public sealed class ConnectionDefaults
    {
        public const string Key = "ConnectionDefaults";

        /// <summary>The floors' sheathing thickness (in): their diaphragm, and the gap an upper story's sill nails cross (AWC TR12).</summary>
        [DataMember(Order = 1)] public double FloorSheathing { get; set; }
        /// <summary>An upper story's sill nail (a common nail by pennyweight, NDS Appendix Table L4).</summary>
        [DataMember(Order = 2)] public string SillNail { get; set; }
        /// <summary>The diaphragms' panel grade: "SheathingAndSingleFloor" or "StructuralI".</summary>
        [DataMember(Order = 3)] public string DiaphragmGrade { get; set; }
        [DataMember(Order = 4)] public string FloorNail { get; set; }
        [DataMember(Order = 5)] public bool FloorBlocked { get; set; }
        /// <summary>The roof's sheathing thickness (in), its nail and blocking.</summary>
        [DataMember(Order = 6)] public double RoofSheathing { get; set; }
        [DataMember(Order = 7)] public string RoofNail { get; set; }
        [DataMember(Order = 8)] public bool RoofBlocked { get; set; }
        /// <summary>The nominal width of the framing's nailed face at the panel edges and boundaries (in).</summary>
        [DataMember(Order = 9)] public double DiaphragmFramingWidth { get; set; }
        /// <summary>The nail lapping a double top plate at a splice where the plates are a collector.</summary>
        [DataMember(Order = 10)] public string SpliceNail { get; set; }
        /// <summary>The toe-nail of a rim (or the roof's blocking) into the top plates of the wall under it (NDS 12.1.6.3).</summary>
        [DataMember(Order = 11)] public string ToeNail { get; set; }

        public ConnectionDefaults() => Start();

        /// <summary>The program's starting values: the one place they are written. A value missing from a file keeps its starting value.</summary>
        private void Start()
        {
            FloorSheathing = 23.0 / 32.0;
            SillNail = "16d";
            DiaphragmGrade = "SheathingAndSingleFloor";
            FloorNail = "10d";
            FloorBlocked = true;
            RoofSheathing = 7.0 / 16.0;
            RoofNail = "8d";
            RoofBlocked = false;
            DiaphragmFramingWidth = 2;
            SpliceNail = "16d";
            ToeNail = "8d";
        }

        [OnDeserializing]
        private void Starting(StreamingContext c) => Start();

        public bool StructuralI => DiaphragmGrade == "StructuralI";

        public ConnectionDefaults Clone() => (ConnectionDefaults)MemberwiseClone();

        private static DataContractJsonSerializer Serializer() => new DataContractJsonSerializer(typeof(ConnectionDefaults));

        /// <summary>The project's (the starting values where it has none, or it cannot be read).</summary>
        public static ConnectionDefaults Read(Building b)
        {
            string json = b?.GetData(Key);
            if (string.IsNullOrEmpty(json)) return new ConnectionDefaults();
            try
            {
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                    return (ConnectionDefaults)Serializer().ReadObject(ms) ?? new ConnectionDefaults();
            }
            catch (SerializationException) { return new ConnectionDefaults(); }
        }

        public string ToJson()
        {
            using (var ms = new MemoryStream())
            {
                Serializer().WriteObject(ms, this);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        public static void Write(Building b, ConnectionDefaults d) => b?.SetData(Key, d?.ToJson());
    }
}
