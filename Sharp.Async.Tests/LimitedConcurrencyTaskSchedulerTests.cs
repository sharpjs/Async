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
            var scheduler = new TestableScheduler();

            scheduler
                .Invoking(s => s.QueueTask(null))
                .Should().Throw<ArgumentNullException>();
        }

        [Test]
        public void TryDequeue_NullTask()
        {
            var scheduler = new TestableScheduler();

            scheduler.TryDequeue(null).Should().BeFalse();
        }

        [Test]
        public void Queue_Initial()
        {
            var scheduler = new TestableScheduler() { IsReliable = true };

            scheduler.GetScheduledTasks()    .Should().BeEmpty();
            scheduler.TryStartDispatcherCount.Should().Be(0);
        }

        [Test]
        public void Queue_Initial_ThenTryDequeue()
        {
            var scheduler = new TestableScheduler() { IsReliable = true };

            scheduler.TryDequeue(ATask()).Should().BeFalse();

            scheduler.GetScheduledTasks()    .Should().BeEmpty();
            scheduler.TryStartDispatcherCount.Should().Be(0);
        }

        [Test]
        public void Queue_AfterQueueTasks()
        {
            var scheduler = new TestableScheduler() { IsReliable = true };
            var task0     = ATask();
            var task1     = ATask();
            var task2     = ATask();

            scheduler.QueueTask(task0);
            scheduler.QueueTask(task1);
            scheduler.QueueTask(task2);

            scheduler.GetScheduledTasks()    .Should().BeEquivalentTo(task0, task1, task2);
            scheduler.TryStartDispatcherCount.Should().Be(3);
        }

        [Test]
        public void Queue_AfterQueueTasks_ThenTryDequeue()
        {
            var scheduler = new TestableScheduler() { IsReliable = true };
            var task0     = ATask();
            var task1     = ATask();
            var task2     = ATask();

            scheduler.QueueTask(task0);
            scheduler.QueueTask(task1);
            scheduler.QueueTask(task2);
            scheduler.TryDequeue(task1).Should().BeTrue();
            scheduler.TryDequeue(task1).Should().BeFalse();

            scheduler.GetScheduledTasks()    .Should().BeEquivalentTo(task0, task2);
            scheduler.TryStartDispatcherCount.Should().Be(3);
        }

        [Test]
        public void TryExecuteTaskInline_NullTask()
        {
            var scheduler = new TestableScheduler();

            scheduler.TryExecuteTaskInline(null, false).Should().BeFalse();
        }

        [Test]
        public void TryExecuteTaskInline_NotInDispatcherThread()
        {
            var scheduler = new TestableScheduler();

            scheduler.TryExecuteTaskInline(ATask(), false).Should().BeFalse();
        }

        [Test]
        public void TryExecuteTaskInline_NonQueuedTask()
        {
            var scheduler = new TestableScheduler();
            var task0     = null as Task;
            var task1     = ATask();
            var task2     = ATask();

            task0 = new Task(() =>
            {
                // Now inside dispatcher thread
                task1.RunSynchronously(scheduler);
                // This should call:
                // scheduler
                //    .TryExecuteTaskInline(task1, taskWasPreviouslyQueued: false)
                //    => true
            });

            task0.Start(scheduler);
            scheduler.IsDispatcherEnabled = true;
            task2.Start(scheduler);

            Task.WaitAll(task0, task1, task2);
        }

        [Test]
        public void TryExecuteTaskInline_QueuedTask()
        {
            var scheduler = new TestableScheduler(concurrency: 1);
            var task0     = null as Task;
            var task1     = ATask();
            var task2     = ATask();

            task0 = new Task(() =>
            {
                // Now inside dispatcher thread
                scheduler
                    .TryExecuteTaskInline(task1, taskWasPreviouslyQueued: true)
                    .Should().BeTrue();
            });

            task0.Start(scheduler);
            task1.Start(scheduler);
            scheduler.IsDispatcherEnabled = true;
            task2.Start(scheduler);

            Task.WaitAll(task0, task1, task2);
        }

        [Test]
        public void TryExecuteTaskInline_FailedToDequeueTask()
        {
            var scheduler = new TestableScheduler(concurrency: 1);
            var task0     = null as Task;
            var task1     = ATask();
            var task2     = ATask();

            task0 = new Task(() =>
            {
                // Now inside dispatcher thread
                scheduler
                    .TryExecuteTaskInline(task1, taskWasPreviouslyQueued: true)
                    .Should().BeFalse();
            });

            task0.Start(scheduler);
            // Do not queue task1
            scheduler.IsDispatcherEnabled = true;
            task2.Start(scheduler);

            Task.WaitAll(task0, task2);
            task1.Status.Should().Be(TaskStatus.Created);
        }

        [Test]
        public void TryStartDispatcher_ConcurrencyConflict()
        {
            var scheduler = new TestableScheduler { IsDispatcherEnabled = true };
            var task0     = ATask();
            var task1     = ATask();
            var task2     = ATask();

            task0.Start(scheduler);
            task1.Start(scheduler);
            task2.Start(scheduler);

            Task.WaitAll(task0, task1, task2);

            scheduler.TryStartDispatcherCount.Should().BeGreaterThan(3);
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
            private int _fate;
            private int _tryStartDispatcherCount;

            public TestableScheduler(int? concurrency = null)
                : base(concurrency ?? CoreCount) { }

            public bool IsDispatcherEnabled { get; set; }

            public bool IsReliable { get; set; }

            public int TryStartDispatcherCount
                => _tryStartDispatcherCount;

            public new void QueueTask(Task task)
                => base.QueueTask(task);

            public new bool TryDequeue(Task task)
                => base.TryDequeue(task);

            public new IEnumerable<Task> GetScheduledTasks()
                => base.GetScheduledTasks();

            public new bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
                => base.TryExecuteTaskInline(task, taskWasPreviouslyQueued);

            private protected override bool TryStartDispatcher(int count)
            {
                const int InvalidCount = -42;

                Interlocked.Increment(ref _tryStartDispatcherCount);

                var succeed
                    =  IsReliable
                    || Interlocked.Increment(ref _fate) % 2 == 0;

                return IsDispatcherEnabled
                    ? base.TryStartDispatcher(succeed ? count : InvalidCount)
                    : succeed; // pretend
            }
        }
    }
}
