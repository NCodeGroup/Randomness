// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;

namespace NCode.Randomness.Tests;

public abstract class UnitTestBase : IDisposable
{
    private MockRepository Mocks { get; } = new(MockBehavior.Strict);

    protected Mock<T> CreateStrictMock<T>(params IEnumerable<object> args)
        where T : class => Mocks.Create<T>([.. args]);

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Mocks.Verify();
        }
    }
}
