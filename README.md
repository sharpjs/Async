# Sharp.Async

A (very) small set of helpers for asynchronous programming in .NET.

## Status

In limited production use.  &ge;96% test coverage.

## Overview

This package provides the following types in the `Sharp.Async` namespace:

Name                              | Description
----------------------------------|------------
`AsyncGate`                       | Like `ManualResetEvent`, but for async code.<br>– `Wait()` returns a `Task`.<br>– Uses `Open()`and `Close()` instead of `Set()` and `Reset()`.
`AsyncScope`                      | Run and wait for an async operation from synchronous code.<br>– Prevents a common source of deadlocks.<br>– No need for `ConfigureAwait(false)`*.
`LimitedConcurrencyTaskScheduler` | A `TaskScheduler` that limits the number of concurrently executing tasks.<br>– Improves upon other implementations.<br>– Lock-free implementation.

*assumes use of the default `TaskScheduler` or `LimitedConcurrencyTaskScheduler`.
