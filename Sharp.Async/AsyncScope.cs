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
using System.Threading;
using System.Threading.Tasks;

namespace Sharp.Async
{
    /// <summary>
    ///   Makes a synchronous code block able to invoke
    ///   <see cref="Task"/>-based asynchronous code without deadlocks.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     In some scenarios, the current <see cref="SynchronizationContext"/>
    ///     for a thread might attempt to resume <c>await</c>s on that same
    ///     thread.  This causes a deadlock when synchronous code starts an an
    ///     asynchronous <see cref="Task"/> and blocks until the task completes.
    ///     The deadlock occurs because the task's <c>await</c>s cannot resume
    ///     on the blocked thread.  Temporarily suppressing the
    ///     <c>SynchronizationContext</c> causes <c>await</c>s to resume on
    ///     <see cref="ThreadPool"/> threads instead, avoiding the deadlock.
    ///   </para>
    /// </remarks>
    public sealed class AsyncScope : IDisposable
    {
        private readonly SynchronizationContext _context;

        /// <summary>
        ///   Initializes a new instance of the <see cref="AsyncScope"/> class.
        /// </summary>
        public AsyncScope()
        {
            _context = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
        }

        void IDisposable.Dispose()
        {
            SynchronizationContext.SetSynchronizationContext(_context);
        }
    }
}
