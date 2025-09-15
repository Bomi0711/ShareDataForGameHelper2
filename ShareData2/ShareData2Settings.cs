// <copyright file="ShareData2Settings.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace ShareData2
{
    using GameHelper.Plugin;

    /// <summary>
    ///     Settings for ShareData2 plugin.
    /// </summary>
    public sealed class ShareData2Settings : IPSettings
    {
        /// <summary>
        ///     Gets or sets a value indicating whether the plugin is enabled.
        /// </summary>
        public bool Enable { get; set; } = true;

        /// <summary>
        ///     Gets or sets the HTTP server port.
        /// </summary>
        public int Port { get; set; } = 53868;

        /// <summary>
        ///     Gets or sets a value indicating whether to show debug information.
        /// </summary>
        public bool ShowDebugInfo { get; set; } = false;

        /// <summary>
        ///     Gets or sets a value indicating whether to enable detailed logging.
        /// </summary>
        public bool EnableDetailedLogging { get; set; } = false;
    }
}
