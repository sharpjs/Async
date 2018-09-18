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

using System.Threading;
using FluentAssertions;
using NUnit.Framework;

namespace Sharp.Async.Tests
{
    [TestFixture]
    public class AsyncScopeTests
    {
        private SynchronizationContext PriorContext;

        [SetUp]
        public void SetUp()
        {
            PriorContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
        }

        [TearDown]
        public void TearDown()
        {
            SynchronizationContext.SetSynchronizationContext(PriorContext);
            PriorContext = null;
        }

        [Test]
        public void Construct_WithNoCurrentContext()
        {
            SynchronizationContext.Current.Should().BeNull();

            using (new AsyncScope())
            {
                SynchronizationContext.Current.Should().BeNull();
            }

            SynchronizationContext.Current.Should().BeNull();
        }

        [Test]
        public void Construct_WithCurrentContext()
        {
            var context = new SynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(context);

            SynchronizationContext.Current.Should().BeSameAs(context);

            using (new AsyncScope())
            {
                // Synchronization context is null here, allowing non-async
                // code to invoke .Wait() or .Result on a task without fear of
                // deadlocks.
                SynchronizationContext.Current.Should().BeNull();
            }

            SynchronizationContext.Current.Should().BeSameAs(context);
        }
    }
}
