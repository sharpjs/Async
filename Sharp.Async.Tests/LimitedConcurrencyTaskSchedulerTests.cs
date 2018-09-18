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
            var scheduler = new TestableLimitedConcurrencyTaskScheduler();

            scheduler
                .Invoking(s => s.QueueTask(null))
                .Should().Throw<ArgumentNullException>();
        }

        [Test]
        public void QueueTask_Ok()
        {
            var scheduler = new TestableLimitedConcurrencyTaskScheduler();
            var task      = ATask();

            scheduler.QueueTask(task);

            WaitUntil(() => scheduler.TriedToExecuteTask(task));
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

        private static void WaitUntil(Func<bool> condition, TimeSpan? duration = null)
        {
            var limit = DateTime.UtcNow + (duration ?? 3.Seconds());

            for (;;)
            {
                if (condition())
                    return;

                if (DateTime.UtcNow > limit)
                    Assert.Fail("Gave up waiting for condition.");

                Thread.Sleep(20.Milliseconds());
            }
        }

        private static Task ATask() => new Task(() => { });

        private static Task NonExecutableTask => Task.CompletedTask;

        private class TestableLimitedConcurrencyTaskScheduler : LimitedConcurrencyTaskScheduler
        {
            private readonly Func<Task, bool> _execute;
            private readonly List<Execution>  _executions;
            private readonly object           _executionsMutex;

            public TestableLimitedConcurrencyTaskScheduler(Func<Task, bool> execute = null)
                : base(4)
            {
                _execute         = execute ?? (_ => true);
                _executions      = new List<Execution>();
                _executionsMutex = new object();
            }

            public new void QueueTask(Task task)
                => base.QueueTask(task);

            public new bool TryDequeue(Task task)
                => base.TryDequeue(task);

            public new IEnumerable<Task> GetScheduledTasks()
                => base.GetScheduledTasks();

            public new bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
                => base.TryExecuteTaskInline(task, taskWasPreviouslyQueued);

            protected override bool TryExecuteTask(Task task)
            {
                lock (_executionsMutex)
                    _executions.Add(new Execution(task, Thread.CurrentThread));

                return _execute(task);
            }

            public Execution[] GetExecutions()
            {
                lock (_executionsMutex)
                    return _executions.ToArray();
            }

            public bool TriedToExecuteTask(Task task)
                => GetExecutions().Any(d => d.Task == task);
        }

        private class Execution
        {
            public Execution(Task task, Thread thread)
            {
                Task   = task;
                Thread = thread;
                Time   = DateTime.UtcNow;
            }

            public Task     Task   { get; }
            public Thread   Thread { get; }
            public DateTime Time   { get; }
        }
    }
}
