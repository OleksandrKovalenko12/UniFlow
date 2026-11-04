namespace UniFlow.Infrastructure.Numbering;

// One row per calendar year; LastNumber = the last number given out in that year
public class ApplicationNumberCounter
{
    public int Year { get; private set; }
    public int LastNumber { get; private set; }

    private ApplicationNumberCounter() { }
}