using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Extensions;
using NUnit.Framework;

namespace Sharp.Async
{
    [TestFixture]
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
