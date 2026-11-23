using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace FootballBoard.Domain.Config
{
    /// <summary>
    /// Reads and writes rules.json. Unknown keys and wrong types are errors (to catch typos),
    /// missing keys keep their defaults, and // comments are allowed.
    /// </summary>
    public static class RulesConfigLoader
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            MissingMemberHandling = MissingMemberHandling.Error,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            NullValueHandling = NullValueHandling.Include,
            Formatting = Formatting.Indented,
            MaxDepth = 16
        };

        /// <summary>Parses and validates. Throws <see cref="RulesConfigException"/> on any problem.</summary>
        public static RulesConfig FromJson(string json)
        {
            if (TryFromJson(json, out var config, out var errors)) return config;
            throw new RulesConfigException(errors);
        }

        public static bool TryFromJson(string json, out RulesConfig config, out IReadOnlyList<string> errors)
        {
            config = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                errors = new[] { "The rules file is empty." };
                return false;
            }

            RulesConfig parsed;
            try
            {
                parsed = JsonConvert.DeserializeObject<RulesConfig>(json, Settings);
            }
            catch (JsonException ex)
            {
                errors = new[] { "The rules file is not valid: " + ex.Message };
                return false;
            }

            if (parsed == null)
            {
                errors = new[] { "The rules file does not contain an object." };
                return false;
            }

            var problems = parsed.Validate();
            if (problems.Count > 0)
            {
                errors = problems;
                return false;
            }

            config = parsed;
            errors = Array.Empty<string>();
            return true;
        }

        /// <summary>
        /// Loads the file but never fails: on any problem it returns the built-in defaults
        /// and reports the errors so the caller can log them.
        /// </summary>
        public static RulesConfig FromJsonOrDefault(string json, out IReadOnlyList<string> errors)
        {
            return TryFromJson(json, out var config, out errors) ? config : RulesConfig.CreateDefault();
        }

        public static string ToJson(RulesConfig config) => JsonConvert.SerializeObject(config, Settings);

        /// <summary>Deep copy, so a running match cannot be affected by later edits to the config object.</summary>
        public static RulesConfig Clone(RulesConfig config) =>
            JsonConvert.DeserializeObject<RulesConfig>(ToJson(config), Settings);
    }
}
