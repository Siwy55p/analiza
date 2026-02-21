using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace STSAnaliza.Models;

public sealed class MatchListItem : INotifyPropertyChanged
{
    public string Tournament { get; set; } = "";
    public string PlayerA { get; set; } = "";
    public string PlayerB { get; set; } = "";
    public string Day { get; set; } = "";
    public string Hour { get; set; } = "";
    public decimal? OddA { get; set; }
    public decimal? OddB { get; set; }

    public int SourceIndex { get; set; }

    private string? _surface;
    private string? _formatMeczu;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string? Surface
    {
        get => _surface;
        set => SetField(ref _surface, value);
    }

    public string? FormatMeczu
    {
        get => _formatMeczu;
        set => SetField(ref _formatMeczu, value);
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}