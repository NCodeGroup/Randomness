// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using FsCheck;
using FsCheck.Xunit;
using NCode.Randomness.Nist.Pulses;
using NCode.Randomness.Nist.Tests.Fixtures;

namespace NCode.Randomness.Nist.Tests.Pulses;

public class NistPulsePropertyTests
{
    private const string Replay = "20261125,1";

    #region Parse Tests

    [Property(Replay = Replay)]
    public void Parse_WithAnyBytes_ThrowsNistFormatException(NonNull<byte[]> bytes)
    {
        Assert.Throws<NistFormatException>(() => NistPulse.Parse(bytes.Get));
    }

    [Property(Replay = Replay, MaxTest = 500)]
    public void Parse_WithOneByteChanged_ThrowsNistFormatExceptionOrKeepsTheOutput(
        NonNegativeInt position,
        byte value
    )
    {
        var json = NistFixtures.Pulse(NistFixtures.LatestIndex);
        var bytes = Encoding.UTF8.GetBytes(json);
        bytes[position.Get % bytes.Length] = value;
        var der = NistFixtures.Certificate("5b6386e6");
        var id = Convert.FromHexString(NistFixtures.CurrentCertificateId);
        using var certificate = NistCertificate.Parse(der, id);

        NistPulse pulse;
        try
        {
            pulse = NistPulse.Parse(bytes);
            pulse.Verify(certificate);
        }
        catch (NistFormatException)
        {
            return;
        }

        var output = Convert.ToHexString(pulse.Output);

        Assert.Equal(NistFixtures.LatestOutput, output);
    }

    #endregion
}
