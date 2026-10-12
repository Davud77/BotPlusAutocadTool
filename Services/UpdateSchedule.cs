// SPDX-License-Identifier: AGPL-3.0-only
using System;
namespace BotPlusAutocadTool.Services
{
    public sealed class UpdateSchedule
    {
        public DateTime NextCheckUtc { get; private set; } = DateTime.MinValue;
        public bool IsDue(DateTime utcNow) => utcNow >= NextCheckUtc;
        public void Checked(DateTime utcNow) => NextCheckUtc = utcNow.AddHours(24);
        public void NewSession() => NextCheckUtc = DateTime.MinValue;
    }
}
