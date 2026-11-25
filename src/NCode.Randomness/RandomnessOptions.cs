// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness;

/// <summary>
/// Settings for the secure generator and the beacon collector, bound from the <c>Randomness</c> configuration section.
/// </summary>
public sealed class RandomnessOptions
{
    /// <summary>
    /// The configuration section these options bind from by convention.
    /// </summary>
    public const string SectionName = "Randomness";

    /// <summary>
    /// Gets or sets how the registered beacons are used. The default is <see cref="BeaconMode.Continuous"/>.
    /// </summary>
    public BeaconMode Mode { get; set; } = BeaconMode.Continuous;

    /// <summary>
    /// Gets or sets how often each beacon is asked for its latest pulse. The default is one minute, the cadence at
    /// which CURBy publishes.
    /// </summary>
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the longest wait between attempts while a beacon fails; the wait doubles from
    /// <see cref="RefreshInterval"/> up to it, with jitter. The default is fifteen minutes.
    /// </summary>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Gets or sets how long one attempt to fetch a pulse may take. The default is thirty seconds.
    /// </summary>
    public TimeSpan FetchTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the oldest a pulse may be, by its own timestamp, to be mixed in; an older one means a beacon or a
    /// mirror has stalled. It applies to every beacon <see cref="MaxPulseAgeByBeacon"/> doesn't name. The default is
    /// ten minutes.
    /// </summary>
    public TimeSpan MaxPulseAge { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Gets or sets the oldest a pulse may be, by beacon name, for the beacons it names, in place of
    /// <see cref="MaxPulseAge"/>, such as <c>Randomness:MaxPulseAgeByBeacon:curby-q</c> for a beacon that publishes
    /// less often. A name that no registered beacon has fails the host at start. Empty by default.
    /// </summary>
    public Dictionary<string, TimeSpan> MaxPulseAgeByBeacon { get; set; } =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets how far a pulse's timestamp may lie ahead of this machine's clock. The default is two minutes.
    /// </summary>
    public TimeSpan MaxClockSkew { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Gets or sets whether the host fails to start unless a beacon pulse is mixed in within
    /// <see cref="InitialPulseTimeout"/>. The default is <see langword="false"/>: the generator is secure from the
    /// operating system's entropy alone, so a beacon outage never stops an application.
    /// </summary>
    public bool RequireInitialPulse { get; set; }

    /// <summary>
    /// Gets or sets how long the host waits at start for the first pulse when <see cref="RequireInitialPulse"/> is set.
    /// The default is thirty seconds.
    /// </summary>
    public TimeSpan InitialPulseTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the longest any generator shard runs before it reseeds from the operating system. The default is
    /// one minute. A shard also reseeds as soon as a new beacon pulse is mixed in.
    /// </summary>
    public TimeSpan ReseedInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the most bytes any generator shard produces before it reseeds from the operating system. The
    /// default is one mebibyte.
    /// </summary>
    public int ReseedAfterBytes { get; set; } = 1 << 20;
}
