﻿using System;
using System.IO;

namespace Vertica.Integration.Infrastructure.Extensions
{
    internal static class ActionRepeaterExtensions
    {
        public static ActionRepeater Repeat(this Action action, TimeSpan delay, TextWriter outputter = null)
        {
            return ActionRepeater.Start(action, delay, outputter);
        }
    }
}