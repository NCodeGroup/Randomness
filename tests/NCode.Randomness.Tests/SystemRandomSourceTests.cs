// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Tests;

public class SystemRandomSourceTests
{
    #region Fill Tests

    [Fact]
    public void Fill_WithBuffer_FillsItFromTheOperatingSystem()
    {
        var first = SystemRandomSource.Instance.GetBytes(32);
        var second = SystemRandomSource.Instance.GetBytes(32);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Instance_IsASecureRandom()
    {
        Assert.IsAssignableFrom<ISecureRandom>(SystemRandomSource.Instance);
    }

    #endregion
}
