// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;

namespace NCode.Randomness.Tests;

public abstract class UnitTestBase : IDisposable
{
    private readonly MockRepository _mocks = new(MockBehavior.Strict);

    protected Mock<T> CreateStrictMock<T>(params object[] args)
        where T : class => _mocks.Create<T>(args);

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _mocks.Verify();
        }
    }
}
