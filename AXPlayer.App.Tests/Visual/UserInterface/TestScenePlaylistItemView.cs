// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System.Collections.Generic;
using System.Linq;
using AXPlayer.App.Graphics.UserInterface;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;

namespace AXPlayer.App.Tests.Visual.UserInterface
{
    /// <summary>
    /// Renders the rows of a loaded playlist. The rows are marked as the current item while a video
    /// plays, which is skipped for an index the playlist does not hold yet.
    /// </summary>
    [TestFixture]
    public partial class TestScenePlaylistItemView : AXPlayerTestScene
    {
        private FillFlowContainer rowContainer = null!;
        private readonly List<PlaylistItemView> rows = new List<PlaylistItemView>();

        public TestScenePlaylistItemView()
        {
            Add(rowContainer = new FillFlowContainer
            {
                Width = 500,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
            });
        }

        [Test]
        public void TestRowsAreRenderedAndMarked()
        {
            AddStep("show the rows of the playlist", () =>
            {
                rowContainer.Clear();
                rows.Clear();

                for (int i = 0; i < 3; i++)
                {
                    var row = new PlaylistItemView(i) { RelativeSizeAxes = Axes.X };

                    rows.Add(row);
                    rowContainer.Add(row);
                }
            });

            AddAssert("every row is shown", () => rowContainer.Children.Count, () => Is.EqualTo(3));
            AddAssert("the rows keep their index", () => rows.Select(row => row.Index).ToArray(), () => Is.EqualTo(new[] { 0, 1, 2 }));

            AddStep("mark the second row as the current item", () =>
            {
                foreach (PlaylistItemView row in rows)
                    row.UpdateState(row.Index == 1);
            });

            AddAssert("the second row is marked", () => rows[1].BorderThickness, () => Is.EqualTo(3));
            AddAssert("the first row is not marked", () => rows[0].BorderThickness, () => Is.EqualTo(0));
        }
    }
}
