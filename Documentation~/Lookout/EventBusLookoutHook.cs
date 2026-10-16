using System;
using DGP.EventBus.Bridges;
using UnityEditor;
using VoidCat.Lookout;

namespace DGP.EventBus.Lookout {

    /// <summary>Forwards every raise the EventBus reports on its debug bridge (<see cref="EventBusBridge.OnEventRaised"/>,
    /// the event's type only, before its handlers run) to Lookout, so raises pulse live in the Lookout graph and reach its
    /// Trace. No payload or call site: the bridge doesn't pass them. Lookout's probe does nothing unless Play is on and a
    /// Lookout window is open. See Documentation~/Lookout.md.</summary>
    [InitializeOnLoad]
    internal static class EventBusLookoutHook {

        static EventBusLookoutHook() {
            EventBusBridge.OnEventRaised -= Forward;
            EventBusBridge.OnEventRaised += Forward;
        }

        // "(object)null" picks the (Type, object) overload. A bare null binds to RecordRaise<Type>, and every raise
        // would be recorded as an event named "Type".
        private static void Forward(Type eventType) => LookoutProbe.RecordRaise(eventType, (object)null);
    }
}
