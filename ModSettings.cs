using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Spriggit_Helper
{
    public class ModSettings
    {
        public static JsonSerializerOptions serializerOptions = new JsonSerializerOptions() { WriteIndented = true };

        public Dictionary<string, string> MasterLocations { get; set; } = [];
        public string SpriggitPath { get; set; } = "";
    }
}
