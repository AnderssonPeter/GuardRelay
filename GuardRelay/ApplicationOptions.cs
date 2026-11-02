namespace GuardRelay;

internal class ApplicationOptions
{
    public TimeSpan FetchInterval { get; set; } = TimeSpan.FromSeconds(5);
    public string DatabaseLocation { get; set; } = ".\\GuardRelay.sqlite";
}
