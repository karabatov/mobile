﻿using System;

namespace Toggl.Phoebe
{
    public static class DateTimeExtensions
    {
        private static readonly DateTime UnixStart = new DateTime (1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        public static TimeSpan? ToUnix (this DateTime? val)
        {
            if (val == null)
                return null;
            return val.Value.ToUnix ();
        }

        public static TimeSpan ToUnix (this DateTime val)
        {
            return val.ToUtc () - UnixStart;
        }

        public static DateTime? ToUtc (this DateTime? val)
        {
            if (val == null)
                return null;
            return val.Value.ToUtc ();
        }

        public static DateTime ToUtc (this DateTime val)
        {
            if (val.Kind == DateTimeKind.Unspecified) {
                return DateTime.SpecifyKind (val, DateTimeKind.Utc);
            } else {
                return val.ToUniversalTime ();
            }
        }

        public static DateTime? Truncate (this DateTime? val, long percisionTicks)
        {
            if (val == null)
                return null;
            return val.Value.Truncate (percisionTicks);
        }

        public static DateTime Truncate (this DateTime val, long percisionTicks)
        {
            return val.AddTicks (-(val.Ticks % percisionTicks));
        }
    }
}

