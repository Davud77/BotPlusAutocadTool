// ============================================================================
// OOXGN - Copyright (c) 2026. All rights reserved.
// Author: OOXGN | Project: BotPlusAutocadTool
// SPDX-License-Identifier: AGPL-3.0-only
// ============================================================================

using System.Collections.Generic;

namespace BotPlusAutocadTool.Models
{
    /// <summary>
    /// Container for all stamp properties
    /// </summary>
    public class StampProperties
    {
        public Dictionary<string, string> Properties { get; set; }

        public StampProperties()
        {
            Properties = new Dictionary<string, string>();
        }

        public string GetValue(string key)
        {
            return Properties.TryGetValue(key, out string value) ? value : string.Empty;
        }

        public void SetValue(string key, string value)
        {
            if (Properties.ContainsKey(key))
                Properties[key] = value;
            else
                Properties.Add(key, value);
        }
    }
}

