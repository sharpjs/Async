# Sharp.Async

A (very) small set of helpers for asynchronous programming in .NET.

## Status

In limited production use.  &ge;96% test coverage.

## Overview

This package provides the following types in the `Sharp.Async` namespace:

Name                              | Description
----------------------------------|------------
`AsyncGate`                       | Like `ManualResetEvent`, but for async code.<br>– `Wait()` returns a `Task`.<br>– Uses `Open()`and `Close()` instead of `Set()` and `Reset()`.
`AsyncScope`                      | Run and wait for an async operation from synchronous code.<br>– Can prevent a common source of deadlocks.\*<br>– An alternative to `ConfigureAwait(false)`.\*
`LimitedConcurrencyTaskScheduler` | A `TaskScheduler` that limits the number of concurrently executing tasks.<br>– Improves upon other implementations.<br>– Lock-free implementation.

*`AsyncScope` is intended *only* for use in application code where a
synchronous method must invoke and wait for async code, and *only* when using
the default `TaskScheduler` (or some other scheduler that will not reintroduce
a deadlock).  There is a [good argment](https://stackoverflow.com/a/41795923/142138)
against using `AsyncScope` in other scenarios.
