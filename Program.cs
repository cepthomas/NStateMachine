using System;

// New: Top-level statement.
new NStateMachine.TestLock().Run();



//- this>>>> public readonly override string ToString() =>
//    $"({X}, {Y}) is {Distance} from the origin";
//public readonly double Distance => Math.Sqrt(X * X + Y * Y);

//- This language support relies on two new types, and two new operators:
//    - System.Index represents an index into a sequence.
//    - The index from end operator ^, which specifies that an index is relative to the end of the sequence.
//    - System.Range represents a sub range of a sequence.
//    - The range operator .., which specifies the start and end of a range as its operands. Range phrase = 1..4;

//-You can use the ??= operator to assign the value of its right-hand operand to its left-hand operand only if the left-hand operand evaluates to null.

//- you can omit the type in a new expression when the created object's type is already known. The most common use is in field declarations:
//    private List<WeatherObservation> _observations = new();
//var forecast = station.ForecastFor(DateTime.Now.AddDays(2), new());

//WeatherStation station = new() { Location = "Seattle, WA" };

//-you can add the static modifier to lambda expressions or anonymous methods. Static lambda expressions are analogous to the static local functions: a static lambda or anonymous method can't capture local variables or instance state. The static modifier prevents accidentally capturing other variables.

//- you can now apply attributes to local functions. For example, you can apply nullable attribute annotations to local functions.
