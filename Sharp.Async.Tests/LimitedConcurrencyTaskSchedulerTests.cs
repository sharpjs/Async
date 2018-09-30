/*
    Copyright (C) 2018 Jeffrey Sharp

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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Extensions;
using NUnit.Framework;

namespace Sharp.Async.Tests
{
    [TestFixture]
    public class LimitedConcurrencyTaskSchedulerTests
    {
        private static readonly int
            CoreCount = Environment.ProcessorCount;

        private static readonly TimeSpan
            SleepTime = 3.Seconds(),
            GraceTime = 1.Seconds();

        [Test]
        public void Construct_ZeroConcurrency()
        {
            this.Invoking(_ => new LimitedConcurrencyTaskScheduler(0))
                .Should().Throw<ArgumentOutOfRangeException>();
        }

        [Test]
        public void Construct_NegativeConcurrency()
        {
            this.Invoking(_ => new LimitedConcurrencyTaskScheduler(-1))
                .Should().Throw<ArgumentOutOfRangeException>();
        }

        [Test]
        public void MaximumConcurrencyLevel()
        {
            var scheduler = new LimitedConcurrencyTaskScheduler(42);

            scheduler.MaximumConcurrencyLevel.Should().Be(42);
        }

        [Test]
        public void QueueTask_NullTask()
        {
            var scheduler = new FakeDispatchScheduler();

            scheduler
                .Invoking(s => s.QueueTask(null))
                .Should().Throw<ArgumentNullException>();
        }

        [Test]
        public void TryDequeue_NullTask()
        {
            var scheduler = new FakeDispatchScheduler();

            scheduler.TryDequeue(null).Should().BeFalse();
        }

        [Test]
        public void Queue_Initial()
        {
            var scheduler = new FakeDispatchScheduler();

            scheduler.GetScheduledTasks() .Should().BeEmpty();
            scheduler.DispatcherStartCount.Should().Be(0);
        }

        [Test]
        public void Queue_Initial_ThenTryDequeue()
        {
            var scheduler = new FakeDispatchScheduler();

            scheduler.TryDequeue(ATask()).Should().BeFalse();

            scheduler.GetScheduledTasks() .Should().BeEmpty();
            scheduler.DispatcherStartCount.Should().Be(0);
        }

        [Test]
        public void Queue_AfterQueueTasks()
        {
            var scheduler = new FakeDispatchScheduler();
            var task0     = ATask();
            var task1     = ATask();
            var task2     = ATask();

            scheduler.QueueTask(task0);
            scheduler.QueueTask(task1);
            scheduler.QueueTask(task2);

            scheduler.GetScheduledTasks() .Should().BeEquivalentTo(task0, task1, task2);
            scheduler.DispatcherStartCount.Should().Be(3);
        }

        [Test]
        public void Queue_AfterQueueTasks_ThenTryDequeue()
        {
            var scheduler = new FakeDispatchScheduler();
            var task0     = ATask();
            var task1     = ATask();
            var task2     = ATask();

            scheduler.QueueTask(task0);
            scheduler.QueueTask(task1);
            scheduler.QueueTask(task2);
            scheduler.TryDequeue(task1).Should().BeTrue();
            scheduler.TryDequeue(task1).Should().BeFalse();

            scheduler.GetScheduledTasks() .Should().BeEquivalentTo(task0, task2);
            scheduler.DispatcherStartCount.Should().Be(3);
        }

        [Test]
        public void TryExecuteTaskInline_NullTask()
        {
            using (var scheduler = new DelayedDispatchScheduler(concurrency: 1))
            {
                scheduler.TryExecuteTaskInline(null, false).Should().BeFalse();
            }
        }

        [Test]
        public void TryExecuteTaskInline_NotInDispatcherThread()
        {
            using (var scheduler = new DelayedDispatchScheduler(concurrency: 1))
            {
                scheduler.TryExecuteTaskInline(ATask(), false).Should().BeFalse();
            }
        }

        [Test]
        public void TryExecuteTaskInline_NonQueuedTask()
        {
            using (var scheduler = new DelayedDispatchScheduler(concurrency: 1))
            {
                var task1 = ATask();

                var task0 = new Task(() =>
                {
                    // Now inside dispatcher thread
                    task1.RunSynchronously(scheduler);
                    //scheduler.TryExecuteTaskInline(task1, false).Should().BeTrue(); // intent
                });

                task0.Start(scheduler);

                scheduler.EnableDispatch();
                task0.Wait();
                task1.Wait();
            }
        }

        [Test]
        public void TryExecuteTaskInline_QueuedTask()
        {
            using (var scheduler = new DelayedDispatchScheduler(concurrency: 1))
            {
                var task1 = ATask();

                var task0 = new Task(() =>
                {
                    // Now inside dispatcher thread
                    scheduler.TryExecuteTaskInline(task1, true).Should().BeTrue();
                });

                task0.Start(scheduler);
                task1.Start(scheduler);

                scheduler.EnableDispatch();
                task0.Wait();
                task1.Wait();
            }
        }

        [Test]
        public void TryExecuteTaskInline_FailedToDequeueTask()
        {
            using (var scheduler = new DelayedDispatchScheduler(concurrency: 1))
            {
                var task1 = ATask();

                var task0 = new Task(() =>
                {
                    // Now inside dispatcher thread
                    scheduler.TryExecuteTaskInline(task1, true).Should().BeFalse();
                });

                task0.Start(scheduler);
                // Do not queue task1

                scheduler.EnableDispatch();
                task0.Wait();
                task1.Status.Should().Be(TaskStatus.Created);
            }
        }

        [Test]
        public void TryStartDispatcher_ConcurrencyConflict()
        {
            using (var scheduler = new DelayedDispatchScheduler(concurrency: 2))
            {
                var task0 = ATask();
                var task1 = ATask();
                var task2 = ATask();

                task0.Start(scheduler);
                task1.Start(scheduler);
                task2.Start(scheduler);

                scheduler.EnableDispatch();
                task0.Wait();
                task1.Wait();
                task2.Wait();
            }
        }

        [Test, Retry(3)]
        public void Run_UpToConcurrencyLimit()
        {
            var scheduler  = new LimitedConcurrencyTaskScheduler(CoreCount);
            var tasks      = CreateTasks(CoreCount, SleepThenReturnStartTime);
            var baseline   = DateTime.UtcNow;

            var startTimes = RunTasks(tasks, scheduler);

            startTimes.Count(t => t < baseline + GraceTime).Should().Be(CoreCount);
        }

        [Test, Retry(3)]
        public void Run_OverConcurrencyLimit()
        {
            var scheduler  = new LimitedConcurrencyTaskScheduler(CoreCount);
            var tasks      = CreateTasks(CoreCount + 1, SleepThenReturnStartTime);
            var baseline   = DateTime.UtcNow;

            var startTimes = RunTasks(tasks, scheduler);

            startTimes.Count(t => t < baseline             + GraceTime).Should().Be(CoreCount    );
            startTimes.Count(t => t < baseline + SleepTime + GraceTime).Should().Be(CoreCount + 1);
        }

        private static DateTime SleepThenReturnStartTime()
        {
            var startTime = DateTime.UtcNow;
            Thread.Sleep(SleepTime);
            return startTime;
        }

        private static Task<T>[] CreateTasks<T>(int count, Func<T> action)
        {
            return Enumerable
                .Range(0, count)
                .Select(_ => new Task<T>(action))
                .ToArray();
        }

        private static T[] RunTasks<T>(Task<T>[] tasks, LimitedConcurrencyTaskScheduler scheduler)
        {
            scheduler.CurrentConcurrencyLevel.Should().Be(0);

            foreach (var task in tasks)
                task.Start(scheduler);

            scheduler.CurrentConcurrencyLevel.Should().BeGreaterThan(0);
            scheduler.CurrentConcurrencyLevel.Should().BeLessOrEqualTo(scheduler.MaximumConcurrencyLevel);

            Task.WaitAll(tasks);

            scheduler.CurrentConcurrencyLevel.Should().Be(0);

            return tasks
                .Select(t => t.Result)
                .ToArray();
        }

        private static Task ATask() => new Task(() => { });

        private class TestableScheduler : LimitedConcurrencyTaskScheduler
        {
            public TestableScheduler(int concurrency)
                : base(concurrency) { }

            public new void QueueTask(Task task)
                => base.QueueTask(task);

            public new bool TryDequeue(Task task)
                => base.TryDequeue(task);

            public new IEnumerable<Task> GetScheduledTasks()
                => base.GetScheduledTasks();

            public new bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
                => base.TryExecuteTaskInline(task, taskWasPreviouslyQueued);
        }

        private class FakeDispatchScheduler : TestableScheduler
        {
            private int _dispatcherStartCount;

            public FakeDispatchScheduler()
                : base(concurrency: int.MaxValue) { }

            public int DispatcherStartCount => _dispatcherStartCount;

            private protected override bool TryStartDispatcher(int count)
            {
                Interlocked.Increment(ref _dispatcherStartCount);

                return true; // pretend it started
            }
        }

        private class DelayedDispatchScheduler : TestableScheduler, IDisposable
        {
            private readonly ManualResetEventSlim _enabled
                = new ManualResetEventSlim();

            public DelayedDispatchScheduler(int concurrency = 1)
                : base(concurrency) { }

            public void EnableDispatch()
            {
                _enabled.Set();
            }

            private protected override bool TryStartDispatcher(int count)
            {
                Task.Factory.StartNew(() =>
                {
                    _enabled.Wait();
                    base.TryStartDispatcher(count);
                });

                return true; // pretend it started
            }

            void IDisposable.Dispose()
            {
                _enabled.Dispose();
            }
        }
    }
}
