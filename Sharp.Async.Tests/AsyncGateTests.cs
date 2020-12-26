/*
    Copyright 2020 Jeffrey Sharp

    Permission to use, copy, modify, and distribute this software for any
    purpose with or without fee is hereby granted, provided that the above
    copyright notice and this permission notice appear in all copies.

    THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES
    WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF
    MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR
    ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES
    WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN
    ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF
    OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.
*/

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Extensions;
using NUnit.Framework;

namespace Sharp.Async
{
    [TestFixture, NonParallelizable]
    public class AsyncGateTests
    {
        [Test]
        public void Construct_Closed()
        {
            var gate = new AsyncGate();

            ShouldBeClosed(gate);
        }

        [Test]
        public void Construct_Open()
        {
            var gate = new AsyncGate(open: true);

            ShouldBeOpen(gate);
        }

        [Test]
        public void WhenClosed_Close()
        {
            var gate = new AsyncGate();

            gate.Close();

            ShouldBeClosed(gate);
        }

        [Test]
        public void WhenClosed_SetClosed()
        {
            var gate = new AsyncGate();

            gate.IsOpen = false;

            ShouldBeClosed(gate);
        }

        [Test]
        public void WhenClosed_Open()
        {
            var gate = new AsyncGate();

            gate.Open();

            ShouldBeOpen(gate);
        }

        [Test]
        public void WhenClosed_SetOpen()
        {
            var gate = new AsyncGate();

            gate.IsOpen = true;

            ShouldBeOpen(gate);
        }

        [Test]
        public void WhenOpen_Close()
        {
            var gate = new AsyncGate(open: true);

            gate.Close();

            ShouldBeClosed(gate);
        }

        [Test]
        public void WhenOpen_SetClosed()
        {
            var gate = new AsyncGate(open: true);

            gate.IsOpen = false;

            ShouldBeClosed(gate);
        }

        [Test]
        public void WhenOpen_Open()
        {
            var gate = new AsyncGate(open: true);

            gate.Open();

            ShouldBeOpen(gate);
        }

        [Test]
        public void WhenOpen_SetOpen()
        {
            var gate = new AsyncGate(open: true);

            gate.IsOpen = true;

            ShouldBeOpen(gate);
        }

        [Test]
        public void OnOpeningWhenTasksAreWaiting()
        {
            var gate = new AsyncGate();
            var sign = 0;

            var task = Task.Run(async () =>
            {
                sign = 1;
                await gate.WaitAsync();
                sign = 2;
            });

            WaitUntil(() => sign == 1, 100.Milliseconds());

            gate.Open();

            WaitUntil(() => sign == 2, 100.Milliseconds());

            task.Wait(100.Milliseconds()).Should().BeTrue();
        }

        [Test]
        public void Stampede()
        {
            var gate = new AsyncGate();

            var herd = Enumerable
                .Range(0, Environment.ProcessorCount * 32)
                .Select(_ => Task.Run(async () =>
                {
                    await gate.WaitAsync();
                }))
                .ToList();

            gate.Open();

            Task.WhenAll(herd);
        }

        private static void ShouldBeClosed(AsyncGate gate)
        {
            gate.IsOpen.Should().BeFalse();
            gate.WaitAsync().Wait(millisecondsTimeout: 100).Should().BeFalse();
        }

        private static void ShouldBeOpen(AsyncGate gate)
        {
            gate.IsOpen.Should().BeTrue();
            gate.WaitAsync().Wait(millisecondsTimeout: 100).Should().BeTrue();
        }

        private static void WaitUntil(Func<bool> condition, TimeSpan timeout)
        {
            var limit = DateTime.UtcNow + timeout;

            while (!condition())
            {
                Thread.Yield();

                if (DateTime.UtcNow >= limit)
                    Assert.Fail("Timed out waiting on condition.");
            }
        }
    }
}
