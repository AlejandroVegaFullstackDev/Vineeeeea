using Vineeeeea.App.Utils;

namespace Vineeeeea.App.Tests;

public class TimelineMagnetTests
{
    [Fact]
    public void A_start_within_reach_of_a_target_snaps_to_it()
    {
        // Start at 78 with a target (playhead) at 80, within the 5-frame threshold → snaps to 80.
        Assert.Equal(80, TimelineMagnet.SnapStart(desiredStart: 78, durationFrames: 60, targets: [0, 80], thresholdFrames: 5));
    }

    [Fact]
    public void The_end_edge_also_snaps_so_clips_sit_flush()
    {
        // A clip of length 60 dragged to start 38 ends at 98; the end snaps to the neighbour's start at 100,
        // pulling the clip to start 40 so the two sit flush.
        Assert.Equal(40, TimelineMagnet.SnapStart(desiredStart: 38, durationFrames: 60, targets: [100], thresholdFrames: 5));
    }

    [Fact]
    public void Nothing_within_reach_leaves_the_clip_where_it_was_dragged()
    {
        Assert.Equal(200, TimelineMagnet.SnapStart(desiredStart: 200, durationFrames: 60, targets: [0, 80], thresholdFrames: 5));
    }

    [Fact]
    public void A_snap_never_pushes_the_clip_before_the_start()
    {
        Assert.Equal(0, TimelineMagnet.SnapStart(desiredStart: 2, durationFrames: 60, targets: [-1], thresholdFrames: 5));
    }
}
