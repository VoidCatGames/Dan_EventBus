# Using EventBus with Lookout

Lookout (part of VoidCat Studios' CodeCompass suite for Unity) draws a project's events as a graph:
who raises each event, who listens, and what isn't cleaned up. This guide shows how to make it show a project that uses
this EventBus (`com.dgp.eventbus`): the graph from a scan, and live pulses and the Trace while the game runs.

## 1. Install the library

1. Window ▸ Package Manager ▸ **+** ▸ **Install package from git URL**, and enter
   `https://github.com/VoidCatGames/Dan_EventBus.git`.
2. **Async handlers (optional).** This package compiles its UniTask support only with the `UNITASK_SUPPORT`
   scripting define. If your handlers return `UniTask`, install UniTask and add `UNITASK_SUPPORT` under Project
   Settings ▸ Player ▸ Other Settings ▸ Scripting Define Symbols. Lookout itself doesn't read this define.

## 2. Point Lookout at your code

1. Open Project Settings ▸ CodeCompass ▸ Lookout (or the gear button in the Lookout window).
2. Under **Scanning ▸ Included Folders**, add the folders that hold your event code.
3. Open Tools ▸ CodeCompass ▸ Lookout and scan.

You don't need a custom bus. This package's static API (`EventBus<T>.Raise`, `EventBus<T>.Register`,
`EventBus<T>.Deregister`, events implementing `IEvent`) is written the same way as Lookout's own EventBus. So Lookout
reads it as that bus, and its events appear under the **EventBus** chip and color.

What the scan finds:

| Construct | On the graph |
|---|---|
| `EventBus<T>.Raise(new T(…))` | a raise of `T` |
| `EventBus<T>.Register(Handler, priority)`, `Register(e => …)` | a listener of `T`; one per call, so two priorities show as two listeners |
| `EventBus<T>.Deregister(binding)` / `Deregister(Handler)` | the matching listener counts as cleaned up (no LEAK RISK). Each deregister undoes one registration |
| `async UniTask Handler(T e)` registered as above | a listener marked async UniTask |
| an `IStoppableEvent` | raised and listened to like any other event. Stopping isn't shown |

## 3. Live pulses and the Trace

Lookout's weaver instruments its own EventBus, not this one. So nothing pulses on its own. This EventBus
reports every raise through its own debug bridge (`EventBusBridge.OnEventRaised`, in the package's
`EventBus.Bridges` assembly). A small editor script forwards it to Lookout.

1. Create an `Editor` folder beside your code with an assembly definition that:
   - includes only the **Editor** platform
   - references `EventBus.Bridges` and `VoidCat.Lookout.Editor`
   - has the define constraint `!LOOKOUT_DISABLED`. Then a project that turns Lookout off still compiles.
2. Add this script. Both it and that assembly definition are in `Documentation~/Lookout/`: copy them into an
   `Editor` folder in your project (Unity doesn't compile folders ending in `~`).

```csharp
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
```

3. Open the Lookout window, press Play, and raise events. Each raise pulses its node and appears in the Trace.

The bridge passes neither the payload nor the call site. So the Trace shows each event's type and frame, but no
payload fields, and the pulse starts at the node rather than at the raising method.

**Async handlers that really wait** (`await UniTask.Delay(…)`) need the bus's `RaiseAsync`. The synchronous `Raise`
blocks on an async handler's result, and UniTask throws "Not yet completed" when the handler hasn't finished. This
is this package's own behavior, not Lookout's. You can see it with Lookout off too.

## 4. What Lookout doesn't see yet

These parts of the package run correctly, and the hook pulses them, but the scan doesn't place them on the graph:

- **Instanced `EventContainer`**: `container.Register<T>(…)`, `container.Raise(…)`. The node has no raise or listen
  sites.
- **The fluent builder**: `RaiseEvent.Event(evt).WithContainer(c)….RaiseSync()`.
- **Polymorphic raise**: `EventBus.Raise(new LevelLoaded(…))` (the non-generic `EventBus` class) isn't read as a
  raise. A handler registered on an interface (`EventBus<IGameEvent>.Register`) appears on an `IGameEvent` node,
  apart from the concrete events that reach it. At runtime the hook pulses each container the raise reaches.
- **Events declared through a derived interface**: `struct LevelLoaded : IGameEvent` (where `IGameEvent : IEvent`)
  isn't recognized as an event declaration. Raises and listeners of `LevelLoaded` still appear.
- Priorities and stop-propagation are not modeled. Two priorities show as two listeners, with no order between them.

If you rely on any of these, mark the code with Lookout's attributes (`[LookoutRaises("…")]` on the raising method,
`[LookoutListens("…")]` on the handler). The scan then places those sites on the graph.
