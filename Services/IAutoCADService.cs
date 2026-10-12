// ============================================================================
// OOXGN - Copyright (c) 2026. All rights reserved.
// Author: OOXGN | Project: BotPlusAutocadTool
// SPDX-License-Identifier: AGPL-3.0-only
// ============================================================================

using BotPlusAutocadTool.Models;

namespace BotPlusAutocadTool.Services
{
    /// <summary>
    /// Service interface for AutoCAD database operations
    /// </summary>
    public interface IAutoCADService
    {
        StampProperties LoadStampProperties();
        void SaveStampProperties(StampProperties properties);
    }
}

