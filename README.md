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



TODO1 new features:

Misc
-------------------
- What is Span<Coords<int>> coordinates
- Init only setters
- Top-level statements
- this>>>> public readonly override string ToString() =>
    $"({X}, {Y}) is {Distance} from the origin";
  public readonly double Distance => Math.Sqrt(X * X + Y * Y);
- using doesn't need braces. using var file = new System.IO.StreamWriter("WriteLines2.txt");
- This language support relies on two new types, and two new operators:
    - System.Index represents an index into a sequence.
    - The index from end operator ^, which specifies that an index is relative to the end of the sequence.
    - System.Range represents a sub range of a sequence.
    - The range operator .., which specifies the start and end of a range as its operands. Range phrase = 1..4;
- You can use the ??= operator to assign the value of its right-hand operand to its left-hand operand only if the left-hand operand evaluates to null.
- you can omit the type in a new expression when the created object's type is already known. The most common use is in field declarations:
    private List<WeatherObservation> _observations = new();
    var forecast = station.ForecastFor(DateTime.Now.AddDays(2), new());
    WeatherStation station = new() { Location = "Seattle, WA" };
- you can add the static modifier to lambda expressions or anonymous methods. Static lambda expressions are analogous to the static local functions: a static lambda or anonymous method can't capture local variables or instance state. The static modifier prevents accidentally capturing other variables.
- you can now apply attributes to local functions. For example, you can apply nullable attribute annotations to local functions.


Switch expressions, patterns
-----------------------------
public static RGBColor FromRainbow(Rainbow colorBand) =>
    colorBand switch
    {
        Rainbow.Red    => new RGBColor(0xFF, 0x00, 0x00),
        Rainbow.Orange => new RGBColor(0xFF, 0x7F, 0x00),
        Rainbow.Yellow => new RGBColor(0xFF, 0xFF, 0x00),
        Rainbow.Green  => new RGBColor(0x00, 0xFF, 0x00),
        Rainbow.Blue   => new RGBColor(0x00, 0x00, 0xFF),
        Rainbow.Indigo => new RGBColor(0x4B, 0x00, 0x82),
        Rainbow.Violet => new RGBColor(0x94, 0x00, 0xD3),
        _              => throw new ArgumentException(message: "invalid enum value", paramName: nameof(colorBand)),
    };

Property patterns
public static decimal ComputeSalesTax(Address location, decimal salePrice) =>
    location switch
    {
        { State: "WA" } => salePrice * 0.06M,
        { State: "MN" } => salePrice * 0.075M,
        { State: "MI" } => salePrice * 0.05M,
        // other cases removed for brevity...
        _ => 0M
    };

Tuple patterns    
public static string RockPaperScissors(string first, string second)
    => (first, second) switch
    {
        ("rock", "paper") => "rock is covered by paper. Paper wins.",
        ("rock", "scissors") => "rock breaks scissors. Rock wins.",
        ("paper", "rock") => "paper covers rock. Paper wins.",
        ("paper", "scissors") => "paper is cut by scissors. Scissors wins.",
        ("scissors", "rock") => "scissors is broken by rock. Rock wins.",
        ("scissors", "paper") => "scissors cuts paper. Scissors wins.",
        (_, _) => "tie"
    };


static Quadrant GetQuadrant(Point point) => point switch
{
    (0, 0) => Quadrant.Origin,
    var (x, y) when x > 0 && y > 0 => Quadrant.One,
    var (x, y) when x < 0 && y > 0 => Quadrant.Two,
    var (x, y) when x < 0 && y < 0 => Quadrant.Three,
    var (x, y) when x > 0 && y < 0 => Quadrant.Four,
    var (_, _) => Quadrant.OnBorder,
    _ => Quadrant.Unknown
};


One of the most common uses is a new syntax for a null check:
if (e is not null)


Records C#9
--------------------
public record Person
{
    public string LastName { get; }
    public string FirstName { get; }
    public Person(string first, string last) => (FirstName, LastName) = (first, last);
}
- or
public record Person(string FirstName, string LastName);

The record definition creates a Person type that contains two readonly properties: FirstName and LastName. The Person type is a reference type. If you looked at the IL, it’s a class. It’s immutable in that none of the properties can be modified once it's been created. When you define a record type, the compiler synthesizes several other methods for you:
- Methods for value-based equality comparisons
- Override for GetHashCode()
- Copy and Clone members
- PrintMembers and ToString()
Records support inheritance.
Records should have the following capabilities:
- Equality is value-based, and includes a check that the types match. For example, a Student can't be equal to a Person, even if the two records share the same name.
- Records have a consistent string representation generated for you.
- Records support copy construction. Correct copy construction must include inheritance hierarchies, and properties added by developers.
- Records can be copied with modification. These copy and modify operations supports non-destructive mutation.
The compiler synthesizes two methods that support printed output: a ToString() override, and PrintMembers. The PrintMembers takes a System.Text.StringBuilder as its argument.

A with expression instructs the compiler to create a copy of a record, but with specified properties modified:
Person brother = person with { FirstName = "Paul" };
