# NStateMachine

Targets .NET 5. No dependencies on third party components.

Probably I should make this into a nuget package at some point.

- Semi-hierarchical state machine for .NET.
- Generates diagrams via dot.

TODO add more plus examples and pics.

## API

States:
- Each state must have a name, except the (optional) default state identified by DEF_STATE.
- The current state is checked first, then the default state.
- Each state must have one or more Transitions.
- Each state may have an optional enter and/or exit action executed on state changes. Otherwise use NO_FUNC.


Transitions:
- Each transition must have an event name, except the (optional) default transition identified by DEF_EVENT.
- If a transition for the event name is not found, the DEF_EVENT transition is executed.
- Each transition may have a next state name or SAME_STATE which stays in the same state.
- Each transition may have an optional transition action. Otherwise use NO_FUNC.

