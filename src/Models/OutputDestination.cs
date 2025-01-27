using System;
using System.ComponentModel;

namespace MicroDude.Models
{
    public enum OutputDestination
    {
        [Description("None")]
        None = 0,

        [Description("MicroDude")]
        MicroDude = 1,

        [Description("MicroDude or Active Pane")]
        MicroDudeOrActivePane = 2
    }
}