// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Randomness.Tests.TestDoubles;

/// <summary>
/// A beacon whose answers a test scripts: each fetch takes the next answer, a pulse or an exception, and the last
/// answer repeats. A gate can hold a fetch until the test releases it.
/// </summary>
internal sealed class FakeBeacon(string name) : IRandomnessBeacon
{
    private Lock Gate { get; } = new();

    private Queue<Func<BeaconPulse>> Answers { get; } = new();

    private Func<BeaconPulse>? Last { get; set; }

    public string Name { get; } = name;

    public int Fetches { get; private set; }

    public TaskCompletionSource? Hold { get; set; }

    public FakeBeacon Returns(BeaconPulse pulse) => Enqueue(() => pulse);

    public FakeBeacon Throws(Exception exception) => Enqueue(() => throw exception);

    public async ValueTask<BeaconPulse> GetLatestPulseAsync(CancellationToken cancellationToken)
    {
        Func<BeaconPulse> answer;
        lock (Gate)
        {
            Fetches++;
            answer =
                Answers.Count > 0
                    ? Answers.Dequeue()
                    : Last ?? throw new InvalidOperationException("No answer.");
            Last = answer;
        }

        if (Hold is { } hold)
        {
            await hold.Task.WaitAsync(cancellationToken);
        }

        return answer();
    }

    public ValueTask<BeaconPulse> GetPulseAsync(long round, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    private FakeBeacon Enqueue(Func<BeaconPulse> answer)
    {
        lock (Gate)
        {
            Answers.Enqueue(answer);
        }

        return this;
    }
}
