﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.Coyote.Actors;
using Microsoft.Coyote.Actors.Timers;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Coyote.TestingServices.Tests.Actors
{
    public class BasicTimerTest : BaseTest
    {
        public BasicTimerTest(ITestOutputHelper output)
            : base(output)
        {
        }

        private class T1 : StateMachine
        {
            private int Count;

            [Start]
            [OnEntry(nameof(InitOnEntry))]
            [OnEventDoAction(typeof(TimerElapsedEvent), nameof(HandleTimeout))]
            private class Init : State
            {
            }

            private void InitOnEntry()
            {
                this.Count = 0;

                // Start a regular timer.
                this.StartTimer(TimeSpan.FromMilliseconds(10));
            }

            private void HandleTimeout()
            {
                this.Count++;
                this.Assert(this.Count == 1);
            }
        }

        [Fact(Timeout=10000)]
        public void TestBasicTimerOperation()
        {
            this.Test(r =>
            {
                r.CreateActor(typeof(T1));
            },
            configuration: Configuration.Create().WithNumberOfIterations(200).WithMaxSteps(200));
        }

        private class T2 : StateMachine
        {
            private TimerInfo Timer;
            private int Count;

            [Start]
            [OnEntry(nameof(InitOnEntry))]
            [OnEventDoAction(typeof(TimerElapsedEvent), nameof(HandleTimeout))]
            private class Init : State
            {
            }

            private void InitOnEntry()
            {
                this.Count = 0;

                // Start a periodic timer.
                this.Timer = this.StartPeriodicTimer(TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(10));
            }

            private void HandleTimeout()
            {
                this.Count++;
                this.Assert(this.Count <= 10);

                if (this.Count == 10)
                {
                    this.StopTimer(this.Timer);
                }
            }
        }

        [Fact(Timeout=10000)]
        public void TestBasicPeriodicTimerOperation()
        {
            this.Test(r =>
            {
                r.CreateActor(typeof(T2));
            },
            configuration: Configuration.Create().WithNumberOfIterations(200));
        }

        private class T3 : StateMachine
        {
            private TimerInfo PingTimer;
            private TimerInfo PongTimer;

            /// <summary>
            /// Start the PingTimer and start handling the timeout events from it.
            /// After handling 10 events, stop the timer and move to the Pong state.
            /// </summary>
            [Start]
            [OnEntry(nameof(DoPing))]
            [IgnoreEvents(typeof(TimerElapsedEvent))]
            private class Ping : State
            {
            }

            /// <summary>
            /// Start the PongTimer and start handling the timeout events from it.
            /// After handling 10 events, stop the timer and move to the Ping state.
            /// </summary>
            [OnEntry(nameof(DoPong))]
            [OnEventDoAction(typeof(TimerElapsedEvent), nameof(HandleTimeout))]
            private class Pong : State
            {
            }

            private void DoPing()
            {
                this.PingTimer = this.StartPeriodicTimer(TimeSpan.FromMilliseconds(5), TimeSpan.FromMilliseconds(5));
                this.StopTimer(this.PingTimer);

                this.Goto<Pong>();
            }

            private void DoPong()
            {
                this.PongTimer = this.StartPeriodicTimer(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(50));
            }

            private void HandleTimeout()
            {
                var timeout = this.ReceivedEvent as TimerElapsedEvent;
                this.Assert(timeout.Info == this.PongTimer);
            }
        }

        [Fact(Timeout=10000)]
        public void TestDropTimeoutsAfterTimerDisposal()
        {
            this.Test(r =>
            {
                r.CreateActor(typeof(T3));
            },
            configuration: Configuration.Create().WithNumberOfIterations(200).WithMaxSteps(200));
        }

        private class T4 : StateMachine
        {
            [Start]
            [OnEntry(nameof(Initialize))]
            private class Init : State
            {
            }

            private void Initialize()
            {
                this.StartTimer(TimeSpan.FromSeconds(-1));
            }
        }

        [Fact(Timeout=10000)]
        public void TestIllegalDueTimeSpecification()
        {
            this.TestWithError(r =>
            {
                r.CreateActor(typeof(T4));
            },
            configuration: Configuration.Create().WithNumberOfIterations(200).WithMaxSteps(200),
            expectedError: "'T4()' registered a timer with a negative due time.",
            replay: true);
        }

        private class T5 : StateMachine
        {
            [Start]
            [OnEntry(nameof(Initialize))]
            private class Init : State
            {
            }

            private void Initialize()
            {
                this.StartPeriodicTimer(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(-1));
            }
        }

        [Fact(Timeout=10000)]
        public void TestIllegalPeriodSpecification()
        {
            this.TestWithError(r =>
            {
                r.CreateActor(typeof(T5));
            },
            configuration: Configuration.Create().WithNumberOfIterations(200).WithMaxSteps(200),
            expectedError: "'T5()' registered a periodic timer with a negative period.",
            replay: true);
        }

        private class TransferTimerEvent : Event
        {
            public TimerInfo Timer;

            public TransferTimerEvent(TimerInfo timer)
            {
                this.Timer = timer;
            }
        }

        private class T6 : StateMachine
        {
            [Start]
            [OnEntry(nameof(Initialize))]
            [IgnoreEvents(typeof(TimerElapsedEvent))]
            private class Init : State
            {
            }

            private void Initialize()
            {
                var timer = this.StartPeriodicTimer(TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(10));
                this.CreateActor(typeof(T7), new TransferTimerEvent(timer));
            }
        }

        private class T7 : StateMachine
        {
            [Start]
            [OnEntry(nameof(Initialize))]
            private class Init : State
            {
            }

            private void Initialize()
            {
                var timer = (this.ReceivedEvent as TransferTimerEvent).Timer;
                this.StopTimer(timer);
            }
        }

        [Fact(Timeout=10000)]
        public void TestTimerDisposedByNonOwner()
        {
            this.TestWithError(r =>
            {
                r.CreateActor(typeof(T6));
            },
            configuration: Configuration.Create().WithNumberOfIterations(200).WithMaxSteps(200),
            expectedError: "'T7()' is not allowed to dispose timer '', which is owned by 'T6()'.",
            replay: true);
        }

        private class T8 : StateMachine
        {
            [Start]
            [OnEntry(nameof(InitOnEntry))]
            [IgnoreEvents(typeof(TimerElapsedEvent))]
            private class Init : State
            {
            }

            private void InitOnEntry()
            {
                // Start a regular timer.
                this.StartTimer(TimeSpan.FromMilliseconds(10));
                this.Goto<Final>();
            }

            [OnEntry(nameof(FinalOnEntry))]
            [IgnoreEvents(typeof(TimerElapsedEvent))]
            private class Final : State
            {
            }

            private void FinalOnEntry()
            {
                this.RaiseEvent(new HaltEvent());
            }
        }

        [Fact(Timeout=10000)]
        public void TestExplicitHaltWithTimer()
        {
            this.Test(r =>
            {
                r.CreateActor(typeof(T8));
            },
            configuration: Configuration.Create().WithNumberOfIterations(200).WithMaxSteps(200));
        }
    }
}