namespace MyMascada.Application.Features.Waitlist.DTOs;

public class JoinWaitlistResponse
{
    public bool IsSuccess { get; set; }
    public string? Message { get; set; }
    public List<string> Errors { get; set; } = new();
}
