namespace Vineeeeea.Application.Services;

/// <summary>Small per-user choices that are a convenience, never the user's work (so losing them is harmless).</summary>
public interface IUserPreferences
{
    bool TimelineTipDismissed { get; }

    void DismissTimelineTip();
}
