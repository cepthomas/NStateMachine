# NStateMachine

Targets .NET 5. No dependencies on third party components.

Probably I should make this into a nuget package at some point.

- Semi-hierarchical state machine for .NET.
- Generates diagrams via dot.

TODO1 examples and pics.

## API

States:
- Each state must have a name, except the (optional) default state identified by null.
  The default state is checked first, then the current state.
- Each state must have one or more Transitions.
- Each state may have an enter and/or exit action executed on state changes.


Transitions:
- Each transition must have an event name, except the (optional) default transition identified by null.
  If a transition for the event name is not found, the default transition is executed.
- Each transition may have a next state name otherwise stays in the same state.
- Each transition may have a transition action.

